using System;
using UnityEngine.XR.Management;

namespace BowMan.Networking
{
    // Lets a player started with -desktop run without the headset.
    public static class DesktopXR
    {
        public const string Flag = "-desktop";

        public static bool CommandLineRequested => UnityEngine.Application.platform != UnityEngine.RuntimePlatform.WebGLPlayer &&
            Requested(Environment.GetCommandLineArgs());

        // Stops XR when the command line contains -desktop; does nothing otherwise or once XR is already off.
        public static void ApplyCommandLine()
        {
            if (!CommandLineRequested) return;
            var manager = XRGeneralSettings.Instance != null ? XRGeneralSettings.Instance.Manager : null;
            if (manager == null || !manager.isInitializationComplete) return;
            manager.StopSubsystems();
            manager.DeinitializeLoader();
        }

        // True when any argument is -desktop, ignoring case.
        public static bool Requested(string[] args)
        {
            if (args == null) return false;
            foreach (var arg in args)
                if (string.Equals(arg, Flag, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }
    }
}
