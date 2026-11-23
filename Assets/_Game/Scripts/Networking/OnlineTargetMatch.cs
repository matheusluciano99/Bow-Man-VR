using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;

namespace BowMan.Networking
{
    // Scores ten alternating arrows per archer, locally against the CPU or through the Relay host.
    public sealed class OnlineTargetMatch : MonoBehaviour
    {
        public const int ArrowsPerSeat = 10;
        public const int DefaultDistance = 15;
        public const float TargetLaunchSpeed = 32f;
        public static readonly System.Collections.ObjectModel.ReadOnlyCollection<int> Distances =
            System.Array.AsReadOnly(new[] { DefaultDistance, 30, 50, 60, 70 });
        public int HostDistance { get; private set; } = DefaultDistance;
        public int RoundDistance { get; private set; } = DefaultDistance;
        [SerializeField] private PvPConnectionController connection;
        [SerializeField] private Bow bow;
        [SerializeField] private VRBowInput vrInput;
        [SerializeField] private DesktopBowTest desktop;
        [SerializeField] private ArrowFlight arrowPrefab;
        [SerializeField] private Transform stand;
        [SerializeField] private Collider face;
        [SerializeField] private TextMesh feedback;

        [SerializeField] private GameObject firstPrefab, secondPrefab;
        [SerializeField] private LocalRigBinder rig;
        private OpponentBowAction cpuBow;
        public bool AgainstCpu { get; private set; }
        private Transform match;
        private DuelAudio sounds;
        private TargetImpactDisplay impactDisplay;
        private ArrowFlight pending, replica;
        private Vector3 origin, velocity;
        private float launchTime;
        private InputAction hand, rematch;
        private bool awaiting, flying, desktopMode;
        private int votes, last;
        private Vector2 lastContact;
        public int Round { get; private set; }
        public int Completed { get; private set; }
        public int FirstScore { get; private set; }
        public int SecondScore { get; private set; }
        public bool Playing => match != null;
        public bool Finished => Completed == 2 * ArrowsPerSeat;
        private NetworkManager Net => connection.GetComponent<NetworkManager>();
        private int LocalSeat => AgainstCpu || Net.IsServer ? 0 : 1;
        private PlayerPresence Seat => Net.LocalClient.PlayerObject.GetComponent<PlayerPresence>();

        // Moves the entire target assembly down the common firing line without resizing the rings.
        public static void PlaceTarget(Transform firingLine, Transform scoringFace, int metres)
        {
            scoringFace.parent.position = firingLine.position + firingLine.forward * metres;
        }

        // Changes the proposed course only while the local player can still host a new competition.
        public void ChooseHostDistance(int metres)
        {
            if (Playing || !connection.Status.CanStart || !Distances.Contains(metres)) return;
            HostDistance = metres;
            PlaceTarget(stand, face.transform, metres);
        }

        // Allocates controller shortcuts and keeps the bow locked until the second player arrives.
        private void Start()
        {
            hand = new InputAction("Bow hand", binding: "<XRController>{RightHand}/secondaryButton");
            if (DesktopBowTest.DesktopAvailable) hand.AddBinding("<Keyboard>/h");
            hand.AddBinding("<WebXRController>{RightHand}/buttonB");
            hand.Enable();
            rematch = new InputAction("Rematch", binding: "<XRController>{RightHand}/primaryButton");
            if (DesktopBowTest.DesktopAvailable) rematch.AddBinding("<Keyboard>/r");
            rematch.AddBinding("<WebXRController>{RightHand}/buttonA");
            rematch.Enable();
            bow.SetMaximumLaunchSpeed(TargetLaunchSpeed);
            desktopMode = DesktopBowTest.DesktopAvailable && !VRBowInput.HeadsetActive;
            vrInput.SetBowHand(VRBowInput.PreferredBowLeft);
            bow.SetShotAllowed(false);
            bow.gameObject.SetActive(false);
            bow.Fired += Released;
            connection.StatusChanged += ConnectionChanged;
        }

        // Seats the player at the firing line and starts a local competition at the chosen distance.
        public void StartCpuMatch()
        {
            if (Playing || !connection.Status.CanStart) return;
            AgainstCpu = true;
            rig.PlaceAt(ArcherRole.Archer1);
            NewRound();
        }

