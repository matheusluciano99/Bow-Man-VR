#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BowMan;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
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
        var volume = target.GetComponent<Collider>();
        var point = volume.bounds.center;
        // Aim at the outer shoulder visible beside the chest in the relaxed stance.
        if (region is BodyRegion.LeftShoulder or BodyRegion.RightShoulder)
            point = volume.ClosestPoint(point + duel.Opponent.transform.right *
                (region == BodyRegion.LeftShoulder ? -0.035f : 0.035f));
        var bow = Find<Bow>();
        var start = duel.LocalArcher.transform.position + Vector3.up * 1.4f + Vector3.forward * 0.6f;
        Assert.That(ArrowBallistics.TryVelocity(start, point, 25f, out var velocity), Is.True);
        var direction = velocity.normalized;
        bow.transform.rotation = Quaternion.LookRotation(direction);
        bow.transform.position += start - bow.FullDrawTip;
        bow.SetHeld(true);
        Assert.That(bow.TryBeginDraw(bow.RestPosition), Is.True);
        bow.PullTo(bow.RestPosition - direction * bow.MaximumDrawDistance);
        var arrow = bow.Release();
        Assert.That(arrow, Is.Not.Null);
        Assert.That(duel.Flow.Phase, Is.EqualTo(DuelPhase.Flying));
        Assert.That(bow.TryBeginDraw(bow.RestPosition), Is.False);
        return arrow;
    }

    // Checks both sides of the stretch threshold, a held full draw, easing off and cancellation.
    [UnityTest]
    public IEnumerator StretchStartsAboveSixtyPercentAndStopsWhenTheStringRelaxes()
    {
        Time.timeScale = 1f;
        Find<VRBowInput>().enabled = false;
        Find<DesktopBowTest>().enabled = false;
        var sounds = Find<DuelAudio>();
        var bow = Find<Bow>();
        var creak = sounds.transform.Find("Draw").GetComponent<AudioSource>();
        bow.SetHeld(true);
        Assert.That(bow.TryBeginDraw(bow.RestPosition), Is.True);
        yield return null;
        Assert.That(sounds.transform.Find("Nock"), Is.Not.Null);
        bow.PullTo(bow.RestPosition - bow.transform.forward * (bow.MaximumDrawDistance * 0.59f));
        yield return null;
        Assert.That(creak.isPlaying, Is.False);
        bow.PullTo(bow.RestPosition - bow.transform.forward * (bow.MaximumDrawDistance * 0.61f));
        yield return new WaitForSeconds(0.16f);
        Assert.That(creak.isPlaying, Is.True);
        var onsetGain = creak.volume;
        var onsetPitch = creak.pitch;
        Assert.That(onsetGain, Is.GreaterThan(0.05f));
        bow.PullTo(bow.RestPosition - bow.transform.forward * bow.MaximumDrawDistance);
        yield return new WaitForSeconds(0.3f);
        Assert.That(creak.volume, Is.GreaterThan(onsetGain * 2f));
        Assert.That(creak.pitch, Is.GreaterThan(onsetPitch));
        bow.PullTo(bow.RestPosition - bow.transform.forward * (bow.MaximumDrawDistance * 0.58f));
        yield return null;
        Assert.That(creak.isPlaying, Is.False);
        bow.PullTo(bow.RestPosition - bow.transform.forward * bow.MaximumDrawDistance);
        yield return null;
        Assert.That(creak.isPlaying, Is.True);
        bow.CancelDraw();
        yield return null;
        Assert.That(creak.isPlaying, Is.False);
        Assert.That(sounds.transform.Find("Release"), Is.Null);
        Assert.That(Find<LocalDuel>().Flow.CanShoot(0), Is.True);
    }

    // Renders the imported breath on each side of the actual player listener and compares stereo output.
    [UnityTest]
    public IEnumerator HeadLevelEmitterPansAcrossTheDesktopListener()
    {
        var duel = Find<LocalDuel>();
        var listener = Find<AudioListener>();
        var sources = Find<DuelAudio>().GetComponentsInChildren<AudioSource>();
        var voice = duel.Opponent.GetComponentsInChildren<AudioSource>().Single(s => s.clip.name == "Breath");
        foreach (var source in sources) source.mute = source != voice;
        voice.transform.SetParent(listener.transform, false);
        voice.volume = 1f;
        var savedVolume = AudioListener.volume;
        var frameRate = Time.captureFramerate;
        try
        {
            AudioListener.volume = 1f;
            Time.captureFramerate = 60;
            Assert.That(AudioRenderer.Start(), Is.True);
            foreach (var side in new[] { -1, 1 })
            {
                voice.transform.localPosition = Vector3.right * side;
                double left = 0, right = 0;
                for (var frame = 0; frame < 48; frame++)
                {
                    yield return null;
                    int samples = AudioRenderer.GetSampleCountForCaptureFrame();
                    using var buffer = new Unity.Collections.NativeArray<float>(samples * 2, Unity.Collections.Allocator.Temp);
                    Assert.That(AudioRenderer.Render(buffer), Is.True);
                    if (frame < 4) continue;
                    for (int i = 0; i < buffer.Length; i += 2)
                    {
                        left += buffer[i] * buffer[i];
                        right += buffer[i + 1] * buffer[i + 1];
                    }
                }
                Debug.Log($"Duel breath stereo energy, side {side}: left {left:F4}, right {right:F4}");
                Assert.That(side < 0 ? left : right, Is.GreaterThan(0.001));
                Assert.That(side < 0 ? left : right, Is.GreaterThan(2 * (side < 0 ? right : left)));
            }
        }
        finally
        {
            AudioRenderer.Stop();
            Time.captureFramerate = frameRate;
            AudioListener.volume = savedVolume;
        }
    }

    // Checks the actual round emitters through a bow release, a lethal contact and a rematch.
    [UnityTest]
    public IEnumerator AudioFollowsTheShotAndStopsBreathingOnDefeat()
    {
        var duel = Find<LocalDuel>();
        var sounds = Find<DuelAudio>();
        var room = sounds.GetComponentsInChildren<AudioSource>();
        Assert.That(room.Count(s => s.clip.name == "Breath" && s.spatialBlend == 1f), Is.EqualTo(2));
        var ambience = room.Single(s => s.spatialBlend == 0f);
        Assert.That(ambience.clip.channels, Is.EqualTo(2));
        var arrow = Shoot(duel, BodyRegion.Head);
        var launch = sounds.transform.Find("Release");
        Assert.That(launch.position, Is.EqualTo(Find<Bow>().transform.position));
        Assert.That(arrow.transform.Find("Flight"), Is.Not.Null);
        var deadline = Time.time + 3f;
        while (arrow.IsFlying && Time.time < deadline) yield return null;
        Assert.That(duel.Opponent.IsDefeated, Is.True);
        Assert.That(Vector3.Distance(sounds.transform.Find("Body").position, arrow.transform.position), Is.LessThan(0.10f));
        Assert.That(duel.Opponent.GetComponentsInChildren<AudioSource>().Single(s => s.clip.name == "Breath").isPlaying, Is.False);
        Assert.That(arrow.transform.Find("Flight") == null || !arrow.transform.Find("Flight").GetComponent<AudioSource>().isPlaying);
        duel.RestartMatch();
        yield return null;
        Assert.That(sounds == null, Is.True);
        Assert.That(Find<DuelAudio>().GetComponentsInChildren<AudioSource>().Count(s => s.clip.name == "Breath"), Is.EqualTo(2));
    }

    // Checks visible breathing at duel distance while the local tracking origin stays still.
    [UnityTest]
    public IEnumerator StandingOpponentMovesWhileTrackingOriginStaysStill()
    {
        var duel = Find<LocalDuel>();
        var shoulder = duel.Opponent.GetComponentInChildren<Animator>().GetBoneTransform(HumanBodyBones.LeftShoulder);
        var origin = Find<Camera>().transform.root;
        var position = origin.position;
        var rotation = origin.rotation;
        var start = shoulder.localRotation;
        var maxAngle = 0f;
        var end = Time.time + 4f;
        while (Time.time < end)
        {
            yield return null;
            maxAngle = Mathf.Max(maxAngle, Quaternion.Angle(start, shoulder.localRotation));
            Assert.That(Vector3.Distance(position, origin.position), Is.LessThan(0.001f));
            Assert.That(Quaternion.Angle(rotation, origin.rotation), Is.LessThan(0.01f));
        }
        Assert.That(maxAngle, Is.GreaterThan(0.3f));
    }

    // Checks the visible AI draw against the fingertips and the one projectile admitted by the turn.
    [UnityTest]
    public IEnumerator OpponentNocksDrawsAndReleasesOneArrow()
    {
        var duel = Find<LocalDuel>();
        var ai = duel.Opponent.GetComponent<AIArcher>();
        var action = ai.GetComponent<OpponentBowAction>();
        var rig = ai.GetComponentInChildren<Animator>();
        var settings = new UnityEditor.SerializedObject(ai);
        settings.FindProperty("thinkDelay").floatValue = 2f;
        settings.ApplyModifiedPropertiesWithoutUndo();
        Shoot(duel, BodyRegion.LeftHand);
        var end = Time.time + 22f;
        var sawInjury = false;
        var samples = 0;
        var largestGap = 0f;
        var firstString = Vector3.zero;
        var travel = 0f;
        while (ai.ShotsFired == 0 && Time.time < end)
        {
            yield return null;
            var equipment = ai.transform.Find("Opponent bow");
            if (!equipment.gameObject.activeSelf) sawInjury = true;
            if (!action.IsNocked) continue;
            var pinch = (rig.GetBoneTransform(HumanBodyBones.RightIndexDistal).position +
                rig.GetBoneTransform(HumanBodyBones.RightThumbDistal).position) * 0.5f;
            largestGap = Mathf.Max(largestGap, Vector3.Distance(pinch, action.StringPoint));
            var relativeString = equipment.InverseTransformPoint(action.StringPoint);
            if (samples++ == 0) firstString = relativeString;
            travel = Mathf.Max(travel, Vector3.Distance(firstString, relativeString));
        }
        Assert.That(sawInjury, Is.True);
        Assert.That(samples, Is.GreaterThan(1));
        Assert.That(travel, Is.GreaterThan(0.3f));
        Assert.That(largestGap, Is.LessThan(0.045f), "Drawing fingers to moving string");
        Assert.That(ai.ShotsFired, Is.EqualTo(1));
        Assert.That(action.IsNocked, Is.False);
        Assert.That(action.transform.Find("Opponent bow").GetComponentsInChildren<ArrowFlight>(true), Is.Empty);
    }

    // Exercises a real twenty-metre bow hit, full recovery, an AI response, fatal victory and a clean same-session restart.
    [UnityTest]
    public IEnumerator BowDamageRecoveryAIResultAndRestartWorkTogether()
    {
        var duel = Find<LocalDuel>();
        var bow = Find<Bow>();
        // Isolates flow/recovery from random aim; imperfect aiming has its own AI tests.
        typeof(AIArcher).GetField("aimError", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(duel.Opponent.GetComponent<AIArcher>(), 0f);
        Assert.That(duel.Flow.CanShoot(0), Is.True, $"{duel.Flow.Phase}: local health {duel.LocalArcher.CurrentHealth}");
        var bowInput = new UnityEditor.SerializedObject(Find<VRBowInput>());
        Assert.That(bowInput.FindProperty("gripRotationOffset").vector3Value, Is.EqualTo(new Vector3(90f, 0f, 0f)));
        Assert.That(Find<TextMesh>().text, Does.Contain(UnityEngine.XR.XRSettings.isDeviceActive ? "Hold its trigger" : "Press Tab"));
        Assert.That(Find<TextMesh>().text, Does.Not.Contain("Grip: hold bow"));
        Assert.That(duel.LocalArcher.GetComponentsInChildren<Renderer>().All(r => !r.enabled), Is.True);
        Assert.That(duel.Opponent.GetComponentsInChildren<SkinnedMeshRenderer>().Any(r => r.enabled), Is.True);
        var cutaway = Find<FatalCutaway>();
        var fatalities = 0;
        AnatomicalImpact lastFatality = null;
        duel.Flow.FatalityRequested += (target, report) =>
        {
            Assert.That(target, Is.EqualTo(1));
            Assert.That(duel.Opponent.IsDefeated, Is.True);
            lastFatality = report;
            fatalities++;
        };
        var arrow = Shoot(duel, BodyRegion.Chest);
        var deadline = Time.time + 3f;
        while (duel.Opponent.CurrentHealth == 100 && Time.time < deadline) yield return null;
        Assert.That(duel.Opponent.CurrentHealth, Is.EqualTo(75));
        Assert.That(fatalities, Is.Zero);
        Assert.That(cutaway.IsVisible, Is.False);
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
        Assert.That(fatalities, Is.EqualTo(1));
        Assert.That(lastFatality.Entry.Region, Is.EqualTo(BodyRegion.Head));
        Assert.That(bow.ShotAllowed, Is.False);
        Assert.That(cutaway.IsVisible, Is.True);
        Assert.That(cutaway.Caption, Does.Contain("Head"));
        var window = cutaway.transform.Find("Fatal cutaway");
        Assert.That(window.Find("Arrow path"), Is.Not.Null);
        Assert.That(window.childCount, Is.EqualTo(2 + lastFatality.Bones.Count +
            lastFatality.Organs.Sum(h => h.Organ.Surfaces.Count)));
        foreach (var organ in lastFatality.Organs)
        {
            Assert.That(cutaway.Caption, Does.Contain(organ.Organ.DisplayName));
            Assert.That(window.Find(organ.Organ.DisplayName).GetComponent<MeshFilter>().sharedMesh,
                Is.SameAs(organ.Organ.Surfaces[0]));
        }
        var wounds = duel.Opponent.GetComponentsInChildren<ParticleSystem>();
        Assert.That(wounds.Length, Is.GreaterThan(0));
        var old = duel.Opponent;
        var rig = Find<Camera>().transform.root;
        var oldRig = rig.GetInstanceID();
        Find<VRBowInput>().SetBowHand(false);
        duel.RestartMatch();
        yield return null;
        Assert.That(old == null, Is.True);
        Assert.That(cutaway.IsVisible, Is.False);
        Assert.That(wounds.All(w => w == null), Is.True, "Restart must remove wounds still emitting on the old actor");
        Assert.That(duel.LocalArcher.CurrentHealth, Is.EqualTo(100));
        Assert.That(duel.Opponent.CurrentHealth, Is.EqualTo(100));
        Assert.That(duel.Flow.CanShoot(0), Is.True);
        Assert.That(Find<VRBowInput>().BowInLeftHand, Is.False);
        Assert.That(Find<Camera>().transform.root.GetInstanceID(), Is.EqualTo(oldRig));
        Assert.That(scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<ArrowFlight>()).Count(), Is.Zero);
        Assert.That(Find<TextMesh>().text, Does.Contain("You 100"));
        Assert.That(duel.Opponent.GetComponent<AIArcher>().ShotsFired, Is.Zero);

        var neckArrow = Shoot(duel, BodyRegion.Neck);
        deadline = Time.time + 3f;
        while (duel.Flow.Phase != DuelPhase.Finished && Time.time < deadline) yield return null;
        Assert.That(duel.Flow.Phase, Is.EqualTo(DuelPhase.Finished));
        Assert.That(neckArrow.transform.parent.GetComponent<BodyHitRegion>().Region, Is.EqualTo(BodyRegion.Neck));
        Assert.That(duel.Opponent.IsDefeated, Is.True);
        Assert.That(results, Is.EqualTo(2));
        Assert.That(fatalities, Is.EqualTo(2));
        Assert.That(lastFatality.Entry.Region, Is.EqualTo(BodyRegion.Neck));
        Assert.That(cutaway.IsVisible, Is.True);
        yield return new WaitForSecondsRealtime(3.1f);
        Assert.That(cutaway.IsVisible, Is.False);
        Assert.That(duel.Flow.Phase, Is.EqualTo(DuelPhase.Finished));
    }

    // Plays a sided injury and an AI victory in the saved scene, then restarts through the player's keyboard binding.
    [UnityTest]
    public IEnumerator AIWinsAgainstLocalArcherAndPlayerCanRestart()
    {
        var duel = Find<LocalDuel>();
        var ai = duel.Opponent.GetComponent<AIArcher>();
        var settings = new UnityEditor.SerializedObject(ai);
        settings.FindProperty("aimError").floatValue = 0f;
        settings.ApplyModifiedPropertiesWithoutUndo();
        duel.LocalArcher.TakeDamage(duel.LocalArcher.CurrentHealth - 1);
        var victories = 0;
        var defeatedArcher = -1;
        var legRecovered = false;
        duel.Flow.RecoveryRequested += (target, _) => legRecovered |= target == 1;
        duel.Flow.MatchFinished += winner =>
        {
            Assert.That(winner, Is.EqualTo(1));
            victories++;
        };
        duel.Flow.FatalityRequested += (target, _) => defeatedArcher = target;

        var arrow = Shoot(duel, BodyRegion.LeftLeg);
        var deadline = Time.time + 32f;
        while (duel.Flow.Phase != DuelPhase.Finished && Time.time < deadline) yield return null;
        yield return null;
        Assert.That(arrow.GetComponentInParent<BodyHitRegion>().Region, Is.EqualTo(BodyRegion.LeftLeg));
        Assert.That(legRecovered, Is.True);
        Assert.That(ai.ShotsFired, Is.EqualTo(1));
        Assert.That(victories, Is.EqualTo(1));
        Assert.That(defeatedArcher, Is.Zero);
        Assert.That(duel.LocalArcher.IsDefeated, Is.True);
        Assert.That(Find<TextMesh>().text, Does.Contain("DEFEAT"));
        Assert.That(Find<Bow>().ShotAllowed, Is.False);

        var focus = InputSystem.settings.editorInputBehaviorInPlayMode;
        var background = InputSystem.settings.backgroundBehavior;
        var keyboard = InputSystem.GetDevice<Keyboard>();
        var added = keyboard == null;
        if (added) keyboard = InputSystem.AddDevice<Keyboard>();
        var wasEnabled = keyboard.enabled;
        var originalVolume = AudioListener.volume;
        var savedVolume = PlayerPrefs.GetFloat("BowMan.DuelVolume", -1f);
        try
        {
            // Batch Mode has no focused Game view to receive keyboard events.
            InputSystem.settings.editorInputBehaviorInPlayMode =
                InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.EnableDevice(keyboard);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
            AudioListener.volume = 1f;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.V));
            yield return null;
            yield return null;
            Assert.That(AudioListener.volume, Is.Zero);
            Assert.That(PlayerPrefs.GetFloat("BowMan.DuelVolume"), Is.Zero);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.R));
            yield return null;
            yield return null;
            Assert.That(duel.Flow.CanShoot(0), Is.True);
            Assert.That(duel.LocalArcher.CurrentHealth, Is.EqualTo(100));
            Assert.That(duel.Opponent.CurrentHealth, Is.EqualTo(100));
            Assert.That(Find<TextMesh>().text, Does.Contain("Your turn"));
            Assert.That(AudioListener.volume, Is.Zero, "The next round preserves mute");
        }
        finally
        {
            AudioListener.volume = originalVolume;
            if (savedVolume < 0f) PlayerPrefs.DeleteKey("BowMan.DuelVolume");
            else PlayerPrefs.SetFloat("BowMan.DuelVolume", savedVolume);
            PlayerPrefs.Save();
            if (added) InputSystem.RemoveDevice(keyboard);
            else
            {
                InputSystem.ResetDevice(keyboard);
                if (!wasEnabled) InputSystem.DisableDevice(keyboard);
            }
            InputSystem.settings.backgroundBehavior = background;
            InputSystem.settings.editorInputBehaviorInPlayMode = focus;
        }
    }

    // Runs all fifteen region cases through the saved PvE bow, wound, death and living recovery gates.
    [UnityTest]
    public IEnumerator EveryRegionResolvesInThePlayablePvEScene()
    {
        var duel = Find<LocalDuel>();
        var failures = new List<string>();
        foreach (BodyRegion region in System.Enum.GetValues(typeof(BodyRegion)))
        {
            duel.RestartMatch();
            yield return null;
            var arrow = Shoot(duel, region);
            var deadline = Time.time + 3f;
            while (duel.Flow.Phase == DuelPhase.Flying && Time.time < deadline) yield return null;
            var report = arrow != null ? arrow.GetComponent<ArrowAnatomy>().LastImpact : null;
            if (report == null || report.Entry.Region != region)
            {
                failures.Add($"{region} reached {report?.Entry.Region.ToString() ?? "scenery"}");
                continue;
            }
            Assert.That(arrow.transform.parent, Is.SameAs(report.Entry.Volume.transform));
            Assert.That(report.Entry.Volume.GetComponentsInChildren<ParticleSystem>(), Is.Not.Empty, region.ToString());
            var fatal = region == BodyRegion.Head || region == BodyRegion.Neck;
            Assert.That(Find<FatalCutaway>().IsVisible, Is.EqualTo(fatal), region.ToString());
            Assert.That(duel.Opponent.CurrentHealth, Is.EqualTo(fatal ? 0 : 75));
            if (fatal)
            {
                Assert.That(duel.Flow.Winner, Is.Zero);
                continue;
            }
            deadline = Time.time + 24f;
            var opponent = duel.Opponent.GetComponent<AIArcher>();
            while (!duel.Flow.CanShoot(1) && opponent.ShotsFired == 0 && Time.time < deadline) yield return null;
            Assert.That(duel.Flow.CanShoot(1) || opponent.ShotsFired == 1, Is.True, region.ToString());
            Assert.That(duel.Opponent.GetComponent<ArcherReactionAnimation>().IsInjured, Is.False, region.ToString());
        }
        Assert.That(failures, Is.Empty, string.Join(", ", failures));
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
