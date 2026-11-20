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
                case BodyRegion.LeftShoulder:
                case BodyRegion.RightShoulder:
                    Lean(rig.GetBoneTransform(HumanBodyBones.UpperChest), incoming, up, 3f * amount);
                    Deflect(rig, left ? HumanBodyBones.LeftShoulder : HumanBodyBones.RightShoulder,
                        left ? HumanBodyBones.LeftUpperArm : HumanBodyBones.RightUpperArm,
                        (incoming * 0.037f + outside * 0.024f - up * 0.006f) * amount, 14f * amount);
                    Deflect(rig, left ? HumanBodyBones.LeftUpperArm : HumanBodyBones.RightUpperArm,
                        left ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm,
                        (outside * 0.095f + incoming * 0.012f) * amount, 18f * amount);
                    break;
                case BodyRegion.LeftArm:
                case BodyRegion.RightArm:
                    Deflect(rig, left ? HumanBodyBones.LeftUpperArm : HumanBodyBones.RightUpperArm,
                        left ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm,
                        (incoming * 0.055f + outside * 0.022f) * amount, 13f * amount);
                    break;
                case BodyRegion.LeftLeg:
                case BodyRegion.RightLeg:
                    Buckle(rig, incoming, outside, amount);
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


        // Briefly shifts weight away from the injured leg and keeps both boots on the authored contacts.
        private static void Buckle(Animator rig, Vector3 incoming, Vector3 outside, float amount)
        {
            var leftBoot = rig.GetBoneTransform(HumanBodyBones.LeftFoot);
            var rightBoot = rig.GetBoneTransform(HumanBodyBones.RightFoot);
            var leftPosition = leftBoot.position;
            var rightPosition = rightBoot.position;
            var leftRotation = leftBoot.rotation;
            var rightRotation = rightBoot.rotation;
            var leftBend = rig.GetBoneTransform(HumanBodyBones.LeftLowerLeg).position -
                rig.GetBoneTransform(HumanBodyBones.LeftUpperLeg).position;
            var rightBend = rig.GetBoneTransform(HumanBodyBones.RightLowerLeg).position -
                rig.GetBoneTransform(HumanBodyBones.RightUpperLeg).position;
            rig.GetBoneTransform(HumanBodyBones.Hips).position +=
                (incoming * 0.022f - outside * 0.027f - rig.transform.up * 0.027f) * amount;
            ArcherLimbIK.Solve(rig, HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg,
                HumanBodyBones.LeftFoot, leftPosition, leftBend);
            ArcherLimbIK.Solve(rig, HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg,
                HumanBodyBones.RightFoot, rightPosition, rightBend);
            leftBoot.rotation = leftRotation;
            rightBoot.rotation = rightRotation;
        }
    }
}