        // Starts a clean scorecard once the admitted opponent has its network player.
        public void PeerReady(ulong peer)
        {
            if (Net.IsServer && !Playing && connection.Seats.RoleOf(peer) == ArcherRole.Archer2) NewRound();
        }

        // Broadcasts a fresh twenty-shot competition, including a new identifier for stale releases.
        private void NewRound()
        {
            ApplyScore(Round + 1, 0, 0, 0, -1, false, 0, HostDistance, Vector2.zero);
            Publish();
        }

        // Restores the lobby when a seat leaves, including during an unresolved shot.
        private void ConnectionChanged(ConnectionStatus status)
        {
            if (Playing && status.Phase != ConnectionPhase.Connected)
            {
                ClearRound();
                PlaceTarget(stand, face.transform, HostDistance);
            }
        }

        // Discards remaining arrows and pending input without moving the headset.
        private void ClearRound()
        {
            bow.SetShotAllowed(false);
            bow.gameObject.SetActive(false);
            StopAllCoroutines();
            if (pending != null) { pending.Hit -= Hit; pending.Expired -= Expired; }
            pending = replica = null;
            if (match != null) { match.gameObject.SetActive(false); Destroy(match.gameObject); }
            if (impactDisplay != null) Destroy(impactDisplay.gameObject);
            match = null;
            awaiting = flying = false;
        }

        // Applies one host snapshot and unlocks input only for the seat entitled to the next arrow.
        public void ApplyScore(int round, int completed, int first, int second, int points, bool inFlight, int ready, int metres, Vector2 contact)
        {
            if (!Distances.Contains(metres)) return;
            if (Playing && (round < Round || (round == Round && completed < Completed))) return;
            if (!Playing || Round != round)
            {
                ClearRound();
                RoundDistance = metres;
                PlaceTarget(stand, face.transform, metres);
                match = new GameObject("Target match").transform;
                match.SetParent(transform, false);
                bow.gameObject.SetActive(true);
                vrInput.ShowArcherHands();
                desktop.PlaceStand(stand.TransformPoint(new Vector3(-0.45f, 1.3f, 0.6f)), stand.rotation);
                desktop.UseDesktopControls(desktopMode);
                impactDisplay = new GameObject("Target impact inset").AddComponent<TargetImpactDisplay>();
                impactDisplay.Build(feedback.transform.parent, face.transform);
                sounds = match.gameObject.AddComponent<DuelAudio>();
                if (AgainstCpu)
                {
                    var player = ArcherSelection.Preferred;
                    var opponent = ArcherSelection.Other(player);
                    var prefab = opponent == ArcherModel.English ? firstPrefab : secondPrefab;
                    var position = stand.position + stand.right * 1.5f;
                    var facing = Quaternion.LookRotation(Vector3.ProjectOnPlane(face.transform.position - position, Vector3.up));
                    var actor = Instantiate(prefab, position, facing, match);
                    var health = actor.GetComponent<ArcherHealth>() ?? actor.AddComponent<ArcherHealth>();
                    actor.GetComponent<ArcherReactionAnimation>().PrepareStanding(health);
                    cpuBow = actor.AddComponent<OpponentBowAction>();
                    cpuBow.Bind(bow, arrowPrefab);
                    foreach (var volume in actor.GetComponentsInChildren<Collider>()) volume.enabled = false;
                    sounds.BindBow(bow, ArenaSetting.WoodlandRange, player, opponent);
                }
                else sounds.BindBow(bow, ArenaSetting.WoodlandRange, connection.FirstArcher, connection.SecondArcher);
            }
            if (replica != null && !inFlight) { Destroy(replica.gameObject); replica = null; }
            Round = round;
            Completed = completed;
            FirstScore = first;
            SecondScore = second;
            last = points;
            lastContact = contact;
            impactDisplay.Mark(points > 0 ? (Vector2?)contact : null);
            votes = ready;
            awaiting = false;
            flying = inFlight;
            GateBow();
        }

        // Sends the complete authoritative scorecard including any in-flight lock.
        private void Publish()
        {
            if (!AgainstCpu) Seat.TargetScoreRpc(Round, Completed, FirstScore, SecondScore, last, flying, votes, RoundDistance, lastContact);
        }

        // Unlocks the shared bow input only while this peer owns the next release.
        private void GateBow() => bow.SetShotAllowed(Playing && !Finished && !flying && !awaiting && Completed % 2 == LocalSeat);

