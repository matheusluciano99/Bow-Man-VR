using System;
using System.Collections;
using BowMan;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public class ArcherHealthTests
{
    private ArcherHealth health;

    // Creates a fresh archer health component before each test.
    [SetUp]
    public void SetUp()
    {
        health = new GameObject("Test archer").AddComponent<ArcherHealth>();
    }

    // Removes the test archer and waits for Unity to finish destroying it.
    [UnityTearDown]
    public IEnumerator CleanUp()
    {
        Object.Destroy(health.gameObject);
        yield return null;
    }

    // Checks that an archer starts alive at full health and survives nonlethal damage.
    [Test]
    public void NonlethalDamageReducesHealthWithoutDefeat()
    {
        Assert.That(health.CurrentHealth, Is.EqualTo(health.MaxHealth));
        Assert.That(health.IsDefeated, Is.False);
        var defeats = 0;
        health.Defeated += () => defeats++;

        health.TakeDamage(25);

        Assert.That(health.CurrentHealth, Is.EqualTo(health.MaxHealth - 25));
        Assert.That(health.IsDefeated, Is.False);
        Assert.That(defeats, Is.Zero);
    }

    // Checks that reaching zero announces defeat once, including when a listener attempts more damage.
    [Test]
    public void LethalDamageTriggersDefeatOnceAndRemainsDefeated()
    {
        var defeats = 0;
        health.Defeated += () =>
        {
            defeats++;
            Assert.That(health.CurrentHealth, Is.Zero);
            Assert.That(health.IsDefeated, Is.True);
            if (defeats == 1) health.TakeDamage(1);
        };

        health.TakeDamage(health.MaxHealth - 1);
        Assert.That(defeats, Is.Zero);
        health.TakeDamage(1);
        health.TakeDamage(1);

        Assert.That(health.CurrentHealth, Is.Zero);
        Assert.That(defeats, Is.EqualTo(1));
    }

    // Checks that damage above remaining health clamps at zero without overflow or repeated defeat.
    [Test]
    public void OverkillClampsHealthAtZero()
    {
        var defeats = 0;
        health.Defeated += () => defeats++;

        health.TakeDamage(int.MaxValue);
        health.TakeDamage(int.MaxValue);

        Assert.That(health.CurrentHealth, Is.Zero);
        Assert.That(defeats, Is.EqualTo(1));
    }

    // Checks that zero or negative damage is rejected without healing or changing the archer's state.
    [Test]
    public void RejectsNonpositiveDamage()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => health.TakeDamage(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => health.TakeDamage(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => health.TakeDamage(int.MinValue));
        Assert.That(health.CurrentHealth, Is.EqualTo(health.MaxHealth));
        Assert.That(health.IsDefeated, Is.False);
    }
}
