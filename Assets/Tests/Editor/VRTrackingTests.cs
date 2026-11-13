using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.XR.Management;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features.Interactions;

public class VRTrackingTests
{
    [Test]
    public void PcBuildStartsOpenXRWithTouchControllers()
    {
        var settings = XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(BuildTargetGroup.Standalone);
        Assert.That(settings, Is.Not.Null);
        Assert.That(settings.InitManagerOnStart, Is.True);
        Assert.That(settings.Manager.activeLoaders.OfType<OpenXRLoader>().Count(), Is.EqualTo(1));
        Assert.That(OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Standalone)
            .GetFeature<OculusTouchControllerProfile>().enabled, Is.True);
    }

    [Test]
    public void RigBindingsResolveHeadAndIndependentControllerDevices()
    {
        var rig = PrefabUtility.LoadPrefabContents("Assets/Prefabs/XRPlayer.prefab");
        var devices = new InputDevice[3];
        var drivers = rig.GetComponentsInChildren<TrackedPoseDriver>();
        try
        {
            Assert.That(drivers.Length, Is.EqualTo(3));
            Assert.That(rig.GetComponent<Unity.XR.CoreUtils.XROrigin>().Camera, Is.Not.Null);
            devices[0] = InputSystem.AddDevice<XRHMD>();
            devices[1] = InputSystem.AddDevice<XRController>();
            devices[2] = InputSystem.AddDevice<XRController>();
            InputSystem.SetDeviceUsage(devices[1], UnityEngine.InputSystem.CommonUsages.LeftHand);
            InputSystem.SetDeviceUsage(devices[2], UnityEngine.InputSystem.CommonUsages.RightHand);

            var head = rig.GetComponent<Unity.XR.CoreUtils.XROrigin>().Camera.GetComponent<TrackedPoseDriver>();
            var left = rig.transform.Find("Camera Offset/Left Controller").GetComponent<TrackedPoseDriver>();
            var right = rig.transform.Find("Camera Offset/Right Controller").GetComponent<TrackedPoseDriver>();
            drivers = new[] { head, left, right };
            for (var i = 0; i < drivers.Length; i++)
            {
                drivers[i].positionInput.action.Enable();
                drivers[i].rotationInput.action.Enable();
                drivers[i].trackingStateInput.action.Enable();
                Assert.That(drivers[i].positionInput.action.controls,
                    Does.Contain(devices[i][i == 0 ? "centerEyePosition" : "devicePosition"]));
                Assert.That(drivers[i].rotationInput.action.controls,
                    Does.Contain(devices[i][i == 0 ? "centerEyeRotation" : "deviceRotation"]));
                Assert.That(drivers[i].trackingStateInput.action.controls, Does.Contain(devices[i]["trackingState"]));
            }
            Assert.That(left.positionInput.action.controls.Any(control => control.device == devices[2]), Is.False);
            Assert.That(right.positionInput.action.controls.Any(control => control.device == devices[1]), Is.False);
        }
        finally
        {
            foreach (var driver in drivers)
            {
                driver.positionInput.action.Disable();
                driver.rotationInput.action.Disable();
                driver.trackingStateInput.action.Disable();
            }
            foreach (var device in devices)
                if (device != null) InputSystem.RemoveDevice(device);
            PrefabUtility.UnloadPrefabContents(rig);
        }
    }
}