        // Submits the joining archer's launch parameters or accepts the host's physical arrow.
        private void Released(ArrowFlight arrow)
        {
            if (!Playing || Finished || flying || awaiting || Completed % 2 != LocalSeat) { Destroy(arrow.gameObject); return; }
            if (AgainstCpu || Net.IsServer) Track(arrow);
            else
            {
                awaiting = true;
                GateBow();
                Seat.ReleaseRpc(Round, Completed, arrow.transform.position, arrow.Velocity.normalized, arrow.ShotStrength);
                arrow.gameObject.SetActive(false);
                Destroy(arrow.gameObject);
            }
        }

        // Validates the remote seat, current turn and release geometry before running host physics.
        public void ReceiveRelease(ulong peer, int round, int turn, Vector3 start, Vector3 direction, float strength)
        {
            if (!Net.IsServer || !Playing || connection.Seats.RoleOf(peer) != ArcherRole.Archer2) return;
            if (round != Round || turn != Completed || turn % 2 != 1 || flying || Finished ||
                !PvPDuel.ValidRelease(stand.position, start, direction, strength))
            {
                Publish();
                return;
            }
            var arrow = Instantiate(arrowPrefab, start, Quaternion.identity, match);
            arrow.Launch(direction.normalized * (bow.MaximumLaunchSpeed * strength), shotStrength: strength);
            Track(arrow);
        }

        // Locks the turn until first contact or expiry and shows the accepted launch to both seats.
        private void Track(ArrowFlight arrow)
        {
            pending = arrow;
            arrow.transform.SetParent(match, true);
            arrow.Hit += Hit;
            arrow.Expired += Expired;
            flying = true;
            GateBow();
            sounds.Flight(Completed, arrow);
            if (!AgainstCpu) Seat.FlightRpc(Round, Completed, arrow.transform.position, arrow.Velocity);
        }

        // Scores the painted disc in face-local coordinates, awarding a dividing line to its higher ring.
        public static int RingScore(Vector2 point)
        {
            float rings = point.magnitude * 10f / 0.48f;
            return rings > 10.00001f ? 0 : Mathf.Clamp(11 - Mathf.CeilToInt(rings - 0.00001f), 1, 10);
        }

        // Reports the exact impact and awards zero outside the scoring disc.
        private void Hit(RaycastHit hit)
        {
            var onFace = hit.collider == face;
            var surface = onFace ? ImpactSound.Wood : sounds.Surface(hit.collider);
            sounds.Impact(hit.point, surface);
            if (!AgainstCpu) Seat.SceneryRpc(Round, Completed, hit.point, pending.transform.rotation, pending.SceneryLifetime, surface);
            Vector2 contact = face.transform.InverseTransformPoint(hit.point);
            Settle(onFace ? RingScore(contact) : 0, contact);
        }

        // Counts an arrow that flew out of the range as a miss.
        private void Expired() => Settle(0);

        // Charges one shot to its owner and publishes both totals before the next release.
        private void Settle(int points, Vector2 contact = default)
        {
            pending.Hit -= Hit;
            pending.Expired -= Expired;
            pending = null;
            if (Completed % 2 == 0) FirstScore += points;
            else SecondScore += points;
            Completed++;
            flying = false;
            last = points;
            lastContact = contact;
            impactDisplay.Mark(points > 0 ? (Vector2?)contact : null);
            GateBow();
            Publish();
            if (AgainstCpu && !Finished && Completed % 2 == 1) StartCoroutine(ShootCpuArrow());
        }

        // Plays the rival's recorded draw, then launches one physical arrow toward a varied point near the bullseye.
        private System.Collections.IEnumerator ShootCpuArrow()
        {
            var error = Random.insideUnitCircle * (.32f + RoundDistance * .012f);
            var target = face.bounds.center + stand.right * error.x + stand.up * error.y;
            var shoulder = cpuBow.transform.position + stand.up * 1.45f;
            ArrowBallistics.TryVelocity(shoulder, target, TargetLaunchSpeed, out var aim);
            const float drawTime = 1.6f;
            for (float elapsed = 0; elapsed < drawTime; elapsed += Time.deltaTime)
            {
                cpuBow.PrepareAI(Mathf.Clamp01(elapsed / (drawTime * .85f)), aim.normalized);
                yield return null;
            }
            var origin = cpuBow.ArrowTip;
            ArrowBallistics.TryVelocity(origin, target, TargetLaunchSpeed, out var speed);
            var arrow = Instantiate(arrowPrefab, origin, Quaternion.LookRotation(speed), match);
            cpuBow.Release();
            arrow.Launch(speed, cpuBow.transform);
            Track(arrow);
        }

