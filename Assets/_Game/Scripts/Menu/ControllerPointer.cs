using System;
using UnityEngine;
using UnityEngine.InputSystem;

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

        public ModeOption Hovered { get; private set; }
        public event Action<ModeOption> Selected;

        // Creates the right-trigger action used to confirm the hovered panel.
        private void OnEnable()
        {
            if (rayOrigin == null) rayOrigin = transform;
            select = new InputAction("Select mode", InputActionType.Button, "<XRController>{RightHand}/triggerPressed");
            select.Enable();
        }

        // Gives dynamically created lobbies the same pointer as the main menu.
        public void AddVisibleRay()
        {
            line = gameObject.AddComponent<LineRenderer>();
            line.positionCount = 2;
            line.useWorldSpace = true;
            line.widthMultiplier = .003f;
            line.sharedMaterial = Resources.Load<Material>("Interface/Brass");
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        // Releases the trigger action.
        private void OnDisable()
        {
            select?.Dispose();
            select = null;
            if (line != null) line.enabled = false;
        }

        // Aims from the controller (or the mouse on desktop) and confirms on trigger or left click.
        private void Update()
        {
            if (!DesktopBowTest.DesktopAvailable && VRBowInput.HeadsetInputBlocked)
            {
                if (line != null) line.enabled = false;
                return;
            }
            var mouse = Mouse.current;
            var useMouse = DesktopBowTest.DesktopAvailable && !VRBowInput.HeadsetActive && desktopCamera != null && mouse != null;
            var ray = useMouse
                ? desktopCamera.ScreenPointToRay(mouse.position.ReadValue())
                : new Ray(rayOrigin.position, rayOrigin.forward);
            Point(ray, !useMouse);
            var pressed = (select != null && select.WasPressedThisFrame()) || (useMouse && mouse.leftButton.wasPressedThisFrame);
            if (pressed) Confirm();
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
