using System;
using System.Collections;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Management;

namespace BowMan.Networking
{
    // Links the local XR rig to the network: moves it to the role's spawn and exposes its tracked poses.
    public sealed class LocalRigBinder : MonoBehaviour
    {
        public const string DesktopFlag = "-desktop";
        private const float DesktopEyeHeight = 1.6f;

        [SerializeField] private Transform rigRoot;
        [SerializeField] private Transform cameraOffset;
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

        // Raises the desktop camera to standing eye height once XR has had a frame to start.
        private IEnumerator Start()
        {
            yield return null;
            if (!XRSettings.isDeviceActive && cameraOffset != null)
            {
                var offset = cameraOffset.localPosition;
                cameraOffset.localPosition = new Vector3(offset.x, DesktopEyeHeight, offset.z);
            }
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
