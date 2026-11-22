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
    // Confirms that a fresh round or rejoin leaves one archer hand on each local tracked controller.
    private static void CheckHands()
    {
        var rig = Object.FindAnyObjectByType<LocalRigBinder>();
        foreach (var controller in new[] { rig.LeftHand, rig.RightHand })
        {
            var hands = controller.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            Assert.That(hands.Length, Is.EqualTo(1));
            Assert.That(hands[0].sharedMesh.blendShapeCount, Is.EqualTo(2));
        }
    }

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

    // Compares the host-selected environment and the instantiated scenery on each seat of this round.
    private static IEnumerator SameEnvironment(PvPDuel duel, string folder, string role)
    {
        Assert.That(duel.Archer(0).name, Does.StartWith("ErikaArcher"));
        Assert.That(duel.Archer(1).name, Does.StartWith("EnglishArcher"));
        var setting = duel.Setting.ToString();
        var scenery = duel.Flow.GetComponentInChildren<DuelEnvironment>();
        Assert.That(scenery.name, Is.EqualTo(setting + "(Clone)"));
        var prefix = Path.Combine(folder, "arena-" + duel.Round + "-");
        var path = prefix + role;
        File.WriteAllText(path + ".tmp", setting);
        File.Move(path + ".tmp", path);
        var peer = prefix + (role == "host" ? "join" : "host");
        yield return Until(() => File.Exists(peer), "Peer loaded its arena");
        Assert.That(File.ReadAllText(peer), Is.EqualTo(setting));
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
        Release(bow, start, velocity.normalized);
    }

    // Releases the real bow from the supplied scene position and direction.
    private static void Release(Bow bow, Vector3 start, Vector3 direction)
    {
        bow.transform.rotation = Quaternion.LookRotation(direction);
        bow.transform.position += start - bow.RestPosition;
        bow.SetHeld(true);
        Assert.That(bow.TryBeginDraw(bow.RestPosition), Is.True);
        bow.PullTo(bow.RestPosition - direction * bow.MaximumDrawDistance);
        Assert.That(bow.Release(), Is.Not.Null);
    }

    // Waits for the other seat to see the nock, the extended string and the correct drawing fingers.
    private static IEnumerator PreviewDraw(PvPDuel duel, Bow bow, bool shooting, int shooter, string folder)
    {
        var archer = duel.Archer(shooter);
        var bowLeft = shooter == 0;
        var ready = Path.Combine(folder, "draw-" + shooter);
        if (shooting)
        {
            var input = Object.FindAnyObjectByType<VRBowInput>(FindObjectsInactive.Include);
            input.SetBowHand(bowLeft);
            var grip = archer.transform.TransformPoint(new Vector3(bowLeft ? -0.16f : 0.16f, 1.38f, 0.47f));
            bow.transform.SetPositionAndRotation(grip, archer.transform.rotation);
            bow.SetHeld(true);
            Assert.That(bow.TryBeginDraw(bow.RestPosition), Is.True);
            yield return Until(() => File.Exists(ready + "-nock"), "Peer saw resting nock");
            bow.PullTo(bow.RestPosition - bow.transform.forward * 0.52f);
            yield return Until(() => File.Exists(ready + "-pull"), "Peer saw drawn bow");
            bow.CancelDraw();
            yield return Until(() => File.Exists(ready + "-cancel"), "Peer discarded cancelled draw");
        }
        else
        {
            var action = archer.GetComponent<OpponentBowAction>();
            var rig = archer.GetComponentInChildren<Animator>();
            var equipment = archer.transform.Find("Opponent bow");
            yield return Until(() => action.IsNocked, "Remote arrow on the string");
            var creak = Object.FindAnyObjectByType<DuelAudio>().transform.Find("Draw").GetComponent<AudioSource>();
            Assert.That(creak.isPlaying, Is.False, "Resting remote nock stays quiet");
            var initial = equipment.InverseTransformPoint(action.StringPoint);
            File.WriteAllText(ready + "-nock", "");
            yield return Until(() => Vector3.Distance(initial, equipment.InverseTransformPoint(action.StringPoint)) > 0.45f,
                "Remote draw extends by half a metre");
            var thumb = rig.GetBoneTransform(bowLeft ? HumanBodyBones.RightThumbDistal : HumanBodyBones.LeftThumbDistal);
            var index = rig.GetBoneTransform(bowLeft ? HumanBodyBones.RightIndexDistal : HumanBodyBones.LeftIndexDistal);
            yield return Until(() => Vector3.Distance((thumb.position + index.position) * 0.5f, action.StringPoint) < 0.045f,
                "Remote drawing fingers meet the string", 3f);
            Assert.That(equipment.GetComponentsInChildren<ArrowFlight>(true), Is.Empty);
            yield return Until(() => creak.isPlaying && creak.volume > 0.05f, "Remote bow sounds under tension");
            File.WriteAllText(ready + "-pull", "");
            yield return Until(() => !action.IsNocked, "Remote cancellation");
            yield return Until(() => !creak.isPlaying, "Remote stretch stops on cancellation");
            File.WriteAllText(ready + "-cancel", "");
        }
    }

    // Checks turn and health replication for a ground impact and an arrow that flies outside the arena.
    private static IEnumerator MissesPassTheTurn(PvPDuel duel, Bow bow, bool host, string folder)
    {
        for (var shooter = 0; shooter < 2; shooter++)
        {
            var flow = duel.Flow;
            yield return Until(() => flow.CanShoot(shooter), "Turn before the missed shot");
            var previousShot = flow.ShotId;
            var ready = Path.Combine(folder, "miss-" + shooter);
            File.WriteAllText(ready + (host ? "-host" : "-join"), "");
            yield return Until(() => File.Exists(ready + (host ? "-join" : "-host")), "Both peers ready for miss");
            if (host == (shooter == 0))
            {
                var actor = duel.Archer(shooter).transform;
                var start = actor.TransformPoint(new Vector3(0f, 1.4f, 0.6f));
                var direction = shooter == 0 ? (actor.forward + Vector3.down).normalized : actor.right;
                Release(bow, start, direction);
            }
            yield return Until(() => flow.ShotId == previousShot + 1 && flow.CanShoot(1 - shooter),
                "One turn handoff after the missed shot");
            Assert.That(duel.Archer(0).CurrentHealth, Is.EqualTo(100));
            Assert.That(duel.Archer(1).CurrentHealth, Is.EqualTo(100));
            Assert.That(Object.FindAnyObjectByType<FatalCutaway>().IsVisible, Is.False);
        }
    }

    // Runs the full inventory across real Relay turns and compares each peer's recorded outcome.
    private static IEnumerator RegionMatrix(PvPDuel duel, Bow bow, bool host, string folder)
    {
        var role = host ? "host" : "join";
        var other = host ? "join" : "host";
        var rows = new List<string>();
        var failures = new List<string>();
        foreach (BodyRegion region in Enum.GetValues(typeof(BodyRegion)))
        {
            if (duel.Flow.Phase == DuelPhase.Finished)
            {
                var round = duel.Round;
                duel.RequestRematch();
                yield return Until(() => duel.Round > round, "Fresh matrix round");
            }
            yield return Until(() => duel.Flow.Phase == DuelPhase.Aiming, "Available turn for " + region);
            var flow = duel.Flow;
            var shooter = flow.ActiveArcher;
            var previous = flow.ShotId;
            BodyRegion? actual = null;
            string record = null;
            void Capture(int index, BodyImpact entry, bool lethal, int token)
            {
                var actor = duel.Archer(index);
                var volume = entry.Volume.transform;
                var report = actor.GetComponentsInChildren<ArrowAnatomy>()
                    .Select(a => a.LastImpact).Last(r => r != null && r.Entry.Volume == entry.Volume);
                actual = entry.Region;
                record = $"{region}/{entry.Region}/{entry.Side}/{index}/{token}/{lethal}/" +
                    $"{Vector3Int.RoundToInt(volume.InverseTransformPoint(entry.Point) * 1000f)}/" +
                    $"{string.Join(",", report.Organs.Select(h => h.Organ.DisplayName))}/" +
                    $"{string.Join(",", report.Bones.Select(h => h.Bone + (h.Fractured ? "*" : "")))}";
            }
            flow.ReactionRequested += Capture;
            try
            {
                File.WriteAllText(Path.Combine(folder, $"{region}-{role}-ready"), "");
                yield return Until(() => File.Exists(Path.Combine(folder, $"{region}-{other}-ready")), "Peer ready for " + region);
                if (host == (shooter == 0)) Fire(duel, bow, shooter, region);
                yield return Until(() => actual.HasValue || flow.ShotId > previous && flow.Phase == DuelPhase.Aiming,
                    "Outcome for " + region);
                rows.Add((record ?? region + "/miss") + $"/{duel.Archer(0).CurrentHealth}/{duel.Archer(1).CurrentHealth}");
                if (actual != region) failures.Add($"{region} reached {actual?.ToString() ?? "scenery"}");
            }
            finally { flow.ReactionRequested -= Capture; }
            yield return Until(() => flow.Phase == DuelPhase.Finished || flow.Phase == DuelPhase.Aiming, "Recovery for " + region);
        }
        var file = Path.Combine(folder, role + "-regions.txt");
        File.WriteAllLines(file + ".tmp", rows);
        File.Move(file + ".tmp", file);
        var peer = Path.Combine(folder, other + "-regions.txt");
        yield return Until(() => File.Exists(peer), "Other region matrix");
        Assert.That(File.ReadAllLines(peer), Is.EqualTo(rows));
        Assert.That(failures, Is.Empty, string.Join(", ", failures));
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
        var preferred = PlayerPrefs.GetInt("BowMan.Archer", -1);
        try
        {
            ArcherSelection.Preferred = host ? ArcherModel.Erika : ArcherModel.English;
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
            yield return SameEnvironment(duel, folder, role);
            var bow = Object.FindAnyObjectByType<Bow>();
            var hits = new List<string>();
            var sounds = Object.FindAnyObjectByType<DuelAudio>();
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
            CheckHands();
            var observer = duel.Archer(host ? 1 : 0).GetComponentInChildren<Animator>();
            var shoulder = observer.GetBoneTransform(HumanBodyBones.LeftShoulder);
            var standing = shoulder.rotation;
            yield return Until(() => Quaternion.Angle(standing, shoulder.rotation) > 0.5f,
                "Standing opponent moves on " + role, 8f);
            File.WriteAllText(Path.Combine(folder, role + "-ready"), "");
            yield return Until(() => File.Exists(Path.Combine(folder, (host ? "join" : "host") + "-ready")), "Peer entered match");
            var directInput = Object.FindAnyObjectByType<DesktopBowTest>();
            directInput.enabled = false;
            Object.FindAnyObjectByType<VRBowInput>(FindObjectsInactive.Include).enabled = false;
            var regions = new[] { BodyRegion.LeftLeg, BodyRegion.Chest, BodyRegion.Head };
            for (var shot = 0; shot < regions.Length; shot++)
            {
                var shooter = shot % 2;
                if (shot < 2 || host == (shooter == 0))
                    yield return Until(() => duel.Flow.CanShoot(shooter), "Recovered shooting turn " + shot);
                if (shot < 2) yield return PreviewDraw(duel, bow, host == (shooter == 0), shooter, folder);
                if (host == (shooter == 0))
                {
                    Fire(duel, bow, shooter, regions[shot]);
                }
                yield return Until(() => hits.Count == shot + 1, "Replicated wound " + shot);
                Assert.That(hits.Last(), Does.Contain("/" + regions[shot] + "/"));
                var cues = sounds.GetComponentsInChildren<AudioSource>();
                Assert.That(cues.Count(c => c.clip.name == "Body"), Is.EqualTo(1), "One contact on " + role);
                Assert.That(cues.Any(c => c.clip.name == "Flight" && c.isPlaying), Is.False);
                var embedded = duel.Archer(1 - shooter).GetComponentsInChildren<ArrowAnatomy>()
                    .Last(a => a.LastImpact?.Entry.Region == regions[shot]);
                Assert.That(Vector3.Distance(cues.Single(c => c.clip.name == "Body").transform.position,
                    embedded.LastImpact.Entry.Point), Is.LessThan(0.15f));
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
            CheckHands();
            Assert.That(duel.Archer(0).CurrentHealth, Is.EqualTo(100));
            Assert.That(duel.Archer(1).CurrentHealth, Is.EqualTo(100));
            Assert.That(duel.Flow.CanShoot(0), Is.True);
            Assert.That(duel.RematchRequested, Is.False);
            yield return SameEnvironment(duel, folder, role);
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
            CheckHands();
            if (Argument("-duelTestMatrix") == "all")
            {
                yield return MissesPassTheTurn(duel, bow, host, folder);
                yield return RegionMatrix(duel, bow, host, folder);
            }
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
        finally
        {
            controller.Leave();
            if (preferred < 0) PlayerPrefs.DeleteKey("BowMan.Archer");
            else PlayerPrefs.SetInt("BowMan.Archer", preferred);
            PlayerPrefs.Save();
        }
    }
}
#endif
