using UnityEngine;

namespace BowMan
{
    public static class ArcherLimbIK
    {
        // Braces the actual abdominal wound with both hands while retaining the character's measured arm lengths.
        public static void ReachAbdomen(Animator animator, Vector3 point, Vector3 outward, float weight)
        {
            if (weight <= 0f) return;
            var spine = animator.GetBoneTransform(HumanBodyBones.Spine);
            for (var handIndex = 0; handIndex < 2; handIndex++)
            {
                var left = handIndex == 1;
                var wrist = animator.GetBoneTransform(left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
                var target = point + spine.right * (left ? -0.05f : 0.05f) + outward * 0.03f;
                if (left) target -= spine.up * 0.04f;
                Solve(animator, left ? HumanBodyBones.LeftUpperArm : HumanBodyBones.RightUpperArm,
                    left ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm,
                    left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand,
                    Vector3.Lerp(wrist.position, target, weight), spine.right * (left ? -1f : 1f) + outward * 0.3f);
                PointHand(animator, left, spine.right * (left ? 1f : -1f), weight);
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
