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
        private SkinnedMeshRenderer leftHand, rightHand;
        private float pinch;
        private bool focused = true;
        private bool paused;

        private Transform BowHand => bowInLeftHand ? leftController : rightController;
        private Transform DrawHand => bowInLeftHand ? rightController : leftController;
        public bool BowInLeftHand => bowInLeftHand;

        // Replaces the tracked rig's markers with first-person archer hands.
        public void ShowArcherHands()
        {
            if (leftHand != null) return;
            leftHand = CreateHand(leftController, "Left");
            rightHand = CreateHand(rightController, "Right");
        }

        // Attaches one hand to its tracked controller and hides the original marker.
        private static SkinnedMeshRenderer CreateHand(Transform controller, string side)
        {
            var prefab = Resources.Load<SkinnedMeshRenderer>("ArcherHands/" + side);
            var hand = Instantiate(prefab, controller, false);
            hand.gameObject.SetActive(false);
            HideTrackingMarker(controller);
            return hand;
        }

        // Hides the marker inherited by playable rigs from the tracking prefab.
        public static void HideTrackingMarker(Transform controller)
        {
            var marker = controller.Find("Controller Marker");
            if (marker != null) marker.gameObject.SetActive(false);
        }

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
            if (leftHand != null) leftHand.gameObject.SetActive(false);
            if (rightHand != null) rightHand.gameObject.SetActive(false);
            pinch = 0f;
            drawAction?.Dispose();
            bowTracked?.Dispose();
            drawTracked?.Dispose();
        }

        // Removes the visual hands from a surviving tracking rig when its bow input is replaced.
        private void OnDestroy()
        {
            RemoveHand(leftHand);
            RemoveHand(rightHand);
        }

        // Removes the rendered hand from its controller.
        private static void RemoveHand(SkinnedMeshRenderer hand)
        {
            if (hand != null) Destroy(hand.gameObject);
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
            if (!bow.isActiveAndEnabled) return;
            pinch = Mathf.MoveTowards(pinch, bow.IsDrawing ? 100f : 0f, Time.unscaledDeltaTime * 1200f);
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

        // Updates the tracked bow grip and uses relaxed hands while the bow is inactive.
        [BeforeRenderOrder(100)]
        private void UpdatePose()
        {
            if (bow == null || BowHand == null || DrawHand == null) return;
            var equipped = bow.isActiveAndEnabled;
            if (equipped)
            {
                bow.transform.SetPositionAndRotation(BowHand.TransformPoint(gripPositionOffset),
                    BowHand.rotation * Quaternion.Euler(gripRotationOffset));
                if (bow.IsDrawing) bow.PullTo(DrawHand.position);
            }
            if (leftHand == null) return;
            var grip = bowInLeftHand ? leftHand : rightHand;
            var draw = bowInLeftHand ? rightHand : leftHand;
            grip.gameObject.SetActive(focused && !paused && bowTracked.IsPressed());
            draw.gameObject.SetActive(focused && !paused && drawTracked.IsPressed());
            if (equipped)
                grip.transform.SetPositionAndRotation(bow.transform.position,
                    bow.transform.rotation * Quaternion.Euler(-90f, 0f, 0f));
            else
                grip.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            draw.transform.SetPositionAndRotation(equipped && bow.IsDrawing ? bow.StringPosition : DrawHand.position, DrawHand.rotation);
            grip.SetBlendShapeWeight(0, equipped ? 100f : 0f);
            grip.SetBlendShapeWeight(1, 0f);
            draw.SetBlendShapeWeight(0, 0f);
            draw.SetBlendShapeWeight(1, equipped ? pinch : 0f);
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
