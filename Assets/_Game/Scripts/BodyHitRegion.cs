using UnityEngine;

namespace BowMan
{
    public enum BodyRegion
    {
        Head, Neck, Chest, Abdomen, Pelvis,
        LeftShoulder, RightShoulder, LeftArm, RightArm,
        LeftHand, RightHand, LeftLeg, RightLeg, LeftFoot, RightFoot
    }

    public enum BodySide { Center, Left, Right }

    public readonly struct BodyImpact
    {
        public BodyHitRegion Volume { get; }
        public BodyRegion Region { get; }
        public BodySide Side { get; }
        public Vector3 Point { get; }
        public Vector3 Direction { get; }
        public Vector3 Normal { get; }
        public float PenetrationDepth { get; }
        public float ShotStrength { get; }
        public Vector3 PathEnd => Point + Direction * PenetrationDepth;
        public Collider Collider { get; }

        // Captures the actual struck volume, entry point, surface normal and arrow travel direction.
        public BodyImpact(BodyHitRegion region, RaycastHit hit, Vector3 direction, float penetrationDepth, float shotStrength = 0f)
            : this(region, hit.point, hit.normal, direction, penetrationDepth, shotStrength) { Collider = hit.collider; }

        // Restores a confirmed entry against the matching animated volume on another instance of the same actor.
        public BodyImpact(BodyHitRegion region, Vector3 point, Vector3 normal, Vector3 direction,
            float penetrationDepth, float shotStrength)
        {
            Volume = region;
            Region = region.Region;
            Side = region.Side;
            Point = point;
            Direction = direction.normalized;
            Normal = normal;
            PenetrationDepth = penetrationDepth;
            ShotStrength = shotStrength;
            Collider = region.GetComponent<Collider>();
        }
    }

    [DisallowMultipleComponent]
    public sealed class BodyHitRegion : MonoBehaviour
    {
        [SerializeField] private BodyRegion region;
        public BodyRegion Region => region;
        public BodySide Side => region switch
        {
            BodyRegion.LeftShoulder or BodyRegion.LeftArm or BodyRegion.LeftHand or
                BodyRegion.LeftLeg or BodyRegion.LeftFoot => BodySide.Left,
            BodyRegion.RightShoulder or BodyRegion.RightArm or BodyRegion.RightHand or
                BodyRegion.RightLeg or BodyRegion.RightFoot => BodySide.Right,
            _ => BodySide.Center
        };

        // Sets the anatomical identity when authoring a bone-bound hit volume.
        public void Configure(BodyRegion value) => region = value;
    }
}
