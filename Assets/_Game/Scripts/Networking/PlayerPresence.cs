using Unity.Netcode;
using UnityEngine;

namespace BowMan.Networking
{
    // One owner-authored sample for the remote body and bow, scoped to a single aiming turn.
    public struct BowPose : INetworkSerializable
    {
        public int Round, Turn;
        public bool Left;
        public float Strength;
        public Vector3 Grip, Pull;
        public Quaternion Rotation;

        // Sends the contact geometry only while this seat has an arrow on its string.
        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Round);
            serializer.SerializeValue(ref Turn);
            serializer.SerializeValue(ref Left);
            serializer.SerializeValue(ref Strength);
            if (Strength < 0f) return;
            serializer.SerializeValue(ref Grip);
            serializer.SerializeValue(ref Rotation);
            serializer.SerializeValue(ref Pull);
        }
    }

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

        private readonly NetworkVariable<BowPose> bowPose = OwnerVariable(default(BowPose));
        public BowPose Draw => bowPose.Value;
        private LocalRigBinder rig;
        private PvPDuel duel;
        private OnlineTargetMatch range;
        public Vector3 TrackedHead => IsOwner && rig != null ? rig.Head.position : headPosition.Value;

        // Creates a variable every client can read and only the owning client can write.
        private static NetworkVariable<T> OwnerVariable<T>(T value) =>
            new NetworkVariable<T>(value, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        // Assigns the role on the server, hides the owner's own markers and places the owner's rig.
        public override void OnNetworkSpawn()
        {
            duel = FindAnyObjectByType<PvPDuel>();
            range = FindAnyObjectByType<OnlineTargetMatch>();
            if (IsServer)
            {
                var controller = FindAnyObjectByType<PvPConnectionController>();
                if (controller != null) Role.Value = controller.Seats.RoleOf(OwnerClientId);
            }
            SetMarkersVisible(!IsOwner && range == null);
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
                if (duel != null && duel.Flow != null) bowPose.Value = duel.LocalBowPose();
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
            if (role == ArcherRole.Archer2 && (duel != null || range != null)) ReadyRpc();
        }

        // Confirms the joiner has received its assigned seat and can accept a fresh round.
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        private void ReadyRpc()
        {
            if (range != null) range.PeerReady(OwnerClientId);
            else duel?.PeerReady(OwnerClientId);
        }

        // Submits a released bow to the host; only the owning seat can make the request.
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        public void ReleaseRpc(int round, int turn, Vector3 origin, Vector3 direction, float strength)
        {
            if (range != null) range.ReceiveRelease(OwnerClientId, round, turn, origin, direction, strength);
            else duel?.ReceiveRelease(OwnerClientId, round, turn, origin, direction, strength);
        }

        // Records a rematch vote for the authenticated owner of this player object.
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        public void RematchRpc(int round)
        {
            if (range != null) range.ReceiveRematch(OwnerClientId, round);
            else duel?.ReceiveRematch(OwnerClientId, round);
        }

        // Shows both seats' rematch choices while the current result is still visible.
        [Rpc(SendTo.NotServer, InvokePermission = RpcInvokePermission.Server)]
        public void VotesRpc(int round, int votes) => duel?.ShowVotes(round, votes);

        // Starts the joiner's replica after the host has created its match.
        [Rpc(SendTo.NotServer, InvokePermission = RpcInvokePermission.Server)]
        public void BeginRpc(int round, ArenaSetting setting) => duel?.Begin(round, setting);

        // Replays a host-approved arrow without running collision resolution on the client.
        [Rpc(SendTo.NotServer, InvokePermission = RpcInvokePermission.Server)]
        public void FlightRpc(int round, int shot, Vector3 origin, Vector3 velocity)
        {
            if (range != null) range.ShowFlight(round, shot, origin, velocity);
            else duel?.ShowFlight(round, shot, origin, velocity);
        }

        // Stops a missed arrow at the host's actual scenery contact for its remaining visual lifetime.
        [Rpc(SendTo.NotServer, InvokePermission = RpcInvokePermission.Server)]
        public void SceneryRpc(int round, int shot, Vector3 point, Quaternion rotation, float lifetime, ImpactSound surface)
        {
            if (range != null) range.ShowContact(round, shot, point, rotation, lifetime, surface);
            else duel?.ShowScenery(round, shot, point, rotation, lifetime, surface);
        }

        // Synchronizes both target scores, accepted shots and rematch votes from the host.
        [Rpc(SendTo.NotServer, InvokePermission = RpcInvokePermission.Server)]
        public void TargetScoreRpc(int round, int completed, int first, int second, int last, bool flying, int votes) =>
            range?.ApplyScore(round, completed, first, second, last, flying, votes);

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
        internal void SetMarkersVisible(bool visible)
        {
            foreach (var marker in new[] { headMarker, leftMarker, rightMarker })
                if (marker != null) marker.gameObject.SetActive(visible);
        }
    }
}
