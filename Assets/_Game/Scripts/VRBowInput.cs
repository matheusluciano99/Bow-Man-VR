using UnityEngine;
using UnityEngine.InputSystem;

namespace BowMan
{
    public sealed class VRBowInput : MonoBehaviour
    {
        [SerializeField] private Bow bow;
        [SerializeField] private Transform leftController;
        [SerializeField] private Transform rightController;
        [SerializeField] private bool bowInLeftHand = true;
        [SerializeField] private Vector3 gripPositionOffset;
        [SerializeField] private Vector3 gripRotationOffset = new Vector3(90f, 0f, 0f);

        private InputAction drawAction;
        private InputAction bowTracked;
        private InputAction drawTracked;
        private bool focused = true;
        private bool paused;

        private Transform BowHand => bowInLeftHand ? leftController : rightController;
        private Transform DrawHand => bowInLeftHand ? rightController : leftController;
        public bool BowInLeftHand => bowInLeftHand;

        // Creates handed trigger and tracking bindings for the continuously equipped bow.
        private void OnEnable()
        {
            focused = Application.isFocused;
            var holdingHand = bowInLeftHand ? "LeftHand" : "RightHand";
            var drawingHand = bowInLeftHand ? "RightHand" : "LeftHand";
            drawAction = Button("Draw string", drawingHand, "triggerPressed", false);
            bowTracked = Button("Bow hand tracked", holdingHand, "isTracked");
            drawTracked = Button("Draw hand tracked", drawingHand, "isTracked");
            Application.onBeforeRender += UpdatePose;
        }

        // Cancels the current shot and releases the component's owned input actions.
        private void OnDisable()
        {
            Application.onBeforeRender -= UpdatePose;
            if (bow != null) bow.SetHeld(false);
            drawAction?.Dispose();
            bowTracked?.Dispose();
            drawTracked?.Dispose();
        }

        // Switches controller roles, cancelling a tracked draw and rebuilding handed bindings.
        public void SetBowHand(bool left)
        {
            if (bowInLeftHand == left) return;
            var active = isActiveAndEnabled;
            if (active) enabled = false;
            bowInLeftHand = left;
            if (active) enabled = true;
        }

        // Equips while both hands are tracked and converts the opposite trigger into draw and release.
        private void Update()
        {
            if (bow == null || BowHand == null || DrawHand == null) return;
            if (!focused || paused || !bowTracked.IsPressed() || !drawTracked.IsPressed())
            {
                bow.SetHeld(false);
                UpdatePose();
                return;
            }
            bow.SetHeld(true);
            UpdatePose();
            if (!bow.IsHeld) return;
            if (drawAction.WasPressedThisFrame()) bow.TryBeginDraw(DrawHand.position);
            if (bow.IsDrawing) bow.PullTo(DrawHand.position);
            if (drawAction.WasReleasedThisFrame()) bow.Release();
        }

        // Keeps the bow attached to the tracked grip even while shooting is locked or a draw is cancelled.
        [BeforeRenderOrder(100)]
        private void UpdatePose()
        {
            if (bow == null || BowHand == null || DrawHand == null) return;
            bow.transform.SetPositionAndRotation(BowHand.TransformPoint(gripPositionOffset),
                BowHand.rotation * Quaternion.Euler(gripRotationOffset));
            if (bow.IsDrawing) bow.PullTo(DrawHand.position);
        }

        // Cancels a nocked shot when application focus is lost.
        private void OnApplicationFocus(bool hasFocus)
        {
            focused = hasFocus;
            if (!focused && bow != null) bow.SetHeld(false);
        }

        // Cancels a nocked shot when the XR application pauses.
        private void OnApplicationPause(bool isPaused)
        {
            paused = isPaused;
            if (paused && bow != null) bow.SetHeld(false);
        }

        // Creates and enables a button action scoped to one tracked controller hand.
        private static InputAction Button(string name, string hand, string control, bool checkInitialState = true)
        {
            var action = new InputAction(name, InputActionType.Button,
                $"<XRController>{{{hand}}}/{control}");
            action.wantsInitialStateCheck = checkInitialState;
            action.Enable();
            return action;
        }

        // Stores the menu's bow-hand selection locally for either duel mode.
        public static bool PreferredBowLeft
        {
            get => PlayerPrefs.GetInt("BowMan.BowLeft", 1) != 0;
            set
            {
                PlayerPrefs.SetInt("BowMan.BowLeft", value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        // Shows the next action for the player's input method and current string state.
        public string ShotHint(bool desktop)
        {
            if (desktop) return bow.IsDrawing ? "Release Space to shoot" : "Arrow keys to aim • Hold Space to draw";
            if (!UnityEngine.XR.XRSettings.isDeviceActive) return "Press Tab for desktop controls";
            if (bow.IsDrawing) return "Pull back, aim, then release the trigger";
            var hand = BowInLeftHand ? "right" : "left";
            return $"Bring your {hand} hand to the string • Hold its trigger";
        }
    }
}
