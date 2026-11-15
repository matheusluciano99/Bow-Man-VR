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

    // Keeps right-arm impact identity distinct while its own supported recovery completes automatically.
    [UnityTest]
    public IEnumerator RightArmRecoveryUsesItsOwnAnatomicalSequence()
    {
        var flow = BodyShot(out var target, out var arrow, BodyRegion.RightArm);
        yield return new WaitForSeconds(1.1f);
        Assert.That(flow.Phase, Is.EqualTo(DuelPhase.Holding));
        Assert.That(arrow.transform.parent.GetComponent<BodyHitRegion>().Region, Is.EqualTo(BodyRegion.RightArm));
        yield return new WaitForSeconds(4f);
        Assert.That(flow.CanShoot(1), Is.True);
        Assert.That(target.CurrentHealth, Is.EqualTo(75));
    }

    // Checks that a shoulder hit reaches the actual opposite-hand shoulder contact, rather than the arm cradle.
    [Test]
    public void ShoulderClipsCupTheCorrectShoulderWithPlantedFeet()
    {
        foreach (var name in new[] { "EnglishArcher", "ErikaArcher" })
        {
            var root = Character(name);
            var player = root.GetComponent<ArcherReactionAnimation>();
            Assert.That(player.Supports(BodyRegion.LeftShoulder), Is.True);
            var a = root.GetComponentInChildren<Animator>();
            foreach (var region in new[] { BodyRegion.LeftShoulder, BodyRegion.RightShoulder }.Where(player.Supports))
            {
                var left = region == BodyRegion.LeftShoulder;
                var feet = new[] { a.GetBoneTransform(HumanBodyBones.LeftFoot), a.GetBoneTransform(HumanBodyBones.RightFoot) };
                var planted = feet.Select(t => t.position).ToArray();
                var chest = a.GetBoneTransform(HumanBodyBones.UpperChest);
                var start = chest.rotation;
                player.Entry(region).SampleAnimation(root, player.Entry(region).length);
                var shoulder = a.GetBoneTransform(left ? HumanBodyBones.LeftUpperArm : HumanBodyBones.RightUpperArm);
                var support = a.GetBoneTransform(left ? HumanBodyBones.RightHand : HumanBodyBones.LeftHand);
                var finger = a.GetBoneTransform(left ? HumanBodyBones.RightMiddleProximal : HumanBodyBones.LeftMiddleProximal);
                var palm = Vector3.Lerp(support.position, finger.position, 0.5f);
                Assert.That(Vector3.Distance(palm, shoulder.position), Is.LessThan(0.12f), name + " " + region);
                var elbow = a.GetBoneTransform(left ? HumanBodyBones.RightLowerArm : HumanBodyBones.LeftLowerArm);
                Assert.That(root.transform.InverseTransformPoint(elbow.position).z, Is.GreaterThan(0.10f), "Support elbow must clear the chest");
                Assert.That(support.position.y, Is.GreaterThan(1.35f));
                Assert.That(Quaternion.Angle(chest.rotation, start), Is.GreaterThan(15f));
                for (var i = 0; i < feet.Length; i++) Assert.That(Vector3.Distance(feet[i].position, planted[i]), Is.LessThan(0.002f));
                Assert.That(Mathf.Abs(Floor(root)), Is.LessThan(0.006f));
                player.Recovery(region).SampleAnimation(root, player.Recovery(region).length);
                Assert.That(Quaternion.Angle(chest.rotation, start), Is.LessThan(0.01f));
            }
            Object.DestroyImmediate(root);
        }
    }

    // Verifies a real left-shoulder hit uses its separate automatic sequence and preserves impact identity.
    [UnityTest]
    public IEnumerator LeftShoulderUsesItsOwnRecoveryAndImpactIdentity()
    {
        var flow = BodyShot(out var target, out var arrow, BodyRegion.LeftShoulder);
        yield return new WaitForSeconds(1.1f);
        Assert.That(flow.Phase, Is.EqualTo(DuelPhase.Holding));
        Assert.That(arrow.transform.parent.GetComponent<BodyHitRegion>().Region, Is.EqualTo(BodyRegion.LeftShoulder));
        yield return new WaitForSeconds(4f);
        Assert.That(flow.CanShoot(1), Is.True);
        Assert.That(target.CurrentHealth, Is.EqualTo(75));
    }

    // Keeps a right-shoulder impact distinct through its own supported hold and complete recovery.
    [UnityTest]
    public IEnumerator RightShoulderUsesItsOwnRecoveryAndImpactIdentity()
    {
        var flow = BodyShot(out var target, out var arrow, BodyRegion.RightShoulder);
        yield return new WaitForSeconds(1.1f);
        Assert.That(flow.Phase, Is.EqualTo(DuelPhase.Holding));
        Assert.That(arrow.transform.parent.GetComponent<BodyHitRegion>().Region, Is.EqualTo(BodyRegion.RightShoulder));
        yield return new WaitForSeconds(4f);
        Assert.That(flow.CanShoot(1), Is.True);
        Assert.That(target.CurrentHealth, Is.EqualTo(75));
    }

    // Checks the injured open hand is raised and supported at its wrist on both character skeletons.
    [Test]
    public void HandClipsOpenTheCorrectFingersAndSupportItsWrist()
    {
        foreach (var name in new[] { "EnglishArcher", "ErikaArcher" })
        {
            var root = Character(name);
            var player = root.GetComponent<ArcherReactionAnimation>();
            Assert.That(player.Supports(BodyRegion.LeftHand), Is.True);
            var a = root.GetComponentInChildren<Animator>();
            foreach (var region in new[] { BodyRegion.LeftHand, BodyRegion.RightHand }.Where(player.Supports))
            {
                var left = region == BodyRegion.LeftHand;
                var feet = new[] { a.GetBoneTransform(HumanBodyBones.LeftFoot), a.GetBoneTransform(HumanBodyBones.RightFoot) };
                var planted = feet.Select(t => t.position).ToArray();
                player.Entry(region).SampleAnimation(root, player.Entry(region).length);
                var wrist = a.GetBoneTransform(left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
                var support = a.GetBoneTransform(left ? HumanBodyBones.RightHand : HumanBodyBones.LeftHand);
                var fingertip = a.GetBoneTransform(left ? HumanBodyBones.LeftMiddleDistal : HumanBodyBones.RightMiddleDistal);
                Assert.That(Vector3.Distance(support.position, wrist.position), Is.LessThan(0.09f), name + " " + region);
                Assert.That(Vector3.Distance(fingertip.position, wrist.position), Is.GreaterThan(0.12f));
                Assert.That(fingertip.position.y - wrist.position.y, Is.GreaterThan(0.12f));
                for (var i = 0; i < feet.Length; i++) Assert.That(Vector3.Distance(feet[i].position, planted[i]), Is.LessThan(0.002f));
                Assert.That(Mathf.Abs(Floor(root)), Is.LessThan(0.006f));
                player.Recovery(region).SampleAnimation(root, player.Recovery(region).length);
            }
            Object.DestroyImmediate(root);
        }
    }

    // Verifies left-hand hit identity, attached arrow and automatic completion without borrowing the arm sequence.
    [UnityTest]
    public IEnumerator LeftHandUsesItsOwnSupportedRecovery()
    {
        var flow = BodyShot(out var target, out var arrow, BodyRegion.LeftHand);
        yield return new WaitForSeconds(1.1f);
        Assert.That(flow.Phase, Is.EqualTo(DuelPhase.Holding));
        Assert.That(arrow.transform.parent.GetComponent<BodyHitRegion>().Region, Is.EqualTo(BodyRegion.LeftHand));
        yield return new WaitForSeconds(4f);
        Assert.That(flow.CanShoot(1), Is.True);
        Assert.That(target.CurrentHealth, Is.EqualTo(75));
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
        yield return new WaitForSeconds(4f);
        Assert.That(flow.Phase, Is.EqualTo(DuelPhase.Recovering));
        Assert.That(flow.CanShoot(1), Is.False);
        yield return new WaitForSeconds(1.5f);
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
        var stopped = head.position;
        yield return new WaitForSeconds(1f);
        Assert.That(flow.Phase, Is.EqualTo(DuelPhase.Finished));
        Assert.That(flow.CanShoot(1), Is.False);
        Assert.That(Vector3.Distance(head.position, stopped), Is.LessThan(0.001f));
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
            for (var frame = 0; frame <= 30; frame++)
            {
                clip.SampleAnimation(root, clip.length * frame / 30f);
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
    public IEnumerator FatalNeckReachFollowsTheActualImpactOnEitherSide()
    {
        foreach (var offset in new[] { -0.035f, 0.035f })
        {
            var flow = BodyShot(out var target, out var arrow, BodyRegion.Neck);
            arrow.transform.position += Vector3.right * offset;
            var results = 0;
            var recoveries = 0;
            flow.MatchFinished += _ => results++;
            flow.RecoveryRequested += (_, __) => recoveries++;
            yield return new WaitForSeconds(0.65f);
            Assert.That(target.CurrentHealth, Is.Zero);
            Assert.That(flow.Phase, Is.EqualTo(DuelPhase.Finished));
            Assert.That(arrow.transform.parent.GetComponent<BodyHitRegion>().Region, Is.EqualTo(BodyRegion.Neck));
            var a = target.GetComponentInChildren<Animator>();
            var neck = a.GetBoneTransform(HumanBodyBones.Neck);
            var wrist = a.GetBoneTransform(HumanBodyBones.RightHand);
            var expected = arrow.transform.position - neck.up * 0.045f - arrow.transform.forward * 0.025f;
            Assert.That(Vector3.Distance(wrist.position, expected), Is.LessThan(0.008f), "Actual wound side " + offset);
            yield return new WaitForSeconds(2f);
            Assert.That(a.GetBoneTransform(HumanBodyBones.Head).position.y - target.transform.position.y, Is.LessThan(0.35f));
            Assert.That(flow.CanShoot(1), Is.False);
            Assert.That(results, Is.EqualTo(1));
            Assert.That(recoveries, Is.Zero);
            yield return CleanUp();
        }
    }

    // Measures visible contact near a supported joint rather than treating its bone origin as the skin surface.
    private static float ContactFloor(GameObject root, Vector3 joint)
    {
        var floor = float.PositiveInfinity;
        var mesh = new Mesh();
        try
        {
            foreach (var skin in root.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                skin.BakeMesh(mesh, true);
                foreach (var vertex in mesh.vertices)
                {
                    var point = skin.transform.TransformPoint(vertex);
                    if (Vector3.Distance(point, joint) < 0.18f) floor = Mathf.Min(floor, point.y);
                }
                mesh.Clear();
            }
            return floor;
        }
        finally { Object.DestroyImmediate(mesh); }
    }

    // Requires the injured knee and same-side glove on the ground while the healthy foot remains planted.
    [Test]
    public void LegClipsSupportTheCorrectKneeAndHandOnBothModels()
    {
        foreach (var name in new[] { "EnglishArcher", "ErikaArcher" })
        {
            var root = Character(name);
            var actor = root.GetComponent<ArcherReactionAnimation>();
            var a = root.GetComponentInChildren<Animator>();
            Assert.That(actor.Supports(BodyRegion.LeftLeg), Is.True);
            foreach (var region in new[] { BodyRegion.LeftLeg, BodyRegion.RightLeg }.Where(actor.Supports))
            {
                var left = region == BodyRegion.LeftLeg;
                var support = a.GetBoneTransform(left ? HumanBodyBones.RightFoot : HumanBodyBones.LeftFoot);
                var knee = a.GetBoneTransform(left ? HumanBodyBones.LeftLowerLeg : HumanBodyBones.RightLowerLeg);
                var otherKnee = a.GetBoneTransform(left ? HumanBodyBones.RightLowerLeg : HumanBodyBones.LeftLowerLeg);
                var hand = a.GetBoneTransform(left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
                var head = a.GetBoneTransform(HumanBodyBones.Head);
                var standingHead = head.position;
                var planted = support.position;
                var entry = actor.Entry(region);
                for (var frame = 0; frame <= 81; frame++)
                {
                    entry.SampleAnimation(root, entry.length * frame / 81f);
                    Assert.That(Vector3.Distance(support.position, planted), Is.LessThan(0.002f), name + " " + region);
                    Assert.That(Floor(root), Is.GreaterThan(-0.006f), name + " " + region + " frame " + frame);
                }
                Assert.That(knee.position.y, Is.LessThan(otherKnee.position.y - 0.12f));
                Assert.That(otherKnee.position.y, Is.GreaterThan(0.18f));
                Assert.That(hand.position.y, Is.LessThan(0.1f));
                Assert.That(ContactFloor(root, hand.position), Is.InRange(-0.006f, 0.01f));
                Assert.That(ContactFloor(root, knee.position), Is.LessThan(0.03f));
                var restingHand = hand.position;
                var recovery = actor.Recovery(region);
                recovery.SampleAnimation(root, 0f);
                Assert.That(Vector3.Distance(hand.position, restingHand), Is.LessThan(0.001f));
                for (var frame = 0; frame <= 81; frame++)
                {
                    recovery.SampleAnimation(root, recovery.length * frame / 81f);
                    Assert.That(Vector3.Distance(support.position, planted), Is.LessThan(0.002f));
                    Assert.That(Floor(root), Is.GreaterThan(-0.006f));
                }
                Assert.That(Vector3.Distance(head.position, standingHead), Is.LessThan(0.001f));
            }
            Object.DestroyImmediate(root);
        }
    }

    // Uses a real left-leg impact through kneeling, the full hold and the complete standing recovery.
    [UnityTest]
    public IEnumerator LeftLegHitWaitsForItsSupportedRecovery()
    {
        var flow = BodyShot(out var target, out var arrow, BodyRegion.LeftLeg);
        yield return new WaitForSeconds(1.1f);
        Assert.That(flow.Phase, Is.EqualTo(DuelPhase.Holding));
        Assert.That(flow.CanShoot(1), Is.False);
        Assert.That(arrow.transform.parent.GetComponent<BodyHitRegion>().Region, Is.EqualTo(BodyRegion.LeftLeg));
        yield return new WaitForSeconds(4f);
        Assert.That(flow.CanShoot(1), Is.True);
        Assert.That(target.CurrentHealth, Is.EqualTo(75));
    }

    // Keeps a right-leg impact distinct through its own same-side supported kneeling and standing recovery.
    [UnityTest]
    public IEnumerator RightLegHitUsesItsOwnSupportedRecovery()
    {
        var flow = BodyShot(out var target, out var arrow, BodyRegion.RightLeg);
        yield return new WaitForSeconds(1.1f);
        Assert.That(flow.Phase, Is.EqualTo(DuelPhase.Holding));
        Assert.That(flow.CanShoot(1), Is.False);
        Assert.That(arrow.transform.parent.GetComponent<BodyHitRegion>().Region, Is.EqualTo(BodyRegion.RightLeg));
        yield return new WaitForSeconds(4f);
        Assert.That(flow.CanShoot(1), Is.True);
        Assert.That(target.CurrentHealth, Is.EqualTo(75));
    }

    // Verifies a right-hand hit uses its own wrist-support motion and only grants the turn after full recovery.
    [UnityTest]
    public IEnumerator RightHandUsesItsOwnSupportedRecovery()
    {
        var flow = BodyShot(out var target, out var arrow, BodyRegion.RightHand);
        yield return new WaitForSeconds(1.1f);
        Assert.That(flow.Phase, Is.EqualTo(DuelPhase.Holding));
        Assert.That(arrow.transform.parent.GetComponent<BodyHitRegion>().Region, Is.EqualTo(BodyRegion.RightHand));
        yield return new WaitForSeconds(4f);
        Assert.That(flow.CanShoot(1), Is.True);
        Assert.That(target.CurrentHealth, Is.EqualTo(75));
    }
}
#endif
