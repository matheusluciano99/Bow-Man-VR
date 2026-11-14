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
        public BodyRegion Region => Volume.Region;
        public BodySide Side => Volume.Side;
        public Vector3 Point { get; }
        public Vector3 Direction { get; }
        public Collider Collider { get; }

        // Captures the struck volume and the arrow's world-space tip position and travel direction.
        public BodyImpact(BodyHitRegion region, RaycastHit hit, Vector3 direction)
        {
            Volume = region;
            Point = hit.point;
            Direction = direction.normalized;
            Collider = hit.collider;
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
