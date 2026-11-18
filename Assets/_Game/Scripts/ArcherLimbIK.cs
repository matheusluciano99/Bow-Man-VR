using UnityEngine;

namespace BowMan
{
    public static class ArcherLimbIK
    {
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
