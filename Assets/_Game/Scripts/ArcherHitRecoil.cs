using UnityEngine;

namespace BowMan
{
    internal static class ArcherHitRecoil
    {
        private const float PeakTime = 0.055f;
        private const float Duration = 0.22f;

        // Adds a brief directional flinch while the authored entry is still gaining momentum.
        public static void Apply(Animator rig, BodyImpact hit, float time)
        {
            if (time >= Duration || rig == null || !rig.isHuman) return;
            var amount = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, PeakTime, time)) *
                Mathf.SmoothStep(1f, 0f, Mathf.InverseLerp(PeakTime, Duration, time));
            var up = rig.transform.up;
            var incoming = Vector3.ProjectOnPlane(hit.Direction, up).normalized;
            if (incoming.sqrMagnitude < 0.01f) return;
            var left = hit.Side == BodySide.Left;
            var outside = rig.transform.right * (left ? -1f : 1f);

            switch (hit.Region)
            {
                case BodyRegion.Abdomen:
                    Lean(rig.GetBoneTransform(HumanBodyBones.Spine), incoming, up, 7f * amount);
                    break;
                case BodyRegion.LeftArm:
                case BodyRegion.RightArm:
                    Deflect(rig, left ? HumanBodyBones.LeftUpperArm : HumanBodyBones.RightUpperArm,
                        left ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm,
                        (incoming * 0.055f + outside * 0.022f) * amount, 13f * amount);
                    break;
            }
        }

        // Leans the torso in the direction of the incoming arrow without translating the actor.
        private static void Lean(Transform joint, Vector3 incoming, Vector3 up, float angle)
        {
            if (joint != null) joint.rotation = Quaternion.AngleAxis(angle, Vector3.Cross(up, incoming)) * joint.rotation;
        }

        // Swings the struck limb away using its existing joint lengths and a bounded rotation.
        private static void Deflect(Animator rig, HumanBodyBones pivotId, HumanBodyBones endId, Vector3 offset, float maxAngle)
        {
            var pivot = rig.GetBoneTransform(pivotId);
            var end = rig.GetBoneTransform(endId);
            if (pivot == null || end == null) return;
            var lever = end.position - pivot.position;
            var turn = Quaternion.FromToRotation(lever, lever + offset);
            pivot.rotation = Quaternion.RotateTowards(Quaternion.identity, turn, maxAngle) * pivot.rotation;
        }

    }
}
