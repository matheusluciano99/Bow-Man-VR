#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BowMan;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public class InjuryAnimationTests
{
    private readonly List<GameObject> objects = new List<GameObject>();

    // Instantiates the actual prepared character with its authored clip references.
    private GameObject Character(string name)
    {
        var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
            $"Assets/_Game/Prefabs/Characters/{name}.prefab"));
        objects.Add(root);
        return root;
    }

    // Measures the posed visible body's lowest world-space vertex without cached skin bounds.
    private static float Floor(GameObject root)
    {
        var minimum = float.PositiveInfinity;
        var mesh = new Mesh();
        try
        {
            foreach (var skin in root.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                skin.BakeMesh(mesh, true);
                foreach (var v in mesh.vertices) minimum = Mathf.Min(minimum, skin.transform.TransformPoint(v).y);
                mesh.Clear();
            }
            return minimum;
        }
        finally { Object.DestroyImmediate(mesh); }
    }

    // Removes every temporary actor/arrow after each animation scenario.
    [UnityTearDown]
    public IEnumerator CleanUp()
    {
        foreach (var obj in objects) if (obj != null) Object.Destroy(obj);
        objects.Clear();
        yield return null;
    }

    // Checks actual foot lift, planted support and continuous floor contact across entry and recovery on both skins.
    [Test]
    public void FootClipsPreserveTheCorrectSupportAndReturnToNeutral()
    {
        foreach (var name in new[] { "EnglishArcher", "ErikaArcher" })
        {
            var root = Character(name);
            var actor = root.GetComponent<ArcherReactionAnimation>();
            Assert.That(actor.Supports(BodyRegion.LeftFoot), Is.True);
            foreach (var region in new[] { BodyRegion.LeftFoot, BodyRegion.RightFoot }.Where(actor.Supports))
            {
                var animator = root.GetComponentInChildren<Animator>();
                var injured = animator.GetBoneTransform(region == BodyRegion.LeftFoot ? HumanBodyBones.LeftFoot : HumanBodyBones.RightFoot);
                var support = animator.GetBoneTransform(region == BodyRegion.LeftFoot ? HumanBodyBones.RightFoot : HumanBodyBones.LeftFoot);
                var initial = injured.position;
                var planted = support.position;
                var entry = actor.Entry(region);
                for (var frame = 0; frame <= 30; frame++)
                {
                    entry.SampleAnimation(root, entry.length * frame / 30f);
                    Assert.That(Vector3.Distance(support.position, planted), Is.LessThan(0.002f));
                    Assert.That(Mathf.Abs(Floor(root)), Is.LessThan(0.006f));
                }
                Assert.That(injured.position.y - initial.y, Is.GreaterThan(0.13f));
                var rest = injured.position;
                var recovery = actor.Recovery(region);
                recovery.SampleAnimation(root, 0f);
                Assert.That(Vector3.Distance(injured.position, rest), Is.LessThan(0.001f));
                for (var frame = 0; frame <= 30; frame++)
                {
                    recovery.SampleAnimation(root, recovery.length * frame / 30f);
                    Assert.That(Vector3.Distance(support.position, planted), Is.LessThan(0.002f));
                    Assert.That(Mathf.Abs(Floor(root)), Is.LessThan(0.006f));
                }
                Assert.That(Vector3.Distance(injured.position, initial), Is.LessThan(0.001f));
            }
            Object.DestroyImmediate(root);
        }
    }

    // Creates a real regional shot with playback connected to the existing match callbacks.
    private DuelTurnFlow BodyShot(out ArcherHealth target, out ArrowFlight arrow, BodyRegion region = BodyRegion.LeftFoot)
    {
        var firstObject = new GameObject("First");
        objects.Add(firstObject);
        firstObject.transform.position = new Vector3(1000f, 1000f, 1000f);
        var first = firstObject.AddComponent<ArcherHealth>();
        var root = Character("ErikaArcher");
        root.transform.position = firstObject.transform.position + Vector3.forward * 4f;
        target = root.AddComponent<ArcherHealth>();
        var matchObject = new GameObject("Match");
        objects.Add(matchObject);
        var flow = matchObject.AddComponent<DuelTurnFlow>();
        flow.Initialize(first, target);
        root.GetComponent<ArcherReactionAnimation>().Connect(flow, 1, target);
        Physics.SyncTransforms();
        var volume = root.GetComponentsInChildren<BodyHitRegion>().First(r => r.Region == region);
        var obj = new GameObject("Foot arrow");
        objects.Add(obj);
        obj.transform.position = volume.GetComponent<Collider>().bounds.center + Vector3.forward * 3f;
        arrow = obj.AddComponent<ArrowFlight>();
        arrow.Launch(Vector3.back * 1000f, first.transform);
        Assert.That(flow.TryTrackShot(0, arrow), Is.True);
        return flow;
    }

    // Runs the authored entry, full hold and recovery automatically while the embedded tip remains attached.
    [UnityTest]
    public IEnumerator FootAnimationCompletesOnlyAfterEntryHoldAndRecovery()
    {
        var flow = BodyShot(out var target, out var arrow);
        yield return new WaitForFixedUpdate();
        Assert.That(flow.Phase, Is.EqualTo(DuelPhase.Reacting));
        var parent = arrow.transform.parent;
        var localTip = arrow.transform.localPosition;
        yield return new WaitForSeconds(1.05f);
        Assert.That(flow.Phase, Is.EqualTo(DuelPhase.Holding));
        Assert.That(flow.CanShoot(1), Is.False);
        Assert.That(Vector3.Distance(arrow.transform.position, parent.TransformPoint(localTip)), Is.LessThan(0.0001f));
        yield return new WaitForSeconds(2.5f);
        Assert.That(flow.Phase, Is.EqualTo(DuelPhase.Holding));
        yield return new WaitForSeconds(0.6f);
        Assert.That(flow.Phase, Is.EqualTo(DuelPhase.Recovering));
        yield return new WaitForSeconds(1f);
        Assert.That(flow.CanShoot(1), Is.True);
        Assert.That(target.CurrentHealth, Is.EqualTo(75));
    }

    // Death cancels the active surviving sequence and prevents its delayed get-up from granting another turn.
    [UnityTest]
    public IEnumerator DeathCancelsAutomaticFootRecovery()
    {
        var flow = BodyShot(out var target, out _);
        yield return new WaitForSeconds(1.1f);
        target.TakeDamage(int.MaxValue);
        yield return new WaitForSeconds(4.2f);
        Assert.That(flow.Phase, Is.EqualTo(DuelPhase.Finished));
        Assert.That(flow.CanShoot(0) || flow.CanShoot(1), Is.False);
        Assert.That(flow.ActiveArcher, Is.Zero);
    }

    // Verifies the right-foot hit lifts the right side while the left foot supports its complete automatic recovery.
    [UnityTest]
    public IEnumerator RightFootHasItsOwnLiftSupportAndAutomaticReturn()
    {
        var flow = BodyShot(out var target, out _, BodyRegion.RightFoot);
        var animator = target.GetComponentInChildren<Animator>();
        var injured = animator.GetBoneTransform(HumanBodyBones.RightFoot);
        var support = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
        var initial = injured.position;
        var planted = support.position;
        yield return new WaitForSeconds(1.1f);
        Assert.That(flow.Phase, Is.EqualTo(DuelPhase.Holding));
        Assert.That(injured.position.y - initial.y, Is.GreaterThan(0.13f));
        Assert.That(Vector3.Distance(support.position, planted), Is.LessThan(0.002f));
        yield return new WaitForSeconds(4f);
        Assert.That(flow.CanShoot(1), Is.True);
        Assert.That(Vector3.Distance(injured.position, initial), Is.LessThan(0.002f));
    }

    // Checks both planted feet and the opposite hand's actual forearm support on each implemented arm clip.
    [Test]
    public void ArmClipsCradleTheCorrectSideWithoutMovingEitherFoot()
    {
        foreach (var name in new[] { "EnglishArcher", "ErikaArcher" })
        {
            var root = Character(name);
            var actor = root.GetComponent<ArcherReactionAnimation>();
            Assert.That(actor.Supports(BodyRegion.LeftArm), Is.True);
            var animator = root.GetComponentInChildren<Animator>();
            foreach (var region in new[] { BodyRegion.LeftArm, BodyRegion.RightArm }.Where(actor.Supports))
            {
                var left = region == BodyRegion.LeftArm;
                var leftFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
                var rightFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot);
                var leftStart = leftFoot.position;
                var rightStart = rightFoot.position;
                var entry = actor.Entry(region);
                entry.SampleAnimation(root, entry.length);
                var elbow = animator.GetBoneTransform(left ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm);
                var wrist = animator.GetBoneTransform(left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
                var support = animator.GetBoneTransform(left ? HumanBodyBones.RightHand : HumanBodyBones.LeftHand);
                var segment = wrist.position - elbow.position;
                var closest = elbow.position + segment * Mathf.Clamp01(Vector3.Dot(support.position - elbow.position, segment) / segment.sqrMagnitude);
                Assert.That(Vector3.Distance(support.position, closest), Is.LessThan(0.07f), name + " " + region);
                Assert.That(wrist.position.y, Is.GreaterThan(1.1f));
                Assert.That(Vector3.Distance(leftFoot.position, leftStart), Is.LessThan(0.002f));
                Assert.That(Vector3.Distance(rightFoot.position, rightStart), Is.LessThan(0.002f));
                Assert.That(Mathf.Abs(Floor(root)), Is.LessThan(0.006f));
                actor.Recovery(region).SampleAnimation(root, actor.Recovery(region).length);
                Assert.That(Vector3.Distance(leftFoot.position, leftStart), Is.LessThan(0.002f));
                Assert.That(Vector3.Distance(rightFoot.position, rightStart), Is.LessThan(0.002f));
            }
            Object.DestroyImmediate(root);
        }
    }

    // Exercises a real arm impact through the same automatic entry/hold/recovery lifecycle as the foot clips.
    [UnityTest]
    public IEnumerator LeftArmRecoveryKeepsTheShotLockedUntilStandingReady()
    {
        var flow = BodyShot(out var target, out var arrow, BodyRegion.LeftArm);
        yield return new WaitForSeconds(1.1f);
        Assert.That(flow.Phase, Is.EqualTo(DuelPhase.Holding));
        Assert.That(flow.CanShoot(1), Is.False);
        Assert.That(arrow.transform.parent.GetComponent<BodyHitRegion>().Region, Is.EqualTo(BodyRegion.LeftArm));
        yield return new WaitForSeconds(4f);
        Assert.That(flow.CanShoot(1), Is.True);
        Assert.That(target.CurrentHealth, Is.EqualTo(75));
    }
}
#endif
