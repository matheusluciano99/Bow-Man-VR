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

    // Creates a real regional shot with playback connected to the existing match callbacks.
    private DuelTurnFlow BodyShot(out ArcherHealth target, out ArrowFlight arrow, BodyRegion region = BodyRegion.Head, string character = "ErikaArcher")
    {
        var firstObject = new GameObject("First");
        objects.Add(firstObject);
        firstObject.transform.position = new Vector3(1000f, 1000f, 1000f);
        var first = firstObject.AddComponent<ArcherHealth>();
        var root = Character(character);
        root.transform.position = firstObject.transform.position + Vector3.forward * 4f;
        target = root.AddComponent<ArcherHealth>();
        var matchObject = new GameObject("Match");
        objects.Add(matchObject);
        var flow = matchObject.AddComponent<DuelTurnFlow>();
        flow.Initialize(first, target);
        root.GetComponent<ArcherReactionAnimation>().Connect(flow, 1, target);
        Physics.SyncTransforms();
        var volume = root.GetComponentsInChildren<BodyHitRegion>().First(r => r.Region == region);
        var obj = new GameObject("Headshot arrow");
        objects.Add(obj);
        obj.transform.position = volume.GetComponent<Collider>().bounds.center + Vector3.forward * 3f;
        arrow = obj.AddComponent<ArrowFlight>();
        arrow.Launch(Vector3.back * 1000f, first.transform);
        Assert.That(flow.TryTrackShot(0, arrow), Is.True);
        return flow;
    }

    // Requires a dedicated rapid collapse with a relaxed head, visible ground contact and no recovery asset.
    [Test]
    public void HeadCollapseLosesPostureOnBothModels()
    {
        foreach (var name in new[] { "EnglishArcher", "ErikaArcher" })
        {
            var root = Character(name);
            var a = root.GetComponentInChildren<Animator>();
            var head = a.GetBoneTransform(HumanBodyBones.Head);
            var standing = head.position;
            var actor = root.GetComponent<ArcherReactionAnimation>();
            var clip = actor.Entry(BodyRegion.Head);
            Assert.That(actor.Supports(BodyRegion.Head), Is.True);
            Assert.That(actor.Recovery(BodyRegion.Head), Is.Null);
            Assert.That(clip, Is.Not.SameAs(actor.Entry(BodyRegion.Chest)));
            Assert.That(clip.length, Is.LessThan(1.1f));
            clip.SampleAnimation(root, 0.2f);
            Assert.That(head.position.y, Is.LessThan(standing.y - 0.05f));
            for (var frame = 0; frame <= 120; frame++)
            {
                clip.SampleAnimation(root, clip.length * frame / 120f);
                Assert.That(Floor(root), Is.GreaterThan(-0.006f), name + " frame " + frame);
            }
            Assert.That(head.position.y, Is.LessThan(0.4f));
            Assert.That(Mathf.Abs(Floor(root)), Is.LessThan(0.006f));
            Object.DestroyImmediate(root);
        }
    }

    // Ends the result once at impact while the dead archer finishes collapsing and never recovers.
    [UnityTest]
    public IEnumerator HeadshotEndsTheMatchBeforeItsCollapseCompletes()
    {
        var flow = BodyShot(out var target, out var arrow, BodyRegion.Head);
        var results = 0;
        var defeats = 0;
        var recoveries = 0;
        flow.MatchFinished += _ => results++;
        flow.RecoveryRequested += (_, __) => recoveries++;
        target.Defeated += () => defeats++;
        var head = target.GetComponentInChildren<Animator>().GetBoneTransform(HumanBodyBones.Head);
        var standing = head.position.y;
        yield return new WaitForSeconds(0.2f);
        Assert.That(target.CurrentHealth, Is.Zero);
        Assert.That(flow.Phase, Is.EqualTo(DuelPhase.Finished));
        Assert.That(flow.Winner, Is.Zero);
        Assert.That(head.position.y, Is.LessThan(standing - 0.02f));
        Assert.That(arrow.transform.parent.GetComponent<BodyHitRegion>().Region, Is.EqualTo(BodyRegion.Head));
        yield return new WaitForSeconds(1f);
        Assert.That(head.position.y - target.transform.position.y, Is.LessThan(0.4f));
        var fallen = head.position;
        target.TakeDamage(100);
        yield return new WaitForSeconds(3.2f);
        Assert.That(Vector3.Distance(head.position, fallen), Is.LessThan(0.001f));
        Assert.That(flow.CanShoot(0), Is.False);
        Assert.That(flow.CanShoot(1), Is.False);
        Assert.That(results, Is.EqualTo(1));
        Assert.That(defeats, Is.EqualTo(1));
        Assert.That(recoveries, Is.Zero);
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
                actor.Hold(region).SampleAnimation(root, actor.Hold(region).length);
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

    // Confirms the held reaction retains the imported full-body performance rather than substituting limb poses.
    [Test]
    public void FootHoldPreservesTheDownloadedBodyMotion()
    {
        var source = AssetDatabase.LoadAllAssetsAtPath("Assets/ThirdParty/CosindraAnimations/Derived/RightFootPain_Humanoid.fbx")
            .OfType<AnimationClip>().Single(c => c.name == "Right Foot Pain");
        Assert.That(source.isHumanMotion, Is.True);
        foreach (var name in new[] { "EnglishArcher", "ErikaArcher" })
        {
            var reference = Character(name);
            var actual = Character(name);
            var sourceAnimator = reference.GetComponentInChildren<Animator>();
            var actualAnimator = actual.GetComponentInChildren<Animator>();
            foreach (var time in new[] { 0f, 0.4f, 1.5f, 3f })
            {
                source.SampleAnimation(sourceAnimator.gameObject, 0.8f + time);
                actual.GetComponent<ArcherReactionAnimation>().Hold(BodyRegion.RightFoot).SampleAnimation(actual, time);
                foreach (var bone in new[] { HumanBodyBones.Spine, HumanBodyBones.Chest, HumanBodyBones.UpperChest,
                    HumanBodyBones.Neck, HumanBodyBones.Head, HumanBodyBones.LeftUpperArm, HumanBodyBones.RightUpperArm,
                    HumanBodyBones.LeftLowerArm, HumanBodyBones.RightLowerArm, HumanBodyBones.LeftHand, HumanBodyBones.RightHand,
                    HumanBodyBones.LeftUpperLeg, HumanBodyBones.RightUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.RightLowerLeg })
                {
                    var from = sourceAnimator.GetBoneTransform(bone);
                    var to = actualAnimator.GetBoneTransform(bone);
                    if (from != null) Assert.That(Quaternion.Angle(from.localRotation, to.localRotation),
                        Is.LessThan(0.1f), name + " source motion " + bone);
                }
            }
        }
    }

    // Requires load transfer before withdrawal and foot contact before weight returns to the injured side.
    [Test]
    public void FootReactionsUnloadBeforeLiftingAndLandBeforeRecentering()
    {
        foreach (var name in new[] { "EnglishArcher", "ErikaArcher" })
        foreach (var region in new[] { BodyRegion.LeftFoot, BodyRegion.RightFoot })
        {
            var root = Character(name);
            var actor = root.GetComponent<ArcherReactionAnimation>();
            var a = root.GetComponentInChildren<Animator>();
            var injured = a.GetBoneTransform(region == BodyRegion.LeftFoot ? HumanBodyBones.LeftFoot : HumanBodyBones.RightFoot);
            var support = a.GetBoneTransform(region == BodyRegion.LeftFoot ? HumanBodyBones.RightFoot : HumanBodyBones.LeftFoot);
            var hips = a.GetBoneTransform(HumanBodyBones.Hips);
            var rest = injured.position;
            var hipStart = hips.position;
            var side = Mathf.Sign(Vector3.Dot(support.position - hips.position, root.transform.right));
            actor.Entry(region).SampleAnimation(root, 0.05f);
            Assert.That(Vector3.Dot(hips.position - hipStart, root.transform.right) * side, Is.GreaterThan(0.001f));
            Assert.That(Vector3.Distance(injured.position, rest), Is.LessThan(0.001f), name + " must unload before lifting");
            actor.Entry(region).SampleAnimation(root, actor.Entry(region).length);
            var loaded = Vector3.Dot(hips.position - hipStart, root.transform.right) * side;
            actor.Recovery(region).SampleAnimation(root, actor.Recovery(region).length - 0.5f);
            Assert.That(Vector3.Distance(injured.position, rest), Is.LessThan(0.001f), name + " must land before recentering");
            Assert.That(Vector3.Dot(hips.position - hipStart, root.transform.right) * side, Is.GreaterThan(loaded * 0.5f));
            actor.Recovery(region).SampleAnimation(root, actor.Recovery(region).length);
            Assert.That(Vector3.Distance(hips.position, hipStart), Is.LessThan(0.001f));
            Object.DestroyImmediate(root);
        }
    }

    // Checks balanced hold motion, planted support, floor clearance and continuous poses on both real skins.
    [Test]
    public void FootBalanceStaysSupportedAndJoinsEntryAndRecoveryWithoutSnapping()
    {
        foreach (var name in new[] { "EnglishArcher", "ErikaArcher" })
        foreach (var region in new[] { BodyRegion.LeftFoot, BodyRegion.RightFoot })
        {
            var root = Character(name);
            root.transform.rotation = Quaternion.Euler(0f, 37f, 0f);
            var a = root.GetComponentInChildren<Animator>();
            var actor = root.GetComponent<ArcherReactionAnimation>();
            var bones = Enumerable.Range(0, (int)HumanBodyBones.LastBone).Select(i => a.GetBoneTransform((HumanBodyBones)i))
                .Where(b => b != null).Distinct().ToArray();
            var neutral = bones.Select(b => b.position).ToArray();
            var lengths = bones.Select(b => Vector3.Distance(b.position, b.parent.position)).ToArray();
            var support = a.GetBoneTransform(region == BodyRegion.LeftFoot ? HumanBodyBones.RightFoot : HumanBodyBones.LeftFoot);
            var planted = support.position;
            var hold = actor.Hold(region);
            Assert.That(hold, Is.Not.Null);
            Assert.That(hold.length, Is.EqualTo(3f).Within(0.001f));
            var previous = bones.Select(b => b.position).ToArray();
            var heldStart = Vector3.zero;
            var balanceMotion = 0f;
            foreach (var clip in new[] { actor.Entry(region), hold, actor.Recovery(region) })
            {
                clip.SampleAnimation(root, 0f);
                for (var i = 0; i < bones.Length; i++)
                    Assert.That(Vector3.Distance(bones[i].position, previous[i]), Is.LessThan(0.001f), name + " clip seam " + bones[i].name);
                if (clip == hold) heldStart = a.GetBoneTransform(HumanBodyBones.Hips).position;
                for (var frame = 1; frame <= 120; frame++)
                {
                    clip.SampleAnimation(root, clip.length * frame / 120f);
                    Assert.That(Vector3.Distance(support.position, planted), Is.LessThan(0.002f), name + " planted foot");
                    for (var i = 0; i < bones.Length; i++)
                    {
                        if (bones[i] != a.GetBoneTransform(HumanBodyBones.Hips))
                            Assert.That(Vector3.Distance(bones[i].position, bones[i].parent.position), Is.EqualTo(lengths[i]).Within(0.0002f));
                        Assert.That(Vector3.Distance(bones[i].position, previous[i]), Is.LessThan(0.05f), name + " pose jump " + bones[i].name);
                        previous[i] = bones[i].position;
                    }
                    if (clip == hold)
                    {
                        balanceMotion = Mathf.Max(balanceMotion, Vector3.Distance(a.GetBoneTransform(HumanBodyBones.Hips).position, heldStart));
                        if (frame % 4 == 0) Assert.That(Mathf.Abs(Floor(root)), Is.LessThan(0.006f));
                    }
                }
            }
            Assert.That(balanceMotion, Is.GreaterThan(0.0005f), "Supported balance must retain small body motion");
            for (var i = 0; i < bones.Length; i++)
                Assert.That(Vector3.Distance(bones[i].position, neutral[i]), Is.LessThan(0.001f), name + " neutral finish");
            Object.DestroyImmediate(root);
        }
    }

    // Runs the authored entry, full hold and recovery automatically while the embedded tip remains attached.
    [UnityTest]
    public IEnumerator FootAnimationCompletesOnlyAfterEntryHoldAndRecovery()
    {
        var flow = BodyShot(out var target, out var arrow, BodyRegion.LeftFoot);
        var phaseTimes = new Dictionary<DuelPhase, float>();
        flow.Changed += () => phaseTimes[flow.Phase] = Time.time;
        yield return new WaitForFixedUpdate();
        Assert.That(flow.Phase, Is.EqualTo(DuelPhase.Reacting));
        var parent = arrow.transform.parent;
        var localTip = arrow.transform.localPosition;
        yield return new WaitForSeconds(target.GetComponent<ArcherReactionAnimation>().Entry(BodyRegion.LeftFoot).length + 0.1f);
        Assert.That(flow.Phase, Is.EqualTo(DuelPhase.Holding));
        Assert.That(flow.CanShoot(1), Is.False);
        Assert.That(Vector3.Distance(arrow.transform.position, parent.TransformPoint(localTip)), Is.LessThan(0.0001f));
        yield return new WaitForSeconds(1.6f);
        Assert.That(flow.Phase, Is.EqualTo(DuelPhase.Holding));
        yield return new WaitForSeconds(0.6f);
        Assert.That(flow.Phase, Is.EqualTo(DuelPhase.Recovering));
        Assert.That(flow.CanShoot(1), Is.False);
        yield return new WaitForSeconds(0.8f);
        Assert.That(flow.CanShoot(1), Is.True);
        Assert.That(phaseTimes[DuelPhase.Holding] - phaseTimes[DuelPhase.Reacting], Is.EqualTo(1.1f).Within(0.1f));
        Assert.That(phaseTimes[DuelPhase.Recovering] - phaseTimes[DuelPhase.Holding], Is.EqualTo(2f).Within(0.1f));
        Assert.That(phaseTimes[DuelPhase.Aiming] - phaseTimes[DuelPhase.Recovering], Is.EqualTo(0.8f).Within(0.1f));
        Assert.That(target.CurrentHealth, Is.EqualTo(75));
    }

    // Death cancels the active surviving sequence and prevents its delayed get-up from granting another turn.
    [UnityTest]
    public IEnumerator DeathCancelsAutomaticFootRecovery()
    {
        var flow = BodyShot(out var target, out _, BodyRegion.LeftFoot);
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
        var actor = target.GetComponent<ArcherReactionAnimation>();
        var elapsed = actor.Entry(BodyRegion.RightFoot).length + 0.1f;
        yield return new WaitForSeconds(elapsed);
        Assert.That(flow.Phase, Is.EqualTo(DuelPhase.Holding));
        Assert.That(injured.position.y - initial.y, Is.GreaterThan(0.13f));
        Assert.That(Vector3.Distance(support.position, planted), Is.LessThan(0.002f));
        yield return new WaitForSeconds(2f + 0.8f + 0.05f);
        Assert.That(flow.CanShoot(1), Is.True);
        Assert.That(Vector3.Distance(injured.position, initial), Is.LessThan(0.002f));
    }

    // Resolves each lethal foot injury once, finishes the matching collapse and preserves the actual embedded tip.
    [UnityTest]
    public IEnumerator FatalFootHitsFinishTheCollapseWithoutRecovery()
    {
        var originalScale = Time.timeScale;
        Time.timeScale = 4f;
        try
        {
            foreach (var name in new[] { "EnglishArcher", "ErikaArcher" })
            foreach (var region in new[] { BodyRegion.LeftFoot, BodyRegion.RightFoot })
            {
                var flow = BodyShot(out var target, out var arrow, region, name);
                target.TakeDamage(75);
                var results = 0;
                var holds = 0;
                var recoveries = 0;
                flow.MatchFinished += _ => results++;
                flow.RecoveryRequested += (_, __) => recoveries++;
                flow.Changed += () => { if (flow.Phase == DuelPhase.Holding) holds++; };
                yield return new WaitForSeconds(0.12f);
                Assert.That(target.CurrentHealth, Is.Zero, name + " " + region);
                Assert.That(flow.Phase, Is.EqualTo(DuelPhase.Finished));
                Assert.That(flow.Winner, Is.Zero);
                var tip = arrow.transform.localPosition;
                var player = target.GetComponent<ArcherReactionAnimation>();
                var finisher = player.DeathFinisher(region);
                Assert.That(finisher, Is.Not.Null);
                var duration = player.Entry(region).length + finisher.length;
                yield return new WaitForSeconds(duration + 0.25f);
                var head = target.GetComponentInChildren<Animator>().GetBoneTransform(HumanBodyBones.Head);
                Assert.That(head.position.y - target.transform.position.y, Is.LessThan(0.4f), name + " " + region);
                Assert.That(arrow.transform.parent.GetComponent<BodyHitRegion>().Region, Is.EqualTo(region));
                Assert.That(Vector3.Distance(arrow.transform.localPosition, tip), Is.LessThan(0.00001f));
                Assert.That(flow.CanShoot(0) || flow.CanShoot(1), Is.False);
                Assert.That(results, Is.EqualTo(1));
                Assert.That(holds, Is.Zero);
                Assert.That(recoveries, Is.Zero);
                yield return CleanUp();
            }
        }
        finally { Time.timeScale = originalScale; }
    }

}
#endif
