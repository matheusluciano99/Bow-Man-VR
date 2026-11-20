using System;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BowMan.Networking
{
    public sealed class PvPDuel : MonoBehaviour
    {
        [SerializeField] private GameObject firstPrefab;
        [SerializeField] private GameObject secondPrefab;
        [SerializeField] private ArrowFlight arrowPrefab;
        [SerializeField] private Bow bow;
        [SerializeField] private VRBowInput vrInput;
        [SerializeField] private DesktopBowTest desktop;
        [SerializeField] private VRInjuryView injuryView;
        [SerializeField] private LocalRigBinder rig;
        [SerializeField] private ArcherSpawnPoints spawns;
        [SerializeField] private PvPConnectionController connection;
        [SerializeField] private TextMesh feedback;

        private readonly ArcherHealth[] actors = new ArcherHealth[2];
        private Transform match;
        private FatalCutaway cutaway;
        private OpponentBowAction opponentBow;
        private ArrowFlight visualArrow;
        private DuelAudio sounds;
        private Vector3 visualOrigin, visualVelocity;
        private float launchedAt;
        private int displayedShot = -1;
        private string resolvedImpact;
        private bool awaitingRelease;
        private bool desktopMode;
        private int round;
        private InputAction switchHand;
        private InputAction rematch;
        private int votes;
        private PlayerPresence[] presences = Array.Empty<PlayerPresence>();

        public DuelTurnFlow Flow { get; private set; }
        public int Round => round;
        public ArenaSetting Setting { get; private set; }
        public bool RematchRequested => (votes & (1 << LocalIndex)) != 0;
        // Returns the actor occupying a fixed seat during this round.
        public ArcherHealth Archer(int index) => actors[index];
        private NetworkManager Net => connection.GetComponent<NetworkManager>();
        private PlayerPresence Seat => Net.LocalClient.PlayerObject.GetComponent<PlayerPresence>();
        private int LocalIndex => Net.IsServer ? 0 : 1;

        // Keeps the connection lobby usable until the joiner confirms its assigned seat.
        private void Start()
        {
            rematch = new InputAction("Rematch", InputActionType.Button, "<XRController>{RightHand}/primaryButton");
            rematch.AddBinding("<Keyboard>/r");
            rematch.Enable();
            switchHand = new InputAction("Switch bow hand", InputActionType.Button, "<XRController>{RightHand}/secondaryButton");
            switchHand.AddBinding("<Keyboard>/h");
            switchHand.Enable();
            cutaway = rig.Head.gameObject.AddComponent<FatalCutaway>();
            bow.SetShotAllowed(false);
            bow.gameObject.SetActive(false);
            bow.Fired += Released;
            connection.StatusChanged += ConnectionChanged;
            vrInput.SetBowHand(VRBowInput.PreferredBowLeft);
            desktopMode = DesktopXR.Requested(Environment.GetCommandLineArgs());
        }

        // Starts the first round only for the admitted second player.
        public void PeerReady(ulong peer)
        {
            if (!Net.IsServer || match != null || connection.Seats.RoleOf(peer) != ArcherRole.Archer2) return;
            Begin(round + 1, DuelEnvironment.Choose());
            Seat.BeginRpc(round, Setting);
        }

        // Creates identical actor slots on both peers and gives collision and animation authority to the host.
        public void Begin(int newRound, ArenaSetting setting)
        {
            if (newRound <= round && match != null) return;
            EndRound();
            round = newRound;
            Setting = setting;
            presences = FindObjectsByType<PlayerPresence>(FindObjectsSortMode.None);
            match = new GameObject("Online round").transform;
            match.SetParent(transform, false);
            DuelEnvironment.Place(setting, match, spawns.For(ArcherRole.Archer1), spawns.For(ArcherRole.Archer2));
            for (var i = 0; i < actors.Length; i++)
            {
                var spawn = spawns.For(i == 0 ? ArcherRole.Archer1 : ArcherRole.Archer2);
                var go = Instantiate(i == 0 ? firstPrefab : secondPrefab, spawn.position, spawn.rotation, match);
                actors[i] = go.GetComponent<ArcherHealth>() ?? go.AddComponent<ArcherHealth>();
            }
            opponentBow = actors[1 - LocalIndex].gameObject.AddComponent<OpponentBowAction>();
            opponentBow.Bind(bow, arrowPrefab);
            foreach (var presence in presences) presence.SetMarkersVisible(false);
            var local = actors[LocalIndex];
            foreach (var renderer in local.GetComponentsInChildren<Renderer>()) renderer.enabled = false;
            Flow = match.gameObject.AddComponent<DuelTurnFlow>();
            Flow.Initialize(actors[0], actors[1], replica: !Net.IsServer);
            for (var i = 0; i < actors.Length; i++)
                actors[i].GetComponent<ArcherReactionAnimation>().Connect(Flow, i, actors[i]);
            sounds = match.gameObject.AddComponent<DuelAudio>();
            sounds.Bind(Flow, bow, opponentBow, local, actors[1 - LocalIndex], setting, LocalIndex);
            Flow.Changed += Changed;
            Flow.ReactionRequested += React;
            Flow.FatalityRequested += Fatal;
            var eyes = local.GetComponentInChildren<Animator>().GetBoneTransform(HumanBodyBones.Head).Find("EyeAnchor");
            injuryView.Bind(local.transform, eyes, local);
            bow.gameObject.SetActive(true);
            vrInput.ShowArcherHands();
            bow.SetOwner(local.transform);
            desktop.PlaceStand(local.transform.TransformPoint(new Vector3(-0.45f, 1.3f, 0.6f)), local.transform.rotation);
            desktop.UseDesktopControls(desktopMode);
            GateBow();
        }

        // Cancels the round when a peer leaves or the transport goes offline.
        private void ConnectionChanged(ConnectionStatus status)
        {
            if (status.Phase != ConnectionPhase.Connected && match != null) EndRound();
        }

        // Removes round-owned state, arrows and actors and restores the seated player's tracking origin.
        private void EndRound()
        {
            if (Flow != null)
            {
                Flow.Changed -= Changed;
                Flow.ReactionRequested -= React;
                Flow.FatalityRequested -= Fatal;
            }
            foreach (var presence in presences)
                if (presence != null && presence.IsSpawned) presence.SetMarkersVisible(!presence.IsOwner);
            opponentBow = null;
            Flow = null;
            if (match != null) { match.gameObject.SetActive(false); Destroy(match.gameObject); }
            match = null;
            visualArrow = null;
            resolvedImpact = null;
            displayedShot = -1;
            votes = 0;
            awaitingRelease = false;
            if (cutaway != null) cutaway.Clear();
            if (injuryView != null) injuryView.ResetView();
            if (bow != null) { bow.SetShotAllowed(false); bow.gameObject.SetActive(false); }
        }

        // Offers a rematch from the finished result, preserving the current result until both players accept.
        public void RequestRematch()
        {
            if (Flow == null || Flow.Phase != DuelPhase.Finished || RematchRequested) return;
            if (Net.IsServer) ReceiveRematch(Net.LocalClientId, round);
            else Seat.RematchRpc(round);
        }

        // Counts each admitted seat once and replaces the round only after the second vote.
        public void ReceiveRematch(ulong peer, int requestedRound)
        {
            if (!Net.IsServer || Flow == null || Flow.Phase != DuelPhase.Finished || requestedRound != round) return;
            var role = connection.Seats.RoleOf(peer);
            if (role == ArcherRole.None) return;
            votes |= role == ArcherRole.Archer1 ? 1 : 2;
            if (votes == 3)
            {
                Begin(round + 1, DuelEnvironment.Choose());
                Seat.BeginRpc(round, Setting);
            }
            else Seat.VotesRpc(round, votes);
        }

        // Applies the host's votes only to the same finished round.
        public void ShowVotes(int receivedRound, int ready)
        {
            if (Flow != null && receivedRound == round && Flow.Phase == DuelPhase.Finished) votes = ready;
        }

        // Submits a real bow release; a joiner waits for host approval before displaying flight.
        private void Released(ArrowFlight arrow)
        {
            if (Flow == null || !Flow.CanShoot(LocalIndex)) { Destroy(arrow.gameObject); return; }
            if (Net.IsServer) Track(0, arrow);
            else
            {
                awaitingRelease = true;
                GateBow();
                Seat.ReleaseRpc(round, Flow.ShotId, arrow.transform.position, arrow.Velocity.normalized, arrow.ShotStrength);
                arrow.gameObject.SetActive(false);
                Destroy(arrow.gameObject);
            }
        }

        // Accepts a single remote release for the current turn inside the player's reachable play area.
        public void ReceiveRelease(ulong peer, int requestedRound, int turn, Vector3 origin, Vector3 direction, float strength)
        {
            if (!Net.IsServer || Flow == null || connection.Seats.RoleOf(peer) != ArcherRole.Archer2) return;
            if (requestedRound != round || turn != Flow.ShotId || !Flow.CanShoot(1) ||
                !ValidRelease(actors[1].transform.position, origin, direction, strength))
            {
                SendState();
                return;
            }
            var arrow = Instantiate(arrowPrefab, origin, Quaternion.identity, match);
            arrow.Launch(direction.normalized * (bow.MaximumLaunchSpeed * strength), actors[1].transform, shotStrength: strength);
            Track(1, arrow);
        }

        // Bounds a peer's release sample before passing it to Unity physics.
        public static bool ValidRelease(Vector3 actor, Vector3 origin, Vector3 direction, float strength) =>
            strength > 0f && strength <= 1f && direction.sqrMagnitude >= 0.98f && direction.sqrMagnitude <= 1.02f &&
            (origin - actor).sqrMagnitude <= 16f;

        // Sends the accepted launch and captures anatomy before reaction sampling moves its attachment frames.
        private void Track(int shooter, ArrowFlight arrow)
        {
            arrow.transform.SetParent(match, true);
            var token = Flow.ShotId + 1;
            arrow.Hit += hit =>
            {
                if (!hit.collider.TryGetComponent<BodyHitRegion>(out _))
                    Seat.SceneryRpc(round, token, hit.point, arrow.transform.rotation, arrow.SceneryLifetime, sounds.Surface(hit.collider));
            };
            if (!Flow.TryTrackShot(shooter, arrow)) { Destroy(arrow.gameObject); return; }
            if (shooter != LocalIndex) opponentBow.Release();
            resolvedImpact = null;
            arrow.GetComponent<ArrowAnatomy>().Resolved += report =>
                resolvedImpact = JsonUtility.ToJson(ImpactReplay.Capture(actors[1 - shooter], report));
            Seat.FlightRpc(round, Flow.ShotId, arrow.transform.position, arrow.Velocity);
        }

        // Creates a visual-only arrow for the host-approved launch on the observing client.
        public void ShowFlight(int receivedRound, int shot, Vector3 origin, Vector3 velocity)
        {
            if (Flow == null || receivedRound != round || shot != Flow.ShotId || visualArrow != null) return;
            if (Flow.ActiveArcher != LocalIndex) opponentBow.Release();
            visualArrow = Instantiate(arrowPrefab, origin, Quaternion.LookRotation(velocity), match);
            sounds.Flight(shot, visualArrow);
            visualOrigin = origin;
            visualVelocity = velocity;
            launchedAt = Time.time;
        }

        // Keeps the scenery arrow at the host's contact while the turn passes to the other seat.
        public void ShowScenery(int receivedRound, int shot, Vector3 point, Quaternion rotation, float lifetime, ImpactSound surface)
        {
            if (Flow == null || receivedRound != round || shot != Flow.ShotId || visualArrow == null) return;
            sounds.Impact(point, surface);
            visualArrow.transform.SetPositionAndRotation(point, rotation);
            Destroy(visualArrow.gameObject, lifetime);
            visualArrow = null;
        }

        // Applies authoritative health and animation gates on the observing client.
        public void ReceiveState(int receivedRound, int shot, DuelPhase phase, int active, int winner, int hp1, int hp2)
        {
            if (Flow == null || receivedRound != round || shot < Flow.ShotId) return;
            awaitingRelease = false;
            Flow.ApplyReplicaState(phase, active, winner, shot, hp1, hp2);
            if (phase == DuelPhase.Aiming && visualArrow != null)
            {
                Destroy(visualArrow.gameObject);
                visualArrow = null;
            }
        }

        // Attaches the visual arrow and plays the same wound, anatomy and region selected on the host.
        public void ReceiveImpact(int receivedRound, int shot, string snapshot, int hp1, int hp2)
        {
            if (Flow == null || receivedRound != round || shot != Flow.ShotId || displayedShot == shot) return;
            displayedShot = shot;
            var report = JsonUtility.FromJson<ImpactReplay>(snapshot).Restore(actors[1 - Flow.ActiveArcher]);
            Flow.ApplyReplicaState(Flow.Phase, Flow.ActiveArcher, Flow.Winner, shot, hp1, hp2);
            if (visualArrow == null) visualArrow = Instantiate(arrowPrefab, match);
            visualArrow.GetComponent<ArrowAnatomy>().SetReport(report);
            sounds.Impact(report.Entry.Point, ImpactSound.Body);
            visualArrow.Embed(report.Entry);
            visualArrow = null;
            Flow.ApplyReplicaImpact(shot, report);
        }

        // Publishes health and the injury token after authority has resolved damage.
        private void React(int target, BodyImpact impact, bool lethal, int shot)
        {
            if (Net.IsServer) Seat.ImpactRpc(round, shot, resolvedImpact, actors[0].CurrentHealth, actors[1].CurrentHealth);
        }

        // Presents internal feedback exclusively for the confirmed lethal arrow.
        private void Fatal(int target, AnatomicalImpact report) => cutaway.Show(report);

        // Publishes authoritative transitions and updates the local bow whenever the turn changes.
        private void Changed()
        {
            if (Net.IsServer) SendState();
            GateBow();
        }

        // Sends one complete phase snapshot, including both hit-point values.
        private void SendState() => Seat.StateRpc(round, Flow.ShotId, Flow.Phase, Flow.ActiveArcher,
            Flow.Winner, actors[0].CurrentHealth, actors[1].CurrentHealth);

        // Allows input only on the local seat's recovered turn and while no release awaits acknowledgment.
        private void GateBow() => bow.SetShotAllowed(!awaitingRelease && Flow.CanShoot(LocalIndex));

        // Captures the owner's actual handedness, string displacement and equipped bow during this turn.
        internal BowPose LocalBowPose()
        {
            var pose = new BowPose { Round = round, Turn = Flow.ShotId, Left = vrInput.BowInLeftHand, Strength = -1f };
            if (!Flow.CanShoot(LocalIndex) || !bow.IsDrawing) return pose;
            pose.Strength = bow.DrawStrength;
            pose.Grip = bow.transform.position;
            pose.Rotation = bow.transform.rotation;
            pose.Pull = bow.StringPosition - bow.RestPosition;
            return pose;
        }

        // Applies only current, reachable draw samples from the other seat.
        private void ShowDraw(BowPose pose, int index, Vector3 actor)
        {
            if (pose.Round != round || pose.Turn != Flow.ShotId || !Flow.CanShoot(index))
            {
                opponentBow.Rest();
                return;
            }
            var rotationLength = Quaternion.Dot(pose.Rotation, pose.Rotation);
            var valid = pose.Strength >= 0f && pose.Strength <= 1f && (pose.Grip - actor).sqrMagnitude <= 16f &&
                rotationLength >= 0.99f && rotationLength <= 1.01f && pose.Pull.magnitude <= bow.MaximumDrawDistance + 0.001f;
            opponentBow.PreparePlayer(pose.Left, valid ? pose.Strength : -1f, pose.Grip, pose.Rotation, pose.Pull);
        }

        // Updates observer flight, controller shortcuts, tracked targets and the two-player match HUD.
        private void Update()
        {
            if (Flow == null) return;
            if (rematch.WasPressedThisFrame()) RequestRematch();
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.tabKey.wasPressedThisFrame)
            {
                desktopMode = !desktopMode;
                desktop.UseDesktopControls(desktopMode);
            }
            if (switchHand.WasPressedThisFrame() && Flow.CanShoot(LocalIndex))
                vrInput.SetBowHand(!vrInput.BowInLeftHand);
            if (visualArrow != null)
            {
                var seconds = Time.time - launchedAt;
                var velocity = visualVelocity + Physics.gravity * seconds;
                visualArrow.transform.SetPositionAndRotation(visualOrigin + visualVelocity * seconds +
                    Physics.gravity * (0.5f * seconds * seconds), Quaternion.LookRotation(velocity));
            }
            foreach (var presence in presences)
            {
                if (presence == null || !presence.IsSpawned || presence.Role.Value == ArcherRole.None) continue;
                var i = presence.Role.Value == ArcherRole.Archer1 ? 0 : 1;
                var actor = actors[i];
                if (actor.IsDefeated || actor.GetComponent<ArcherReactionAnimation>().IsInjured) continue;
                var pos = presence.TrackedHead;
                var spawn = spawns.For(presence.Role.Value).position;
                pos.y = spawn.y;
                if ((pos - spawn).sqrMagnitude <= 9f) actor.transform.position = pos;
                if (i != LocalIndex) ShowDraw(presence.Draw, i, actor.transform.position);
            }
            var status = Flow.StatusFor(LocalIndex);
            feedback.text = $"{status}    You {actors[LocalIndex].CurrentHealth}  |  Opponent {actors[1 - LocalIndex].CurrentHealth}    {sounds.VolumeHint}\n" +
                (Flow.Phase == DuelPhase.Finished ? (RematchRequested ? "Waiting for opponent" :
                    votes != 0 ? "Opponent wants a rematch — A / R: accept" : "A / R: rematch") +
                    (cutaway.IsVisible ? "\n" + cutaway.Caption : "") : Flow.CanShoot(LocalIndex) ? vrInput.ShotHint(desktopMode) :
                    "Your bow unlocks on your turn");
        }

        // Releases the match and connection listeners when leaving the network scene.
        private void OnDestroy()
        {
            rematch?.Dispose();
            switchHand?.Dispose();
            if (connection != null) connection.StatusChanged -= ConnectionChanged;
            if (bow != null) bow.Fired -= Released;
            EndRound();
            if (cutaway != null) Destroy(cutaway);
        }
    }
}
