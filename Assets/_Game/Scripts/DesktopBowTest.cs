using UnityEngine;
using UnityEngine.InputSystem;

namespace BowMan
{
    public sealed class DesktopBowTest : MonoBehaviour
    {
        [SerializeField] private Bow bow;
        [SerializeField] private VRBowInput vrInput;
        [SerializeField] private bool showPanel = true;

        private bool desktopControls;
        private Vector3 parkedPosition;
        private Quaternion parkedRotation;
        private float pitch = 5f;
        private float yaw = 1.5f;
        private float draw;
        private int shots;
        private int hits;
        private string lastHit = "Waiting for a shot";

        // Remembers the bow stand pose and listens for shots from either input mode.
        private void OnEnable()
        {
            if (bow == null) return;
            parkedPosition = bow.transform.position;
            parkedRotation = bow.transform.rotation;
            bow.Fired += OnFired;
        }

        // Stops a simulated draw and releases the shot listener when the test ends.
        private void OnDisable()
        {
            if (bow == null) return;
            bow.Fired -= OnFired;
            if (desktopControls) UseDesktopControls(false);
        }

        // Discards a desktop draw when the application loses focus.
        private void OnApplicationFocus(bool focused)
        {
            if (!focused && bow != null) bow.CancelDraw();
        }

        // Aims with arrow keys and simulates a hand pulling backward while Space is held.
        private void Update()
        {
            if (!desktopControls || bow == null || Keyboard.current == null) return;
            bow.SetHeld(bow.ShotAllowed);
            var keyboard = Keyboard.current;
            pitch = Mathf.Clamp(pitch + (keyboard.upArrowKey.ReadValue() - keyboard.downArrowKey.ReadValue()) * 30f * Time.deltaTime, -60f, 80f);
            yaw += (keyboard.rightArrowKey.ReadValue() - keyboard.leftArrowKey.ReadValue()) * 30f * Time.deltaTime;
            bow.transform.rotation = Quaternion.Euler(-pitch, yaw, 0f);
            if (keyboard.spaceKey.wasPressedThisFrame)
            {
                draw = 0f;
                bow.TryBeginDraw(bow.RestPosition);
            }
            if (bow.IsDrawing && keyboard.spaceKey.isPressed)
            {
                draw = Mathf.Min(draw + Time.deltaTime * 0.35f, bow.MaximumDrawDistance);
                bow.PullTo(bow.RestPosition - bow.transform.forward * draw);
            }
            if (keyboard.spaceKey.wasReleasedThisFrame) bow.Release();
        }

        // Switches between desktop simulation and tracked VR input without firing a pending draw.
        public void UseDesktopControls(bool enabled)
        {
            if (bow == null) return;
            bow.SetHeld(false);
            if (vrInput != null) vrInput.enabled = false;
            bow.transform.SetPositionAndRotation(parkedPosition, parkedRotation);
            desktopControls = enabled;
            if (enabled) bow.SetHeld(true);
            else if (vrInput != null) vrInput.enabled = true;
        }

        // Counts each released shot and listens for its first solid impact.
        private void OnFired(ArrowFlight arrow)
        {
            shots++;
            arrow.Hit += OnHit;
        }

        // Records which test target or floor the arrow struck.
        private void OnHit(RaycastHit hit)
        {
            hits++;
            lastHit = $"{hit.collider.name} at {hit.point:F2}";
        }

        // Shows desktop test controls and shot feedback without changing the VR interaction.
        private void OnGUI()
        {
            if (bow == null || !showPanel) return;
            GUILayout.BeginArea(new Rect(16, 16, 390, 195), GUI.skin.box);
            GUILayout.Label("Bow interaction test");
            if (vrInput != null && GUILayout.Button(vrInput.BowInLeftHand
                ? "Bow hand: Left (switch to right)" : "Bow hand: Right (switch to left)"))
                vrInput.SetBowHand(!vrInput.BowInLeftHand);
            if (GUILayout.Button(desktopControls ? "Use VR controls" : "Use desktop controls"))
                UseDesktopControls(!desktopControls);
            GUILayout.Label(desktopControls ? "Hold Space to draw; release to shoot. Arrow keys aim."
                : "Bow-hand grip equips; draw-hand trigger grabs the string.");
            GUILayout.Label($"Draw: {bow.DrawStrength:P0}   Shots: {shots}   Hits: {hits}");
            GUILayout.Label(lastHit);
            GUILayout.EndArea();
        }
    }
}
