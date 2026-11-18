using UnityEngine;

namespace BowMan
{
    public sealed class DuelTurnReview : MonoBehaviour
    {
        [SerializeField] private DuelTurnFlow flow;
        [SerializeField] private ArcherHealth first;
        [SerializeField] private ArcherHealth second;
        [SerializeField] private ArrowFlight arrowPrefab;
        [SerializeField] private bool useAI;
        [SerializeField] private BodyRegion selectedRegion = BodyRegion.Chest;
        private string lastReaction = "No reaction requested";
        private bool manualEntry = true;
        private bool manualRecovery = true;

        // Starts the desktop state review after both health components have initialized.
        private void Start()
        {
            flow.Initialize(first, second);
            flow.ReactionRequested += OnReaction;
            var firstAnimation = first.GetComponentInChildren<ArcherReactionAnimation>();
            var secondAnimation = second.GetComponentInChildren<ArcherReactionAnimation>();
            if (firstAnimation != null) firstAnimation.Connect(flow, 0, first);
            if (secondAnimation != null) secondAnimation.Connect(flow, 1, second);
            if (useAI) second.gameObject.AddComponent<AIArcher>().Initialize(flow, 1, second, first, arrowPrefab);
        }

        // Retains the actual region requested by combat without pretending an animation has completed.
        private void OnReaction(int target, BodyImpact hit, bool lethal, int token)
        {
            lastReaction = $"Archer {target + 1}: {hit.Region}, {(lethal ? "fatal" : "survived")}";
            var actor = (target == 0 ? first : second).GetComponentInChildren<ArcherReactionAnimation>();
            manualEntry = actor == null || actor.Entry(hit.Region) == null;
            manualRecovery = actor == null || actor.Recovery(hit.Region) == null;
        }

        // Launches a real shot for the active archer and lets the match own its one outcome.
        private void Fire()
        {
            var shooter = flow.ActiveArcher;
            if (!flow.CanShoot(shooter)) return;
            var owner = shooter == 0 ? first : second;
            var target = shooter == 0 ? second : first;
            foreach (var region in target.GetComponentsInChildren<BodyHitRegion>())
            {
                if (region.Region != selectedRegion) continue;
                var start = owner.transform.position + Vector3.up * 1.4f + owner.transform.forward;
                var arrow = Instantiate(arrowPrefab, start, Quaternion.identity);
                arrow.Launch((region.GetComponent<Collider>().bounds.center - start) / 0.8f - Physics.gravity * 0.4f,
                    owner.transform);
                if (!flow.TryTrackShot(shooter, arrow)) Destroy(arrow.gameObject);
                return;
            }
        }

        // Exposes shot and explicit animation callbacks so reviewers can verify the state contract without clips.
        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(16, 16, 460, 350), GUI.skin.box);
            GUILayout.Label("Turn-flow review — unsupported clips use manual callbacks");
            GUILayout.Label($"Phase: {flow.Phase}   Active archer: {flow.ActiveArcher + 1}");
            GUILayout.Label($"Health: {first.CurrentHealth} / {second.CurrentHealth}");
            GUILayout.Label(lastReaction);
            selectedRegion = (BodyRegion)GUILayout.SelectionGrid((int)selectedRegion,
                System.Enum.GetNames(typeof(BodyRegion)), 3);
            GUI.enabled = flow.CanShoot(flow.ActiveArcher) && (!useAI || flow.ActiveArcher == 0);
            if (GUILayout.Button("Fire active archer")) Fire();
            GUI.enabled = manualEntry && flow.Phase == DuelPhase.Reacting;
            if (GUILayout.Button("Animation reached injured pose")) flow.MarkInjuredPose(flow.ShotId);
            GUI.enabled = manualRecovery && flow.Phase == DuelPhase.Recovering;
            if (GUILayout.Button("Recovery animation finished")) flow.CompleteRecovery(flow.ShotId);
            GUI.enabled = true;
            if (flow.Phase == DuelPhase.Finished) GUILayout.Label($"Winner: archer {flow.Winner + 1}");
            GUILayout.EndArea();
        }

        // Removes the review listener when the scene closes.
        private void OnDestroy()
        {
            if (flow != null) flow.ReactionRequested -= OnReaction;
        }
    }
}
