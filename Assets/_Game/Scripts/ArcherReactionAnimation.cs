using System;
using UnityEngine;

namespace BowMan
{
    public sealed class ArcherReactionAnimation : MonoBehaviour
    {
        [SerializeField] private AnimationClip[] entries = new AnimationClip[15];
        [SerializeField] private AnimationClip[] recoveries = new AnimationClip[15];
        private DuelTurnFlow flow;
        private ArcherHealth health;
        private int archerIndex;
        private int token;
        private BodyRegion region;
        private AnimationClip playing;
        private float elapsed;
        private bool recovering;
        private bool fatalPlayback;

        // Requires an exact entry and, for surviving regions, an authored recovery.
        public bool Supports(BodyRegion value) => Entry(value) != null &&
            (value is BodyRegion.Head or BodyRegion.Neck || Recovery(value) != null);

        // Returns the model-specific reaction clip for animation review and runtime playback.
        public AnimationClip Entry(BodyRegion value) => entries[(int)value];

        // Returns the matching recovery from that region's injured resting pose.
        public AnimationClip Recovery(BodyRegion value) => recoveries[(int)value];

        // Stores an authored entry and its optional, separately prepared recovery.
        public void SetSequence(BodyRegion value, AnimationClip entry, AnimationClip recovery)
        {
            if (entry == null) throw new ArgumentException("An injury entry is required.");
            entries[(int)value] = entry;
            recoveries[(int)value] = recovery;
        }

        // Connects this character's authored sequences to the existing shot-token and recovery contract.
        public void Connect(DuelTurnFlow match, int index, ArcherHealth actor)
        {
            if (match == null || actor == null || index < 0 || index > 1)
                throw new ArgumentException("Reaction playback requires a match, actor and side.");
            Detach();
            flow = match;
            health = actor;
            archerIndex = index;
            flow.ReactionRequested += OnReaction;
            flow.RecoveryRequested += OnRecovery;
            flow.Changed += OnFlowChanged;
        }

        // Plays this exact region's entry; unsupported regions remain pending for their own implementations.
        private void OnReaction(int target, BodyImpact hit, bool lethal, int shotId)
        {
            if (target != archerIndex || Entry(hit.Region) == null) return;
            if (lethal && hit.Region != BodyRegion.Head && hit.Region != BodyRegion.Neck) return;
            region = hit.Region;
            token = shotId;
            recovering = false;
            fatalPlayback = lethal;
            elapsed = 0f;
            playing = Entry(region);
            playing.SampleAnimation(gameObject, 0f);
        }

        // Starts the matching return motion only after the flow's complete injured-pose hold.
        private void OnRecovery(int target, int shotId)
        {
            if (target != archerIndex || shotId != token || !Supports(region) || health.IsDefeated) return;
            recovering = true;
            elapsed = 0f;
            playing = Recovery(region);
        }

        // Advances the authored bones and reports completion only after sampling the final frame.
        private void LateUpdate()
        {
            if (playing == null || health == null || (health.IsDefeated && !fatalPlayback)) return;
            elapsed = Mathf.Min(elapsed + Time.deltaTime, playing.length);
            playing.SampleAnimation(gameObject, elapsed);
            if (elapsed < playing.length) return;
            playing = null;
            if (fatalPlayback) { fatalPlayback = false; return; }
            if (recovering) flow.CompleteRecovery(token);
            else flow.MarkInjuredPose(token);
        }

        // Stops surviving recovery immediately when either defeat finishes the match.
        private void OnFlowChanged()
        {
            if (flow.Phase == DuelPhase.Finished && !fatalPlayback) playing = null;
        }

        // Removes the old match's listeners before reconnecting or destroying the character.
        private void Detach()
        {
            if (flow == null) return;
            flow.ReactionRequested -= OnReaction;
            flow.RecoveryRequested -= OnRecovery;
            flow.Changed -= OnFlowChanged;
            playing = null;
            fatalPlayback = false;
        }

        // Prevents late match callbacks from sampling a destroyed character.
        private void OnDestroy() => Detach();
    }
}
