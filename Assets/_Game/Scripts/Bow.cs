using System;
using UnityEngine;

namespace BowMan
{
    public sealed class Bow : MonoBehaviour
    {
        [SerializeField] private ArrowFlight arrowPrefab;
        [SerializeField] private Transform stringTop;
        [SerializeField] private Transform stringRest;
        [SerializeField] private Transform stringBottom;
        [SerializeField] private LineRenderer bowString;
        [SerializeField] private Transform firingArcher;
        [SerializeField, Min(0.01f)] private float maximumDrawDistance = 0.36f;
        [SerializeField, Min(0.001f)] private float minimumDrawDistance = 0.03f;
        [SerializeField, Min(0.01f)] private float maximumLaunchSpeed = 25f;
        [SerializeField, Min(0.01f)] private float stringGrabRadius = 0.12f;
        [SerializeField, Min(0.01f)] private float arrowLength = 0.96f;

        private ArrowFlight nockedArrow;
        private Vector3 handPosition;

        public bool IsHeld { get; private set; }
        public bool IsDrawing { get; private set; }
        public bool ShotAllowed { get; private set; } = true;
        internal float ArrowLength => arrowLength;
        public Vector3 StringPosition => IsDrawing ? DrawPosition : RestPosition;
        public float MaximumDrawDistance => maximumDrawDistance;
        public float MaximumLaunchSpeed => maximumLaunchSpeed;
        public Vector3 RestPosition => stringRest.position;
        public Vector3 FullDrawTip => RestPosition + transform.forward * (arrowLength - maximumDrawDistance);
        // Measures backward pull before clamping the rendered string travel.
        public float DrawDistance => IsDrawing
            ? Mathf.Clamp(Vector3.Dot(stringRest.position - handPosition, transform.forward), 0f, maximumDrawDistance)
            : 0f;
        public float DrawStrength => DrawDistance / maximumDrawDistance;

        private Vector3 DrawPosition => stringRest.position +
            Vector3.ClampMagnitude(handPosition - stringRest.position, maximumDrawDistance);

        public event Action<ArrowFlight> Fired;

        // Sets this bow instance's full-draw speed for its game mode.
        public void SetMaximumLaunchSpeed(float speed)
        {
            if (IsPositiveFinite(speed)) maximumLaunchSpeed = speed;
        }

        // Assigns the living local actor whose colliders this bow's arrows ignore.
        public void SetOwner(Transform owner) => firingArcher = owner;

        // Locks all input paths during another turn, injury, flight or defeat and discards any pending draw.
        public void SetShotAllowed(bool allowed)
        {
            ShotAllowed = allowed;
            if (!allowed) SetHeld(false);
        }

        // Copies the prop renderers and local string anchors without copying the firing component.
        internal void CopyVisuals(Transform root, out LineRenderer line, out Vector3 top, out Vector3 rest, out Vector3 bottom)
        {
            foreach (Transform child in transform)
                if (child.TryGetComponent<MeshFilter>(out _)) Instantiate(child.gameObject, root, false);
            line = Instantiate(bowString, root, false);
            top = transform.InverseTransformPoint(stringTop.position);
            rest = transform.InverseTransformPoint(stringRest.position);
            bottom = transform.InverseTransformPoint(stringBottom.position);
        }

        // Places the string's endpoints and its moving center in world space.
        internal static void SetString(LineRenderer line, Vector3 top, Vector3 center, Vector3 bottom)
        {
            line.useWorldSpace = true;
            line.positionCount = 3;
            line.SetPosition(0, top);
            line.SetPosition(1, center);
            line.SetPosition(2, bottom);
        }

        // Refreshes the string and nocked arrow after the hand poses have moved.
        private void LateUpdate() => RefreshVisuals();

        // Cancels any unfinished draw when the bow is disabled.
        private void OnDisable() => SetHeld(false);

        // Equips or unequips the bow and cancels a nocked shot when unequipped.
        public void SetHeld(bool held)
        {
            IsHeld = held && ShotAllowed;
            if (!IsHeld) CancelDraw();
        }

        // Nocks an arrow only when the held bow's resting string is within reach.
        public bool TryBeginDraw(Vector3 position)
        {
            if (!ShotAllowed || !IsHeld || IsDrawing || arrowPrefab == null || stringRest == null ||
                !IsFinite(position) || !IsFinite(stringRest.position) ||
                !IsPositiveFinite(maximumDrawDistance) || !IsPositiveFinite(maximumLaunchSpeed) ||
                Vector3.Distance(position, stringRest.position) > stringGrabRadius)
                return false;

            handPosition = position;
            nockedArrow = Instantiate(arrowPrefab, transform);
            IsDrawing = true;
            RefreshVisuals();
            return true;
        }

        // Moves the drawn string with the hand, cancelling invalid tracking positions.
        public void PullTo(Vector3 position)
        {
            if (!IsDrawing) return;
            if (!IsFinite(position))
            {
                CancelDraw();
                return;
            }
            handPosition = position;
            RefreshVisuals();
        }

        // Launches one nocked arrow using the current aim and backward draw strength.
        public ArrowFlight Release()
        {
            if (!IsDrawing) return null;
            var distance = DrawDistance;
            var direction = stringRest.position - DrawPosition;
            var arrow = nockedArrow;
            nockedArrow = null;
            IsDrawing = false;
            RefreshVisuals();

            if (!ShotAllowed || !IsHeld || arrow == null || distance < minimumDrawDistance ||
                !IsFinite(direction) || direction.sqrMagnitude <= 0f)
            {
                if (arrow != null) Destroy(arrow.gameObject);
                return null;
            }

            arrow.transform.SetParent(null, true);
            var strength = distance / maximumDrawDistance;
            arrow.Launch(direction.normalized * (maximumLaunchSpeed * strength), firingArcher, shotStrength: strength);
            Fired?.Invoke(arrow);
            return arrow;
        }

        // Removes the nocked arrow and restores the resting string without firing.
        public void CancelDraw()
        {
            IsDrawing = false;
            if (nockedArrow != null) Destroy(nockedArrow.gameObject);
            nockedArrow = null;
            RefreshVisuals();
        }

        // Draws the string through its nock and keeps the preview arrow's tail on it.
        public void RefreshVisuals()
        {
            if (stringRest == null) return;
            var position = IsDrawing ? DrawPosition : stringRest.position;
            if (bowString != null && stringTop != null && stringBottom != null)
                SetString(bowString, stringTop.position, position, stringBottom.position);
            if (nockedArrow == null) return;
            var direction = stringRest.position - position;
            if (direction.sqrMagnitude < 0.000001f) direction = transform.forward;
            direction.Normalize();
            nockedArrow.transform.SetPositionAndRotation(position + direction * arrowLength,
                Quaternion.LookRotation(direction, transform.up));
        }

        // Rejects nonfinite pose coordinates before they reach the arrow or string geometry.
        private static bool IsFinite(Vector3 value) =>
            !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
            !float.IsNaN(value.y) && !float.IsInfinity(value.y) &&
            !float.IsNaN(value.z) && !float.IsInfinity(value.z);

        // Checks that draw and launch settings can produce a finite nonzero velocity.
        private static bool IsPositiveFinite(float value) =>
            value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
