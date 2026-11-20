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

    // The Windows player requires root-sampled transform clips when the Humanoid Animator is on a child.
    [TestCase("EnglishArcher")]
    [TestCase("ErikaArcher")]
    public void AllInjuryClipsSupportStandaloneRootSampling(string model)
    {
        var actor = Character(model).GetComponent<ArcherReactionAnimation>();
        foreach (BodyRegion region in System.Enum.GetValues(typeof(BodyRegion)))
        {
            var clips = new[] { actor.Entry(region), actor.Hold(region), actor.Recovery(region), actor.DeathFinisher(region), actor.FatalEntry(region) };
            foreach (var clip in clips.Where(c => c != null))
                Assert.That(clip.legacy, Is.True, model + " " + region + " " + clip.name);
        }
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

    // Both rigs reach across to the struck shoulder during the recorded fatal reaction.
    [TestCase("EnglishArcher")]
    [TestCase("ErikaArcher")]
    public void FatalShouldersReachAcrossBeforeFalling(string model)
    {
        var root = Character(model);
        var player = root.GetComponent<ArcherReactionAnimation>();
        var rig = root.GetComponentInChildren<Animator>();
        foreach (var side in new[] { BodyRegion.LeftShoulder, BodyRegion.RightShoulder })
        {
            var clip = player.FatalEntry(side);
            Assert.That(clip, Is.Not.Null, side.ToString());
            var left = side == BodyRegion.LeftShoulder;
            var finger = rig.GetBoneTransform(left ? HumanBodyBones.RightIndexDistal : HumanBodyBones.LeftIndexDistal);
            var struck = rig.GetBoneTransform(left ? HumanBodyBones.LeftUpperArm : HumanBodyBones.RightUpperArm);
            var other = rig.GetBoneTransform(left ? HumanBodyBones.RightUpperArm : HumanBodyBones.LeftUpperArm);
            var nearest = float.PositiveInfinity;
            for (var frame = 12; frame <= 36; frame++)
            {
                clip.SampleAnimation(root, frame / 30f);
                var distance = Vector3.Distance(finger.position, struck.position);
                if (distance < Vector3.Distance(finger.position, other.position)) nearest = Mathf.Min(nearest, distance);
            }
            Assert.That(nearest, Is.LessThan(0.2f), side.ToString());
            clip.SampleAnimation(root, clip.length);
            Assert.That(rig.GetBoneTransform(HumanBodyBones.Head).position.y, Is.LessThan(0.4f));
        }
    }

    // A lethal shoulder arrow runs the complete fall even when the standing reaction is unbound.
    [UnityTest]
    public IEnumerator LethalShoulderArrowsCompleteTheRecordedFall()
    {
        foreach (var model in new[] { "EnglishArcher", "ErikaArcher" })
        foreach (var side in new[] { BodyRegion.LeftShoulder, BodyRegion.RightShoulder })
        {
            var flow = BodyShot(out var target, out var arrow, side, model);
            target.TakeDamage(75);
            var head = target.GetComponentInChildren<Animator>().GetBoneTransform(HumanBodyBones.Head);
            var duration = target.GetComponent<ArcherReactionAnimation>().FatalEntry(side).length;
            yield return new WaitForSeconds(duration + 0.25f);
            Assert.That(arrow.transform.parent.GetComponent<BodyHitRegion>().Region, Is.EqualTo(side));
            Assert.That(flow.Phase, Is.EqualTo(DuelPhase.Finished));
            Assert.That(target.CurrentHealth, Is.Zero);
            Assert.That(target.transform.InverseTransformPoint(head.position).y, Is.LessThan(0.4f), model);
            yield return CleanUp();
        }
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


    // Checks the neck's own wound reach, buckling knees and ground-contact collapse on both actual skins.
    [Test]
    public void NeckEntryReachesThenBucklesAndCollapsesOnBothModels()
    {
        foreach (var name in new[] { "EnglishArcher", "ErikaArcher" })
        {
            var root = Character(name);
            var a = root.GetComponentInChildren<Animator>();
            var actor = root.GetComponent<ArcherReactionAnimation>();
            var clip = actor.Entry(BodyRegion.Neck);
            Assert.That(actor.Supports(BodyRegion.Neck), Is.True);
            Assert.That(actor.Recovery(BodyRegion.Neck), Is.Null);
            Assert.That(clip, Is.Not.SameAs(actor.Entry(BodyRegion.Head)));
            var knee = a.GetBoneTransform(HumanBodyBones.RightLowerLeg);
            var thigh = a.GetBoneTransform(HumanBodyBones.RightUpperLeg);
            var foot = a.GetBoneTransform(HumanBodyBones.RightFoot);
            clip.SampleAnimation(root, 0.65f);
            var hand = a.GetBoneTransform(HumanBodyBones.RightHand);
            var finger = a.GetBoneTransform(HumanBodyBones.RightMiddleProximal);
            var neck = a.GetBoneTransform(HumanBodyBones.Neck);
            Assert.That(Vector3.Distance(Vector3.Lerp(hand.position, finger.position, 0.5f), neck.position), Is.LessThan(0.14f));
            clip.SampleAnimation(root, 1.1f);
            Assert.That(Vector3.Angle(thigh.position - knee.position, foot.position - knee.position), Is.LessThan(120f));
            for (var frame = 0; frame <= 72; frame++)
            {
                clip.SampleAnimation(root, clip.length * frame / 72f);
                Assert.That(Floor(root), Is.GreaterThan(-0.006f), name + " frame " + frame);
            }
            Assert.That(a.GetBoneTransform(HumanBodyBones.Head).position.y, Is.LessThan(0.35f));
            Assert.That(Mathf.Abs(Floor(root)), Is.LessThan(0.006f));
            Object.DestroyImmediate(root);
        }
    }

    // Tracks two actual off-centre wounds instead of reaching toward one generic neck socket.
    [UnityTest]
    public IEnumerator FatalNeckReachFollowsTheActualImpactOnEitherSide(
        [Values("EnglishArcher", "ErikaArcher")] string character)
    {
        foreach (var offset in new[] { -0.035f, 0.035f })
        {
            var flow = BodyShot(out var target, out var arrow, BodyRegion.Neck, character);
            arrow.transform.position += Vector3.right * offset;
            var results = 0;
            var recoveries = 0;
            flow.MatchFinished += _ => results++;
            flow.RecoveryRequested += (_, __) => recoveries++;
            yield return new WaitForSeconds(0.65f);
            Assert.That(target.CurrentHealth, Is.Zero);
            Assert.That(flow.Phase, Is.EqualTo(DuelPhase.Finished));
            Assert.That(arrow.transform.parent, Is.Not.Null, character + " wound " + offset);
            Assert.That(arrow.transform.parent.GetComponent<BodyHitRegion>().Region, Is.EqualTo(BodyRegion.Neck));
            var a = target.GetComponentInChildren<Animator>();
            var neck = a.GetBoneTransform(HumanBodyBones.Neck);
            var wrist = a.GetBoneTransform(HumanBodyBones.RightHand);
            var expected = arrow.transform.position - neck.up * 0.045f - arrow.transform.forward * 0.025f;
            Assert.That(Vector3.Distance(wrist.position, expected), Is.LessThan(0.008f), "Actual wound side " + offset);
            var anchored = arrow.transform.localPosition;
            yield return new WaitForSeconds(2f);
            Assert.That(arrow.transform.localPosition, Is.EqualTo(anchored));
            Assert.That(flow.CanShoot(0), Is.False);
            Assert.That(a.GetBoneTransform(HumanBodyBones.Head).position.y - target.transform.position.y, Is.LessThan(0.35f));
            Assert.That(flow.CanShoot(1), Is.False);
            Assert.That(results, Is.EqualTo(1));
            Assert.That(recoveries, Is.Zero);
            yield return CleanUp();
        }
    }

    // Checks the visible retargeted fall lands face-up behind the stance without moving the model root.
    [Test]
    public void ChestFallLandsOnTheBackOnBothModels()
    {
        foreach (var name in new[] { "EnglishArcher", "ErikaArcher" })
        {
            var root = Character(name);
            var actor = root.GetComponent<ArcherReactionAnimation>();
            var a = root.GetComponentInChildren<Animator>();
            var hips = a.GetBoneTransform(HumanBodyBones.Hips);
            var initial = hips.position;
            var rotation = hips.rotation;
            var modelPosition = a.transform.localPosition;
            var modelRotation = a.transform.localRotation;
            var entry = actor.Entry(BodyRegion.Chest);
            Assert.That(entry, Is.Not.Null);
            for (var sample = 0; sample <= 81; sample++)
            {
                entry.SampleAnimation(root, entry.length * sample / 81f);
                Assert.That(Floor(root), Is.GreaterThan(-0.006f), name + " frame " + sample);
                Assert.That(Vector3.Distance(a.transform.localPosition, modelPosition), Is.LessThan(0.0001f));
                Assert.That(Quaternion.Angle(a.transform.localRotation, modelRotation), Is.LessThan(0.01f));
            }
            Assert.That(hips.position.z - initial.z, Is.LessThan(-0.6f));
            Assert.That((hips.rotation * Quaternion.Inverse(rotation) * Vector3.forward).y, Is.GreaterThan(0.75f));
            Assert.That(a.GetBoneTransform(HumanBodyBones.Head).position.y, Is.LessThan(0.35f));
            Assert.That(Floor(root), Is.LessThan(0.006f));
            Object.DestroyImmediate(root);
        }
    }

    // Keeps entry-only playback automatic while an unauthored recovery remains explicitly pending.
    [UnityTest]
    public IEnumerator ChestFallStartsItsHoldOnlyAfterLanding()
    {
        var flow = BodyShot(out var target, out var arrow, BodyRegion.Chest);
        var player = target.GetComponent<ArcherReactionAnimation>();
        player.SetSequence(BodyRegion.Chest, player.Entry(BodyRegion.Chest), null);
        yield return new WaitForSeconds(0.4f);
        Assert.That(flow.Phase, Is.EqualTo(DuelPhase.Reacting));
        Assert.That(flow.CanShoot(1), Is.False);
        yield return new WaitForSeconds(2.5f);
        Assert.That(flow.Phase, Is.EqualTo(DuelPhase.Holding));
        Assert.That(arrow.transform.parent.GetComponent<BodyHitRegion>().Region, Is.EqualTo(BodyRegion.Chest));
        yield return new WaitForSeconds(0.95f);
        Assert.That(flow.Phase, Is.EqualTo(DuelPhase.Recovering));
        Assert.That(flow.CanShoot(1), Is.False);
        Assert.That(target.CurrentHealth, Is.EqualTo(75));
    }

    // Samples the real back-to-standing motion for continuity, floor contact and a complete neutral finish.
    [Test]
    public void ChestRecoveryStartsAtLandingAndEndsStandingOnBothModels()
    {
        foreach (var name in new[] { "EnglishArcher", "ErikaArcher" })
        {
            var root = Character(name);
            var a = root.GetComponentInChildren<Animator>();
            var bones = a.GetComponentsInChildren<Transform>().Where(t => t != a.transform).ToArray();
            var neutral = bones.Select(t => t.position).ToArray();
            var neutralRotations = bones.Select(t => t.rotation).ToArray();
            var actor = root.GetComponent<ArcherReactionAnimation>();
            Assert.That(actor.Supports(BodyRegion.Chest), Is.True);
            var entry = actor.Entry(BodyRegion.Chest);
            entry.SampleAnimation(root, entry.length);
            var landing = bones.Select(t => t.position).ToArray();
            var rotations = bones.Select(t => t.rotation).ToArray();
            var recovery = actor.Recovery(BodyRegion.Chest);
            recovery.SampleAnimation(root, 0f);
            for (var i = 0; i < bones.Length; i++)
            {
                Assert.That(Vector3.Distance(bones[i].position, landing[i]), Is.LessThan(0.001f), name + " " + bones[i].name);
                Assert.That(Quaternion.Angle(bones[i].rotation, rotations[i]), Is.LessThan(0.1f));
            }
            var hips = a.GetBoneTransform(HumanBodyBones.Hips);
            var leftHand = a.GetBoneTransform(HumanBodyBones.LeftHand);
            var rightHand = a.GetBoneTransform(HumanBodyBones.RightHand);
            for (var frame = 0; frame <= 162; frame++)
            {
                var time = recovery.length * frame / 162f;
                recovery.SampleAnimation(root, time);
                Assert.That(Floor(root), Is.GreaterThan(-0.006f), name + " frame " + frame);
                if (time >= 3.8f)
                {
                    Assert.That(leftHand.position.y - hips.position.y, Is.LessThan(0.12f), name + " left hand at " + time);
                    Assert.That(rightHand.position.y - hips.position.y, Is.LessThan(0.12f), name + " right hand at " + time);
                }
            }
            for (var i = 0; i < bones.Length; i++)
            {
                Assert.That(Vector3.Distance(bones[i].position, neutral[i]), Is.LessThan(0.001f), name + " " + bones[i].name);
                Assert.That(Quaternion.Angle(bones[i].rotation, neutralRotations[i]), Is.LessThan(0.1f));
            }
            Assert.That(a.GetBoneTransform(HumanBodyBones.Head).position.y, Is.GreaterThan(1.5f));
            Assert.That(Mathf.Abs(Floor(root)), Is.LessThan(0.006f));
            Object.DestroyImmediate(root);
        }
    }

    // Checks the fitted hand and boot paths between animation samples on both target rigs.
    [Test]
    public void ChestRecoveryClearsTheCoreWithoutWristJumps()
    {
        foreach (var name in new[] { "EnglishArcher", "ErikaArcher" })
        {
            var root = Character(name);
            var rig = root.GetComponentInChildren<Animator>();
            var clip = root.GetComponent<ArcherReactionAnimation>().Recovery(BodyRegion.Chest);
            var volumes = root.GetComponentsInChildren<BodyHitRegion>();
            var core = volumes.Where(h => h.Region is BodyRegion.Chest or BodyRegion.Abdomen or BodyRegion.Pelvis
                or BodyRegion.Head or BodyRegion.Neck).Select(h => h.GetComponent<Collider>()).ToArray();
            var distalBones = new[] { HumanBodyBones.LeftLowerArm, HumanBodyBones.RightLowerArm,
                HumanBodyBones.LeftHand, HumanBodyBones.RightHand, HumanBodyBones.LeftLowerLeg,
                HumanBodyBones.RightLowerLeg, HumanBodyBones.LeftFoot, HumanBodyBones.RightFoot }
                .Select(rig.GetBoneTransform).ToArray();
            var limbs = volumes.Where(h => distalBones.Contains(h.transform.parent))
                .Select(h => h.GetComponent<Collider>()).ToArray();
            var frames = Mathf.CeilToInt(120f * clip.length);
            clip.SampleAnimation(root, 0f);
            var previous = distalBones.Select(t => t.position).ToArray();

            for (var frame = 0; frame <= frames; frame++)
            {
                clip.SampleAnimation(root, clip.length * frame / frames);
                foreach (var limb in limbs)
                foreach (var torso in core)
                    if (Physics.ComputePenetration(limb, limb.transform.position, limb.transform.rotation,
                        torso, torso.transform.position, torso.transform.rotation, out _, out var depth))
                        Assert.That(depth, Is.LessThan(0.003f), $"{name} {limb.name}/{torso.name} frame {frame}");

                for (var joint = 0; joint < 4; joint++)
                {
                    Assert.That(Vector3.Distance(previous[joint], distalBones[joint].position),
                        Is.LessThan(0.04f), $"{name} {distalBones[joint].name} frame {frame}");
                    previous[joint] = distalBones[joint].position;
                }
            }
            Object.DestroyImmediate(root);
        }
    }

    // Grants the surviving archer a turn only after landing, the chest hold and the complete get-up.
    [UnityTest]
    public IEnumerator ChestRecoveryFinishesBeforeGrantingTheNextTurn()
    {
        var flow = BodyShot(out var target, out var arrow, BodyRegion.Chest);
        yield return new WaitForSeconds(3f);
        Assert.That(flow.Phase, Is.EqualTo(DuelPhase.Holding));
        yield return new WaitForSeconds(0.15f);
        Assert.That(flow.Phase, Is.EqualTo(DuelPhase.Holding));
        yield return new WaitForSeconds(0.7f);
        Assert.That(flow.Phase, Is.EqualTo(DuelPhase.Recovering));
        Assert.That(flow.CanShoot(1), Is.False);
        yield return new WaitForSeconds(3f);
        Assert.That(flow.Phase, Is.EqualTo(DuelPhase.Recovering));
        Assert.That(flow.CanShoot(1), Is.False);
        yield return new WaitForSeconds(0.55f);
        Assert.That(flow.CanShoot(1), Is.True);
        Assert.That(target.CurrentHealth, Is.EqualTo(75));
        Assert.That(arrow.transform.parent.GetComponent<BodyHitRegion>().Region, Is.EqualTo(BodyRegion.Chest));
    }

    // Cancels an active chest get-up when defeat ends the match without a late animation handoff.
    [UnityTest]
    public IEnumerator DeathDuringChestRecoveryStopsTheGetUp()
    {
        var flow = BodyShot(out var target, out var arrow, BodyRegion.Chest);
        yield return new WaitForSeconds(3.85f);
        Assert.That(flow.Phase, Is.EqualTo(DuelPhase.Recovering));
        target.TakeDamage(100);
        var head = target.GetComponentInChildren<Animator>().GetBoneTransform(HumanBodyBones.Head);
        var recoveries = 0;
        flow.RecoveryRequested += (_, __) => recoveries++;
        yield return new WaitForSeconds(1f);
        Assert.That(flow.Phase, Is.EqualTo(DuelPhase.Finished));
        Assert.That(recoveries, Is.Zero);
        Assert.That(flow.CanShoot(1), Is.False);
        Assert.That(head.position.y - target.transform.position.y, Is.LessThan(0.4f));
    }

    // Resolves lethal chest and pelvic hits once, finishes the matching collapse and preserves the embedded tip.
    [UnityTest]
    public IEnumerator FatalGroundedHitsEndOnceAndCollapseWithoutRecovery()
    {
        var originalScale = Time.timeScale;
        Time.timeScale = 4f;
        try
        {
            foreach (var name in new[] { "EnglishArcher", "ErikaArcher" })
            foreach (var region in new[] { BodyRegion.Chest, BodyRegion.Pelvis, BodyRegion.Abdomen })
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

    // Measures visible contact near a supported joint rather than treating its bone origin as the skin surface.
    private static float ContactFloor(GameObject root, Vector3 joint, bool seat = false)
    {
        var floor = float.PositiveInfinity;
        var radius = seat ? 0.25f : 0.18f;
        var mesh = new Mesh();
        try
        {
            foreach (var skin in root.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                skin.BakeMesh(mesh, true);
                foreach (var vertex in mesh.vertices)
                {
                    var point = skin.transform.TransformPoint(vertex);
                    var delta = seat ? Vector3.ProjectOnPlane(point - joint, Vector3.up) : point - joint;
                    if (delta.sqrMagnitude < radius * radius) floor = Mathf.Min(floor, point.y);
                }
                mesh.Clear();
            }
            return floor;
        }
        finally { Object.DestroyImmediate(mesh); }
    }

    // Measures the anatomical palm plane independently of the hand bone's imported local axes.
    private static Vector3 PalmNormal(Animator animator, bool left)
    {
        var hand = animator.GetBoneTransform(left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
        var middle = animator.GetBoneTransform(left ? HumanBodyBones.LeftMiddleProximal : HumanBodyBones.RightMiddleProximal);
        var index = animator.GetBoneTransform(left ? HumanBodyBones.LeftIndexProximal : HumanBodyBones.RightIndexProximal);
        var little = animator.GetBoneTransform(left ? HumanBodyBones.LeftLittleProximal : HumanBodyBones.RightLittleProximal);
        return Vector3.Cross(middle.position - hand.position, index.position - little.position).normalized * (left ? 1f : -1f);
    }

    // Requires a visible supported seat, both palms behind the hips and a continuous return to standing.
    [Test]
    public void PelvisClipsSitWithBothHandsBehindOnBothModels()
    {
        foreach (var name in new[] { "EnglishArcher", "ErikaArcher" })
        {
            var root = Character(name);
            var actor = root.GetComponent<ArcherReactionAnimation>();
            var a = root.GetComponentInChildren<Animator>();
            var hips = a.GetBoneTransform(HumanBodyBones.Hips);
            var head = a.GetBoneTransform(HumanBodyBones.Head);
            var standing = head.position;
            Assert.That(actor.Supports(BodyRegion.Pelvis), Is.True);
            var entry = actor.Entry(BodyRegion.Pelvis);
            Assert.That(entry, Is.Not.SameAs(actor.Entry(BodyRegion.Abdomen)));
            for (var frame = 0; frame <= 81; frame++)
            {
                entry.SampleAnimation(root, entry.length * frame / 81f);
                Assert.That(Floor(root), Is.GreaterThan(-0.006f), name + " frame " + frame);
            }
            Assert.That(hips.position.y, Is.LessThan(0.3f));
            Assert.That(ContactFloor(root, hips.position, true), Is.InRange(-0.006f, 0.015f));
            var hands = new[] { a.GetBoneTransform(HumanBodyBones.LeftHand), a.GetBoneTransform(HumanBodyBones.RightHand) };
            var resting = hands.Select(t => t.position).ToArray();
            for (var i = 0; i < hands.Length; i++)
            {
                Assert.That(hands[i].position.z, Is.LessThan(hips.position.z - 0.08f));
                Assert.That(ContactFloor(root, hands[i].position), Is.InRange(-0.006f, 0.01f));
                Assert.That(PalmNormal(a, i == 0).y, Is.GreaterThan(0.95f));
            }
            Assert.That(a.GetBoneTransform(HumanBodyBones.LeftLowerLeg).position.y, Is.GreaterThan(hips.position.y + 0.1f));
            Assert.That(a.GetBoneTransform(HumanBodyBones.RightLowerLeg).position.y, Is.GreaterThan(hips.position.y + 0.1f));
            var recovery = actor.Recovery(BodyRegion.Pelvis);
            recovery.SampleAnimation(root, 0f);
            for (var i = 0; i < hands.Length; i++) Assert.That(Vector3.Distance(hands[i].position, resting[i]), Is.LessThan(0.001f));
            recovery.SampleAnimation(root, 0.65f);
            var leadBoot = a.GetBoneTransform(HumanBodyBones.LeftFoot);
            var planted = leadBoot.position;
            Assert.That(a.GetBoneTransform(HumanBodyBones.RightLowerLeg).position.y, Is.LessThan(0.15f));
            for (var frame = 1; frame <= 45; frame++)
            {
                recovery.SampleAnimation(root, 0.65f + frame / 60f);
                Assert.That(Vector3.Distance(leadBoot.position, planted), Is.LessThan(0.01f), name + " lead boot");
            }
            for (var frame = 0; frame <= 81; frame++)
            {
                recovery.SampleAnimation(root, recovery.length * frame / 81f);
                Assert.That(Floor(root), Is.GreaterThan(-0.006f));
            }
            Assert.That(Vector3.Distance(head.position, standing), Is.LessThan(0.001f));
            Object.DestroyImmediate(root);
        }
    }

    // Holds the actual pelvic impact in a supported seat before a completed recovery grants the turn.
    [UnityTest]
    public IEnumerator PelvisHitWaitsForTheFullSeatedHoldAndRecovery()
    {
        var flow = BodyShot(out var target, out var arrow, BodyRegion.Pelvis);
        var animation = target.GetComponent<ArcherReactionAnimation>();
        var landed = -1f;
        var recoveryStarted = -1f;
        flow.Changed += () =>
        {
            if (flow.Phase == DuelPhase.Holding) landed = Time.time;
            if (flow.Phase == DuelPhase.Recovering) recoveryStarted = Time.time;
        };
        yield return new WaitForSeconds(animation.Entry(BodyRegion.Pelvis).length + 0.15f);
        Assert.That(flow.Phase, Is.EqualTo(DuelPhase.Holding));
        Assert.That(flow.CanShoot(1), Is.False);
        Assert.That(arrow.transform.parent.GetComponent<BodyHitRegion>().Region, Is.EqualTo(BodyRegion.Pelvis));
        yield return new WaitForSeconds(3f + animation.Recovery(BodyRegion.Pelvis).length);
        Assert.That(recoveryStarted - landed, Is.GreaterThanOrEqualTo(2.995f));
        Assert.That(flow.CanShoot(1), Is.True);
        Assert.That(target.CurrentHealth, Is.EqualTo(75));
    }

    // Interrupts the supported seated get-up with a full fatal collapse.
    [UnityTest]
    public IEnumerator DeathInterruptsPelvisRecovery()
    {
        var flow = BodyShot(out var target, out var arrow, BodyRegion.Pelvis);
        var entry = target.GetComponent<ArcherReactionAnimation>().Entry(BodyRegion.Pelvis);
        yield return new WaitForSeconds(entry.length + 3.2f);
        Assert.That(flow.Phase, Is.EqualTo(DuelPhase.Recovering));
        target.TakeDamage(100);
        var head = target.GetComponentInChildren<Animator>().GetBoneTransform(HumanBodyBones.Head);
        yield return new WaitForSeconds(1f);
        Assert.That(flow.Phase, Is.EqualTo(DuelPhase.Finished));
        Assert.That(flow.CanShoot(1), Is.False);
        Assert.That(head.position.y - target.transform.position.y, Is.LessThan(0.4f));
    }

    // Requires both visible knees on the floor and a separate folded wound-brace pose before returning to neutral.
    [Test]
    public void AbdomenClipsFoldAndKneelOnBothModels()
    {
        foreach (var name in new[] { "EnglishArcher", "ErikaArcher" })
        {
            var root = Character(name);
            var a = root.GetComponentInChildren<Animator>();
            var actor = root.GetComponent<ArcherReactionAnimation>();
            Assert.That(actor.Supports(BodyRegion.Abdomen), Is.True);
            var head = a.GetBoneTransform(HumanBodyBones.Head);
            var standing = head.position;
            var spine = a.GetBoneTransform(HumanBodyBones.Spine);
            var rotation = spine.rotation;
            var entry = actor.Entry(BodyRegion.Abdomen);
            for (var frame = 0; frame <= 81; frame++)
            {
                entry.SampleAnimation(root, entry.length * frame / 81f);
                Assert.That(Floor(root), Is.GreaterThan(-0.006f), name + " frame " + frame);
            }
            foreach (var bone in new[] { HumanBodyBones.LeftLowerLeg, HumanBodyBones.RightLowerLeg })
                Assert.That(ContactFloor(root, a.GetBoneTransform(bone).position), Is.InRange(-0.006f, 0.015f), name + " " + bone);
            Assert.That(Quaternion.Angle(spine.rotation, rotation), Is.GreaterThan(35f));
            var box = root.GetComponentsInChildren<BodyHitRegion>().Single(r => r.Region == BodyRegion.Abdomen).GetComponent<BoxCollider>();
            var point = box.transform.TransformPoint(box.center + Vector3.forward * (box.size.z * 0.5f));
            var right = a.GetBoneTransform(HumanBodyBones.RightHand);
            var left = a.GetBoneTransform(HumanBodyBones.LeftHand);
            Assert.That(Vector3.Distance(a.GetBoneTransform(HumanBodyBones.RightMiddleProximal).position, point), Is.LessThan(0.1f));
            Assert.That(Vector3.Distance(a.GetBoneTransform(HumanBodyBones.LeftMiddleProximal).position, point), Is.LessThan(0.1f));
            var supportedRight = right.position;
            var supportedLeft = left.position;
            var recovery = actor.Recovery(BodyRegion.Abdomen);
            recovery.SampleAnimation(root, 0f);
            Assert.That(Vector3.Distance(right.position, supportedRight), Is.LessThan(0.001f));
            Assert.That(Vector3.Distance(left.position, supportedLeft), Is.LessThan(0.001f));
            for (var frame = 0; frame <= 81; frame++)
            {
                recovery.SampleAnimation(root, recovery.length * frame / 81f);
                Assert.That(Floor(root), Is.GreaterThan(-0.006f));
            }
            Assert.That(Vector3.Distance(head.position, standing), Is.LessThan(0.001f));
            Object.DestroyImmediate(root);
        }
    }

    // Samples the kneeling entry, recovery and fatal continuation for boot penetration through the pelvis.
    [Test]
    public void AbdomenClipsKeepBothBootsClearOfThePelvis()
    {
        foreach (var name in new[] { "EnglishArcher", "ErikaArcher" })
        {
            var root = Character(name);
            var hitVolumes = root.GetComponentsInChildren<BodyHitRegion>();
            var pelvis = hitVolumes.Single(r => r.Region == BodyRegion.Pelvis).GetComponent<Collider>();
            var boots = hitVolumes.Where(r => r.Region is BodyRegion.LeftFoot or BodyRegion.RightFoot)
                .Select(r => r.GetComponent<Collider>()).ToArray();
            Assert.That(boots, Has.Length.EqualTo(2), name);
            var actor = root.GetComponent<ArcherReactionAnimation>();
            foreach (var clip in new[] { actor.Entry(BodyRegion.Abdomen), actor.Recovery(BodyRegion.Abdomen),
                actor.DeathFinisher(BodyRegion.Abdomen) })
            for (var sample = 0; sample <= 60; sample++)
            {
                var time = clip.length * sample / 60f;
                clip.SampleAnimation(root, time);
                foreach (var boot in boots)
                {
                    if (Physics.ComputePenetration(boot, boot.transform.position, boot.transform.rotation,
                        pelvis, pelvis.transform.position, pelvis.transform.rotation, out _, out var depth))
                        Assert.That(depth, Is.LessThanOrEqualTo(0.002f), $"{name} {clip.name} {boot.name} at {time:F3}s");
                }
            }
            Object.DestroyImmediate(root);
        }
    }

    // Fits both hands to actual left/right abdominal impacts and keeps the turn locked until standing.
    [UnityTest]
    public IEnumerator AbdomenReachFollowsTheActualWoundThroughItsFullHold()
    {
        foreach (var offset in new[] { -0.08f, 0.08f })
        {
            var flow = BodyShot(out var target, out var arrow, BodyRegion.Abdomen);
            var actor = target.GetComponent<ArcherReactionAnimation>();
            arrow.transform.position += Vector3.right * offset;
            var landed = -1f;
            var recoveryStarted = -1f;
            var ready = -1f;
            flow.Changed += () =>
            {
                if (flow.Phase == DuelPhase.Holding) landed = Time.time;
                if (flow.Phase == DuelPhase.Recovering) recoveryStarted = Time.time;
                if (flow.Phase == DuelPhase.Aiming) ready = Time.time;
            };
            yield return new WaitForSeconds(actor.Entry(BodyRegion.Abdomen).length + 0.18f);
            Assert.That(flow.Phase, Is.EqualTo(DuelPhase.Holding));
            Assert.That(arrow.transform.parent.GetComponent<BodyHitRegion>().Region, Is.EqualTo(BodyRegion.Abdomen));
            var a = target.GetComponentInChildren<Animator>();
            var spine = a.GetBoneTransform(HumanBodyBones.Spine);
            var right = a.GetBoneTransform(HumanBodyBones.RightHand);
            var left = a.GetBoneTransform(HumanBodyBones.LeftHand);
            var expected = arrow.transform.position + spine.right * 0.079f - arrow.transform.forward * 0.068f + spine.up * 0.062f;
            Assert.That(Vector3.Distance(right.position, expected), Is.LessThan(0.008f));
            expected = arrow.transform.position - spine.right * 0.079f - arrow.transform.forward * 0.068f + spine.up * 0.05f;
            Assert.That(Vector3.Distance(left.position, expected), Is.LessThan(0.008f));
            Assert.That(flow.CanShoot(1), Is.False);
            yield return new WaitForSeconds(3f + actor.Recovery(BodyRegion.Abdomen).length + 0.1f);
            Assert.That(flow.CanShoot(1), Is.True);
            Assert.That(landed, Is.GreaterThan(0f));
            Assert.That(recoveryStarted - landed, Is.GreaterThanOrEqualTo(2.995f));
            Assert.That(ready - recoveryStarted, Is.GreaterThanOrEqualTo(0.85f));
            Assert.That(target.CurrentHealth, Is.EqualTo(75));
            yield return CleanUp();
        }
    }

    // Checks both rigs on a low off-center impact throughout the real reaction and recovery.
    [UnityTest]
    public IEnumerator LowAbdomenHitsKeepBothForearmsClear()
    {
        foreach (var model in new[] { "EnglishArcher", "ErikaArcher" })
        {
            var flow = BodyShot(out var target, out var arrow, BodyRegion.Abdomen, model);
            arrow.transform.position += Vector3.right * 0.075f - Vector3.up * 0.022f;
            var animator = target.GetComponentInChildren<Animator>();
            var forearms = new[] { animator.GetBoneTransform(HumanBodyBones.LeftLowerArm),
                animator.GetBoneTransform(HumanBodyBones.RightLowerArm) };
            var regions = target.GetComponentsInChildren<BodyHitRegion>();
            var limbs = regions.Where(region => region.Region is BodyRegion.LeftHand or BodyRegion.RightHand ||
                forearms.Contains(region.transform.parent)).Select(region => region.GetComponent<Collider>()).ToArray();
            var body = regions.Where(region => region.Region is BodyRegion.Head or BodyRegion.Neck or BodyRegion.Chest or
                BodyRegion.Abdomen or BodyRegion.Pelvis).Select(region => region.GetComponent<Collider>()).ToArray();

            var deadline = Time.time + 8f;
            while (!flow.CanShoot(1) && Time.time < deadline)
            {
                if (flow.Phase is DuelPhase.Reacting or DuelPhase.Holding or DuelPhase.Recovering)
                {
                    foreach (var limb in limbs)
                    foreach (var core in body)
                        if (Physics.ComputePenetration(limb, limb.transform.position, limb.transform.rotation,
                            core, core.transform.position, core.transform.rotation, out _, out var depth))
                            Assert.That(depth, Is.LessThanOrEqualTo(0.0021f), $"{model} {flow.Phase}: {limb.name}/{core.name}");
                }
                yield return null;
            }
            Assert.That(flow.CanShoot(1), Is.True);
            Assert.That(target.CurrentHealth, Is.EqualTo(75));
            Assert.That(arrow.transform.parent.GetComponent<BodyHitRegion>().Region, Is.EqualTo(BodyRegion.Abdomen));
            yield return CleanUp();
        }
    }

    // Interrupts abdominal recovery with a full fatal collapse and no return to shooting.
    [UnityTest]
    public IEnumerator DeathInterruptsAbdominalRecovery()
    {
        var flow = BodyShot(out var target, out var arrow, BodyRegion.Abdomen);
        var actor = target.GetComponent<ArcherReactionAnimation>();
        yield return new WaitForSeconds(actor.Entry(BodyRegion.Abdomen).length + 3.2f);
        Assert.That(flow.Phase, Is.EqualTo(DuelPhase.Recovering));
        target.TakeDamage(100);
        var head = target.GetComponentInChildren<Animator>().GetBoneTransform(HumanBodyBones.Head);
        yield return new WaitForSeconds(1f);
        Assert.That(flow.Phase, Is.EqualTo(DuelPhase.Finished));
        Assert.That(flow.CanShoot(1), Is.False);
        Assert.That(head.position.y - target.transform.position.y, Is.LessThan(0.4f));
    }
}
#endif
