#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BowMan;
using BowMan.Networking;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public class PvPRelayMatchTests
{
    // Waits in both Editors without accelerating the live Relay transport or the host's animation clock.
    private static IEnumerator Until(Func<bool> complete, string label, float seconds = 60f)
    {
        var deadline = Time.realtimeSinceStartup + seconds;
        while (!complete() && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(complete(), Is.True, label);
    }

    // Reads a named test argument without affecting the regular editor or shipped player.
    private static string Argument(string name)
    {
        var args = Environment.GetCommandLineArgs();
        var index = Array.IndexOf(args, name);
        return index < 0 || index + 1 >= args.Length ? null : args[index + 1];
    }

    // Exercises the shared bow in its assigned arena slot.
    private static void Fire(PvPDuel duel, Bow bow, int shooter, BodyRegion region)
    {
        var owner = duel.Archer(shooter).transform;
        var target = duel.Archer(1 - shooter).GetComponentsInChildren<BodyHitRegion>().First(r => r.Region == region);
        Physics.SyncTransforms();
        var start = owner.TransformPoint(new Vector3(0f, 1.4f, 0.6f));
        Assert.That(ArrowBallistics.TryVelocity(start, target.GetComponent<Collider>().bounds.center,
            bow.MaximumLaunchSpeed, out var velocity), Is.True);
        bow.transform.rotation = Quaternion.LookRotation(velocity);
        bow.transform.position += start - bow.RestPosition;
        bow.SetHeld(true);
        Assert.That(bow.TryBeginDraw(bow.RestPosition), Is.True);
        bow.PullTo(bow.RestPosition - velocity.normalized * bow.MaximumDrawDistance);
        Assert.That(bow.Release(), Is.Not.Null);
    }

    // Plays a real host/client match in two separate Editors and compares both authoritative injury records.
    [UnityTest]
    public IEnumerator TwoEditorsAgreeOnTheDuel()
    {
        var role = Argument("-duelTestRole");
        var folder = Argument("-duelTestFolder");
        if (role == null || folder == null) Assert.Ignore("Requires two Editors with -duelTestRole and a shared -duelTestFolder.");
        Assert.That(role, Is.EqualTo("host").Or.EqualTo("join"));
        var host = role == "host";
        Directory.CreateDirectory(folder);
        var codePath = Path.Combine(folder, "code.txt");
        var scenePath = "Assets/_Game/Scenes/PvPConnectionTest.unity";
        yield return EditorSceneManager.LoadSceneAsyncInPlayMode(scenePath, new LoadSceneParameters(LoadSceneMode.Additive));
        var scene = SceneManager.GetSceneByPath(scenePath);
        SceneManager.SetActiveScene(scene);
        var controller = Object.FindAnyObjectByType<PvPConnectionController>();
        var duel = Object.FindAnyObjectByType<PvPDuel>();
        try
        {
            if (host)
            {
                var starting = controller.HostAsync();
                yield return Until(() => starting.IsCompleted, "Relay host allocation");
                Assert.That(controller.Status.Phase, Is.EqualTo(ConnectionPhase.WaitingForOpponent), controller.Status.Describe());
                File.WriteAllText(codePath + ".tmp", controller.Status.JoinCode);
                File.Move(codePath + ".tmp", codePath);
            }
            else
            {
                yield return Until(() => File.Exists(codePath), "Host code", 150f);
                var joining = controller.JoinAsync(File.ReadAllText(codePath));
                yield return Until(() => joining.IsCompleted, "Relay client allocation");
            }
            yield return Until(() => duel.Flow != null, $"Round start on {role}");
            var bow = Object.FindAnyObjectByType<Bow>();
            var hits = new List<string>();
            duel.Flow.ReactionRequested += (target, impact, lethal, token) =>
            {
                var report = duel.Archer(target).GetComponentsInChildren<ArrowAnatomy>()
                    .Select(a => a.LastImpact).Last(r => r != null && r.Entry.Volume == impact.Volume);
                Assert.That(impact.Volume.GetComponentsInChildren<ParticleSystem>(), Is.Not.Empty);
                var point = impact.Volume.transform.InverseTransformPoint(impact.Point);
                var organs = string.Join(",", report.Organs.Select(h => h.Organ.DisplayName));
                var bones = string.Join(",", report.Bones.Select(h => h.Bone + (h.Fractured ? "*" : "")));
                hits.Add($"{token}/{target}/{impact.Region}/{impact.Side}/{point:F3}/{organs}/{bones}/{lethal}");
            };
            File.WriteAllText(Path.Combine(folder, role + "-ready"), "");
            yield return Until(() => File.Exists(Path.Combine(folder, (host ? "join" : "host") + "-ready")), "Peer entered match");
            var regions = new[] { BodyRegion.LeftLeg, BodyRegion.Chest, BodyRegion.Head };
            for (var shot = 0; shot < regions.Length; shot++)
            {
                var shooter = shot % 2;
                yield return Until(() => duel.Flow.CanShoot(shooter), "Recovered turn " + shot);
                if (host == (shooter == 0)) Fire(duel, bow, shooter, regions[shot]);
                yield return Until(() => hits.Count == shot + 1, "Replicated wound " + shot);
                Assert.That(hits.Last(), Does.Contain("/" + regions[shot] + "/"));
            }
            Assert.That(duel.Flow.Phase, Is.EqualTo(DuelPhase.Finished));
            Assert.That(duel.Flow.Winner, Is.Zero);
            Assert.That(duel.Archer(0).CurrentHealth, Is.EqualTo(75));
            Assert.That(duel.Archer(1).CurrentHealth, Is.Zero);
            Assert.That(bow.ShotAllowed, Is.False);
            Assert.That(Object.FindAnyObjectByType<FatalCutaway>().IsVisible, Is.True);
            var ownResults = Path.Combine(folder, role + "-hits.txt");
            File.WriteAllLines(ownResults + ".tmp", hits);
            File.Move(ownResults + ".tmp", ownResults);
            var otherResults = Path.Combine(folder, (host ? "join" : "host") + "-hits.txt");
            yield return Until(() => File.Exists(otherResults), "Peer finished");
            Assert.That(File.ReadAllLines(otherResults), Is.EqualTo(hits));

            var firstRound = duel.Round;
            if (host)
            {
                duel.RequestRematch();
                duel.RequestRematch();
                Assert.That(duel.Round, Is.EqualTo(firstRound));
                Assert.That(duel.RematchRequested, Is.True);
                File.WriteAllText(Path.Combine(folder, "host-voted"), "");
            }
            else
            {
                yield return Until(() => File.Exists(Path.Combine(folder, "host-voted")), "Host offered rematch");
                Assert.That(duel.Flow.Phase, Is.EqualTo(DuelPhase.Finished));
                duel.RequestRematch();
            }
            yield return Until(() => duel.Round == firstRound + 1, "Both votes started a new round");
            Assert.That(duel.Archer(0).CurrentHealth, Is.EqualTo(100));
            Assert.That(duel.Archer(1).CurrentHealth, Is.EqualTo(100));
            Assert.That(duel.Flow.CanShoot(0), Is.True);
            Assert.That(duel.RematchRequested, Is.False);
            Assert.That(duel.Archer(1).GetComponentsInChildren<ArrowFlight>(), Is.Empty);
            if (host)
            {
                bow.SetHeld(true);
                Assert.That(bow.TryBeginDraw(bow.RestPosition), Is.True);
                File.WriteAllText(Path.Combine(folder, "host-nocked"), "");
                yield return Until(() => controller.Status.Phase == ConnectionPhase.WaitingForOpponent, "Seat reopened");
                Assert.That(duel.Flow, Is.Null);
                Assert.That(bow.IsDrawing, Is.False);
                File.WriteAllText(Path.Combine(folder, "seat-free"), "");
            }
            else
            {
                yield return Until(() => File.Exists(Path.Combine(folder, "host-nocked")), "Active rematch");
                controller.Leave();
                Assert.That(controller.Status.CanStart, Is.True);
                Assert.That(duel.Flow, Is.Null);
                yield return Until(() => File.Exists(Path.Combine(folder, "seat-free")), "Remote seat freed");
                var rejoin = controller.JoinAsync(File.ReadAllText(codePath));
                yield return Until(() => rejoin.IsCompleted, "Rejoin request");
                Assert.That(controller.Status.Phase, Is.Not.EqualTo(ConnectionPhase.Failed), controller.Status.Describe());
            }
            yield return Until(() => duel.Flow != null && duel.Round == firstRound + 2, "Fresh round after rejoin");
            Assert.That(duel.Flow.CanShoot(0), Is.True);
            Assert.That(duel.Archer(1).CurrentHealth, Is.EqualTo(100));
            if (host)
            {
                yield return Until(() => File.Exists(Path.Combine(folder, "join-reentered")), "Peer rejoined");
                controller.Leave();
                Assert.That(controller.Status.CanStart, Is.True);
            }
            else
            {
                File.WriteAllText(Path.Combine(folder, "join-reentered"), "");
                yield return Until(() => controller.Status.Phase == ConnectionPhase.Disconnected, "Host departure");
                Assert.That(controller.Status.CanStart, Is.True);
                Assert.That(duel.Flow, Is.Null);
            }
            File.WriteAllText(Path.Combine(folder, role + "-rematch.txt"), "Rematch, departure and rejoin passed.");
        }
        finally { controller.Leave(); }
    }
}
#endif
