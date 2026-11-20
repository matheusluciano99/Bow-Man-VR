using System;
using System.Linq;
using UnityEngine;

namespace BowMan
{
    [Serializable]
    public sealed class ImpactReplay
    {
        [Serializable]
        private struct TissueContact { public int index; public Vector3 point; }
        [Serializable]
        private struct BoneContact { public int volume; public HumanBodyBones bone; public Vector3 point; public bool fractured; }

        [SerializeField] private int volume;
        [SerializeField] private Vector3 entry, direction, normal;
        [SerializeField] private float depth, strength;
        [SerializeField] private TissueContact[] organs;
        [SerializeField] private BoneContact[] bones;

        // Records the host's result in stable prefab-local frames before an injury animation starts.
        public static ImpactReplay Capture(ArcherHealth actor, AnatomicalImpact hit)
        {
            var volumes = actor.GetComponentsInChildren<BodyHitRegion>();
            var tissues = actor.GetComponentsInChildren<AnatomyOrgan>();
            var frame = hit.Entry.Volume.transform;
            return new ImpactReplay
            {
                volume = Array.IndexOf(volumes, hit.Entry.Volume),
                entry = frame.InverseTransformPoint(hit.Entry.Point),
                direction = frame.InverseTransformDirection(hit.Entry.Direction),
                normal = frame.InverseTransformDirection(hit.Entry.Normal),
                depth = hit.Entry.PenetrationDepth,
                strength = hit.Entry.ShotStrength,
                organs = hit.Organs.Select(h => new TissueContact
                    { index = Array.IndexOf(tissues, h.Organ), point = h.LocalPoint }).ToArray(),
                bones = hit.Bones.Select(h => new BoneContact
                {
                    volume = Array.IndexOf(volumes, h.Frame.GetComponent<BodyHitRegion>()),
                    bone = h.Bone, point = h.LocalPoint, fractured = h.Fractured
                }).ToArray()
            };
        }

        // Places the same reported wound and contacts on another instance of the matching character prefab.
        public AnatomicalImpact Restore(ArcherHealth actor)
        {
            var volumes = actor.GetComponentsInChildren<BodyHitRegion>();
            var tissues = actor.GetComponentsInChildren<AnatomyOrgan>();
            var frame = volumes[volume].transform;
            var body = new BodyImpact(volumes[volume], frame.TransformPoint(entry), frame.TransformDirection(normal),
                frame.TransformDirection(direction), depth, strength);
            var organsHit = organs.Select(h => new OrganHit(tissues[h.index],
                tissues[h.index].transform.TransformPoint(h.point))).ToArray();
            var bonesHit = bones.Select(h => new BoneHit(new RigBoneVolume(h.bone, volumes[h.volume]),
                h.point, h.fractured)).ToArray();
            return new AnatomicalImpact(body, organsHit, bonesHit);
        }
    }
}
