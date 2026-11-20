using System;
using System.Linq;
using UnityEngine;

namespace BowMan
{
    public sealed class ArcherReactionAnimation : MonoBehaviour
    {
        private const float FootRecoveryDuration = 0.8f;
        private const float ChestRecoveryDuration = 3.6f;

        [SerializeField] private AnimationClip standingIdle;
        private float idleTime;
        private Transform[] idleArms;
        private Quaternion[] restingArms;
        private readonly Quaternion[] previousArms = new Quaternion[4];
        private float armBlendTime;
        [SerializeField] private AnimationClip[] entries = new AnimationClip[15];
        [SerializeField] private AnimationClip[] recoveries = new AnimationClip[15];
        [SerializeField] private AnimationClip[] holds = new AnimationClip[15];
        [SerializeField] private AnimationClip[] deathFinishers = new AnimationClip[15];
        [SerializeField] private AnimationClip[] fatalEntries = new AnimationClip[15];
        private ArcherWoundContact woundContact;
        private BodyImpact entryImpact;
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
        private Animator poseAnimator;
        private Transform woundAnchor;
        private Vector3 woundPoint;
        private Vector3 woundOutward;

        public bool IsInjured => injured;

        // Requires an exact entry and, for surviving regions, an authored recovery.
        public bool Supports(BodyRegion value) => Entry(value) != null &&
            (value is BodyRegion.Head or BodyRegion.Neck || Recovery(value) != null);

        // Returns the model-specific reaction clip for animation review and runtime playback.
        public AnimationClip Entry(BodyRegion value) => entries[(int)value];

        // Returns the matching recovery from that region's injured resting pose.
        public AnimationClip Recovery(BodyRegion value) => recoveries[(int)value];

        // Returns optional supported-balance motion sampled during the flow's injured hold.
        public AnimationClip Hold(BodyRegion value) => holds[(int)value];

        // Returns the collapse authored from this region's injured pose, separate from its surviving recovery.
        public AnimationClip DeathFinisher(BodyRegion value) => deathFinishers[(int)value];

        // Returns a complete recorded fatal reaction that starts at impact.
        public AnimationClip FatalEntry(BodyRegion value) => fatalEntries[(int)value];

        // Binds a continuous shoulder performance from first impact through the final collapse.
        public void SetFatalShoulder(BodyRegion side, AnimationClip clip)
        {
            if (clip == null || side is not (BodyRegion.LeftShoulder or BodyRegion.RightShoulder))
                throw new ArgumentException("A complete fatal shoulder clip is required.");
            fatalEntries[(int)side] = clip;
        }

        // Stores a body-region collapse without replacing the independent head and neck fatal entries.
        public void SetDeathFinisher(BodyRegion value, AnimationClip clip)
        {
            if (clip == null || value is BodyRegion.Head or BodyRegion.Neck)
                throw new ArgumentException("A collapse clip for a surviving-hit region is required.");
            deathFinishers[(int)value] = clip;
        }

        // Stores an authored entry, optional recovery and optional supported-balance hold.
        public void SetSequence(BodyRegion value, AnimationClip entry, AnimationClip recovery, AnimationClip hold = null)
        {
            if (entry == null) throw new ArgumentException("An injury entry is required.");
            entries[(int)value] = entry;
            recoveries[(int)value] = recovery;
            holds[(int)value] = hold;
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
            poseAnimator = GetComponentInChildren<Animator>();
            if (poseAnimator != null && poseAnimator.isHuman && (Supports(BodyRegion.Abdomen) || Supports(BodyRegion.Neck)))
                woundContact = new ArcherWoundContact(poseAnimator, GetComponentsInChildren<BodyHitRegion>());
            if (standingIdle != null && poseAnimator != null && poseAnimator.isHuman)
            {
                idleArms = new[] { HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm,
                    HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm }.Select(poseAnimator.GetBoneTransform).ToArray();
                idleTime = 0f;
                standingIdle.SampleAnimation(gameObject, idleTime);
                restingArms = idleArms.Select(bone => bone.localRotation).ToArray();
                BeginArmBlend();
            }
            flow.ReactionRequested += OnReaction;
            flow.RecoveryRequested += OnRecovery;
            flow.Changed += OnFlowChanged;
        }

        // Plays this exact region's entry; unsupported regions remain pending for their own implementations.
        private void OnReaction(int target, BodyImpact hit, bool lethal, int shotId)
        {
            if (target != archerIndex) return;
            var clip = lethal && FatalEntry(hit.Region) != null ? FatalEntry(hit.Region) : Entry(hit.Region);
            if (clip == null) return;
            region = hit.Region;
            token = shotId;
            recovering = false;
            fatalPlayback = lethal;
            finishingDeath = false;
            injured = true;
            idleTime = 0f;
            elapsed = 0f;
            playing = clip;
            entryImpact = hit;
            if (region is BodyRegion.Neck or BodyRegion.Abdomen)
            {
                woundAnchor = hit.Collider.transform;
                woundPoint = woundAnchor.InverseTransformPoint(hit.Point);
                woundOutward = woundAnchor.InverseTransformDirection(-hit.Direction);
            }
            BeginArmBlend();
            // LateUpdate samples the new pose after every hit listener has read the contacted bone.
        }

