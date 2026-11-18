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
}
#endif
