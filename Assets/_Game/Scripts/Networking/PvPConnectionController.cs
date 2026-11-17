using System;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

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

        public ArcherSeatAllocator Seats { get; } = new ArcherSeatAllocator();
        public ConnectionStatus Status { get; private set; } = ConnectionStatus.Offline;
        public event Action<ConnectionStatus> StatusChanged;

        // Finds the sibling network components and installs seat-based connection approval.
        private void Awake()
        {
            if (networkManager == null) networkManager = GetComponent<NetworkManager>();
            if (transport == null) transport = GetComponent<UnityTransport>();
            networkManager.NetworkConfig.ConnectionApproval = true;
            networkManager.ConnectionApprovalCallback = Approve;
        }

        // Listens for connection, transport and shutdown events while enabled.
        private void OnEnable()
        {
            networkManager.OnConnectionEvent += HandleConnectionEvent;
            networkManager.OnTransportFailure += HandleTransportFailure;
            networkManager.OnClientStopped += HandleStopped;
            networkManager.OnServerStopped += HandleStopped;
        }

        // Stops listening to network events.
        private void OnDisable()
        {
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
            try
            {
                BeginSession(string.Empty);
                SetStatus(ConnectionPhase.SigningIn);
                await relay.EnsureSignedInAsync(Profile());
                SetStatus(ConnectionPhase.CreatingMatch);
                joinCode = await relay.HostAsync(transport);
                if (!networkManager.StartHost()) throw new InvalidOperationException("Could not start the host");
                localRole = Seats.RoleOf(NetworkManager.ServerClientId);
                SetStatus(ConnectionPhase.WaitingForOpponent);
            }
            catch (Exception ex)
            {
                Fail(ex.Message);
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
            try
            {
                BeginSession(code);
                SetStatus(ConnectionPhase.SigningIn);
                await relay.EnsureSignedInAsync(Profile());
                SetStatus(ConnectionPhase.Joining);
                await relay.JoinAsync(transport, code);
                if (!networkManager.StartClient()) throw new InvalidOperationException("Could not start the client");
            }
            catch (Exception ex)
            {
                Fail(ex.Message);
            }
        }

        // Ends the local session and returns to Offline.
        public void Leave()
        {
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
            SetStatus(ConnectionPhase.Disconnected, networkManager.DisconnectReason);
        }

        // Shuts the session down and shows why it failed.
        private void Fail(string reason)
        {
            StopNetwork();
            SetStatus(ConnectionPhase.Failed, string.IsNullOrEmpty(reason) ? "Unknown error" : reason);
        }

        // Clears the previous session's state before a new Host or Join attempt.
        private void BeginSession(string code)
        {
            ignoreStopEvents = false;
            Seats.Clear();
            joinCode = code;
            localRole = ArcherRole.None;
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
