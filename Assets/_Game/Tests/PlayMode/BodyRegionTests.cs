#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BowMan;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public class BodyRegionTests
{
    private readonly List<GameObject> objects = new List<GameObject>();
    private static readonly Vector3 Origin = new Vector3(1000f, 1000f, 1000f);

    // Instantiates the actual character away from scene colliders for end-to-end arrow checks.
    private GameObject Character(string name)
    {
        var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
            $"Assets/_Game/Prefabs/Characters/{name}.prefab"));
        objects.Add(root);
        root.transform.position = Origin;
        Physics.SyncTransforms();
        return root;
    }

    // Fires through a chosen world point and verifies one generic and one anatomical impact agree.
    private IEnumerator Shoot(Vector3 point, BodyRegion expected)
    {
        var obj = new GameObject("Region test arrow");
        objects.Add(obj);
        obj.transform.position = point + Vector3.forward * 3f;
        var arrow = obj.AddComponent<ArrowFlight>();
        var bodyCount = 0;
        var hitCount = 0;
        BodyImpact body = default;
        RaycastHit hit = default;
        arrow.BodyHit += value => { bodyCount++; body = value; };
        arrow.Hit += value => { hitCount++; hit = value; };
        arrow.Launch(Vector3.back * 1000f);
        yield return new WaitForFixedUpdate();
        Assert.That(bodyCount, Is.EqualTo(1), expected.ToString());
        Assert.That(hitCount, Is.EqualTo(1));
        Assert.That(body.Region, Is.EqualTo(expected));
        Assert.That(body.Collider, Is.EqualTo(hit.collider));
        Assert.That(body.Point, Is.EqualTo(hit.point));
        Assert.That(body.Normal, Is.EqualTo(hit.normal));
        Assert.That(Vector3.Distance(arrow.transform.position, body.Point), Is.LessThan(0.0001f));
        Assert.That(Vector3.Dot(body.Direction, arrow.transform.forward), Is.GreaterThan(0.9999f));
        Assert.That(body.Direction.magnitude, Is.EqualTo(1f).Within(0.0001f));
        var side = expected.ToString().StartsWith("Left") ? BodySide.Left :
            expected.ToString().StartsWith("Right") ? BodySide.Right : BodySide.Center;
        Assert.That(body.Side, Is.EqualTo(side));
        yield return new WaitForFixedUpdate();
        Assert.That(bodyCount, Is.EqualTo(1));
        Assert.That(hitCount, Is.EqualTo(1));
        Object.Destroy(obj);
        yield return null;
    }

    // Removes all temporary arrows and characters, including those left by assertion failures.
    [UnityTearDown]
    public IEnumerator CleanUp()
    {
        foreach (var obj in objects) if (obj != null) Object.Destroy(obj);
        objects.Clear();
        yield return null;
    }

    // Exercises every region on both shipped prefabs with real swept arrow impacts.
    [UnityTest]
    public IEnumerator BothArchersReportAllFifteenRegionsAndSides()
    {
        foreach (var name in new[] { "EnglishArcher", "ErikaArcher" })
        {
            var root = Character(name);
            var regions = root.GetComponentsInChildren<BodyHitRegion>();
            Assert.That(regions.Select(r => r.Region).Distinct().Count(), Is.EqualTo(15));
            Assert.That(root.GetComponent<Rigidbody>().isKinematic, Is.True);
            foreach (BodyRegion region in Enum.GetValues(typeof(BodyRegion)))
            {
                var volume = regions.First(r => r.Region == region).GetComponent<Collider>();
                Assert.That(volume.isTrigger, Is.False);
                yield return Shoot(volume.bounds.center, region);
            }
            Object.Destroy(root);
            yield return null;
        }
    }

    // Checks just above and below the actual torso/neck/head seams on both characters.
    [UnityTest]
    public IEnumerator CentralBoundariesKeepAdjacentRegionsDistinct()
    {
        var order = new[] { BodyRegion.Pelvis, BodyRegion.Abdomen, BodyRegion.Chest, BodyRegion.Neck, BodyRegion.Head };
        foreach (var name in new[] { "EnglishArcher", "ErikaArcher" })
        {
            var root = Character(name);
            var regions = root.GetComponentsInChildren<BodyHitRegion>();
            for (var i = 0; i < order.Length - 1; i++)
            {
                var below = regions.Single(r => r.Region == order[i]).GetComponent<Collider>();
                var above = regions.Single(r => r.Region == order[i + 1]).GetComponent<Collider>();
                Assert.That(below.bounds.max.y, Is.EqualTo(above.bounds.min.y).Within(0.0002f));
                var seam = below.bounds.center;
                seam.y = below.bounds.max.y;
                yield return Shoot(seam - Vector3.up * 0.003f, order[i]);
                yield return Shoot(seam + Vector3.up * 0.003f, order[i + 1]);
            }
            Object.Destroy(root);
            yield return null;
        }
    }

    // Scans a limb junction from both sides, requiring continuous coverage and one anatomical transition.
    private static void CheckJunction(Vector3 start, Vector3 end, BodyRegion first, BodyRegion second)
    {
        var crossed = false;
        for (var i = 0; i <= 20; i++)
        {
            var point = Vector3.Lerp(start, end, i / 20f);
            Assert.That(Physics.Raycast(point + Vector3.forward * 3f, Vector3.back, out var hit, 6f),
                Is.True, $"Gap between {first} and {second} at sample {i}");
            var region = hit.collider.GetComponent<BodyHitRegion>();
            Assert.That(region, Is.Not.Null);
            Assert.That(region.Region, Is.EqualTo(first).Or.EqualTo(second));
            if (i == 0) Assert.That(region.Region, Is.EqualTo(first));
            if (region.Region == second) crossed = true;
            if (crossed) Assert.That(region.Region, Is.EqualTo(second), "Region changed back across the seam");
        }
        Assert.That(crossed, Is.True, $"Never reached {second}");
    }

    // Checks shoulder/arm, arm/hand and leg/foot divisions on both sides of both actual rigs.
    [UnityTest]
    public IEnumerator LimbJunctionsHaveNoGapsOrAnatomicalReversals()
    {
        foreach (var name in new[] { "EnglishArcher", "ErikaArcher" })
        {
            var root = Character(name);
            var animator = root.GetComponentInChildren<Animator>();
            foreach (var side in new[] { "Left", "Right" })
            {
                var arm = animator.GetBoneTransform((HumanBodyBones)Enum.Parse(typeof(HumanBodyBones), side + "UpperArm")).position;
                var elbow = animator.GetBoneTransform((HumanBodyBones)Enum.Parse(typeof(HumanBodyBones), side + "LowerArm")).position;
                var wrist = animator.GetBoneTransform((HumanBodyBones)Enum.Parse(typeof(HumanBodyBones), side + "Hand")).position;
                var finger = animator.GetBoneTransform((HumanBodyBones)Enum.Parse(typeof(HumanBodyBones), side + "MiddleDistal")).position;
                var ankle = animator.GetBoneTransform((HumanBodyBones)Enum.Parse(typeof(HumanBodyBones), side + "Foot")).position;
                CheckJunction(Vector3.Lerp(arm, elbow, 0.05f), Vector3.Lerp(arm, elbow, 0.7f),
                    (BodyRegion)Enum.Parse(typeof(BodyRegion), side + "Shoulder"), (BodyRegion)Enum.Parse(typeof(BodyRegion), side + "Arm"));
                CheckJunction(Vector3.Lerp(elbow, wrist, 0.7f), Vector3.Lerp(wrist, finger, 0.6f),
                    (BodyRegion)Enum.Parse(typeof(BodyRegion), side + "Arm"), (BodyRegion)Enum.Parse(typeof(BodyRegion), side + "Hand"));
                CheckJunction(ankle + Vector3.up * 0.13f, ankle - Vector3.up * 0.04f,
                    (BodyRegion)Enum.Parse(typeof(BodyRegion), side + "Leg"), (BodyRegion)Enum.Parse(typeof(BodyRegion), side + "Foot"));
            }
            Object.Destroy(root);
            yield return null;
        }
    }

    // Verifies bound volumes move with the sampled animation rather than staying in the neutral world pose.
    [UnityTest]
    public IEnumerator AnimatedHandsAndFeetRemainHittableAtTheirNewPositions()
    {
        foreach (var name in new[] { "EnglishArcher", "ErikaArcher" })
        {
            var root = Character(name);
            var animator = root.GetComponentInChildren<Animator>();
            animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                "Assets/_Game/Animations/RigMobilityCheck.controller");
            animator.enabled = true;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.Rebind();
            animator.Update(0f);
            var regions = root.GetComponentsInChildren<BodyHitRegion>();
            var foot = regions.Single(r => r.Region == BodyRegion.LeftFoot).GetComponent<Collider>();
            var start = foot.bounds.center;
            animator.Play("Rig Mobility Check", 0, 8f / 12f);
            animator.Update(0f);
            animator.enabled = false;
            Physics.SyncTransforms();
            Assert.That(Vector3.Distance(start, foot.bounds.center), Is.GreaterThan(0.06f));
            foreach (var region in new[] { BodyRegion.LeftFoot, BodyRegion.RightFoot, BodyRegion.LeftHand, BodyRegion.RightHand })
                yield return Shoot(regions.Single(r => r.Region == region).GetComponent<Collider>().bounds.center, region);
            Object.Destroy(root);
            yield return null;
        }
    }

    // Verifies scenery never invents anatomy and overlapping volumes still produce only one injury report.
    [UnityTest]
    public IEnumerator SceneryAndOverlapsProduceOneOutcome()
    {
        var target = GameObject.CreatePrimitive(PrimitiveType.Cube);
        objects.Add(target);
        target.transform.position = Origin;
        var obj = new GameObject("Scenery test arrow");
        objects.Add(obj);
        obj.transform.position = Origin - Vector3.forward * 3f;
        var arrow = obj.AddComponent<ArrowFlight>();
        var generic = 0;
        var anatomy = 0;
        arrow.Hit += _ => generic++;
        arrow.BodyHit += _ => anatomy++;
        arrow.Launch(Vector3.forward * 1000f);
        yield return new WaitForFixedUpdate();
        Assert.That(generic, Is.EqualTo(1));
        Assert.That(anatomy, Is.Zero);
        Object.Destroy(obj);
        target.AddComponent<BodyHitRegion>().Configure(BodyRegion.LeftHand);
        var overlap = Object.Instantiate(target);
        objects.Add(overlap);
        Physics.SyncTransforms();
        yield return Shoot(Origin, BodyRegion.LeftHand);
    }
}
#endif
