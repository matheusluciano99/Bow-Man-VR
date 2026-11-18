using System;
using System.Collections;
using System.Collections.Generic;
using BowMan;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public class DuelTurnFlowTests
{
    private readonly List<GameObject> objects = new List<GameObject>();
    private DuelTurnFlow flow;
    private ArcherHealth first;
    private ArcherHealth second;
    private static readonly Vector3 Origin = new Vector3(1000f, 1000f, 1000f);

    // Creates isolated fixtures and tracks them for cleanup after each scenario.
    private GameObject Create(string name)
    {
        var obj = new GameObject(name);
        obj.transform.position = Origin;
        objects.Add(obj);
        return obj;
    }

    // Starts a fresh match with two living archers before each check.
    [SetUp]
    public void SetUp()
    {
        first = Create("First").AddComponent<ArcherHealth>();
        second = Create("Second").AddComponent<ArcherHealth>();
        flow = Create("Match").AddComponent<DuelTurnFlow>();
        flow.Initialize(first, second);
    }

    // Removes every match, arrow and target fixture after the scenario.
    [UnityTearDown]
    public IEnumerator CleanUp()
    {
        foreach (var obj in objects) if (obj != null) Object.Destroy(obj);
        objects.Clear();
        yield return null;
    }

    // Places a target one physics step away and fires a real arrow registered with the active match.
    private ArrowFlight Shoot(BodyRegion? region)
    {
        var target = Create("Impact volume");
        target.transform.position += Vector3.forward;
        target.AddComponent<BoxCollider>().size = new Vector3(0.3f, 0.3f, 0.1f);
        if (region.HasValue)
        {
            target.transform.SetParent(second.transform, true);
            target.AddComponent<BodyHitRegion>().Configure(region.Value);
        }
        var arrow = Create("Arrow").AddComponent<ArrowFlight>();
        arrow.Launch(Vector3.forward * 100f, first.transform);
        Assert.That(flow.TryTrackShot(0, arrow), Is.True);
        return arrow;
    }

    // Keeps shooting locked through entry, the full three-second hold, and unfinished recovery.
    [UnityTest]
    public IEnumerator SurvivingHitWaitsForBothAnimationCallbacksAndHandsOverOnce()
    {
        Shoot(BodyRegion.Chest);
        yield return new WaitForFixedUpdate();
        var token = flow.ShotId;
        Assert.That(second.CurrentHealth, Is.EqualTo(75));
        Assert.That(flow.Phase, Is.EqualTo(DuelPhase.Reacting));
        Assert.That(flow.CanShoot(0) || flow.CanShoot(1), Is.False);
        flow.CompleteRecovery(token);
        flow.MarkInjuredPose(token - 1);
        Assert.That(flow.Phase, Is.EqualTo(DuelPhase.Reacting));
        flow.MarkInjuredPose(token);
        yield return new WaitForSeconds(2.8f);
        Assert.That(flow.Phase, Is.EqualTo(DuelPhase.Holding));
        yield return new WaitForSeconds(0.3f);
        Assert.That(flow.Phase, Is.EqualTo(DuelPhase.Recovering));
        Assert.That(flow.CanShoot(1), Is.False);
        flow.CompleteRecovery(token);
        flow.CompleteRecovery(token);
        Assert.That(flow.ActiveArcher, Is.EqualTo(1));
        Assert.That(flow.CanShoot(1), Is.True);
    }

    // Repeats the original shooter after the target finishes its full injury recovery.
    [UnityTest]
    public IEnumerator AnimationPracticeKeepsTheFirstShooterAfterRecovery()
    {
        flow.Initialize(first, second, alternateShooters: false);
        var recoveringArcher = -1;
        flow.RecoveryRequested += (target, _) => recoveringArcher = target;
        Shoot(BodyRegion.Chest);
        yield return new WaitForFixedUpdate();
        flow.MarkInjuredPose(flow.ShotId);
        yield return new WaitForSeconds(3.1f);
        Assert.That(recoveringArcher, Is.EqualTo(1));
        Assert.That(flow.CanShoot(0), Is.False);
        flow.CompleteRecovery(flow.ShotId);
        Assert.That(flow.CanShoot(0), Is.True);
        Assert.That(flow.CanShoot(1), Is.False);
        Assert.That(second.CurrentHealth, Is.EqualTo(75));
    }

    // Verifies first scenery contact hands over once and later arrow destruction cannot change it again.
    [UnityTest]
    public IEnumerator SceneryMissHandsOverOnce()
    {
        var arrow = Shoot(null);
        Assert.That(flow.CanShoot(0) || flow.CanShoot(1), Is.False);
        yield return new WaitForFixedUpdate();
        Assert.That(flow.ActiveArcher, Is.EqualTo(1));
        Assert.That(flow.Phase, Is.EqualTo(DuelPhase.Aiming));
        Object.Destroy(arrow.gameObject);
        yield return null;
        Assert.That(flow.ActiveArcher, Is.EqualTo(1));
        Assert.That(second.CurrentHealth, Is.EqualTo(100));
    }

    // Checks expiry passes the turn only after the arrow can no longer strike a target.
    [UnityTest]
    public IEnumerator FlightExpiryHandsOver()
    {
        var arrow = Create("Missed arrow").AddComponent<ArrowFlight>();
        arrow.Launch(Vector3.up * 20f);
        Assert.That(flow.TryTrackShot(0, arrow), Is.True);
        yield return new WaitForSeconds(7.8f);
        Assert.That(flow.Phase, Is.EqualTo(DuelPhase.Flying));
        yield return new WaitForSeconds(0.4f);
        Assert.That(flow.ActiveArcher, Is.EqualTo(1));
        Assert.That(flow.Phase, Is.EqualTo(DuelPhase.Aiming));
    }

    // Both head and neck are fatal at full health and cannot start surviving recovery.
    [UnityTest]
    public IEnumerator HeadAndNeckEndTheMatchImmediately()
    {
        var wins = 0;
        flow.MatchFinished += _ => wins++;
        Shoot(BodyRegion.Head);
        yield return new WaitForFixedUpdate();
        Assert.That(second.IsDefeated, Is.True);
        Assert.That(flow.Winner, Is.EqualTo(0));
        Assert.That(flow.Phase, Is.EqualTo(DuelPhase.Finished));
        flow.MarkInjuredPose(flow.ShotId);
        flow.CompleteRecovery(flow.ShotId);
        Assert.That(flow.CanShoot(0) || flow.CanShoot(1), Is.False);
        Assert.That(wins, Is.EqualTo(1));
        foreach(var region in second.GetComponentsInChildren<BodyHitRegion>()) Object.Destroy(region.gameObject);
        second = Create("Replacement second").AddComponent<ArcherHealth>();
        flow.Initialize(first, second);
        Shoot(BodyRegion.Neck);
        yield return new WaitForFixedUpdate();
        Assert.That(second.IsDefeated, Is.True);
        Assert.That(flow.Phase, Is.EqualTo(DuelPhase.Finished));
        Assert.That(wins, Is.EqualTo(2));
    }

    // Death during the hold cancels recovery and ignores late callbacks from that shot.
    [UnityTest]
    public IEnumerator DeathDuringRecoveryCancelsItsHandoff()
    {
        Shoot(BodyRegion.LeftLeg);
        yield return new WaitForFixedUpdate();
        var token = flow.ShotId;
        flow.MarkInjuredPose(token);
        second.TakeDamage(int.MaxValue);
        yield return new WaitForSeconds(3.1f);
        flow.CompleteRecovery(token);
        Assert.That(flow.Phase, Is.EqualTo(DuelPhase.Finished));
        Assert.That(flow.Winner, Is.EqualTo(0));
        Assert.That(flow.ActiveArcher, Is.Zero);
    }

    // Rejects invalid match actors, the wrong shooter, duplicate registrations and arrows not in flight.
    [Test]
    public void InvalidOrDuplicateShotsAreRejected()
    {
        Assert.Throws<ArgumentException>(() => flow.Initialize(first, first));
        var arrow = Create("Arrow").AddComponent<ArrowFlight>();
        Assert.That(flow.TryTrackShot(0, arrow), Is.False);
        arrow.Launch(Vector3.up);
        Assert.That(flow.TryTrackShot(1, arrow), Is.False);
        Assert.That(flow.TryTrackShot(0, arrow), Is.True);
        Assert.That(flow.TryTrackShot(0, arrow), Is.False);
    }
}
