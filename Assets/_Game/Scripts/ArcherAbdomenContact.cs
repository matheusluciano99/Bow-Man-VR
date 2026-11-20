using System.Linq;
using UnityEngine;

namespace BowMan
{
    internal sealed class ArcherAbdomenContact
    {
        private readonly Animator animator;
        private readonly Collider[] body;
        private readonly Arm left;
        private readonly Arm right;

        // Deflects the spine in the incoming shot direction during the first fifth of a second.
        internal void Flinch(BodyImpact hit, float elapsed)
        {
            if (elapsed >= 0.22f) return;
            var up = animator.transform.up;
            var axis = Vector3.Cross(up, Vector3.ProjectOnPlane(hit.Direction, up).normalized);
            if (axis.sqrMagnitude < 0.01f) return;
            var weight = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, 0.055f, elapsed)) *
                Mathf.SmoothStep(1f, 0f, Mathf.InverseLerp(0.055f, 0.22f, elapsed));
            var spine = animator.GetBoneTransform(HumanBodyBones.Spine);
            spine.rotation = Quaternion.AngleAxis(7f * weight, axis) * spine.rotation;
        }

        // Caches the forearm and body volumes used to limit live retargeting.
        internal ArcherAbdomenContact(Animator animator, BodyHitRegion[] regions)
        {
            this.animator = animator;
            body = regions.Where(region => region.Region is BodyRegion.Chest or BodyRegion.Abdomen or
                BodyRegion.Pelvis or BodyRegion.Head or BodyRegion.Neck)
                .Select(region => region.GetComponent<Collider>()).Where(collider => collider != null).ToArray();
            left = new Arm(animator, regions, true);
            right = new Arm(animator, regions, false);
        }

        // Retargets the palms to the actual wound with a clear forearm path.
        internal void Apply(Vector3 point, Vector3 outward, float weight)
        {
            if (weight <= 0f) return;
            var leftAuthored = left.Rotations();
            var rightAuthored = right.Rotations();
            ArcherLimbIK.ReachAbdomen(animator, point, outward, weight);
            left.Limit(leftAuthored, body);
            right.Limit(rightAuthored, body);
        }

        private sealed class Arm
        {
            private const float PenetrationTolerance = 0.002f;
            private readonly Animator animator;
            private readonly bool left;
            private readonly Transform upper;
            private readonly Transform forearm;
            private readonly Transform wrist;
            private readonly Collider[] volumes;

            // Selects a measured arm chain and its two distal collision volumes.
            internal Arm(Animator animator, BodyHitRegion[] regions, bool left)
            {
                this.animator = animator;
                this.left = left;
                upper = animator.GetBoneTransform(left ? HumanBodyBones.LeftUpperArm : HumanBodyBones.RightUpperArm);
                forearm = animator.GetBoneTransform(left ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm);
                wrist = animator.GetBoneTransform(left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
                volumes = regions.Where(region => region.transform.parent == forearm || region.transform.parent == wrist)
                    .Select(region => region.GetComponent<Collider>()).Where(collider => collider != null).ToArray();
            }

            // Captures local rotations so changes in pose preserve segment lengths.
            internal (Quaternion Upper, Quaternion Forearm, Quaternion Wrist) Rotations() =>
                (upper.localRotation, forearm.localRotation, wrist.localRotation);

            // Reduces a penetrating retarget toward the collision-fitted authored pose.
            internal void Limit((Quaternion Upper, Quaternion Forearm, Quaternion Wrist) authored, Collider[] body)
            {
                var depth = ArcherLimbIK.DeepestOverlap(volumes, body, out _);
                if (depth <= PenetrationTolerance) return;

                ArcherLimbIK.ClearForearm(animator, left, volumes, body,
                    wristOffsetSteps: 0, clearanceTolerance: PenetrationTolerance);
                depth = ArcherLimbIK.DeepestOverlap(volumes, body, out _);
                if (depth <= PenetrationTolerance) return;

                var desired = Rotations();
                ApplyBlend(0f);
                var acceptedDepth = Mathf.Max(PenetrationTolerance, ArcherLimbIK.DeepestOverlap(volumes, body, out _));
                if (depth <= acceptedDepth)
                {
                    ApplyBlend(1f);
                    return;
                }

                var clear = 0f;
                var blocked = 1f;
                for (var step = 0; step < 6; step++)
                {
                    var blend = (clear + blocked) * 0.5f;
                    ApplyBlend(blend);
                    if (ArcherLimbIK.DeepestOverlap(volumes, body, out _) <= acceptedDepth) clear = blend;
                    else blocked = blend;
                }
                ApplyBlend(clear);

                // Interpolates between rotations on the same three joints.
                void ApplyBlend(float blend)
                {
                    upper.localRotation = Quaternion.Slerp(authored.Upper, desired.Upper, blend);
                    forearm.localRotation = Quaternion.Slerp(authored.Forearm, desired.Forearm, blend);
                    wrist.localRotation = Quaternion.Slerp(authored.Wrist, desired.Wrist, blend);
                }
            }
        }
    }
}
