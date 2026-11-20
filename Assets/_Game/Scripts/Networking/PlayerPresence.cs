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

        // Creates a variable every client can read and only the owning client can write.
        private static NetworkVariable<T> OwnerVariable<T>(T value) =>
            new NetworkVariable<T>(value, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        // Assigns the role on the server, hides the owner's own markers and places the owner's rig.
        public override void OnNetworkSpawn()
        {
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
        }

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
