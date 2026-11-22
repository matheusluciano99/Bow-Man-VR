#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using BowMan;
using BowMan.Networking;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public class OnlineTargetRelayTests
{
    // Reads the existing two-Editor Relay test arguments.
    private static string Argument(string name)
    {
        var args = Environment.GetCommandLineArgs();
        int i = Array.IndexOf(args, name);
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
    }

    // Gives each live network transition a bounded wait while Unity continues simulating physics.
    private static IEnumerator Until(Func<bool> condition, string label, float timeout = 30f)
    {
        var deadline = Time.realtimeSinceStartup + timeout;
        while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.IsTrue(condition(), label);
    }

    // Shoots the gameplay bow at a world-space point using its actual configured launch speed.
    private static void Shoot(Bow bow, Transform stand, Vector3 target)
    {
        var start = stand.TransformPoint(new Vector3(0f, 1.4f, 0.6f));
        Assert.IsTrue(ArrowBallistics.TryVelocity(start, target, bow.MaximumLaunchSpeed, out var speed));
        var direction = speed.normalized;
        bow.transform.rotation = Quaternion.LookRotation(direction);
        bow.transform.position += start - bow.RestPosition;
        bow.SetHeld(true);
        Assert.IsTrue(bow.TryBeginDraw(bow.RestPosition));
        bow.PullTo(bow.RestPosition - direction * bow.MaximumDrawDistance);
        Assert.IsNotNull(bow.Release());
        Assert.IsFalse(bow.ShotAllowed);
    }

    // Joins two independent players, compares all twenty score updates, votes for a rematch and ties it.
    [UnityTest, Timeout(360000)]
    public IEnumerator TwoEditorsScoreTheMatch()
    {
        var role = Argument("-duelTestRole");
        var folder = Argument("-duelTestFolder");
        if (role == null || folder == null) Assert.Ignore("Requires the paired Relay Editors.");
        var host = role == "host";
        Directory.CreateDirectory(folder);
        var code = Path.Combine(folder, "code.txt");
        const string path = "Assets/_Game/Scenes/TargetRange.unity";
        yield return EditorSceneManager.LoadSceneAsyncInPlayMode(path, new LoadSceneParameters(LoadSceneMode.Additive));
        var scene = SceneManager.GetSceneByPath(path);
        SceneManager.SetActiveScene(scene);
        yield return null;
        var connection = Object.FindAnyObjectByType<PvPConnectionController>();
        var range = Object.FindAnyObjectByType<OnlineTargetMatch>();
        try
        {
            var bow = Object.FindAnyObjectByType<Bow>(FindObjectsInactive.Include);
            Assert.IsFalse(bow.ShotAllowed);
            if (host)
            {
                var start = connection.HostAsync();
                yield return Until(() => start.IsCompleted, "Relay allocation", 90);
                Assert.AreEqual(ConnectionPhase.WaitingForOpponent, connection.Status.Phase, connection.Status.Describe());
                File.WriteAllText(code + ".tmp", connection.Status.JoinCode);
                File.Move(code + ".tmp", code);
            }
            else
            {
                yield return Until(() => File.Exists(code), "Host allocated a code", 150);
                var join = connection.JoinAsync(File.ReadAllText(code));
                yield return Until(() => join.IsCompleted, "Join allocation", 90);
            }
            yield return Until(() => range.Playing, "Both network seats ready: " + role, 150);
            var stand = GameObject.Find("Archer1Spawn").transform;
            var face = GameObject.Find("Scoring face").transform;
            Assert.IsInstanceOf<Texture2D>(face.GetComponent<Renderer>().sharedMaterial.GetTexture("_BaseMap"));
            for (int game = 1; game <= 2; ++game)
            {
                yield return Until(() => range.Round == game, "Fresh round");
                for (int shot = 0; shot < 20; ++shot)
                {
                    if ((shot % 2 == 0) == host)
                    {
                        Assert.IsTrue(bow.ShotAllowed, $"Shot {shot + 1}");
                        bow.SetHeld(true);
                        Assert.IsTrue(bow.TryBeginDraw(bow.RestPosition));
                        bow.CancelDraw();
                        Assert.AreEqual(shot, range.Completed);
                        var point = game == 1 && shot == 1 ? face.TransformPoint(new Vector3(0.49f, 0.49f)) : face.position;
                        Shoot(bow, stand, point);
                    }
                    yield return Until(() => range.Completed >= shot + 1, "Both peers see settled shot " + shot);
                    Assert.AreEqual((shot + 2) / 2 * 10, range.FirstScore);
                    Assert.AreEqual((shot + 1) / 2 * 10 - (game == 1 && shot >= 1 ? 10 : 0), range.SecondScore);
                }
                Assert.IsTrue(range.Finished);
                Assert.IsFalse(bow.ShotAllowed);
                yield return null;
                var hud = GameObject.Find("HeadsetStatus").GetComponent<TextMesh>();
                Assert.That(hud.text, Does.Contain(game == 1 ? "Archer 1 wins" : "Tie"));
                Assert.That(hud.text, Does.Contain("0 left"));
                var marker = Path.Combine(folder, game + "-" + role);
                File.WriteAllText(marker, hud.text);
                yield return Until(() => File.Exists(Path.Combine(folder, game + (host ? "-join" : "-host"))), "Both saw the result");
                if (game == 1) range.RequestRematch();
            }
        }
        finally
        {
            connection.Leave();
            SceneManager.UnloadSceneAsync(scene);
        }
    }
}
#endif
