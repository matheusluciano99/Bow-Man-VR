using System;
using UnityEngine;
using UnityEngine.XR.Management;

namespace BowMan.Networking
{
    // Links the local XR rig to the network: moves it to the role's spawn and exposes its tracked poses.
    public sealed class LocalRigBinder : MonoBehaviour
    {
        public const string DesktopFlag = "-desktop";

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
            if (DesktopRequested()) StopXR();
        }

        // Moves the rig onto the role's spawn, keeping only its facing direction.
        public void PlaceAt(ArcherRole role)
        {
            var spawn = spawns != null ? spawns.For(role) : null;
            if (spawn == null || rigRoot == null) return;
            rigRoot.SetPositionAndRotation(spawn.position, Quaternion.Euler(0f, spawn.eulerAngles.y, 0f));
        }

        // True when the command line contains the -desktop flag.
        private static bool DesktopRequested()
        {
            foreach (var arg in Environment.GetCommandLineArgs())
                if (string.Equals(arg, DesktopFlag, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        // Stops the XR subsystems and loader started by OpenXR at launch.
        private static void StopXR()
        {
            var manager = XRGeneralSettings.Instance != null ? XRGeneralSettings.Instance.Manager : null;
            if (manager == null || !manager.isInitializationComplete) return;
            manager.StopSubsystems();
            manager.DeinitializeLoader();
        }
    }
}
