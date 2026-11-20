using UnityEngine;

namespace BowMan
{
    public readonly struct BoneHit
    {
        public HumanBodyBones Bone { get; }
        public BodySide Side { get; }
        public Transform Frame { get; }
        public Bounds LocalBounds { get; }
        public Vector3 LocalPoint { get; }
        public Vector3 Point => Frame.TransformPoint(LocalPoint);
        public bool Fractured { get; }

        // Keeps the contacted part of the rig and its result attached to the struck character.
        internal BoneHit(RigBoneVolume volume, Vector3 point, bool fractured)
        {
            Bone = volume.Bone;
            Side = volume.Region.Side;
            Frame = volume.Region.transform;
            LocalBounds = volume.Bounds;
            LocalPoint = point;
            Fractured = fractured;
        }
    }

    internal readonly struct RigBoneVolume
    {
        public readonly HumanBodyBones Bone;
        public readonly BodyHitRegion Region;
        public readonly Bounds Bounds;

        // Fits a narrow game bone inside the body volume in the same animated local frame.
        public RigBoneVolume(HumanBodyBones bone, BodyHitRegion region)
        {
            Bone = bone;
            Region = region;
            if (region.TryGetComponent<CapsuleCollider>(out var limb))
            {
                var size = Vector3.one * limb.radius * 0.7f;
                size[limb.direction] = limb.height - limb.radius;
                Bounds = new Bounds(limb.center, size);
            }
            else
            {
                var box = region.GetComponent<BoxCollider>();
                var proportions = bone switch
                {
                    HumanBodyBones.Head or HumanBodyBones.Hips => new Vector3(0.65f, 0.7f, 0.6f),
                    HumanBodyBones.LeftFoot or HumanBodyBones.RightFoot => new Vector3(0.45f, 0.45f, 0.7f),
                    _ => new Vector3(0.25f, 0.9f, 0.35f)
                };
                Bounds = new Bounds(box.center, Vector3.Scale(box.size, proportions));
            }
        }

        // Clips the finite path in bone-local space, including an entry already inside the volume.
        public bool TryHit(BodyImpact entry, bool fractured, out BoneHit hit)
        {
            var frame = Region.transform;
            var start = frame.InverseTransformPoint(entry.Point);
            var delta = frame.InverseTransformPoint(entry.PathEnd) - start;
            var distance = 0f;
            if (!Bounds.Contains(start) && (!Bounds.IntersectRay(new Ray(start, delta), out distance) ||
                distance < 0f || distance > delta.magnitude))
            {
                hit = default;
                return false;
            }
            hit = new BoneHit(this, start + delta.normalized * distance, fractured);
            return true;
        }
    }
}
