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
        [SerializeField] private Vector3 gripRotationOffset;

        private InputAction holdAction;
        private InputAction drawAction;
        private InputAction bowTracked;
        private InputAction drawTracked;
        private Vector3 parkedPosition;
        private Quaternion parkedRotation;
        private bool focused = true;
        private bool paused;

        private Transform BowHand => bowInLeftHand ? leftController : rightController;
        private Transform DrawHand => bowInLeftHand ? rightController : leftController;

        // Creates handed controller bindings and remembers where the unequipped bow rests.
        private void OnEnable()
        {
            if (bow != null)
            {
                parkedPosition = bow.transform.position;
                parkedRotation = bow.transform.rotation;
            }
            var holdingHand = bowInLeftHand ? "LeftHand" : "RightHand";
            var drawingHand = bowInLeftHand ? "RightHand" : "LeftHand";
            holdAction = Button("Hold bow", holdingHand, "gripPressed");
            drawAction = Button("Draw string", drawingHand, "triggerPressed");
            bowTracked = Button("Bow hand tracked", holdingHand, "isTracked");
            drawTracked = Button("Draw hand tracked", drawingHand, "isTracked");
            Application.onBeforeRender += UpdatePose;
        }

        // Cancels the current shot and releases the component's owned input actions.
        private void OnDisable()
        {
            Application.onBeforeRender -= UpdatePose;
            if (bow != null) bow.SetHeld(false);
            holdAction?.Dispose();
            drawAction?.Dispose();
            bowTracked?.Dispose();
            drawTracked?.Dispose();
        }

        // Converts grip and trigger transitions into a held bow, draw, and release.
        private void Update()
        {
            if (bow == null || BowHand == null || DrawHand == null) return;
            if (!focused || paused || !bowTracked.IsPressed() || !drawTracked.IsPressed())
            {
                bow.SetHeld(false);
                UpdatePose();
                return;
            }
            bow.SetHeld(holdAction.IsPressed());
            UpdatePose();
            if (!bow.IsHeld) return;
            if (drawAction.WasPressedThisFrame()) bow.TryBeginDraw(DrawHand.position);
            if (bow.IsDrawing) bow.PullTo(DrawHand.position);
            if (drawAction.WasReleasedThisFrame()) bow.Release();
        }

        // Keeps the weapon aligned with the latest tracked hand pose before rendering.
        [BeforeRenderOrder(100)]
        private void UpdatePose()
        {
            if (bow == null || BowHand == null || DrawHand == null) return;
            if (bow.IsHeld)
            {
                bow.transform.SetPositionAndRotation(BowHand.TransformPoint(gripPositionOffset),
                    BowHand.rotation * Quaternion.Euler(gripRotationOffset));
                if (bow.IsDrawing) bow.PullTo(DrawHand.position);
            }
            else bow.transform.SetPositionAndRotation(parkedPosition, parkedRotation);
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
        private static InputAction Button(string name, string hand, string control)
        {
            var action = new InputAction(name, InputActionType.Button,
                $"<XRController>{{{hand}}}/{control}");
            action.Enable();
            return action;
        }
    }
}
