using Unity.Netcode;
using UnityEngine;

namespace BowMan.Networking
{
    // Networked player: carries the server-assigned role and replicates head and hand poses as markers.
    public sealed class PlayerPresence : NetworkBehaviour
    {
        [SerializeField] private Transform headMarker;
        [SerializeField] private Transform leftMarker;
        [SerializeField] private Transform rightMarker;

        public readonly NetworkVariable<ArcherRole> Role = new NetworkVariable<ArcherRole>(ArcherRole.None);

        private readonly NetworkVariable<Vector3> headPosition = OwnerVariable(Vector3.zero);
        private readonly NetworkVariable<Quaternion> headRotation = OwnerVariable(Quaternion.identity);
        private readonly NetworkVariable<Vector3> leftPosition = OwnerVariable(Vector3.zero);
        private readonly NetworkVariable<Quaternion> leftRotation = OwnerVariable(Quaternion.identity);
        private readonly NetworkVariable<Vector3> rightPosition = OwnerVariable(Vector3.zero);
        private readonly NetworkVariable<Quaternion> rightRotation = OwnerVariable(Quaternion.identity);

        private LocalRigBinder rig;
        private PvPDuel duel;
        public Vector3 TrackedHead => IsOwner && rig != null ? rig.Head.position : headPosition.Value;

        // Creates a variable every client can read and only the owning client can write.
        private static NetworkVariable<T> OwnerVariable<T>(T value) =>
            new NetworkVariable<T>(value, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        // Assigns the role on the server, hides the owner's own markers and places the owner's rig.
        public override void OnNetworkSpawn()
        {
            duel = FindAnyObjectByType<PvPDuel>();
            if (IsServer)
            {
                var controller = FindAnyObjectByType<PvPConnectionController>();
                if (controller != null) Role.Value = controller.Seats.RoleOf(OwnerClientId);
            }
            SetMarkersVisible(!IsOwner);
            if (!IsOwner) return;
            rig = FindAnyObjectByType<LocalRigBinder>();
            Role.OnValueChanged += HandleRoleChanged;
            ApplyLocalRole(Role.Value);
        }

        // Stops following role changes when the object leaves the network.
        public override void OnNetworkDespawn() => Role.OnValueChanged -= HandleRoleChanged;

        // Publishes the owner's tracked poses, or shows the remote player's poses on the markers.
        private void Update()
        {
            if (!IsSpawned) return;
            if (IsOwner)
            {
                if (rig == null) return;
                Write(rig.Head, headPosition, headRotation);
                Write(rig.LeftHand, leftPosition, leftRotation);
                Write(rig.RightHand, rightPosition, rightRotation);
                return;
            }
            Read(headMarker, headPosition, headRotation);
            Read(leftMarker, leftPosition, leftRotation);
            Read(rightMarker, rightPosition, rightRotation);
        }

        // Applies a role the server changed after spawn.
        private void HandleRoleChanged(ArcherRole previous, ArcherRole current) => ApplyLocalRole(current);

        // Moves the local rig to the role's spawn and tells the connection controller.
        private void ApplyLocalRole(ArcherRole role)
        {
            if (role == ArcherRole.None) return;
            if (rig != null) rig.PlaceAt(role);
            var controller = FindAnyObjectByType<PvPConnectionController>();
            if (controller != null) controller.ReportLocalRole(role);
            if (role == ArcherRole.Archer2 && duel != null) ReadyRpc();
        }

        // Confirms the joiner has received its assigned seat and can accept a fresh round.
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        private void ReadyRpc() => duel?.PeerReady(OwnerClientId);

        // Submits a released bow to the host; only the owning seat can make the request.
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        public void ReleaseRpc(int round, int turn, Vector3 origin, Vector3 direction, float strength) =>
            duel?.ReceiveRelease(OwnerClientId, round, turn, origin, direction, strength);

        // Starts the joiner's replica after the host has created its match.
        [Rpc(SendTo.NotServer, InvokePermission = RpcInvokePermission.Server)]
        public void BeginRpc(int round) => duel?.Begin(round);

        // Replays a host-approved arrow without running collision resolution on the client.
        [Rpc(SendTo.NotServer, InvokePermission = RpcInvokePermission.Server)]
        public void FlightRpc(int round, int shot, Vector3 origin, Vector3 velocity) =>
            duel?.ShowFlight(round, shot, origin, velocity);

        // Stops a missed arrow at the host's actual scenery contact for its remaining visual lifetime.
        [Rpc(SendTo.NotServer, InvokePermission = RpcInvokePermission.Server)]
        public void SceneryRpc(int round, int shot, Vector3 point, Quaternion rotation, float lifetime) =>
            duel?.ShowScenery(round, shot, point, rotation, lifetime);

        // Distributes the host's phase and health after each transition or shot rejection.
        [Rpc(SendTo.NotServer, InvokePermission = RpcInvokePermission.Server)]
        public void StateRpc(int round, int shot, DuelPhase phase, int active, int winner, int hp1, int hp2) =>
            duel?.ReceiveState(round, shot, phase, active, winner, hp1, hp2);

        // Delivers the authoritative volume, entry and contacted anatomy after health resolution.
        [Rpc(SendTo.NotServer, InvokePermission = RpcInvokePermission.Server)]
        public void ImpactRpc(int round, int shot, string snapshot, int hp1, int hp2) =>
            duel?.ReceiveImpact(round, shot, snapshot, hp1, hp2);

        // Copies one tracked transform into its owner-written variables.
        private static void Write(Transform source, NetworkVariable<Vector3> position, NetworkVariable<Quaternion> rotation)
        {
            if (source == null) return;
            position.Value = source.position;
            rotation.Value = source.rotation;
        }

        // Places one marker at the replicated pose.
        private static void Read(Transform marker, NetworkVariable<Vector3> position, NetworkVariable<Quaternion> rotation)
        {
            if (marker != null) marker.SetPositionAndRotation(position.Value, rotation.Value);
        }

        // Shows or hides the head and hand markers.
        private void SetMarkersVisible(bool visible)
        {
            foreach (var marker in new[] { headMarker, leftMarker, rightMarker })
                if (marker != null) marker.gameObject.SetActive(visible);
        }
    }
}
