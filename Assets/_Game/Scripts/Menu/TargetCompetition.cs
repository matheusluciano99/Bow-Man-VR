using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;

namespace BowMan.Menu
{
    // Runs two ten-arrow ends with a single bow, firing line and headset.
    public sealed class TargetCompetition : MonoBehaviour
    {
        public const int ArrowsPerArcher = 10;
        public const float FaceRadius = 0.48f;
        [SerializeField] private Bow bow;
        [SerializeField] private VRBowInput vrInput;
        [SerializeField] private DesktopBowTest desktopInput;
        [SerializeField] private Collider face;
        [SerializeField] private TextMesh feedback;

        private readonly int[] scores = new int[2];
        private InputAction confirm, switchHand;
        private ArrowFlight pending;
        private DuelAudio sounds;
        private Transform arrows;
        private Vector3 standPosition;
        private Quaternion standRotation;
        private bool desktopMode;
        private int soundId;
        public int Archer { get; private set; }
        public int Used { get; private set; }
        public int LastPoints { get; private set; } = -1;
        public bool Handoff { get; private set; }
        public bool Finished { get; private set; }

        // Reads either archer's cumulative score for this match.
        public int ScoreFor(int seat) => scores[seat];

        // Sets up shared equipment and starts the first archer on the firing line.
        private void Start()
        {
            confirm = new InputAction("Next archer or rematch", InputActionType.Button, "<XRController>{RightHand}/primaryButton");
            confirm.AddBinding("<Keyboard>/r");
            confirm.Enable();
            switchHand = new InputAction("Switch bow hand", InputActionType.Button, "<XRController>{RightHand}/secondaryButton");
            switchHand.AddBinding("<Keyboard>/h");
            switchHand.Enable();
            vrInput.SetBowHand(VRBowInput.PreferredBowLeft);
            vrInput.ShowArcherHands();
            desktopMode = !XRSettings.isDeviceActive;
            standPosition = bow.transform.position;
            standRotation = bow.transform.rotation;
            sounds = gameObject.AddComponent<DuelAudio>();
            sounds.BindBow(bow, ArenaSetting.WoodlandRange);
            bow.Fired += OnFired;
            BeginEnd();
        }

        // Clears the previous archer's arrows and unlocks the bow at the same firing line.
        private void BeginEnd()
        {
            if (arrows != null) { arrows.gameObject.SetActive(false); Destroy(arrows.gameObject); }
            arrows = new GameObject("Competition arrows").transform;
            arrows.SetParent(transform, false);
            Used = 0;
            LastPoints = -1;
            Handoff = Finished = false;
            desktopInput.PlaceStand(standPosition, standRotation);
            desktopInput.UseDesktopControls(desktopMode);
            bow.SetShotAllowed(true);
        }

        // Accepts a handoff or starts a fresh two-player match after the result.
        public void Confirm()
        {
            if (Handoff) Archer = 1;
            else if (Finished) { Archer = 0; scores[0] = scores[1] = 0; }
            else return;
            BeginEnd();
        }

        // Counts a released arrow and locks the string until its first impact or expiry.
        private void OnFired(ArrowFlight arrow)
        {
            pending = arrow;
            arrow.transform.SetParent(arrows, true);
            Used++;
            bow.SetShotAllowed(false);
            sounds.Flight(++soundId, arrow);
            arrow.Hit += OnHit;
            arrow.Expired += OnMiss;
        }

        // Scores concentric tenths of the painted radius, awarding a line to the higher ring.
        public static int RingScore(Vector2 localPoint)
        {
            float rings = localPoint.magnitude * 10f / FaceRadius;
            return rings > 10.00001f ? 0 : Mathf.Clamp(11 - Mathf.CeilToInt(rings - 0.00001f), 1, 10);
        }

        // Measures the actual impact in the target face plane; every other collider is a miss.
        private void OnHit(RaycastHit hit)
        {
            bool onFace = hit.collider == face;
            sounds.Impact(hit.point, onFace ? ImpactSound.Wood : sounds.Surface(hit.collider));
            Resolve(onFace ? RingScore(face.transform.InverseTransformPoint(hit.point)) : 0);
        }

        // Charges an arrow that left the range for zero points.
        private void OnMiss() => Resolve(0);

        // Settles a single shot before unlocking, handing off or declaring the result.
        private void Resolve(int points)
        {
            if (pending == null) return;
            pending.Hit -= OnHit;
            pending.Expired -= OnMiss;
            pending = null;
            LastPoints = points;
            scores[Archer] += points;
            Handoff = Used == ArrowsPerArcher && Archer == 0;
            Finished = Used == ArrowsPerArcher && Archer == 1;
            bow.SetShotAllowed(!Handoff && !Finished);
        }

        // Shows both totals, shot feedback, and the next action in the headset and desktop camera.
        private void Update()
        {
            if (confirm.WasPressedThisFrame()) Confirm();
            if (Keyboard.current != null && Keyboard.current.tabKey.wasPressedThisFrame)
            {
                desktopMode = !desktopMode;
                desktopInput.UseDesktopControls(desktopMode);
            }
            if (switchHand.WasPressedThisFrame() && pending == null) vrInput.SetBowHand(!vrInput.BowInLeftHand);
            string next;
            if (Finished) next = (scores[0] == scores[1] ? "Tie" : scores[0] > scores[1] ? "Archer 1 wins" : "Archer 2 wins") + "   •   A / R: rematch";
            else if (Handoff) next = "Pass to Archer 2   •   A / R when ready";
            else next = pending != null ? "Arrow in flight" : vrInput.ShotHint(desktopMode);
            var last = LastPoints switch { -1 => "   •   Center: 10 pts", 0 => "   •   Miss", _ => $"   •   Last: +{LastPoints}" };
            feedback.text = $"Archer 1: {scores[0]}     |     Archer 2: {scores[1]}\n" +
                $"Archer {Archer + 1}   •   {ArrowsPerArcher - Used} arrows left{last}\n{next}\nB / H: switch bow hand   •   Menu / Esc: leave";
        }

        // Removes input bindings and flight callbacks when leaving the range.
        private void OnDestroy()
        {
            confirm?.Dispose();
            switchHand?.Dispose();
            if (bow != null) bow.Fired -= OnFired;
            if (pending != null) { pending.Hit -= OnHit; pending.Expired -= OnMiss; }
        }
    }
}
