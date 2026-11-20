using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BowMan
{
    public readonly struct OrganHit
    {
        public AnatomyOrgan Organ { get; }
        public Vector3 LocalPoint { get; }
        public Vector3 Point => Organ.transform.TransformPoint(LocalPoint);

        // Keeps the actual tissue contact attached to its own moving anatomical bone.
        public OrganHit(AnatomyOrgan organ, Vector3 point)
        {
            Organ = organ;
            LocalPoint = organ.transform.InverseTransformPoint(point);
        }
    }

    public sealed class AnatomicalImpact
    {
        public BodyImpact Entry { get; }
        public IReadOnlyList<OrganHit> Organs { get; }
        public IReadOnlyList<BoneHit> Bones { get; }

        // Associates tissue contacts with the real entry region, side and finite internal arrow path.
        public AnatomicalImpact(BodyImpact entry, OrganHit[] organs, BoneHit[] bones = null)
        {
            Entry = entry;
            Organs = organs;
            Bones = bones ?? Array.Empty<BoneHit>();
        }
    }

    public sealed class ArcherAnatomy : MonoBehaviour
    {
        [SerializeField, Range(0f, 1f)] private float fractureStrength = 0.75f;
        private AnatomyOrgan[] organs;
        private RigBoneVolume[] bones;

        // Resolves fitted hit-volume parents to their Humanoid bone identities once per actor.
        private RigBoneVolume[] FindBones()
        {
            var animator = GetComponentInChildren<Animator>();
            if (animator == null || !animator.isHuman) return Array.Empty<RigBoneVolume>();
            var frames = new Dictionary<Transform, HumanBodyBones>();
            for (var i = 0; i < (int)HumanBodyBones.LastBone; i++)
            {
                var bone = (HumanBodyBones)i;
                var frame = animator.GetBoneTransform(bone);
                if (frame != null) frames[frame] = bone;
            }
            var volumes = new List<RigBoneVolume>();
            foreach (var region in GetComponentsInChildren<BodyHitRegion>())
                if (frames.TryGetValue(region.transform.parent, out var bone))
                    volumes.Add(new RigBoneVolume(bone, region));
            return volumes.ToArray();
        }

        // Traces fitted tissue surfaces at impact time and deduplicates organs split across animated bones.
        public AnatomicalImpact Trace(BodyImpact entry)
        {
            if (entry.Collider == null || !entry.Collider.transform.IsChildOf(transform))
                throw new ArgumentException("The entry must belong to this archer.");
            organs ??= GetComponentsInChildren<AnatomyOrgan>();
            var hits = new List<OrganHit>();
            foreach (var organ in organs)
                if (organ.TryHit(entry.Point, entry.PathEnd, out var point)) hits.Add(new OrganHit(organ, point));
            var ordered = hits.OrderBy(h => (h.Point - entry.Point).sqrMagnitude)
                .GroupBy(h => (h.Organ.Kind, h.Organ.Side)).Select(g => g.First()).ToArray();
            bones ??= FindBones();
            var contacts = new List<BoneHit>();
            foreach (var bone in bones)
                if (bone.TryHit(entry, entry.ShotStrength >= fractureStrength, out var hit)) contacts.Add(hit);
            var orderedBones = contacts.OrderBy(h => (h.Point - entry.Point).sqrMagnitude)
                .GroupBy(h => h.Bone).Select(g => g.First()).ToArray();
            return new AnatomicalImpact(entry, ordered, orderedBones);
        }
    }
}
