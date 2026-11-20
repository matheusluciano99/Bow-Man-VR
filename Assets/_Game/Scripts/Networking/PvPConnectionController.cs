using System;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BowMan.Networking
{
    // Starts, tracks and ends a 1v1 session: host and join through Relay, seat approval and status.
    [DisallowMultipleComponent]
    public sealed class PvPConnectionController : MonoBehaviour
    {
        public const string InvalidCodeMessage = "Enter the 6-character join code";

        [SerializeField] private NetworkManager networkManager;
        [SerializeField] private UnityTransport transport;

        private readonly RelayConnector relay = new RelayConnector();
        private string joinCode = string.Empty;
        private ArcherRole localRole;
        private bool ignoreStopEvents;
        private int session;
        private string ownScene;

        public ArcherSeatAllocator Seats { get; } = new ArcherSeatAllocator();
        public ConnectionStatus Status { get; private set; } = ConnectionStatus.Offline;
        public event Action<ConnectionStatus> StatusChanged;

        // Finds the sibling network components, installs seat approval and keeps the single scene unmanaged.
        private void Awake()
        {
            if (networkManager == null) networkManager = GetComponent<NetworkManager>();
            if (transport == null) transport = GetComponent<UnityTransport>();
            networkManager.NetworkConfig.EnableSceneManagement = false;
            networkManager.NetworkConfig.ConnectionApproval = true;
            networkManager.ConnectionApprovalCallback = Approve;
        }

        // Remembers the scene that owns this session so leaving it can end the session.
        private void Start()
        {
            ownScene = SceneManager.GetActiveScene().name;
            SceneManager.activeSceneChanged += HandleActiveSceneChanged;
        }

        // Stops watching scene changes when the network object is destroyed.
        private void OnDestroy() => SceneManager.activeSceneChanged -= HandleActiveSceneChanged;

        // Ends the session and removes the persistent network object when another scene takes over.
        private void HandleActiveSceneChanged(Scene previous, Scene next)
        {
            if (next.name == ownScene) return;
            Leave();
            Destroy(gameObject);
        }

        // Listens for connection, transport and shutdown events while enabled.
        private void OnEnable()
        {
            networkManager.OnConnectionEvent += HandleConnectionEvent;
            networkManager.OnTransportFailure += HandleTransportFailure;
            networkManager.OnClientStopped += HandleStopped;
            networkManager.OnServerStopped += HandleStopped;
        }

        // Invalidates pending attempts and stops listening to network events.
        private void OnDisable()
        {
            session++;
            if (networkManager == null) return;
            networkManager.OnConnectionEvent -= HandleConnectionEvent;
            networkManager.OnTransportFailure -= HandleTransportFailure;
            networkManager.OnClientStopped -= HandleStopped;
            networkManager.OnServerStopped -= HandleStopped;
        }

        // Signs in, creates a Relay match and starts hosting as Archer 1.
        public async Task HostAsync()
        {
            if (!Status.CanStart) return;
            var attempt = BeginSession(string.Empty);
            try
            {
                SetStatus(ConnectionPhase.SigningIn);
                await relay.EnsureSignedInAsync(Profile());
                if (attempt != session) return;
                SetStatus(ConnectionPhase.CreatingMatch);
                var allocation = await relay.HostAsync();
                if (attempt != session) return;
                transport.SetRelayServerData(allocation.Data);
                joinCode = allocation.Code;
                if (!await ReadyToStartAsync(attempt)) return;
                if (!networkManager.StartHost()) throw new InvalidOperationException("Could not start the host");
                localRole = Seats.RoleOf(NetworkManager.ServerClientId);
                SetStatus(ConnectionPhase.WaitingForOpponent);
            }
            catch (Exception ex)
            {
                if (attempt == session) Fail(ex.Message);
            }
        }

        // Signs in, joins the Relay match behind the typed code and starts the client.
        public async Task JoinAsync(string rawCode)
        {
            if (!Status.CanStart) return;
            var code = JoinCode.Normalize(rawCode);
            if (!JoinCode.IsValid(code))
            {
                SetStatus(ConnectionPhase.Failed, InvalidCodeMessage);
                return;
            }
            var attempt = BeginSession(code);
            try
            {
                SetStatus(ConnectionPhase.SigningIn);
                await relay.EnsureSignedInAsync(Profile());
                if (attempt != session) return;
                SetStatus(ConnectionPhase.Joining);
                var allocation = await relay.JoinAsync(code);
                if (attempt != session) return;
                transport.SetRelayServerData(allocation);
                if (!await ReadyToStartAsync(attempt)) return;
                if (!networkManager.StartClient()) throw new InvalidOperationException("Could not start the client");
            }
            catch (Exception ex)
            {
                if (attempt == session) Fail(ex.Message);
            }
        }

        // Ends the local session, cancelling any Host or Join still in progress, and returns to Offline.
        public void Leave()
        {
            session++;
            StopNetwork();
            joinCode = string.Empty;
            localRole = ArcherRole.None;
            SetStatus(ConnectionPhase.Offline);
        }

        // Records the role the server gave the local player and shows it once the client is in.
        public void ReportLocalRole(ArcherRole role)
        {
            if (role == ArcherRole.None) return;
            localRole = role;
            if (!networkManager.IsServer && Status.Phase == ConnectionPhase.Joining) SetStatus(ConnectionPhase.Connected);
        }

        // Admits a client into a free archer seat or refuses it because the match is full.
        private void Approve(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
        {
            response.Pending = false;
            response.Approved = Seats.TryAssign(request.ClientNetworkId, out _);
            response.CreatePlayerObject = response.Approved;
            if (!response.Approved) response.Reason = ArcherSeatAllocator.FullReason;
        }

        // Updates host status when the opponent arrives or leaves, freeing the seat on departure.
        private void HandleConnectionEvent(NetworkManager manager, ConnectionEventData data)
        {
            if (!manager.IsServer || data.ClientId == NetworkManager.ServerClientId) return;
            if (data.EventType == ConnectionEvent.ClientConnected)
                SetStatus(ConnectionPhase.Connected);
            else if (data.EventType == ConnectionEvent.ClientDisconnected && Seats.Release(data.ClientId))
                SetStatus(ConnectionPhase.WaitingForOpponent);
        }

        // Reports a lost transport as a failure the player can retry.
        private void HandleTransportFailure() => Fail("Network transport failed");

        // Shows an unexpected stop as Disconnected, with the server's reason when one was sent.
        private void HandleStopped(bool wasHost)
        {
            if (ignoreStopEvents) return;
            ignoreStopEvents = true;
            Seats.Clear();
            localRole = ArcherRole.None;
            SetStatus(ConnectionPhase.Disconnected, ConnectionStatus.ReadableDisconnectReason(networkManager.DisconnectReason));
        }

        // Shuts the session down and shows why it failed.
        private void Fail(string reason)
        {
            StopNetwork();
            SetStatus(ConnectionPhase.Failed, string.IsNullOrEmpty(reason) ? "Unknown error" : reason);
        }

        // Waits for any previous NGO shutdown before allowing callbacks from the new session.
        private async Task<bool> ReadyToStartAsync(int attempt)
        {
            while (attempt == session && networkManager.ShutdownInProgress) await Task.Yield();
            if (attempt != session) return false;
            ignoreStopEvents = false;
            return true;
        }

        // Clears the previous session's state and returns the new attempt's number.
        private int BeginSession(string code)
        {
            session++;
            ignoreStopEvents = true;
            Seats.Clear();
            joinCode = code;
            localRole = ArcherRole.None;
            return session;
        }

        // Shuts down NGO without reporting the resulting stop events as a disconnect.
        private void StopNetwork()
        {
            ignoreStopEvents = true;
            if (networkManager.IsListening || networkManager.ShutdownInProgress) networkManager.Shutdown();
            Seats.Clear();
        }

        // Publishes a new status built from the current role and join code.
        private void SetStatus(ConnectionPhase phase, string error = "")
        {
            Status = new ConnectionStatus(phase, localRole, joinCode, error);
            StatusChanged?.Invoke(Status);
        }

        // Gives this process its own anonymous player so two instances on one PC can connect.
        private static string Profile() =>
            AuthProfile.ForProcess(System.Diagnostics.Process.GetCurrentProcess().Id, Application.isEditor ? "editor" : null);
    }
}
