using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using BowMan;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public class AIArcherTests
{
    private readonly List<GameObject> objects = new List<GameObject>();
    private UnityEngine.Random.State randomState;

    // Tracks isolated objects for deterministic cleanup without altering scene actors.
    private GameObject Create(string name, Vector3 position)
    {
        var obj = new GameObject(name);
        obj.transform.position = position;
        objects.Add(obj);
        return obj;
    }

    // Retains the application's random state before the deterministic match scenario.
    [SetUp]
    public void SetUp() => randomState = UnityEngine.Random.state;

    // Removes actors/arrows and restores the application's random state after each test.
    [UnityTearDown]
    public IEnumerator CleanUp()
    {
        foreach (var obj in objects) if (obj != null) Object.Destroy(obj);
        UnityEngine.Random.state = randomState;
        objects.Clear();
        yield return null;
    }

    // Checks solved trajectories hit their target at the requested speed under normal and zero gravity.
    [Test]
    public void BallisticSolutionRespectsSpeedAndRejectsInvalidTargets()
    {
        var gravity = Physics.gravity;
        try
        {
            foreach (var g in new[] { gravity, Vector3.zero })
            {
                Physics.gravity = g;
                var target = new Vector3(2f, 1f, 20f);
                Assert.That(ArrowBallistics.TryVelocity(Vector3.zero, target, 25f, out var velocity), Is.True);
                Assert.That(velocity.magnitude, Is.EqualTo(25f).Within(0.001f));
                var time = target.z / velocity.z;
                Assert.That(Vector3.Distance(velocity * time + g * (0.5f * time * time), target), Is.LessThan(0.001f));
            }
            Physics.gravity = gravity;
            Assert.That(ArrowBallistics.TryVelocity(Vector3.zero, Vector3.forward * 1000f, 1f, out _), Is.False);
            Assert.That(ArrowBallistics.TryVelocity(Vector3.zero, Vector3.zero, 25f, out _), Is.False);
            Assert.That(ArrowBallistics.TryVelocity(Vector3.zero, Vector3.one, float.NaN, out _), Is.False);
            Assert.That(ArrowBallistics.TryVelocity(Vector3.zero, Vector3.one, float.PositiveInfinity, out _), Is.False);
        }
        finally { Physics.gravity = gravity; }
    }

    // Runs two imperfect bots through real arrows and legal turns, with explicit test-host animation callbacks.
    [UnityTest]
    public IEnumerator BotsFinishAMatchWithoutShootingDuringRecoveryOrAfterDefeat()
    {
        UnityEngine.Random.InitState(9123);
        var origin = new Vector3(1000f, 1000f, 1000f);
        var first = Create("First bot", origin).AddComponent<ArcherHealth>();
        var second = Create("Second bot", origin + Vector3.forward * 4f).AddComponent<ArcherHealth>();
        second.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
        foreach (var actor in new[] { first, second })
        {
            var volume = Create("Chest", actor.transform.position + Vector3.up * 1.3f);
            volume.transform.SetParent(actor.transform, true);
            volume.AddComponent<BoxCollider>().size = new Vector3(0.8f, 0.6f, 0.2f);
            volume.AddComponent<BodyHitRegion>().Configure(BodyRegion.Chest);
        }
        var flow = Create("Duel", origin).AddComponent<DuelTurnFlow>();
        flow.Initialize(first, second);
        flow.ReactionRequested += (_, _, lethal, token) => { if (!lethal) flow.MarkInjuredPose(token); };
        flow.RecoveryRequested += (_, token) => flow.CompleteRecovery(token);
        var prefab = Create("Arrow prefab", origin + Vector3.up * 100f).AddComponent<ArrowFlight>();
        var firstAI = first.gameObject.AddComponent<AIArcher>();
        var secondAI = second.gameObject.AddComponent<AIArcher>();
        foreach (var ai in new[] { firstAI, secondAI })
        {
            typeof(AIArcher).GetField("thinkDelay", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(ai, 0.01f);
            typeof(AIArcher).GetField("aimError", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(ai, 0.05f);
        }
        firstAI.Initialize(flow, 0, first, second, prefab);
        secondAI.Initialize(flow, 1, second, first, prefab);
        var deadline = Time.time + 40f;
        while (flow.Phase != DuelPhase.Finished && Time.time < deadline)
        {
            var count = firstAI.ShotsFired + secondAI.ShotsFired;
            if (flow.Phase == DuelPhase.Holding)
            {
                var token = flow.ShotId;
                yield return new WaitForSeconds(0.1f);
                if (flow.Phase == DuelPhase.Holding && flow.ShotId == token)
                    Assert.That(firstAI.ShotsFired + secondAI.ShotsFired, Is.EqualTo(count));
            }
            yield return null;
        }
        Assert.That(flow.Phase, Is.EqualTo(DuelPhase.Finished));
        Assert.That(firstAI.ShotsFired, Is.EqualTo(4));
        Assert.That(secondAI.ShotsFired, Is.EqualTo(3));
        var finishedCount = firstAI.ShotsFired + secondAI.ShotsFired;
        yield return new WaitForSeconds(0.2f);
        Assert.That(firstAI.ShotsFired + secondAI.ShotsFired, Is.EqualTo(finishedCount));
        Assert.That(flow.Winner, Is.Zero);
    }
}
