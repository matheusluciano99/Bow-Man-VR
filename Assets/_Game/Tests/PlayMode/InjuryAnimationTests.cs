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
        yield return new WaitForSeconds(3.2f);
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
            for (var frame = 0; frame <= 162; frame++)
            {
                recovery.SampleAnimation(root, recovery.length * frame / 162f);
                Assert.That(Floor(root), Is.GreaterThan(-0.006f), name + " frame " + frame);
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

    // Grants the surviving archer a turn only after landing, three seconds down and the complete get-up.
    [UnityTest]
    public IEnumerator ChestRecoveryFinishesBeforeGrantingTheNextTurn()
    {
        var flow = BodyShot(out var target, out var arrow, BodyRegion.Chest);
        yield return new WaitForSeconds(3f);
        Assert.That(flow.Phase, Is.EqualTo(DuelPhase.Holding));
        yield return new WaitForSeconds(2.4f);
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
        yield return new WaitForSeconds(6.1f);
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

    // Resolves a lethal chest hit once, finishes the matching collapse and preserves the actual embedded tip.
    [UnityTest]
    public IEnumerator FatalChestHitEndsOnceAndCollapsesWithoutRecovery()
    {
        var originalScale = Time.timeScale;
        Time.timeScale = 4f;
        try
        {
            foreach (var name in new[] { "EnglishArcher", "ErikaArcher" })
            {
                const BodyRegion region = BodyRegion.Chest;
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
