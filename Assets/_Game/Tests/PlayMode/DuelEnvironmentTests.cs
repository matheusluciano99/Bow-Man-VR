#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using BowMan;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public sealed class DuelEnvironmentTests
{
    // Loads every setting at the gameplay spawns and checks ground support, lane clearance and sky cleanup.
    [UnityTest]
    public IEnumerator EverySettingLeavesBothSeatsAndTheFiringLaneClear()
    {
        var scene = SceneManager.CreateScene("Environment test", new CreateSceneParameters(LocalPhysicsMode.Physics3D));
        var physics = scene.GetPhysicsScene();
        var root = new GameObject("Round");
        SceneManager.MoveGameObjectToScene(root, scene);
        var first = new GameObject("First spawn").transform;
        var second = new GameObject("Second spawn").transform;
        first.SetParent(root.transform); second.SetParent(root.transform);
        first.localPosition = Vector3.back * 10f; second.localPosition = Vector3.forward * 10f;
        var skies = new HashSet<Material>();
        var previousSky = RenderSettings.skybox;
        foreach (ArenaSetting setting in System.Enum.GetValues(typeof(ArenaSetting)))
        {
            DuelEnvironment.Place(setting, root.transform, first, second);
            var arena = root.GetComponentInChildren<DuelEnvironment>();
            Assert.That(arena, Is.Not.Null);
            Assert.That(arena.GetComponentsInChildren<Renderer>().Length, Is.GreaterThan(4));
            Assert.That(RenderSettings.skybox.mainTexture, Is.Not.Null);
            Assert.That(skies.Add(RenderSettings.skybox), Is.True);
            physics.Simulate(Time.fixedDeltaTime);
            foreach (var seat in new[] { first, second })
            {
                Assert.That(physics.Raycast(seat.position + Vector3.up, Vector3.down, out var floor, 1.1f), Is.True);
                Assert.That(floor.point.y, Is.EqualTo(0f).Within(.025f));
                var direction = seat == first ? Vector3.forward : Vector3.back;
                foreach (var height in new[] { .1f, .8f, 1.6f })
                    Assert.That(physics.Raycast(seat.position + Vector3.up * height, direction, 20f), Is.False);
            }
            Object.Destroy(arena.gameObject);
            yield return null;
            Assert.That(RenderSettings.skybox, Is.SameAs(previousSky));
        }
        yield return SceneManager.UnloadSceneAsync(scene);
    }
}
#endif
