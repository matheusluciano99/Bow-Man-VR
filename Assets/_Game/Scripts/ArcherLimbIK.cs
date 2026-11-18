using UnityEngine;

namespace BowMan
{
    public static class ArcherLimbIK
    {
        // Reaches early in the descent and keeps the wound covered until standing is underway.
        public static float AbdomenReachWeight(float progress, bool recovering) => recovering
            ? 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.4f, 0.85f, progress))
            : Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.04f, 0.34f, progress));

        // Braces the actual abdominal wound with both hands while retaining the character's measured arm lengths.
        public static void ReachAbdomen(Animator animator, Vector3 point, Vector3 outward, float weight, float passAround = 0f)
        {
            if (weight <= 0f) return;
            var spine = animator.GetBoneTransform(HumanBodyBones.Spine);
            for (var handIndex = 0; handIndex < 2; handIndex++)
            {
                var left = handIndex == 1;
                var wrist = animator.GetBoneTransform(left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
                var side = spine.right * (left ? -1f : 1f);
                var target = point + side * 0.079f + outward * 0.068f + spine.up * (left ? 0.05f : 0.062f);
                Solve(animator, left ? HumanBodyBones.LeftUpperArm : HumanBodyBones.RightUpperArm,
                    left ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm,
                    left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand,
                    Vector3.Lerp(wrist.position, target, weight) + (side * 0.12f + outward * 0.21f) * passAround,
                    side - spine.up * 0.6f + outward * (0.85f + 0.3f * passAround));
                var middle = animator.GetBoneTransform(left ? HumanBodyBones.LeftMiddleProximal : HumanBodyBones.RightMiddleProximal);
                var index = animator.GetBoneTransform(left ? HumanBodyBones.LeftIndexProximal : HumanBodyBones.RightIndexProximal);
                var little = animator.GetBoneTransform(left ? HumanBodyBones.LeftLittleProximal : HumanBodyBones.RightLittleProximal);
                var fingers = middle.position - wrist.position;
                var palm = Vector3.Cross(fingers, index.position - little.position) * (left ? 1f : -1f);
                var turn = Quaternion.LookRotation(-side, -outward) * Quaternion.Inverse(Quaternion.LookRotation(fingers, palm));
                wrist.rotation = Quaternion.Slerp(wrist.rotation, turn * wrist.rotation, weight);
            }
        }

        // Holds the wound early in the neck sequence and releases the arm before landing.
        public static float NeckReachWeight(float time) => Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(time / 0.45f)) *
            (1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((time - 1.1f) / 0.7f)));

        // Fits the palm to the specific neck surface point using the same arm solver as authored injuries.
        public static void ReachNeck(Animator animator, Vector3 point, Vector3 outward, float weight)
        {
            if (weight <= 0f) return;
            var neck = animator.GetBoneTransform(HumanBodyBones.Neck);
            var wrist = animator.GetBoneTransform(HumanBodyBones.RightHand);
            var target = point - neck.up * 0.045f + outward * 0.025f;
            Solve(animator, HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand,
                Vector3.Lerp(wrist.position, target, weight), animator.transform.right - neck.up * 0.3f + outward * 0.5f);
            PointHand(animator, false, neck.up, weight);
        }

        // Aims the open fingers toward the contact direction without translating the supported wrist.
        public static void PointHand(Animator animator, bool left, Vector3 direction, float progress)
        {
            var hand = animator.GetBoneTransform(left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
            var finger = animator.GetBoneTransform(left ? HumanBodyBones.LeftMiddleDistal : HumanBodyBones.RightMiddleDistal);
            var rotation = Quaternion.FromToRotation(finger.position - hand.position, direction) * hand.rotation;
            hand.rotation = Quaternion.Slerp(hand.rotation, rotation, progress);
        }

        // Swivels the elbow around its wrist target and nudges the wrist away only when needed for clearance.
        public static void ClearForearm(Animator animator, bool left, Collider[] armVolumes, Collider[] bodyVolumes,
            int wristOffsetSteps = 5, float clearanceTolerance = 0.0002f)
        {
            var bestDepth = DeepestOverlap(armVolumes, bodyVolumes, out var away);
            if (bestDepth <= clearanceTolerance) return;

            var upperId = left ? HumanBodyBones.LeftUpperArm : HumanBodyBones.RightUpperArm;
            var lowerId = left ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm;
            var wristId = left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand;
            var upper = animator.GetBoneTransform(upperId);
            var lower = animator.GetBoneTransform(lowerId);
            var wrist = animator.GetBoneTransform(wristId);
            var upperStart = upper.localRotation;
            var lowerStart = lower.localRotation;
            var wristRotation = wrist.rotation;
            var target = wrist.position;
            var axis = (target - upper.position).normalized;
            var bend = lower.position - upper.position;
            var bestUpper = upperStart;
            var bestLower = lowerStart;

            for (var push = 0; push <= wristOffsetSteps && bestDepth > clearanceTolerance; push++)
            for (var swivel = 0; swivel <= 24 && bestDepth > clearanceTolerance; swivel++)
            {
                var angle = (swivel + 1) / 2 * 3f * (swivel % 2 == 0 ? -1f : 1f);
                upper.localRotation = upperStart;
                lower.localRotation = lowerStart;
                Solve(animator, upperId, lowerId, wristId, target + away * (push * 0.008f),
                    Quaternion.AngleAxis(angle, axis) * bend);
                wrist.rotation = wristRotation;
                var depth = DeepestOverlap(armVolumes, bodyVolumes, out _);
                if (depth >= bestDepth) continue;
                bestDepth = depth;
                bestUpper = upper.localRotation;
                bestLower = lower.localRotation;
            }

            upper.localRotation = bestUpper;
            lower.localRotation = bestLower;
            wrist.rotation = wristRotation;
        }

        // Finds the deepest penetration and its separating direction using the current bone poses.
        internal static float DeepestOverlap(Collider[] armVolumes, Collider[] bodyVolumes, out Vector3 away)
        {
            var deepest = 0f;
            away = Vector3.zero;
            foreach (var arm in armVolumes)
            foreach (var body in bodyVolumes)
            {
                if (!Physics.ComputePenetration(arm, arm.transform.position, arm.transform.rotation,
                        body, body.transform.position, body.transform.rotation, out var direction, out var depth) ||
                    depth <= deepest) continue;
                deepest = depth;
                away = direction;
            }
            return deepest;
        }

        // Places a two-bone chain using its measured lengths and a projected bend hint, without changing bone lengths.
        public static void Solve(Animator animator, HumanBodyBones upperId, HumanBodyBones lowerId,
            HumanBodyBones endId, Vector3 target, Vector3 hint)
        {
            var upper = animator.GetBoneTransform(upperId);
            var lower = animator.GetBoneTransform(lowerId);
            var end = animator.GetBoneTransform(endId);
            var a = Vector3.Distance(upper.position, lower.position);
            var b = Vector3.Distance(lower.position, end.position);
            var offset = target - upper.position;
            if (offset.sqrMagnitude < 0.00000001f) offset = end.position - upper.position;
            if (offset.sqrMagnitude < 0.00000001f) offset = lower.position - upper.position;
            var direction = offset.normalized;
            var distance = Mathf.Clamp(Vector3.Distance(upper.position, target), Mathf.Abs(a - b) + 0.00001f, a + b - 0.00001f);
            var along = (a * a - b * b + distance * distance) / (2f * distance);
            var bend = Vector3.ProjectOnPlane(hint, direction);
            if (bend.sqrMagnitude < 0.00000001f)
                bend = Vector3.ProjectOnPlane(lower.position - upper.position, direction);
            if (bend.sqrMagnitude < 0.00000001f)
                bend = Vector3.Cross(direction, Mathf.Abs(direction.y) < 0.9f ? Vector3.up : Vector3.right);
            bend.Normalize();
            var joint = upper.position + direction * along + bend * Mathf.Sqrt(Mathf.Max(0f, a * a - along * along));
            upper.rotation = Quaternion.FromToRotation(lower.position - upper.position, joint - upper.position) * upper.rotation;
            lower.rotation = Quaternion.FromToRotation(end.position - lower.position, upper.position + direction * distance - lower.position) * lower.rotation;
        }

    }
}
