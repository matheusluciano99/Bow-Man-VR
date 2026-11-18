#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BowMan;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public class AnatomyHitTests
{
    private readonly List<GameObject> objects = new List<GameObject>();
    private ArrowFlight lastArrow;

    // Instantiates the actual fitted character in an isolated firing lane.
    private GameObject Character(string name, float yaw = 0f)
    {
        var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
            $"Assets/_Game/Prefabs/Characters/{name}.prefab"), new Vector3(1000f, 0f, 1000f), Quaternion.Euler(0f, yaw, 0f));
        objects.Add(root);
        return root;
    }

    // Removes all actors, body-owned arrows and effects after each independent anatomy scenario.
    [UnityTearDown]
    public IEnumerator CleanUp()
    {
        foreach (var obj in objects) if (obj != null) Object.Destroy(obj);
        objects.Clear();
        yield return null;
    }

    // Chooses a real front-facing tissue triangle near the centre of a substantial atomic surface.
    private static Vector3 AimPoint(AnatomyOrgan organ)
    {
        var mesh = organ.Surfaces.OrderByDescending(m => m.bounds.size.sqrMagnitude).First();
        var vertices = mesh.vertices;
        var triangles = mesh.triangles;
        var point = Vector3.zero;
        var score = float.NegativeInfinity;
        for (var i = 0; i < triangles.Length; i += 3)
        {
            var centre = (vertices[triangles[i]] + vertices[triangles[i + 1]] + vertices[triangles[i + 2]]) / 3f;
            var value = centre.z - Mathf.Abs(centre.x - mesh.bounds.center.x) * 0.25f -
                Mathf.Abs(centre.y - mesh.bounds.center.y) * 0.25f;
            if (value <= score) continue;
            point = centre;
            score = value;
        }
        return organ.transform.TransformPoint(point);
    }

    // Fires the configured gameplay arrow through the selected real tissue point at controlled penetration.
    private IEnumerator Shoot(GameObject root, Vector3 point, float penetration = 0.25f, DuelTurnFlow flow = null)
    {
        var arrow = Object.Instantiate(AssetDatabase.LoadAssetAtPath<ArrowFlight>("Assets/_Game/Prefabs/TestArrow.prefab"),
            point + root.transform.forward * 0.8f, Quaternion.identity);
        objects.Add(arrow.gameObject);
        lastArrow = arrow;
        typeof(ArrowFlight).GetField("bodyPenetration", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(arrow, penetration);
        arrow.Launch(-root.transform.forward * 1000f);
        if (flow != null) Assert.That(flow.TryTrackShot(0, arrow), Is.True);
        var deadline = Time.time + 1f;
        while (arrow.GetComponent<ArrowAnatomy>().LastImpact == null && Time.time < deadline) yield return null;
        Assert.That(arrow.GetComponent<ArrowAnatomy>().LastImpact, Is.Not.Null, "The shot must reach a fitted anatomical actor");
    }

    // Only the arrow that actually defeats an opponent requests the internal finishing presentation.
    [UnityTest]
    public IEnumerator FatalityIsRequestedOnlyForTheActualLethalArrow()
    {
        foreach (var region in new[] { BodyRegion.Chest, BodyRegion.Head, BodyRegion.Neck, BodyRegion.Abdomen })
        {
            var root = Character("EnglishArcher");
            var target = root.GetComponent<ArcherHealth>() ?? root.AddComponent<ArcherHealth>();
            if (region == BodyRegion.Abdomen) target.TakeDamage(75);
            var host = new GameObject("Fatality test match");
            objects.Add(host);
            var shooter = host.AddComponent<ArcherHealth>();
            var flow = host.AddComponent<DuelTurnFlow>();
            flow.Initialize(shooter, target);
            var requests = 0;
            AnatomicalImpact fatality = null;
            flow.FatalityRequested += (index, report) =>
            {
                Assert.That(index, Is.EqualTo(1));
                Assert.That(target.IsDefeated, Is.True);
                requests++;
                fatality = report;
            };
            var volume = root.GetComponentsInChildren<BodyHitRegion>().First(v => v.Region == region).GetComponent<Collider>();
            yield return Shoot(root, volume.bounds.center, flow: flow);
            var lethal = region != BodyRegion.Chest;
            Assert.That(target.IsDefeated, Is.EqualTo(lethal));
            Assert.That(requests, Is.EqualTo(lethal ? 1 : 0));
            if (lethal)
            {
                Assert.That(flow.Phase, Is.EqualTo(DuelPhase.Finished));
                Assert.That(fatality.Entry.Region, Is.EqualTo(region));
                Assert.That(fatality.Organs.Select(h => h.Organ.Kind),
                    Is.EquivalentTo(lastArrow.GetComponent<ArrowAnatomy>().LastImpact.Organs.Select(h => h.Organ.Kind)));
            }
            else
            {
                target.TakeDamage(int.MaxValue);
                Assert.That(requests, Is.Zero, "A later unrelated death must not replay a surviving arrow as a fatality");
            }
            yield return new WaitForFixedUpdate();
            Assert.That(requests, Is.EqualTo(lethal ? 1 : 0));
            Object.Destroy(root);
            Object.Destroy(host);
            yield return null;
        }
    }

    // Requires readable real tissue, correct paired laterality and no extra physics targets on both prepared prefabs.
    [Test]
    public void BothRigsContainTheCompleteFittedOrganInventory()
    {
        foreach (var name in new[] { "EnglishArcher", "ErikaArcher" })
        {
            var root = Character(name);
            var organs = root.GetComponentsInChildren<AnatomyOrgan>();
            Assert.That(root.GetComponent<ArcherAnatomy>(), Is.Not.Null);
            Assert.That(root.GetComponentsInChildren<BodyHitRegion>().Length, Is.EqualTo(19));
            Assert.That(organs.Select(o => (o.Kind, o.Side)).Distinct().Count(), Is.EqualTo(13));
            foreach (var organ in organs)
            {
                Assert.That(organ.Surfaces.Count, Is.GreaterThan(0));
                Assert.That(organ.Surfaces.All(m => m != null && m.isReadable && m.vertexCount > 0), Is.True);
                Assert.That(organ.GetComponentsInChildren<Collider>(), Is.Empty);
                if (organ.Side == BodySide.Center) continue;
                var centre = organ.Surfaces.Select(m => organ.transform.TransformPoint(m.bounds.center)).Aggregate(Vector3.zero,
                    (sum, point) => sum + point) / organ.Surfaces.Count;
                var x = root.transform.InverseTransformPoint(centre).x;
                Assert.That(organ.Side == BodySide.Left ? x < 0f : x > 0f, Is.True, name + " " + organ.DisplayName);
            }
            Object.DestroyImmediate(root);
        }
    }

    // Requires the central frontal cortex to sit behind the actual visible head skin rather than the face-inclusive hit box.
    [Test]
    public void FrontalBrainSurfaceStaysInsideTheVisibleHead()
    {
        foreach (var name in new[] { "EnglishArcher", "ErikaArcher" })
        {
            var root = Character(name);
            var eyes = root.GetComponentInChildren<Animator>().GetBoneTransform(HumanBodyBones.Head).Find("EyeAnchor");
            var brain = root.GetComponentsInChildren<AnatomyOrgan>().Single(o => o.Kind == OrganKind.Brain);
            var points = brain.Surfaces.SelectMany(m => m.vertices).Select(brain.transform.TransformPoint)
                .Where(p => Mathf.Abs(p.x - eyes.position.x) < 0.045f && p.y > eyes.position.y + 0.02f &&
                    p.y < eyes.position.y + 0.12f)
                .GroupBy(p => Mathf.FloorToInt((p.y - eyes.position.y) / 0.02f))
                .Select(g => g.OrderByDescending(p => p.z).First()).ToArray();
            Assert.That(points.Length, Is.GreaterThan(2));
            var skins = new List<(AnatomySurface Surface, Transform Frame)>();
            foreach (var skin in root.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                var mesh = new Mesh();
                try
                {
                    skin.BakeMesh(mesh, true);
                    mesh.RecalculateBounds();
                    skins.Add((new AnatomySurface(mesh), skin.transform));
                }
                finally { Object.DestroyImmediate(mesh); }
            }
            foreach (var point in points)
            {
                var start = point + Vector3.forward * 0.6f;
                Assert.That(skins.Any(s => s.Surface.TryHit(s.Frame.InverseTransformPoint(start),
                    s.Frame.InverseTransformPoint(point), out _)), Is.True, name + " frontal tissue at " + point);
            }
            Object.DestroyImmediate(root);
        }
    }

    // Resolves real shots through every enabled organ on both rigs, including a rotated archer and moving contacts.
    [UnityTest]
    public IEnumerator RealArrowPathsResolveEveryOrganAndKeepContactsOnAnimatedBones()
    {
        var profile = new List<string>();
        foreach (var name in new[] { "EnglishArcher", "ErikaArcher" })
        {
            var root = Character(name, name == "ErikaArcher" ? 37f : 0f);
            var anatomy = root.GetComponent<ArcherAnatomy>();
            var organs = root.GetComponentsInChildren<AnatomyOrgan>().GroupBy(o => (o.Kind, o.Side)).Select(g => g.First()).ToArray();
            OrganHit moving = default;
            foreach (var organ in organs)
            {
                yield return Shoot(root, AimPoint(organ));
                var report = lastArrow.GetComponent<ArrowAnatomy>().LastImpact;
                Assert.That(report.Organs.Any(h => h.Organ.Kind == organ.Kind && h.Organ.Side == organ.Side), Is.True,
                    name + " expected " + organ.DisplayName + ", found " + string.Join(", ", report.Organs.Select(h => h.Organ.DisplayName)));
                Assert.That(report.Entry.Region, Is.EqualTo(report.Entry.Volume.Region));
                Assert.That(report.Entry.Side, Is.EqualTo(report.Entry.Volume.Side));
                Assert.That(report.Organs.Select(h => (h.Organ.Kind, h.Organ.Side)).Distinct().Count(), Is.EqualTo(report.Organs.Count));
                foreach (var hit in report.Organs)
                {
                    var along = Vector3.Dot(hit.Point - report.Entry.Point, report.Entry.Direction);
                    Assert.That(along, Is.InRange(-0.001f, report.Entry.PenetrationDepth + 0.001f));
                    Assert.That(Vector3.Distance(hit.Point, report.Entry.Point + report.Entry.Direction * along), Is.LessThan(0.001f));
                }
                if (organ.Kind == OrganKind.Brain) moving = report.Organs.First(h => h.Organ.Kind == OrganKind.Brain);
                var watch = System.Diagnostics.Stopwatch.StartNew();
                anatomy.Trace(report.Entry);
                profile.Add(name + " " + organ.DisplayName + " " + watch.Elapsed.TotalMilliseconds.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) + " ms");
            }
            var local = moving.LocalPoint;
            var standing = moving.Point;
            var reaction = root.GetComponent<ArcherReactionAnimation>();
            reaction.Entry(BodyRegion.Head).SampleAnimation(root, reaction.Entry(BodyRegion.Head).length);
            Assert.That(moving.LocalPoint, Is.EqualTo(local));
            Assert.That(Vector3.Distance(moving.Point, standing), Is.GreaterThan(0.2f));
            Object.Destroy(root);
            yield return null;
        }
        System.IO.Directory.CreateDirectory("Artifacts/AnatomyReview");
        System.IO.File.WriteAllLines("Artifacts/AnatomyReview/query-times.txt", profile);
    }

    // Rejects an actual soft-tissue edge hit and a path that ends before the registered brain surface.
    [UnityTest]
    public IEnumerator SoftTissueAndShortPenetrationDoNotInventOrganDamage()
    {
        foreach (var name in new[] { "EnglishArcher", "ErikaArcher" })
        {
            var root = Character(name);
            var chest = root.GetComponentsInChildren<BodyHitRegion>().First(v => v.Region == BodyRegion.Chest).GetComponent<BoxCollider>();
            var point = chest.transform.TransformPoint(chest.center + new Vector3(chest.size.x * 0.5f - 0.005f, 0f, chest.size.z * 0.5f));
            yield return Shoot(root, point);
            Assert.That(lastArrow.GetComponent<ArrowAnatomy>().LastImpact.Entry.Region, Is.EqualTo(BodyRegion.Chest));
            Assert.That(lastArrow.GetComponent<ArrowAnatomy>().LastImpact.Organs, Is.Empty);
            var brain = root.GetComponentsInChildren<AnatomyOrgan>().Single(o => o.Kind == OrganKind.Brain);
            yield return Shoot(root, AimPoint(brain), 0.001f);
            Assert.That(lastArrow.GetComponent<ArrowAnatomy>().LastImpact.Entry.Region, Is.EqualTo(BodyRegion.Head));
            Assert.That(lastArrow.GetComponent<ArrowAnatomy>().LastImpact.Organs, Is.Empty);
            Object.Destroy(root);
            yield return null;
        }
    }
}
#endif
