using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BowMan
{
    public sealed class LocalDuel : MonoBehaviour
    {
        [SerializeField] private GameObject localArcherPrefab;
        [SerializeField] private GameObject opponentPrefab;
        [SerializeField] private Transform localSpawn;
        [SerializeField] private Transform opponentSpawn;
        [SerializeField] private Transform head;
        [SerializeField] private Bow bow;
        [SerializeField] private VRBowInput vrInput;
        [SerializeField] private DesktopBowTest desktopInput;
        [SerializeField] private VRInjuryView injuryView;
        [SerializeField] private ArrowFlight arrowPrefab;
        [SerializeField] private TextMesh feedback;
        private Transform matchRoot;
        private FatalCutaway cutaway;
        private InputAction restart;
        private InputAction switchHand;
        private InputAction desktop;
        private bool desktopMode;

        public DuelTurnFlow Flow { get; private set; }
        public ArcherHealth LocalArcher { get; private set; }
        public ArcherHealth Opponent { get; private set; }

        // Starts the shared duel authority and controller shortcuts after scene references are available.
        private void Start()
        {
            if (localArcherPrefab == null || opponentPrefab == null || localSpawn == null || opponentSpawn == null ||
                bow == null || vrInput == null || desktopInput == null || injuryView == null || arrowPrefab == null ||
                head == null || feedback == null)
                throw new InvalidOperationException("The local duel scene is missing a required character, bow, view or feedback reference.");
            Flow = gameObject.AddComponent<DuelTurnFlow>();
            cutaway = head.gameObject.AddComponent<FatalCutaway>();
            Flow.FatalityRequested += OnFatality;
            Flow.Changed += OnFlowChanged;
            bow.Fired += OnFired;
            restart = new InputAction("Restart match", InputActionType.Button, "<XRController>{RightHand}/primaryButton");
            restart.AddBinding("<Keyboard>/r");
            switchHand = new InputAction("Switch bow hand", InputActionType.Button, "<XRController>{RightHand}/secondaryButton");
            switchHand.AddBinding("<Keyboard>/h");
            desktop = new InputAction("Desktop controls", InputActionType.Button, "<Keyboard>/tab");
            restart.Enable();
            switchHand.Enable();
            desktop.Enable();
            RestartMatch();
        }

        // Replaces only match-owned actors/arrows, keeping the active XR session and handed preference.
        public void RestartMatch()
        {
            bow.SetShotAllowed(false);
            cutaway.Clear();
            injuryView.ResetView();
            if (matchRoot != null)
            {
                matchRoot.gameObject.SetActive(false);
                Destroy(matchRoot.gameObject);
            }
            matchRoot = new GameObject("Current match").transform;
            matchRoot.SetParent(transform, false);
            LocalArcher = Spawn(localArcherPrefab, localSpawn);
            Opponent = Spawn(opponentPrefab, opponentSpawn);
            foreach (var renderer in LocalArcher.GetComponentsInChildren<Renderer>()) renderer.enabled = false;
            Flow.Initialize(LocalArcher, Opponent);
            LocalArcher.GetComponent<ArcherReactionAnimation>().Connect(Flow, 0, LocalArcher);
            Opponent.GetComponent<ArcherReactionAnimation>().Connect(Flow, 1, Opponent);
            bow.SetOwner(LocalArcher.transform);
            var animator = LocalArcher.GetComponentInChildren<Animator>();
            injuryView.Bind(LocalArcher.transform, animator.GetBoneTransform(HumanBodyBones.Head).Find("EyeAnchor"));
            Opponent.gameObject.AddComponent<AIArcher>().Initialize(Flow, 1, Opponent, LocalArcher, arrowPrefab);
            if (desktopMode) desktopInput.UseDesktopControls(true);
            OnFlowChanged();
        }

        // Instantiates a prepared rig at its floor spawn and gives the match its health component.
        private ArcherHealth Spawn(GameObject prefab, Transform spawn)
        {
            var actor = Instantiate(prefab, spawn.position, spawn.rotation, matchRoot);
            return actor.GetComponent<ArcherHealth>() ?? actor.AddComponent<ArcherHealth>();
        }

        // Accepts exactly one released bow arrow through the same authority used by the opponent.
        private void OnFired(ArrowFlight arrow)
        {
            arrow.transform.SetParent(matchRoot, true);
            if (!Flow.TryTrackShot(0, arrow)) Destroy(arrow.gameObject);
        }

        // Presents the shared anatomy report after the duel confirms a fatal arrow.
        private void OnFatality(int target, AnatomicalImpact report) => cutaway.Show(report);

        // Cancels the player's nock whenever the duel leaves their legal aiming turn.
        private void OnFlowChanged() => bow.SetShotAllowed(Flow.CanShoot(0));

        // Handles restart/handedness shortcuts and keeps the standing hidden target under the tracked player.
        private void Update()
        {
            if (Flow == null) return;
            if (restart.WasPressedThisFrame() && Flow.Phase == DuelPhase.Finished) RestartMatch();
            if (switchHand.WasPressedThisFrame() && Flow.CanShoot(0)) vrInput.SetBowHand(!vrInput.BowInLeftHand);
            if (desktop.WasPressedThisFrame())
            {
                desktopMode = !desktopMode;
                desktopInput.UseDesktopControls(desktopMode);
            }
            if (!LocalArcher.IsDefeated && !LocalArcher.GetComponent<ArcherReactionAnimation>().IsInjured)
            {
                var position = LocalArcher.transform.position;
                position.x = head.position.x;
                position.z = head.position.z;
                LocalArcher.transform.position = position;
            }
            var status = Flow.Phase == DuelPhase.Finished ? (Flow.Winner == 0 ? "VICTORY" : "DEFEAT") :
                Flow.CanShoot(0) ? "Your turn" : Flow.Phase == DuelPhase.Aiming ? "Opponent aiming" : Flow.Phase.ToString();
            if (cutaway.IsVisible)
            {
                feedback.text = $"{status}     A / R: restart\n{cutaway.Caption}";
                return;
            }
            feedback.text = $"{status}     You {LocalArcher.CurrentHealth}  |  Opponent {Opponent.CurrentHealth}\n" +
                $"Bow: {(vrInput.BowInLeftHand ? "left" : "right")} hand   Draw: {bow.DrawStrength:P0}\n" +
                (Flow.Phase == DuelPhase.Finished ? "A / R: restart" :
                desktopMode ? "Arrow keys: aim   Hold/release Space: shoot   Tab: VR" :
                "Trigger: draw/release   B: switch bow hand");
        }

        // Releases input actions and listeners when this duel scene closes.
        private void OnDestroy()
        {
            restart?.Dispose();
            switchHand?.Dispose();
            desktop?.Dispose();
            if (bow != null) bow.Fired -= OnFired;
            if (Flow != null)
            {
                Flow.Changed -= OnFlowChanged;
                Flow.FatalityRequested -= OnFatality;
            }
            if (cutaway != null) Destroy(cutaway);
            if (injuryView != null) injuryView.ResetView();
            if (matchRoot != null) Destroy(matchRoot.gameObject);
        }
    }
}
