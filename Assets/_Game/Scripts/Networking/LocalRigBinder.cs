using UnityEngine;

namespace BowMan.Networking
{
    // Links the local XR rig to the network: moves it to the role's spawn and exposes its tracked poses.
    public sealed class LocalRigBinder : MonoBehaviour
    {
        [SerializeField] private Transform rigRoot;
        [SerializeField] private Transform head;
        [SerializeField] private Transform leftHand;
        [SerializeField] private Transform rightHand;
        [SerializeField] private ArcherSpawnPoints spawns;

        public Transform Head => head;
        public Transform LeftHand => leftHand;
        public Transform RightHand => rightHand;

        // Turns XR off when the player was started with -desktop.
        private void Awake()
        {
            DesktopXR.ApplyCommandLine();
        }

        // Moves the rig onto the role's spawn, keeping only its facing direction.
        public void PlaceAt(ArcherRole role)
        {
            var spawn = spawns != null ? spawns.For(role) : null;
            if (spawn == null || rigRoot == null) return;
            rigRoot.SetPositionAndRotation(spawn.position, Quaternion.Euler(0f, spawn.eulerAngles.y, 0f));
        }
    }
}
