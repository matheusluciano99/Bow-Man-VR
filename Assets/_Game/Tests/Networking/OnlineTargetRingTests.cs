using BowMan.Networking;
using NUnit.Framework;
using UnityEngine;

public class OnlineTargetRingTests
{
    // Confirms radial scoring at the center, each dividing line, the outer edge and outside the disc.
    [Test]
    public void HigherRingOwnsItsOuterLine()
    {
        Assert.AreEqual(10, OnlineTargetMatch.RingScore(Vector2.zero));
        for (int i = 1; i <= 10; i++)
        {
            var line = Vector2.up * (0.048f * i);
            Assert.AreEqual(11 - i, OnlineTargetMatch.RingScore(line));
            Assert.AreEqual(10 - i, OnlineTargetMatch.RingScore(line + Vector2.up * 0.0001f));
        }
    }
}
