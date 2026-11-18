using System;
using System.Linq;
using UnityEngine;

namespace BowMan
{
    public sealed class ArcherReactionAnimation : MonoBehaviour
    {
        private const float ChestRecoveryDuration = 3.6f;

        [SerializeField] private AnimationClip[] entries = new AnimationClip[15];
        [SerializeField] private AnimationClip[] recoveries = new AnimationClip[15];
        [SerializeField] private AnimationClip[] deathFinishers = new AnimationClip[15];
        private DuelTurnFlow flow;
        private ArcherHealth health;
        private int archerIndex;
        private int token;
        private BodyRegion region;
        private AnimationClip playing;
        private float elapsed;
        private bool recovering;
        private bool fatalPlayback;
        private bool injured;
        private bool finishingDeath;
        private Transform[] deathBlendBones;
        private Vector3[] deathBlendPositions;
        private Quaternion[] deathBlendRotations;

        // Requires an exact entry and, for surviving regions, an authored recovery.
        public bool Supports(BodyRegion value) => Entry(value) != null &&
            (value is BodyRegion.Head or BodyRegion.Neck || Recovery(value) != null);

        // Returns the model-specific reaction clip for animation review and runtime playback.
        public AnimationClip Entry(BodyRegion value) => entries[(int)value];

        // Returns the matching recovery from that region's injured resting pose.
        public AnimationClip Recovery(BodyRegion value) => recoveries[(int)value];

        // Returns the collapse authored from this region's injured pose, separate from its surviving recovery.
        public AnimationClip DeathFinisher(BodyRegion value) => deathFinishers[(int)value];

        // Stores a body-region collapse without replacing the independent head and neck fatal entries.
        public void SetDeathFinisher(BodyRegion value, AnimationClip clip)
        {
            if (clip == null || value is BodyRegion.Head or BodyRegion.Neck)
                throw new ArgumentException("A collapse clip for a surviving-hit region is required.");
            deathFinishers[(int)value] = clip;
        }

        // Stores an authored entry and its optional recovery.
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
            if (lethal && hit.Region is not BodyRegion.Head and not BodyRegion.Neck &&
                DeathFinisher(hit.Region) == null) return;
            region = hit.Region;
            token = shotId;
            recovering = false;
            fatalPlayback = lethal;
            finishingDeath = false;
            injured = true;
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
            var recoverySpeed = recovering && region == BodyRegion.Chest
                ? playing.length / ChestRecoveryDuration : 1f;
            elapsed = Mathf.Min(elapsed + Time.deltaTime * recoverySpeed, playing.length);
            playing.SampleAnimation(gameObject, elapsed);
            if (finishingDeath && elapsed < 0.15f)
            {
                var blend = Mathf.SmoothStep(0f, 1f, elapsed / 0.15f);
                for (var i = 0; i < deathBlendBones.Length; i++)
                {
                    deathBlendBones[i].localPosition = Vector3.Lerp(deathBlendPositions[i], deathBlendBones[i].localPosition, blend);
                    deathBlendBones[i].localRotation = Quaternion.Slerp(deathBlendRotations[i], deathBlendBones[i].localRotation, blend);
                }
            }
            if (elapsed < playing.length) return;
            playing = null;
            if (fatalPlayback)
            {
                if (!finishingDeath && region is not BodyRegion.Head and not BodyRegion.Neck)
                {
                    BeginDeathFinisher();
                    return;
                }
                fatalPlayback = false;
                finishingDeath = false;
                injured = false;
                return;
            }
            if (recovering)
            {
                injured = false;
                flow.CompleteRecovery(token);
            }
            else
            {
                elapsed = 0f;
                flow.MarkInjuredPose(token);
            }
        }

        // Starts the authored collapse from the actual current pose, including interrupted recovery.
        private void BeginDeathFinisher()
        {
            var clip = DeathFinisher(region);
            deathBlendBones = GetComponentsInChildren<SkinnedMeshRenderer>().SelectMany(s => s.bones)
                .Where(t => t != null).Distinct().ToArray();
            deathBlendPositions = deathBlendBones.Select(t => t.localPosition).ToArray();
            deathBlendRotations = deathBlendBones.Select(t => t.localRotation).ToArray();
            fatalPlayback = true;
            finishingDeath = true;
            recovering = false;
            elapsed = 0f;
            playing = clip;
        }

        // Interrupts get-up on defeat while allowing the defeated archer's final collapse to finish visually.
        private void OnFlowChanged()
        {
            if (flow.Phase != DuelPhase.Finished || fatalPlayback) return;
            if (health.IsDefeated && injured && DeathFinisher(region) != null) BeginDeathFinisher();
            else playing = null;
        }

        // Removes the old match's listeners before reconnecting or destroying the character.
        private void Detach()
        {
            if (flow != null)
            {
                flow.ReactionRequested -= OnReaction;
                flow.RecoveryRequested -= OnRecovery;
                flow.Changed -= OnFlowChanged;
            }
            flow = null;
            playing = null;
            fatalPlayback = false;
            finishingDeath = false;
            injured = false;
            deathBlendBones = null;
            deathBlendPositions = null;
            deathBlendRotations = null;
        }

        // Prevents late match callbacks from sampling a destroyed character.
        private void OnDestroy() => Detach();
    }
}
