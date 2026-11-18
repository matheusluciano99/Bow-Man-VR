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

public class ArrowWoundBloodTests
{
    private readonly List<GameObject> objects = new List<GameObject>();
    private float timeScale;

    // Speeds up finite emission checks while preserving the application's original time scale.
    [SetUp]
    public void SetUp() { timeScale = Time.timeScale; Time.timeScale = 4f; }

    // Removes test-owned characters/arrows and restores time even after a failed impact assertion.
    [UnityTearDown]
    public IEnumerator CleanUp()
    {
        Time.timeScale = timeScale;
        foreach (var obj in objects) if (obj != null) Object.Destroy(obj);
        objects.Clear();
        yield return null;
    }

    // Launches the actual configured gameplay arrow and tracks it for cleanup.
    private ArrowFlight Arrow(Vector3 start, Vector3 velocity)
    {
        var arrow = Object.Instantiate(AssetDatabase.LoadAssetAtPath<ArrowFlight>("Assets/_Game/Prefabs/TestArrow.prefab"),
            start, Quaternion.identity);
        objects.Add(arrow.gameObject);
        arrow.Launch(velocity);
        return arrow;
    }

    // Verifies exact entry anchoring, outward spray, bone-following and bounded cleanup on both models and hand sides.
    [UnityTest]
    public IEnumerator RealWoundsFollowTheHitBoneThenFinishWithoutRemovingTheArrow()
    {
        foreach (var name in new[] { "EnglishArcher", "ErikaArcher" })
        foreach (var region in new[] { BodyRegion.LeftHand, BodyRegion.RightHand })
        {
            var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
                $"Assets/_Game/Prefabs/Characters/{name}.prefab"), new Vector3(1000f, 0f, 1000f), Quaternion.identity);
            objects.Add(root);
            var volume = root.GetComponentsInChildren<BodyHitRegion>().First(v => v.Region == region);
            foreach (var collider in root.GetComponentsInChildren<Collider>())
                collider.enabled = collider.gameObject == volume.gameObject;
            Physics.SyncTransforms();
            var point = volume.GetComponent<Collider>().bounds.center;
            var arrow = Arrow(point + Vector3.forward * 0.35f, Vector3.back * 20f);
            BodyImpact hit = default;
            var impacts = 0;
            arrow.BodyHit += value => { hit = value; impacts++; };
            var deadline = Time.time + 1f;
            while (impacts == 0 && Time.time < deadline) yield return null;
            Assert.That(impacts, Is.EqualTo(1), name + " " + region);
            var wound = volume.GetComponentInChildren<ParticleSystem>();
            Assert.That(wound, Is.Not.Null);
            Assert.That(Vector3.Distance(wound.transform.position, hit.Point), Is.LessThan(0.001f));
            Assert.That(Vector3.Dot(wound.transform.forward, hit.Normal), Is.GreaterThan(0.999f));
            Assert.That(wound.isPlaying, Is.True);
            Assert.That(wound.main.simulationSpace, Is.EqualTo(ParticleSystemSimulationSpace.World));
            yield return new WaitForSeconds(0.05f);
            Assert.That(wound.particleCount, Is.GreaterThan(0), "Blood must escape the body's collision surface");
            var localEntry = volume.transform.InverseTransformPoint(hit.Point);
            var bone = volume.transform.parent;
            var previousPosition = wound.transform.position;
            bone.Rotate(Vector3.right, 35f, Space.Self);
            Assert.That(Vector3.Distance(wound.transform.position, previousPosition), Is.GreaterThan(0.005f));
            Assert.That(Vector3.Distance(wound.transform.position, volume.transform.TransformPoint(localEntry)), Is.LessThan(0.001f));
            Assert.That(arrow.transform.parent, Is.SameAs(volume.transform));
            yield return new WaitForSeconds(3f);
            yield return null;
            Assert.That(wound == null, Is.True, "Finished blood must remove its emitter");
            Assert.That(arrow != null, Is.True, "Blood cleanup must retain the embedded arrow");
            Assert.That(root != null, Is.True);
            Object.Destroy(root);
            yield return null;
        }
    }

    // A real scenery impact cannot create a wound emitter or retain body-only blood work.
    [UnityTest]
    public IEnumerator SceneryImpactDoesNotEmitBlood()
    {
        var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.transform.position = new Vector3(1100f, 1f, 1100f);
        objects.Add(wall);
        var arrow = Arrow(wall.transform.position + Vector3.forward, Vector3.back * 20f);
        var hit = false;
        arrow.Hit += _ => hit = true;
        var deadline = Time.time + 1f;
        while (!hit && Time.time < deadline) yield return null;
        Assert.That(hit, Is.True);
        Assert.That(wall.GetComponentsInChildren<ParticleSystem>(), Is.Empty);
        Assert.That(arrow.GetComponentsInChildren<ParticleSystem>(), Is.Empty);
        Assert.That(Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None), Is.Empty);
    }
}
#endif
