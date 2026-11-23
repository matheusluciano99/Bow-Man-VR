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
        bow.transform.position += start - bow.FullDrawTip;
        bow.SetHeld(true);
        Assert.IsTrue(bow.TryBeginDraw(bow.RestPosition));
        bow.PullTo(bow.RestPosition - direction * bow.MaximumDrawDistance);
        Assert.IsNotNull(bow.Release());
        Assert.IsFalse(bow.ShotAllowed);
    }

    // Fires a real arrow down each selected woodland lane and checks that its first collision scores ten.
    [UnityTest]
    public IEnumerator AllFiveDistancesReachTheScoringFace()
    {
        const string path = "Assets/_Game/Scenes/TargetRange.unity";
        var previous = SceneManager.GetActiveScene();
        yield return EditorSceneManager.LoadSceneAsyncInPlayMode(path, new LoadSceneParameters(LoadSceneMode.Additive));
        var scene = SceneManager.GetSceneByPath(path);
        SceneManager.SetActiveScene(scene);
        yield return null;
        var range = Object.FindAnyObjectByType<OnlineTargetMatch>();
        var line = GameObject.Find("Archer1Spawn").transform;
        var face = GameObject.Find("Scoring face").transform;
        var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<ArrowFlight>("Assets/_Game/Prefabs/TestArrow.prefab");
        try
        {
            CollectionAssert.AreEqual(new[] { 15, 30, 50, 60, 70 }, OnlineTargetMatch.Distances);
            range.ChooseHostDistance(42);
            Assert.AreEqual(15, range.HostDistance);
            foreach (int metres in OnlineTargetMatch.Distances)
            {
                range.ChooseHostDistance(metres);
                Physics.SyncTransforms();
                Assert.AreEqual(metres, range.HostDistance);
                Assert.That(Vector3.Dot(face.position - line.position, line.forward), Is.EqualTo(metres).Within(.001f));
                var origin = line.TransformPoint(new Vector3(0, 1.4f, .6f));
                Assert.IsTrue(ArrowBallistics.TryVelocity(origin, face.position, OnlineTargetMatch.TargetLaunchSpeed, out var velocity));
                var arrow = Object.Instantiate(prefab, origin, Quaternion.LookRotation(velocity));
                RaycastHit contact = default;
                arrow.Hit += hit => contact = hit;
                arrow.Launch(velocity);
                yield return Until(() => contact.collider != null, $"First contact at {metres} metres", 8f);
                Assert.AreEqual(face, contact.collider.transform);
                Assert.AreEqual(10, OnlineTargetMatch.RingScore(face.InverseTransformPoint(contact.point)));
                Object.Destroy(arrow.gameObject);
            }
        }
        finally
        {
            if (previous.isLoaded) SceneManager.SetActiveScene(previous);
            SceneManager.UnloadSceneAsync(scene);
        }
    }

    // Completes ten turns per side offline, compares CPU points to real impacts and restarts at the chosen distance.
    [UnityTest]
    public IEnumerator CpuCompetitionScoresPhysicalArrowsAndRematches()
    {
        const string path = "Assets/_Game/Scenes/TargetRange.unity";
        var previous = SceneManager.GetActiveScene();
        yield return EditorSceneManager.LoadSceneAsyncInPlayMode(path, new LoadSceneParameters(LoadSceneMode.Additive));
        var scene = SceneManager.GetSceneByPath(path);
        SceneManager.SetActiveScene(scene);
        yield return null;
        var range = Object.FindAnyObjectByType<OnlineTargetMatch>();
        var connection = Object.FindAnyObjectByType<PvPConnectionController>();
        var face = GameObject.Find("Scoring face").transform;
        var line = GameObject.Find("Archer1Spawn").transform;
        float originalScale = Time.timeScale;
        try
        {
            Time.timeScale = 3f;
            range.ChooseHostDistance(70);
            range.StartCpuMatch();
            range.StartCpuMatch();
            var bow = Object.FindAnyObjectByType<Bow>();
            Assert.IsTrue(range.AgainstCpu && range.Playing);
            var opponent = Object.FindAnyObjectByType<OpponentBowAction>();
            Assert.That(opponent.name, Does.StartWith(ArcherSelection.Other(ArcherSelection.Preferred) == ArcherModel.Erika
                ? "ErikaArcher" : "EnglishArcher"));
            int cpuPoints = 0;
            for (int turn = 0; turn < OnlineTargetMatch.ArrowsPerSeat; turn++)
            {
                Assert.IsTrue(bow.ShotAllowed);
                Shoot(bow, line, face.position);
                yield return Until(() => range.Completed == turn * 2 + 1, "Player shot scored");
                Assert.IsFalse(bow.ShotAllowed);
                yield return Until(() => opponent.IsNocked, "CPU has drawn its bow");
                ArrowFlight shot = null;
                yield return Until(() =>
                {
                    foreach (var arrow in range.GetComponentsInChildren<ArrowFlight>())
                        if (arrow.IsFlying) { shot = arrow; return true; }
                    return false;
                }, "CPU projectile in flight");
                shot.Hit += hit =>
                {
                    if (hit.collider.transform == face)
                        cpuPoints += OnlineTargetMatch.RingScore(face.InverseTransformPoint(hit.point));
                };
                yield return Until(() => range.Completed == (turn + 1) * 2, "CPU shot settled");
                Assert.AreEqual(cpuPoints, range.SecondScore);
                Assert.AreEqual((turn + 1) * 10, range.FirstScore);
            }
            Assert.IsTrue(range.Finished);
            Assert.IsFalse(bow.ShotAllowed);
            Assert.That(GameObject.Find("HeadsetStatus").GetComponent<TextMesh>().text, Does.Contain("CPU"));
            Assert.AreEqual(ConnectionPhase.Offline, connection.Status.Phase);
            range.RequestRematch();
            Assert.AreEqual(2, range.Round);
            Assert.AreEqual(70, range.RoundDistance);
            Assert.AreEqual(0, range.Completed + range.FirstScore + range.SecondScore);
            Assert.IsTrue(bow.ShotAllowed);
        }
        finally
        {
            Time.timeScale = originalScale;
            if (previous.isLoaded) SceneManager.SetActiveScene(previous);
            SceneManager.UnloadSceneAsync(scene);
        }
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
            range.ChooseHostDistance(host ? 70 : 30);
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
                Assert.AreEqual(70, range.RoundDistance);
                Assert.That(Vector3.Dot(face.position - stand.position, stand.forward), Is.EqualTo(70f).Within(.001f));
                range.ChooseHostDistance(15);
                Assert.AreEqual(70, range.RoundDistance);
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
                    var reticle = GameObject.Find("Target impact inset").transform.Find("Last impact");
                    bool scored = game != 1 || shot != 1;
                    Assert.AreEqual(scored, reticle.gameObject.activeSelf);
                    if (scored) Assert.That(reticle.localPosition.magnitude, Is.LessThan(.015f));
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
