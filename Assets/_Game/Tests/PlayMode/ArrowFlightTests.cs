using System;
using System.Collections;
using System.Collections.Generic;
using BowMan;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public class ArrowFlightTests
{
    private readonly List<GameObject> objects = new List<GameObject>();
    private static readonly Vector3 Origin = new Vector3(1000f, 1000f, 1000f);

    // Creates an object away from the arena and tracks it for cleanup after the test.
    private GameObject Create(string name)
    {
        var obj = new GameObject(name);
        objects.Add(obj);
        obj.transform.position = Origin;
        return obj;
    }

    // Places a thin solid target at the given forward distance from the test origin.
    private BoxCollider Target(float z)
    {
        var target = Create("Target");
        target.transform.position += Vector3.forward * z;
        var collider = target.AddComponent<BoxCollider>();
        collider.size = new Vector3(2f, 2f, 0.01f);
        return collider;
    }

    // Removes all test objects and waits one frame for Unity to finish destroying them.
    [UnityTearDown]
    public IEnumerator CleanUp()
    {
        foreach (var obj in objects) if (obj != null) Object.Destroy(obj);
        objects.Clear();
        yield return null;
    }

    // Checks that position follows a gravity-driven arc and the arrow faces its current velocity.
    [UnityTest]
    public IEnumerator FlightFollowsGravityAndFacesVelocity()
    {
        var arrow = Create("Arrow").AddComponent<ArrowFlight>();
        var initialVelocity = new Vector3(0f, 5f, 20f);
        arrow.Launch(initialVelocity);
        var startTime = Time.fixedTime;
        for (var i = 0; i < 10; i++) yield return new WaitForFixedUpdate();
        var elapsed = Time.fixedTime - startTime;
        var expected = Origin + initialVelocity * elapsed + Physics.gravity * (0.5f * elapsed * elapsed);
        Assert.That(Vector3.Distance(arrow.transform.position, expected), Is.LessThan(0.01f));
        Assert.That(Vector3.Dot(arrow.transform.forward,
            (initialVelocity + Physics.gravity * elapsed).normalized), Is.GreaterThan(0.999f));
    }

    // Checks that a fast arrow stops at the nearest thin target, reports once, and expires after impact.
    [UnityTest]
    public IEnumerator FastArrowHitsNearestThinColliderExactlyOnceAndCleansUp()
    {
        var near = Target(1f);
        Target(2f);
        Physics.SyncTransforms();
        var arrow = Create("Arrow").AddComponent<ArrowFlight>();
        var count = 0;
        RaycastHit impact = default;
        arrow.Hit += hit => { count++; impact = hit; };
        arrow.Launch(Vector3.forward * 1000f);
        yield return new WaitForFixedUpdate();
        Assert.That(count, Is.EqualTo(1));
        Assert.That(impact.collider, Is.EqualTo(near));
        Assert.That(impact.point.z, Is.EqualTo(Origin.z + 0.995f).Within(0.002f));
        var stoppedAt = arrow.transform.position;
        yield return new WaitForFixedUpdate();
        Assert.That(count, Is.EqualTo(1));
        Assert.That(arrow.transform.position, Is.EqualTo(stoppedAt));
        yield return new WaitForSeconds(3.1f);
        Assert.That(arrow == null, Is.True);
    }

    // Checks that the arrow passes through the shooter's colliders and triggers to hit a solid target.
    [UnityTest]
    public IEnumerator IgnoresShooterHierarchyAndTriggers()
    {
        var shooter = Create("Shooter");
        Target(0.5f).transform.SetParent(shooter.transform, true);
        Target(0.8f).isTrigger = true;
        var target = Target(1.2f);
        Physics.SyncTransforms();
        var arrow = Create("Arrow").AddComponent<ArrowFlight>();
        Collider actual = null;
        arrow.Hit += hit => actual = hit.collider;
        arrow.Launch(Vector3.forward * 100f, shooter.transform);
        yield return new WaitForFixedUpdate();
        Assert.That(actual, Is.EqualTo(target));
    }

    // Checks that an arrow missing every target expires without reporting an impact.
    [UnityTest]
    public IEnumerator MissedArrowExpiresWithoutReportingHit()
    {
        var arrow = Create("Arrow").AddComponent<ArrowFlight>();
        var count = 0;
        arrow.Hit += _ => count++;
        arrow.Launch(Vector3.up * 20f);
        yield return new WaitForSeconds(8.1f);
        Assert.That(arrow == null, Is.True);
        Assert.That(count, Is.Zero);
    }

    // Checks that zero or nonfinite launch velocities and a second launch are rejected.
    [Test]
    public void RejectsInvalidVelocityAndRelaunch()
    {
        var arrow = Create("Arrow").AddComponent<ArrowFlight>();
        Assert.Throws<ArgumentException>(() => arrow.Launch(Vector3.zero));
        Assert.Throws<ArgumentException>(() => arrow.Launch(new Vector3(float.NaN, 0f, 1f)));
        Assert.Throws<ArgumentException>(() => arrow.Launch(new Vector3(0f, float.PositiveInfinity, 1f)));
        arrow.Launch(Vector3.forward);
        Assert.Throws<InvalidOperationException>(() => arrow.Launch(Vector3.forward));
    }
}
