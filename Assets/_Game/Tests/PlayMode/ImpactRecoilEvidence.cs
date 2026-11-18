#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BowMan;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public class ImpactRecoilEvidence
{
    // Samples every requested region on both complete rigs with the exact overlay applied by the player.
    [TestCase("EnglishArcher")]
    [TestCase("ErikaArcher")]
    public void FirstContactFlinchesWithoutChangingTheSupportOrHandoff(string model)
    {
        var apply = typeof(ArcherReactionAnimation).Assembly.GetType("BowMan.ArcherHitRecoil")
            .GetMethod("Apply", BindingFlags.Public | BindingFlags.Static);
        var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/_Game/Prefabs/Characters/{model}.prefab"));
        try
        {
            var rig = root.GetComponentInChildren<Animator>();
            var injury = root.GetComponent<ArcherReactionAnimation>();
            var volumes = root.GetComponentsInChildren<BodyHitRegion>();
            var core = volumes.Where(v => v.Region is BodyRegion.Chest or BodyRegion.Abdomen or BodyRegion.Pelvis
                or BodyRegion.Head or BodyRegion.Neck).Select(v => v.GetComponent<Collider>()).ToArray();
            var limbs = volumes.Where(v => v.Region is BodyRegion.LeftArm or BodyRegion.RightArm
                or BodyRegion.LeftHand or BodyRegion.RightHand or BodyRegion.LeftFoot or BodyRegion.RightFoot)
                .Select(v => v.GetComponent<Collider>()).ToArray();
            var leftBoot = rig.GetBoneTransform(HumanBodyBones.LeftFoot);
            var rightBoot = rig.GetBoneTransform(HumanBodyBones.RightFoot);
            var regions = new[] { BodyRegion.Abdomen, BodyRegion.LeftShoulder, BodyRegion.RightShoulder,
                BodyRegion.LeftArm, BodyRegion.RightArm, BodyRegion.LeftHand, BodyRegion.RightHand,
                BodyRegion.LeftLeg, BodyRegion.RightLeg };
            var failures = new List<string>();
            foreach (var region in regions)
            {
                var clip = injury.Entry(region);
                clip.SampleAnimation(root, 0f);
                Physics.SyncTransforms();
                var contact = volumes.First(v => v.Region == region);
                var collider = contact.GetComponent<Collider>();
                var incoming = -rig.transform.forward;
                Assert.That(collider.Raycast(new Ray(collider.bounds.center - incoming * 2f, incoming), out var hit, 4f), Is.True);
                var impact = new BodyImpact(contact, hit, incoming, 0.25f);
                var marker = rig.GetBoneTransform(Marker(region));
                var max = 0f;
                var worstAddedOverlap = 0f;
                var worstSupport = 0f;
                for (var i = 0; i <= 30; i++)
                {
                    var time = i / 120f;
                    clip.SampleAnimation(root, time);
                    var basePose = marker.position;
                    var leftSupport = leftBoot.position;
                    var rightSupport = rightBoot.position;
                    var originalOverlap = Overlap(limbs, core);
                    apply.Invoke(null, new object[] { rig, impact, time });
                    var displacement = marker.position - basePose;
                    if (time <= 0.1f)
                        max = Mathf.Max(max, Vector3.Dot(displacement, incoming));
                    if (time >= 0.22f)
                        Assert.That(displacement.magnitude, Is.LessThan(0.0001f), $"{model} {region} blend handoff");
                    worstSupport = Mathf.Max(worstSupport,
                        Mathf.Max(Vector3.Distance(leftSupport, leftBoot.position), Vector3.Distance(rightSupport, rightBoot.position)));
                    worstAddedOverlap = Mathf.Max(worstAddedOverlap, Overlap(limbs, core) - originalOverlap);
                }
                Debug.Log($"Impact flinch {model} {region}: {max * 1000f:F1} mm by 100 ms, {worstAddedOverlap * 1000f:F2} mm added overlap");
                if (max <= 0.012f || worstAddedOverlap >= 0.003f || worstSupport >= 0.0015f)
                    failures.Add($"{model} {region}: recoil {max:F4} m, added overlap {worstAddedOverlap:F4} m, support shift {worstSupport:F4} m");
            }
            Assert.That(failures, Is.Empty, string.Join("\n", failures));
        }
        finally { Object.DestroyImmediate(root); }
    }

    // Tracks the region-specific moving joint instead of applying one whole-body metric.
    private static HumanBodyBones Marker(BodyRegion region) => region switch
    {
        BodyRegion.Abdomen => HumanBodyBones.UpperChest,
        BodyRegion.LeftShoulder => HumanBodyBones.LeftUpperArm,
        BodyRegion.RightShoulder => HumanBodyBones.RightUpperArm,
        BodyRegion.LeftArm => HumanBodyBones.LeftLowerArm,
        BodyRegion.RightArm => HumanBodyBones.RightLowerArm,
        BodyRegion.LeftHand => HumanBodyBones.LeftHand,
        BodyRegion.RightHand => HumanBodyBones.RightHand,
        _ => HumanBodyBones.Hips
    };

    // Measures added nonadjacent limb and torso intersections during the blended contact phase.
    private static float Overlap(Collider[] limbs, Collider[] core)
    {
        var deepest = 0f;
        foreach (var limb in limbs)
        foreach (var body in core)
            if (Physics.ComputePenetration(limb, limb.transform.position, limb.transform.rotation,
                body, body.transform.position, body.transform.rotation, out _, out var depth))
                deepest = Mathf.Max(deepest, depth);
        return deepest;
    }
}
#endif