        // Replays the host-approved trajectory without any client-side collision scoring.
        public void ShowFlight(int round, int shot, Vector3 start, Vector3 speed)
        {
            if (!Playing || round != Round || shot != Completed || replica != null) return;
            flying = true;
            GateBow();
            origin = start;
            velocity = speed;
            launchTime = Time.time;
            replica = Instantiate(arrowPrefab, origin, Quaternion.LookRotation(speed), match);
            sounds.Flight(shot, replica);
        }

        // Stops the visible arrow at the same point and orientation as the host's impact.
        public void ShowContact(int round, int shot, Vector3 point, Quaternion rotation, float lifetime, ImpactSound surface)
        {
            if (round != Round || shot != Completed || replica == null) return;
            replica.transform.SetPositionAndRotation(point, rotation);
            sounds.Impact(point, surface);
            Destroy(replica.gameObject, lifetime);
            replica = null;
        }

        // Requests a new competition after this archer has seen the final result.
        public void RequestRematch()
        {
            if (!Playing || !Finished) return;
            if (AgainstCpu) NewRound();
            else if (Net.IsServer) ReceiveRematch(Net.LocalClientId, Round);
            else Seat.RematchRpc(Round);
        }

        // Keeps the final scores until both authenticated archers accept a rematch.
        public void ReceiveRematch(ulong peer, int round)
        {
            if (!Net.IsServer || !Playing || !Finished || round != Round) return;
            var role = connection.Seats.RoleOf(peer);
            if (role == ArcherRole.None) return;
            votes |= role == ArcherRole.Archer1 ? 1 : 2;
            if (votes == 3) NewRound();
            else Publish();
        }

        // Updates the observer projectile, local controls and the synchronized match HUD.
        private void Update()
        {
            if (!Playing) return;
            if (rematch.WasPressedThisFrame()) RequestRematch();
            if (DesktopBowTest.DesktopAvailable && Keyboard.current != null && Keyboard.current.tabKey.wasPressedThisFrame)
            {
                desktopMode = !desktopMode;
                desktop.UseDesktopControls(desktopMode);
            }
            if (hand.WasPressedThisFrame()) vrInput.SetBowHand(!vrInput.BowInLeftHand);
            if (replica != null)
            {
                float t = Time.time - launchTime;
                replica.transform.SetPositionAndRotation(origin + velocity * t + Physics.gravity * (0.5f * t * t),
                    Quaternion.LookRotation(velocity + Physics.gravity * t));
            }
            int used = Completed + (flying ? 1 : 0);
            string first = AgainstCpu ? "You" : "Archer 1";
            string second = AgainstCpu ? "CPU" : "Archer 2";
            string action;
            if (Finished)
            {
                var result = FirstScore == SecondScore ? "Tie" : FirstScore > SecondScore ? (AgainstCpu ? "You win" : "Archer 1 wins") : second + " wins";
                action = result + ((votes & (1 << LocalSeat)) != 0 ? "   •   Waiting for opponent" : "   •   A: rematch");
            }
            else action = flying || awaiting ? "Arrow in flight" : Completed % 2 == LocalSeat ?
                vrInput.ShotHint(desktopMode) : AgainstCpu ? "CPU is drawing" : "Opponent's turn";
            feedback.text = $"{first}: {FirstScore} pts ({ArrowsPerSeat - (used + 1) / 2} left)    |    {second}: {SecondScore} pts ({ArrowsPerSeat - used / 2} left)\n" +
                $"{RoundDistance} m   •   Archer {LocalSeat + 1}   •   {(last < 0 ? "Center: 10 pts" : last == 0 ? "Miss" : $"{((Completed - 1) % 2 == 0 ? first : second)}: +{last}")}\n{action}";
        }

        // Releases round-owned objects and listeners when the player exits to the menu.
        private void OnDestroy()
        {
            hand?.Dispose();
            rematch?.Dispose();
            if (connection != null) connection.StatusChanged -= ConnectionChanged;
            if (bow != null) { bow.Fired -= Released; ClearRound(); }
        }
    }
}
