#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using BowMan;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public class LocalDuelTests
{
    private Scene scene;
    private Scene previousScene;
    private float timeScale;
    private Random.State random;

    // Loads the actual playable scene with deterministic timing and restores the previous active scene afterward.
    [UnitySetUp]
    public IEnumerator OpenScene()
    {
        random = Random.state;
        Random.InitState(9123);
        timeScale = Time.timeScale;
        Time.timeScale = 4f;
        previousScene = SceneManager.GetActiveScene();
        yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/_Game/Scenes/LocalDuel.unity",
            new LoadSceneParameters(LoadSceneMode.Additive));
        scene = SceneManager.GetSceneByPath("Assets/_Game/Scenes/LocalDuel.unity");
        SceneManager.SetActiveScene(scene);
        yield return null;
    }

    // Unloads match-owned arrows, rigs and actors and restores application state even after an assertion fails.
    [UnityTearDown]
    public IEnumerator CloseScene()
    {
        Time.timeScale = timeScale;
        Random.state = random;
        if (previousScene.IsValid() && previousScene.isLoaded) SceneManager.SetActiveScene(previousScene);
        if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
    }

    // Finds a component in this scene without accidentally selecting an existing review-scene object.
    private T Find<T>() where T : Component => scene.GetRootGameObjects()
        .SelectMany(g => g.GetComponentsInChildren<T>(true)).First();

    // Fires a full-strength real bow shot with gravity compensation at the selected anatomical volume.
    private ArrowFlight Shoot(LocalDuel duel, BodyRegion region)
    {
        Physics.SyncTransforms();
        var target = duel.Opponent.GetComponentsInChildren<BodyHitRegion>().First(v => v.Region == region);
        var point = target.GetComponent<Collider>().bounds.center;
        var bow = Find<Bow>();
        var start = duel.LocalArcher.transform.position + Vector3.up * 1.4f + Vector3.forward * 0.6f;
        Assert.That(ArrowBallistics.TryVelocity(start, point, 25f, out var velocity), Is.True);
        var direction = velocity.normalized;
        bow.transform.rotation = Quaternion.LookRotation(direction);
        bow.transform.position += start - bow.RestPosition;
        bow.SetHeld(true);
        Assert.That(bow.TryBeginDraw(bow.RestPosition), Is.True);
        bow.PullTo(bow.RestPosition - direction * bow.MaximumDrawDistance);
        var arrow = bow.Release();
        Assert.That(arrow, Is.Not.Null);
        Assert.That(duel.Flow.Phase, Is.EqualTo(DuelPhase.Flying));
        Assert.That(bow.TryBeginDraw(bow.RestPosition), Is.False);
        return arrow;
    }

    // Exercises a real twenty-metre bow hit, full recovery, an AI response, fatal victory and a clean same-session restart.
    [UnityTest]
    public IEnumerator BowDamageRecoveryAIResultAndRestartWorkTogether()
    {
        var duel = Find<LocalDuel>();
        var bow = Find<Bow>();
        Assert.That(duel.Flow.CanShoot(0), Is.True);
        Assert.That(duel.LocalArcher.GetComponentsInChildren<Renderer>().All(r => !r.enabled), Is.True);
        Assert.That(duel.Opponent.GetComponentsInChildren<SkinnedMeshRenderer>().Any(r => r.enabled), Is.True);
        var arrow = Shoot(duel, BodyRegion.Chest);
        var deadline = Time.time + 3f;
        while (duel.Opponent.CurrentHealth == 100 && Time.time < deadline) yield return null;
        Assert.That(duel.Opponent.CurrentHealth, Is.EqualTo(75));
        Assert.That(arrow.transform.parent.GetComponent<BodyHitRegion>().Region, Is.EqualTo(BodyRegion.Chest));
        Assert.That(bow.ShotAllowed, Is.False);
        deadline = Time.time + 32f;
        while (!duel.Flow.CanShoot(0) && Time.time < deadline)
        {
            Assert.That(bow.IsDrawing, Is.False);
            yield return null;
        }
        Assert.That(duel.Flow.CanShoot(0), Is.True);
        Assert.That(duel.Opponent.GetComponent<AIArcher>().ShotsFired, Is.EqualTo(1));
        Assert.That(duel.LocalArcher.CurrentHealth, Is.EqualTo(75));
        Assert.That(bow.ShotAllowed, Is.True);
        var results = 0;
        duel.Flow.MatchFinished += _ => results++;
        Shoot(duel, BodyRegion.Head);
        deadline = Time.time + 3f;
        while (duel.Flow.Phase != DuelPhase.Finished && Time.time < deadline) yield return null;
        Assert.That(duel.Flow.Winner, Is.Zero);
        Assert.That(results, Is.EqualTo(1));
        Assert.That(bow.ShotAllowed, Is.False);
        var old = duel.Opponent;
        var rig = Find<Camera>().transform.root;
        var oldRig = rig.GetInstanceID();
        Find<VRBowInput>().SetBowHand(false);
        duel.RestartMatch();
        yield return null;
        Assert.That(old == null, Is.True);
        Assert.That(duel.LocalArcher.CurrentHealth, Is.EqualTo(100));
        Assert.That(duel.Opponent.CurrentHealth, Is.EqualTo(100));
        Assert.That(duel.Flow.CanShoot(0), Is.True);
        Assert.That(Find<VRBowInput>().BowInLeftHand, Is.False);
        Assert.That(Find<Camera>().transform.root.GetInstanceID(), Is.EqualTo(oldRig));
        Assert.That(scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<ArrowFlight>()).Count(), Is.Zero);
        Assert.That(Find<TextMesh>().text, Does.Contain("You 100"));
        Assert.That(duel.Opponent.GetComponent<AIArcher>().ShotsFired, Is.Zero);
    }

    // Preserves physical head/controller offsets while all animated-eye displacement is applied exactly once.
    [UnityTest]
    public IEnumerator InjuryViewPreservesTrackedOffsetsAndReturnsToStanding()
    {
        var duel = Find<LocalDuel>();
        var actor = duel.LocalArcher.gameObject;
        var animator = actor.GetComponentInChildren<Animator>();
        var eye = animator.GetBoneTransform(HumanBodyBones.Head).Find("EyeAnchor");
        var camera = Find<Camera>().transform;
        var origin = camera.root;
        foreach (var driver in origin.GetComponentsInChildren<UnityEngine.InputSystem.XR.TrackedPoseDriver>())
            driver.enabled = false;
        var hand = origin.Find("Camera Offset/Left Controller");
        camera.localPosition = new Vector3(0.03f, 1.6f, 0.02f);
        camera.localRotation = Quaternion.Euler(3f, 11f, 0f);
        hand.localPosition = new Vector3(-0.3f, 1.1f, 0.2f);
        hand.localRotation = Quaternion.Euler(15f, 20f, 5f);
        var view = Find<VRInjuryView>();
        view.Bind(actor.transform, eye);
        var baseline = camera.position;
        var standingEye = eye.position;
        var standingRotation = eye.rotation;
        var headPose = camera.localPosition;
        var handPose = hand.localPosition;
        var headRotation = camera.localRotation;
        var handRotation = hand.localRotation;
        var animation = actor.GetComponent<ArcherReactionAnimation>();
        animation.Entry(BodyRegion.Chest).SampleAnimation(actor, animation.Entry(BodyRegion.Chest).length);
        view.SendMessage("LateUpdate");
        var delta = eye.rotation * Quaternion.Inverse(standingRotation);
        var expected = eye.position + delta * (baseline - standingEye);
        Assert.That(Vector3.Distance(camera.position, expected), Is.LessThan(0.001f));
        Assert.That(camera.localPosition, Is.EqualTo(headPose));
        Assert.That(hand.localPosition, Is.EqualTo(handPose));
        Assert.That(Quaternion.Angle(camera.localRotation, headRotation), Is.LessThan(0.001f));
        Assert.That(Quaternion.Angle(hand.localRotation, handRotation), Is.LessThan(0.001f));
        Assert.That(camera.position.y, Is.LessThan(0.6f));
        camera.localPosition += Vector3.right * 0.05f;
        view.SendMessage("LateUpdate");
        Assert.That(Vector3.Distance(camera.position, expected), Is.EqualTo(0.05f).Within(0.001f));
        animation.Recovery(BodyRegion.Chest).SampleAnimation(actor, animation.Recovery(BodyRegion.Chest).length);
        view.SendMessage("LateUpdate");
        Assert.That(Vector3.Distance(origin.position, new Vector3(0f, 0f, -10f)), Is.LessThan(0.001f));
        Assert.That(Quaternion.Angle(origin.rotation, Quaternion.identity), Is.LessThan(0.01f));
        yield return null;
    }
}
#endif