        // Starts the matching return motion only after the flow's complete injured-pose hold.
        private void OnRecovery(int target, int shotId)
        {
            if (target != archerIndex || shotId != token || !Supports(region) || health.IsDefeated) return;
            var hold = Hold(region);
            if (hold != null) hold.SampleAnimation(gameObject, hold.length);
            recovering = true;
            elapsed = 0f;
            playing = Recovery(region);
        }

        // Advances the authored bones and reports completion only after sampling the final frame.
        private void LateUpdate()
        {
            if (health == null || (health.IsDefeated && !fatalPlayback)) return;
            if (playing == null)
            {
                if (!injured && standingIdle != null)
                {
                    idleTime = Mathf.Repeat(idleTime + Time.deltaTime, standingIdle.length);
                    standingIdle.SampleAnimation(gameObject, idleTime);
                    BlendArms(Time.deltaTime);
                    return;
                }
                var hold = Hold(region);
                if (injured && flow.Phase == DuelPhase.Holding && hold != null)
                {
                    var holdSpeed = hold.length / DuelTurnFlow.InjuredHoldDuration(region);
                    elapsed = Mathf.Min(elapsed + Time.deltaTime * holdSpeed, hold.length);
                    hold.SampleAnimation(gameObject, elapsed);
                }
                return;
            }
            var recoverySpeed = recovering ? region switch
            {
                BodyRegion.Chest => playing.length / ChestRecoveryDuration,
                BodyRegion.LeftFoot or BodyRegion.RightFoot => playing.length / FootRecoveryDuration,
                _ => 1f
            } : 1f;
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
            if (fatalPlayback && region == BodyRegion.Neck && woundAnchor != null && woundContact != null)
                woundContact.ApplyNeck(woundAnchor.TransformPoint(woundPoint),
                    woundAnchor.TransformDirection(woundOutward).normalized, ArcherLimbIK.NeckReachWeight(elapsed));
            var completeFatalEntry = fatalPlayback && playing == FatalEntry(region);
            if (!recovering && !finishingDeath && !completeFatalEntry)
                ArcherHitRecoil.Apply(poseAnimator, entryImpact, elapsed);
            if (region == BodyRegion.Abdomen && woundAnchor != null && !finishingDeath && woundContact != null)
            {
                var reach = AbdomenContactWeight(elapsed / playing.length, recovering);
                woundContact.ApplyAbdomen(woundAnchor.TransformPoint(woundPoint),
                    woundAnchor.TransformDirection(woundOutward).normalized, reach);
            }
            BlendArms(Time.deltaTime);
            if (recovering && idleArms != null)
            {
                var weight = Mathf.SmoothStep(0f, 1f, 1f - (playing.length - elapsed) / (0.22f * recoverySpeed));
                for (var i = 0; i < idleArms.Length; i++)
                    idleArms[i].localRotation = Quaternion.Slerp(idleArms[i].localRotation, restingArms[i], weight);
            }
            if (elapsed < playing.length) return;
            playing = null;
            if (fatalPlayback)
            {
                if (!finishingDeath && !completeFatalEntry && region is not BodyRegion.Head and not BodyRegion.Neck)
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
                if (standingIdle != null) standingIdle.SampleAnimation(gameObject, 0f);
                flow.CompleteRecovery(token);
            }
            else
            {
                elapsed = 0f;
                flow.MarkInjuredPose(token);
            }
        }

        // Captures arm rotations at the transition between the recorded resting pose and a regional clip.
        private void BeginArmBlend()
        {
            if (idleArms == null) return;
            for (var i = 0; i < idleArms.Length; i++) previousArms[i] = idleArms[i].localRotation;
            armBlendTime = 0f;
        }

        // Eases the resting arms over the first tenth of a second while the impact and torso play immediately.
        private void BlendArms(float deltaTime)
        {
            const float duration = 0.1f;
            if (idleArms == null || armBlendTime >= duration) return;
            armBlendTime += deltaTime;
            var weight = Mathf.SmoothStep(0f, 1f, armBlendTime / duration);
            for (var i = 0; i < idleArms.Length; i++)
                idleArms[i].localRotation = Quaternion.Slerp(previousArms[i], idleArms[i].localRotation, weight);
        }

        // Retargets near contact while letting the fitted clips handle the approach and release.
        internal static float AbdomenContactWeight(float progress, bool recovering) => recovering
            ? Mathf.SmoothStep(1f, 0f, Mathf.InverseLerp(0.37f, 0.54f, progress))
            : Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.34f, 0.53f, progress));

        // Starts the authored collapse from the actual current pose, including interrupted recovery.
        private void BeginDeathFinisher()
        {
            var clip = DeathFinisher(region);
            if (clip == null) throw new InvalidOperationException("Missing death collapse for " + region);
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
            if (health.IsDefeated && injured) BeginDeathFinisher();
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
            woundContact = null;
        }

        // Prevents late match callbacks from sampling a destroyed character.
        private void OnDestroy() => Detach();
    }
}
