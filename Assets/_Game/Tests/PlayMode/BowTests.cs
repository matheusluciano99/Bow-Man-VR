using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using BowMan;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public class BowTests
{
    private readonly List<GameObject> objects = new List<GameObject>();
    private readonly List<InputDevice> devices = new List<InputDevice>();
    private readonly List<InputDevice> disabledControllers = new List<InputDevice>();
    private readonly Dictionary<InputDevice, int> pressedButtons = new Dictionary<InputDevice, int>();
    private static readonly Vector3 Origin = new Vector3(1000f, 1000f, 1000f);

    // Creates isolated test objects and records them for cleanup.
    private GameObject Create(string name)
    {
        var obj = new GameObject(name);
        obj.transform.position = Origin;
        objects.Add(obj);
        return obj;
    }

    // Supplies serialized scene references without adding runtime configuration methods.
    private static void Set(object target, string field, object value) =>
        target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);

    // Builds a bow with a resting nock and a real arrow-flight component.
    private Bow CreateBow()
    {
        var bow = Create("Bow").AddComponent<Bow>();
        var rest = Create("String rest").transform;
        rest.SetParent(bow.transform, true);
        Set(bow, "stringRest", rest);
        Set(bow, "arrowPrefab", Create("Arrow source").AddComponent<ArrowFlight>());
        bow.Fired += arrow => objects.Add(arrow.gameObject);
        return bow;
    }

    // Starts a draw at the resting nock, then moves the hand to its release position.
    private static ArrowFlight Shoot(Bow bow, Vector3 pull)
    {
        bow.SetHeld(true);
        Assert.That(bow.TryBeginDraw(bow.RestPosition), Is.True);
        bow.PullTo(bow.RestPosition + pull);
        return bow.Release();
    }

    // Removes temporary devices, arrows and scene objects after each test.
    [UnityTearDown]
    public IEnumerator CleanUp()
    {
        foreach (var obj in objects) if (obj != null) Object.Destroy(obj);
        yield return null;
        foreach (var device in devices) if (device.added) InputSystem.RemoveDevice(device);
        foreach (var device in disabledControllers) if (device.added) InputSystem.EnableDevice(device);
        disabledControllers.Clear();
        devices.Clear();
        pressedButtons.Clear();
        objects.Clear();
        InputSystem.RemoveLayout("BowTestController");
    }

    // Requires an equipped bow and a hand close enough to the resting string before nocking.
    [Test]
    public void NockingRequiresEquippedBowAndNearbyString()
    {
        var bow = CreateBow();
        Assert.That(bow.TryBeginDraw(bow.RestPosition), Is.False);
        bow.SetHeld(true);
        Assert.That(bow.TryBeginDraw(bow.RestPosition + Vector3.back * 0.3f), Is.False);
        Assert.That(bow.TryBeginDraw(bow.RestPosition + new Vector3(float.NaN, 0f, 0f)), Is.False);
        Assert.That(bow.TryBeginDraw(bow.RestPosition), Is.True);
        Assert.That(bow.TryBeginDraw(bow.RestPosition), Is.False);
    }

    // Cancels a pending draw and prevents either input path from nocking during a locked turn.
    [Test]
    public void MatchGateCancelsDrawAndRejectsFurtherShots()
    {
        var bow = CreateBow();
        bow.SetHeld(true);
        Assert.That(bow.TryBeginDraw(bow.RestPosition), Is.True);
        bow.PullTo(bow.RestPosition + Vector3.back * 0.7f);
        bow.SetShotAllowed(false);
        bow.SetHeld(true);
        Assert.That(bow.IsHeld, Is.False);
        Assert.That(bow.IsDrawing, Is.False);
        Assert.That(bow.Release(), Is.Null);
        Assert.That(bow.TryBeginDraw(bow.RestPosition), Is.False);
        bow.SetShotAllowed(true);
        bow.SetHeld(true);
        Assert.That(bow.TryBeginDraw(bow.RestPosition), Is.True);
    }

    // Zero, forward and sideways hand movement cannot produce a powered shot.
    [Test]
    public void OnlyBackwardDrawCanFire()
    {
        var bow = CreateBow();
        Assert.That(Shoot(bow, Vector3.zero), Is.Null);
        Assert.That(Shoot(bow, Vector3.forward * 0.5f), Is.Null);
        Assert.That(Shoot(bow, Vector3.right * 0.5f), Is.Null);
        Assert.That(Shoot(bow, Vector3.back * 0.01f), Is.Null);
    }

    // Measures real arrow travel to compare half draw, full draw and capped overdraw.
    [UnityTest]
    public IEnumerator DrawStrengthChangesFlightAndOverdrawIsCapped()
    {
        var bow = CreateBow();
        var half = Shoot(bow, Vector3.back * 0.35f);
        var full = Shoot(bow, Vector3.back * 0.70f);
        var over = Shoot(bow, Vector3.back * 1.4f);
        var halfStart = half.transform.position;
        var fullStart = full.transform.position;
        var overStart = over.transform.position;
        yield return new WaitForFixedUpdate();
        var halfTravel = half.transform.position.z - halfStart.z;
        var fullTravel = full.transform.position.z - fullStart.z;
        var overTravel = over.transform.position.z - overStart.z;
        Assert.That(halfTravel, Is.GreaterThan(0f));
        Assert.That(fullTravel, Is.EqualTo(halfTravel * 2f).Within(0.002f));
        Assert.That(overTravel, Is.EqualTo(fullTravel).Within(0.002f));
    }

    // Rotating the bow and hand line redirects the launched arrow in world space.
    [UnityTest]
    public IEnumerator RotatedAimSteersFlightAndReleaseFiresOnlyOnce()
    {
        var bow = CreateBow();
        bow.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
        var shots = 0;
        bow.Fired += _ => shots++;
        var arrow = Shoot(bow, -bow.transform.forward * 0.7f);
        var start = arrow.transform.position;
        Assert.That(bow.Release(), Is.Null);
        yield return new WaitForFixedUpdate();
        Assert.That(arrow.transform.position.x, Is.GreaterThan(start.x));
        Assert.That(arrow.transform.position.z, Is.EqualTo(start.z).Within(0.002f));
        Assert.That(shots, Is.EqualTo(1));
    }

    // Losing the bow, cancelling, invalid tracking and disabling all discard the nocked shot.
    [Test]
    public void InterruptedDrawNeverFires()
    {
        var bow = CreateBow();
        var shots = 0;
        bow.Fired += _ => shots++;
        for (var interruption = 0; interruption < 4; interruption++)
        {
            bow.SetHeld(true);
            Assert.That(bow.TryBeginDraw(bow.RestPosition), Is.True);
            bow.PullTo(bow.RestPosition + Vector3.back * 0.7f);
            if (interruption == 0) bow.SetHeld(false);
            if (interruption == 1) bow.CancelDraw();
            if (interruption == 2) bow.PullTo(new Vector3(float.PositiveInfinity, 0f, 0f));
            if (interruption == 3) bow.enabled = false;
            Assert.That(bow.IsDrawing, Is.False);
            Assert.That(bow.Release(), Is.Null);
        }
        Assert.That(shots, Is.Zero);
    }

    // Exercises left-bow/right-draw bindings through the real Input System.
    [UnityTest]
    public IEnumerator LeftBowControlsFireAndTrackingLossCancels() => CheckControllerInput(true);

    // Exercises right-bow/left-draw bindings through the real Input System.
    [UnityTest]
    public IEnumerator RightBowControlsFireAndTrackingLossCancels() => CheckControllerInput(false);

    // Queues a complete controller state, preserving buttons changed earlier in the frame.
    private void Button(InputDevice device, string control, bool pressed)
    {
        var names = new[] { "isTracked", "gripPressed", "triggerPressed" };
        var bit = 1 << System.Array.IndexOf(names, control);
        pressedButtons.TryGetValue(device, out var state);
        state = pressed ? state | bit : state & ~bit;
        pressedButtons[device] = state;
        using (StateEvent.From(device, out var eventPtr))
        {
            for (var i = 0; i < names.Length; i++)
                ((ButtonControl)device[names[i]]).WriteValueIntoEvent((state & (1 << i)) != 0 ? 1f : 0f, eventPtr);
            InputSystem.QueueEvent(eventPtr);
        }
    }

    // Verifies handed bindings, one release, tracking loss, focus loss and component cleanup.
    private IEnumerator CheckControllerInput(bool leftBow)
    {
        foreach (var device in InputSystem.devices)
            if (device is UnityEngine.InputSystem.XR.XRController && device.enabled)
            {
                disabledControllers.Add(device);
                InputSystem.DisableDevice(device);
            }
        InputSystem.RegisterLayout(@"{""name"":""BowTestController"",""extend"":""XRController"",""controls"":[{""name"":""gripPressed"",""layout"":""Button""},{""name"":""triggerPressed"",""layout"":""Button""}]}");
        var left = InputSystem.AddDevice("BowTestController");
        var right = InputSystem.AddDevice("BowTestController");
        devices.Add(left);
        devices.Add(right);
        InputSystem.SetDeviceUsage(left, CommonUsages.LeftHand);
        InputSystem.SetDeviceUsage(right, CommonUsages.RightHand);
        var holding = leftBow ? left : right;
        var drawing = leftBow ? right : left;
        var bow = CreateBow();
        var leftPose = Create("Left controller").transform;
        var rightPose = Create("Right controller").transform;
        var holdingPose = leftBow ? leftPose : rightPose;
        holdingPose.rotation = Quaternion.Euler(10f, 25f, 15f);
        var drawPose = leftBow ? rightPose : leftPose;
        var inputObject = Create("VR bow input");
        inputObject.SetActive(false);
        var input = inputObject.AddComponent<VRBowInput>();
        Set(input, "bow", bow);
        Set(input, "leftController", leftPose);
        Set(input, "rightController", rightPose);
        Set(input, "bowInLeftHand", !leftBow);
        var shots = 0;
        bow.Fired += _ => shots++;
        inputObject.SetActive(true);
        input.SetBowHand(leftBow);
        input.SendMessage("OnApplicationFocus", true);
        Button(left, "isTracked", true);
        Button(right, "isTracked", true);
        yield return null;
        yield return null;
        Assert.That(bow.IsHeld, Is.True);
        Assert.That(Vector3.Distance(bow.transform.position, holdingPose.position), Is.LessThan(0.0001f));
        Assert.That(Vector3.Dot(bow.transform.up, holdingPose.forward), Is.GreaterThan(0.9999f));
        Assert.That(Vector3.Dot(bow.transform.forward, -holdingPose.up), Is.GreaterThan(0.9999f));
        Button(drawing, "triggerPressed", true);
        yield return null;
        yield return null;
        Assert.That(bow.IsDrawing, Is.True);
        Button(holding, "gripPressed", true);
        yield return null;
        Button(holding, "gripPressed", false);
        yield return null;
        Assert.That(bow.IsDrawing, Is.True, "Grip is not an equip or release control");
        drawPose.position = bow.RestPosition - bow.transform.forward * 0.7f;
        yield return null;
        Button(drawing, "triggerPressed", false);
        yield return null;
        yield return null;
        Assert.That(shots, Is.EqualTo(1));

        bow.SetShotAllowed(false);
        holdingPose.position += Vector3.right * 0.1f;
        yield return null;
        Assert.That(bow.IsHeld, Is.False);
        Assert.That(Vector3.Distance(bow.transform.position, holdingPose.position), Is.LessThan(0.0001f));
        Button(drawing, "triggerPressed", true);
        yield return null;
        Assert.That(bow.IsDrawing, Is.False);
        Assert.That(shots, Is.EqualTo(1));
        Button(drawing, "triggerPressed", false);
        bow.SetShotAllowed(true);
        yield return null;

        drawPose.position = bow.RestPosition;
        Button(drawing, "triggerPressed", true);
        yield return null;
        yield return null;
        Assert.That(bow.IsDrawing, Is.True);
        input.SetBowHand(!leftBow);
        Assert.That(bow.IsDrawing, Is.False);
        Assert.That(bow.IsHeld, Is.False);
        Assert.That(shots, Is.EqualTo(1));
        input.SetBowHand(leftBow);
        // Restores simulated application focus after the input actions rebind.
        input.SendMessage("OnApplicationFocus", true);
        Button(drawing, "triggerPressed", false);
        yield return null;
        yield return null;
        drawPose.position = bow.RestPosition;
        Button(drawing, "triggerPressed", true);
        yield return null;
        yield return null;
        drawPose.position = bow.RestPosition - bow.transform.forward * 0.7f;
        Assert.That(bow.IsDrawing, Is.True);
        yield return null;
        Button(drawing, "isTracked", false);
        Button(drawing, "triggerPressed", false);
        yield return null;
        yield return null;
        Assert.That(bow.IsHeld, Is.False);
        Assert.That(bow.IsDrawing, Is.False);
        Assert.That(shots, Is.EqualTo(1));

        Button(drawing, "isTracked", true);
        drawPose.position = bow.RestPosition;
        yield return null;
        yield return null;
        Button(drawing, "triggerPressed", true);
        yield return null;
        yield return null;
        Assert.That(bow.IsDrawing, Is.True);
        input.SendMessage("OnApplicationFocus", false);
        Button(drawing, "triggerPressed", false);
        yield return null;
        yield return null;
        Assert.That(bow.IsHeld, Is.False);
        Assert.That(shots, Is.EqualTo(1));
        input.enabled = false;
        Assert.That(bow.IsDrawing, Is.False);
    }
}
