using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR;

namespace BowMan.Menu
{
    // Right-controller ray that hovers mode panels and confirms with the trigger; follows the mouse without a headset.
    public sealed class ControllerPointer : MonoBehaviour
    {
        [SerializeField] private Transform rayOrigin;
        [SerializeField] private LineRenderer line;
        [SerializeField] private Camera desktopCamera;
        [SerializeField] private float maxDistance = 10f;

        private InputAction select;
        private Transform leftController;
        private MeshRenderer pointingHand;
        private SkinnedMeshRenderer relaxedHand;

        public ModeOption Hovered { get; private set; }
        public event Action<ModeOption> Selected;

        // Prepares the authored rig and the right trigger for menu selection.
        private void OnEnable()
        {
            if (rayOrigin == null) rayOrigin = transform;
            leftController = rayOrigin.parent != null ? rayOrigin.parent.Find("Left Controller") : null;
            if (leftController != null)
            {
                VRBowInput.HideTrackingMarker(leftController);
                VRBowInput.HideTrackingMarker(rayOrigin);
            }
            select = new InputAction("Select mode", InputActionType.Button, "<XRController>{RightHand}/triggerPressed");
            select.Enable();
        }

        // Releases the trigger action and hides the ray while the pointer is disabled.
        private void OnDisable()
        {
            select?.Dispose();
            select = null;
            ClearRay();
            ShowHands(false, false);
        }

        // Removes the menu hand visuals from the rig if the pointer is destroyed.
        private void OnDestroy()
        {
            if (pointingHand != null) Destroy(pointingHand.gameObject);
            if (relaxedHand != null) Destroy(relaxedHand.gameObject);
        }

        // Aims from the controller (or the mouse on desktop) and confirms on trigger or left click.
        private void Update()
        {
            var mouse = Mouse.current;
            var useMouse = !XRSettings.isDeviceActive && desktopCamera != null && mouse != null;
            var ray = useMouse
                ? desktopCamera.ScreenPointToRay(mouse.position.ReadValue())
                : new Ray(rayOrigin.position, rayOrigin.forward);
            if (!useMouse && leftController != null && XRSettings.isDeviceActive)
            {
                var right = XRController.rightHand;
                var rightTracked = IsTracked(right, XRNode.RightHand);
                var leftTracked = IsTracked(XRController.leftHand, XRNode.LeftHand);
                var aim = AimPose(right);
                PresentHands(aim, leftTracked, rightTracked);
                if (!rightTracked)
                {
                    ClearRay();
                    return;
                }
                ray = new Ray(aim.position, aim.rotation * Vector3.forward);
            }
            else ShowHands(false, false);

            Point(ray, !useMouse);
            var pressed = (select != null && select.WasPressedThisFrame()) || (useMouse && mouse.leftButton.wasPressedThisFrame);
            if (pressed) Confirm();
        }

        // Accepts the tracked pose from either the Input System or the native headset driver.
        private static bool IsTracked(XRController controller, XRNode node)
        {
            if (controller != null && controller.isTracked.isPressed) return true;
            var device = InputDevices.GetDeviceAtXRNode(node);
            return device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.isTracked, out var tracked) && tracked;
        }

        // Converts the controller's pointing pose from tracking space into world space.
        private Pose AimPose(XRController controller)
        {
            var position = controller?.TryGetChildControl<Vector3Control>("pointerPosition");
            var rotation = controller?.TryGetChildControl<QuaternionControl>("pointerRotation");
            if (position != null && rotation != null)
            {
                var aim = rotation.ReadValue();
                if (Quaternion.Dot(aim, aim) > 0.5f)
                {
                    var space = rayOrigin.parent;
                    return new Pose(space.TransformPoint(position.ReadValue()), space.rotation * aim);
                }
            }
            return new Pose(rayOrigin.position, rayOrigin.rotation);
        }

        // Places the right index fingertip at the ray origin and keeps the left hand at rest.
        private void PresentHands(Pose aim, bool leftTracked, bool rightTracked)
        {
            if (!leftTracked && !rightTracked)
            {
                ShowHands(false, false);
                return;
            }
            if (pointingHand == null)
            {
                var hand = new GameObject("Right pointing hand");
                hand.transform.SetParent(rayOrigin, false);
                var pointingMesh = hand.AddComponent<MeshFilter>();
                pointingMesh.sharedMesh = Resources.Load<Mesh>("ArcherHands/Point");
                pointingHand = hand.AddComponent<MeshRenderer>();
                pointingHand.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                pointingHand.receiveShadows = false;
                pointingHand.sharedMaterials = Resources.Load<SkinnedMeshRenderer>("ArcherHands/Right").sharedMaterials;
                relaxedHand = Instantiate(Resources.Load<SkinnedMeshRenderer>("ArcherHands/Left"), leftController, false);
            }
            pointingHand.transform.SetPositionAndRotation(aim.position, aim.rotation);
            ShowHands(leftTracked, rightTracked);
        }

        // Shows each menu hand only while its own controller supplies a pose.
        private void ShowHands(bool leftTracked, bool rightTracked)
        {
            if (relaxedHand != null) relaxedHand.enabled = leftTracked;
            if (pointingHand != null) pointingHand.enabled = rightTracked;
        }

        // Clears the cursor and its hover when the right controller cannot select a panel.
        private void ClearRay()
        {
            if (Hovered != null) Hovered.SetHighlighted(false);
            Hovered = null;
            if (line != null) line.enabled = false;
        }

        // Finds the panel under the ray, moves the highlight to it and draws the ray when asked.
        public void Point(Ray ray, bool drawRay = false)
        {
            ModeOption target = null;
            var end = ray.origin + ray.direction * maxDistance;
            if (Physics.Raycast(ray, out var hit, maxDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide))
            {
                target = hit.collider.GetComponentInParent<ModeOption>();
                end = hit.point;
            }
            if (target != Hovered)
            {
                if (Hovered != null) Hovered.SetHighlighted(false);
                Hovered = target;
                if (Hovered != null) Hovered.SetHighlighted(true);
            }
            if (line == null) return;
            line.enabled = drawRay;
            line.SetPosition(0, ray.origin);
            line.SetPosition(1, end);
        }

        // Reports the hovered panel as chosen; does nothing when no panel is hovered.
        public void Confirm()
        {
            if (Hovered != null && Hovered.isActiveAndEnabled) Selected?.Invoke(Hovered);
        }
    }
}
