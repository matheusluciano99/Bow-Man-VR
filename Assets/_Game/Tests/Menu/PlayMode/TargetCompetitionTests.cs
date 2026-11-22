#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using BowMan;
using BowMan.Menu;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

public class TargetCompetitionTests
{
    private Scene scene;
    private Bow bow;
    private TargetCompetition match;
    private Transform face;
    private float previousScale;

    // Opens the shipped range and finds the same bow, target and scoreboard used by players.
    [UnitySetUp]
    public IEnumerator OpenRange()
    {
        previousScale = Time.timeScale;
        Time.timeScale = 4f;
        var path = "Assets/_Game/Scenes/TargetRange.unity";
        yield return EditorSceneManager.LoadSceneAsyncInPlayMode(path, new LoadSceneParameters(LoadSceneMode.Additive));
        scene = SceneManager.GetSceneByPath(path);
        yield return null;
        var roots = scene.GetRootGameObjects();
        match = roots.SelectMany(g => g.GetComponentsInChildren<TargetCompetition>()).Single();
        bow = roots.SelectMany(g => g.GetComponentsInChildren<Bow>()).Single();
        face = roots.Single(g => g.name == "Target stand").transform.Find("Scoring face");
    }

    // Releases the range and restores the Editor's simulation speed.
    [UnityTearDown]
    public IEnumerator CloseRange()
    {
        Time.timeScale = previousScale;
        if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
    }

    // Verifies the saved face can resolve its imported color rings before checking their boundaries.
    [Test]
    public void PaintedFaceScoresTheHigherRingOnTheLine()
    {
        var texture = face.GetComponent<Renderer>().sharedMaterial.GetTexture("_BaseMap");
        Assert.That(texture, Is.InstanceOf<Texture2D>());
        Assert.That(texture.width, Is.EqualTo(texture.height));
        Assert.AreEqual(10, TargetCompetition.RingScore(Vector2.zero));
        for (int i = 1; i <= 10; i++)
        {
            float line = TargetCompetition.FaceRadius * i / 10f;
            Assert.AreEqual(11 - i, TargetCompetition.RingScore(Vector2.right * line));
            Assert.AreEqual(10 - i, TargetCompetition.RingScore(Vector2.up * (line + 0.0001f)));
        }
    }

    // Fires the physical bow from the shared firing line and waits for the actual shot outcome.
    private IEnumerator Shoot(Vector3 destination, bool expire = false)
    {
        var origin = face.position - Vector3.forward * 14.4f;
        Assert.IsTrue(ArrowBallistics.TryVelocity(origin, destination, bow.MaximumLaunchSpeed, out var velocity));
        var direction = expire ? Vector3.up : velocity.normalized;
        bow.transform.rotation = Quaternion.LookRotation(direction, expire ? Vector3.forward : Vector3.up);
        bow.transform.position += origin - bow.RestPosition;
        bow.SetHeld(true);
        Assert.IsTrue(bow.TryBeginDraw(bow.RestPosition));
        bow.PullTo(bow.RestPosition - direction * bow.MaximumDrawDistance);
        var arrow = bow.Release();
        Assert.IsNotNull(arrow);
        Assert.IsFalse(bow.ShotAllowed);
        var gravity = Physics.gravity;
        bool expired = false;
        arrow.Expired += () => expired = true;
        try
        {
            if (expire) Physics.gravity = Vector3.zero;
            var deadline = Time.time + 10f;
            while (arrow != null && arrow.IsFlying && Time.time < deadline) yield return null;
            Assert.IsFalse(arrow != null && arrow.IsFlying);
            Assert.AreEqual(expire, expired);
        }
        finally { Physics.gravity = gravity; }
    }

    // Sends the real handoff/rematch key through a temporary Game-view input device.
    private static IEnumerator PressReady()
    {
        var focus = InputSystem.settings.editorInputBehaviorInPlayMode;
        var background = InputSystem.settings.backgroundBehavior;
        var keyboard = InputSystem.AddDevice<Keyboard>();
        try
        {
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.EnableDevice(keyboard);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.R));
            yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
        }
        finally
        {
            InputSystem.RemoveDevice(keyboard);
            InputSystem.settings.editorInputBehaviorInPlayMode = focus;
            InputSystem.settings.backgroundBehavior = background;
        }
    }

    // Plays both ends, including a cancelled draw, actual misses, the handoff lock, a tie and a rematch.
    [UnityTest]
    public IEnumerator TwentyReleasesDetermineTheResultAndReset()
    {
        var hud = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<TextMesh>())
            .Single(t => t.name == "Match feedback");
        bow.SetHeld(true);
        Assert.IsTrue(bow.TryBeginDraw(bow.RestPosition));
        bow.CancelDraw();
        Assert.AreEqual(0, match.Used);
        for (int round = 0; round < 2; round++)
        {
            for (int seat = 0; seat < 2; seat++)
            {
                for (int i = 0; i < TargetCompetition.ArrowsPerArcher; i++)
                {
                    var miss = round == 0 && seat == 0 && i < 2;
                    yield return Shoot(miss ? face.TransformPoint(new Vector3(0.495f, 0.495f)) : face.position,
                        expire: miss && i == 0);
                    Assert.AreEqual(miss ? 0 : 10, match.LastPoints);
                    Assert.AreEqual(i + 1, match.Used);
                }
                Assert.IsFalse(bow.ShotAllowed);
                Assert.IsFalse(bow.TryBeginDraw(bow.RestPosition));
                if (seat == 0)
                {
                    Assert.IsTrue(match.Handoff);
                    yield return null;
                    Assert.That(hud.text, Does.Contain("Pass to Archer 2"));
                    yield return PressReady();
                    Assert.AreEqual(1, match.Archer);
                    Assert.AreEqual(0, match.Used);
                }
            }
            Assert.IsTrue(match.Finished);
            Assert.AreEqual(round == 0 ? 80 : 100, match.ScoreFor(0));
            Assert.AreEqual(100, match.ScoreFor(1));
            yield return null;
            Assert.That(hud.text, Does.Contain(round == 0 ? "Archer 2 wins" : "Tie"));
            yield return PressReady();
            Assert.AreEqual(0, match.Archer);
            Assert.AreEqual(0, match.ScoreFor(0) + match.ScoreFor(1));
            Assert.IsTrue(bow.ShotAllowed);
        }
    }
}
#endif
