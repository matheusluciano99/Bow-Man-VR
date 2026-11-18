using System;
using UnityEngine;

namespace BowMan
{
    public enum DuelPhase { Aiming, Flying, Reacting, Holding, Recovering, Finished }

    public sealed class DuelTurnFlow : MonoBehaviour
    {
        [SerializeField, Min(1)] private int bodyDamage = 25;
        private readonly ArcherHealth[] archers = new ArcherHealth[2];
        private ArrowFlight pendingArrow;
        private float holdRemaining;
        private bool initialized;

        public DuelPhase Phase { get; private set; } = DuelPhase.Finished;
        public int ActiveArcher { get; private set; }
        public int Winner { get; private set; } = -1;
        public int ShotId { get; private set; }
        public event Action Changed;
        public event Action<int, BodyImpact, bool, int> ReactionRequested;
        public event Action<int, int> RecoveryRequested;
        public event Action<int> MatchFinished;
        public event Action<int, AnatomicalImpact> FatalityRequested;

        // Starts a duel with two distinct living archers and invalidates callbacks from any earlier match.
        public void Initialize(ArcherHealth first, ArcherHealth second)
        {
            if (first == null || second == null || first == second || first.IsDefeated || second.IsDefeated)
                throw new ArgumentException("A duel requires two distinct living archers.");
            if (bodyDamage <= 0) throw new InvalidOperationException("Body damage must be positive.");
            Detach();
            archers[0] = first;
            archers[1] = second;
            first.Defeated += FirstDefeated;
            second.Defeated += SecondDefeated;
            initialized = true;
            ShotId++;
            ActiveArcher = 0;
            Winner = -1;
            SetPhase(DuelPhase.Aiming);
        }

        // Allows only the living active archer to prepare another shot after full recovery.
        public bool CanShoot(int archer) => initialized && archer >= 0 && archer < 2 &&
            archer == ActiveArcher && Phase == DuelPhase.Aiming && !archers[archer].IsDefeated;

        // Registers an already launched arrow before its first physics step and locks both archers' shooting.
        public bool TryTrackShot(int shooter, ArrowFlight arrow)
        {
            if (!CanShoot(shooter) || arrow == null || !arrow.IsFlying) return false;
            pendingArrow = arrow;
            ShotId++;
            arrow.BodyHit += OnBodyHit;
            arrow.Hit += OnSceneryHit;
            arrow.Expired += OnExpired;
            SetPhase(DuelPhase.Flying);
            return true;
        }

        // Sets the hold from the end of the region's entry to the start of its recovery.
        internal static float InjuredHoldDuration(BodyRegion region) => region switch
        {
            BodyRegion.Chest => 0.75f,
            BodyRegion.LeftFoot or BodyRegion.RightFoot => 2f,
            _ => 3f
        };

        // Starts the hold only when the current reaction has reached its injured resting pose.
        public void MarkInjuredPose(int shotId)
        {
            if (shotId != ShotId || Phase != DuelPhase.Reacting) return;
            SetPhase(DuelPhase.Holding);
        }

        // Hands over once after the matching get-up or standing recovery animation finishes.
        public void CompleteRecovery(int shotId)
        {
            if (shotId != ShotId || Phase != DuelPhase.Recovering) return;
            NextTurn();
        }

        // Requests recovery after the region’s full hold, excluding entry and get-up time.
        private void Update()
        {
            if (Phase != DuelPhase.Holding) return;
            holdRemaining -= Time.deltaTime;
            if (holdRemaining > 0f) return;
            SetPhase(DuelPhase.Recovering);
            if (Phase == DuelPhase.Recovering) RecoveryRequested?.Invoke(1 - ActiveArcher, ShotId);
        }

        // Applies one anatomical outcome; fatal head/neck hits end the match before visual death motion finishes.
        private void OnBodyHit(BodyImpact impact)
        {
            if (Phase != DuelPhase.Flying) return;
            var target = impact.Collider.GetComponentInParent<ArcherHealth>();
            var targetIndex = 1 - ActiveArcher;
            if (target != archers[targetIndex]) { OnExpired(); return; }
            ClearArrow();
            var token = ShotId;
            holdRemaining = InjuredHoldDuration(impact.Region);
            SetPhase(DuelPhase.Reacting);
            target.TakeDamage(impact.Region is BodyRegion.Head or BodyRegion.Neck ? int.MaxValue : bodyDamage);
            if (token != ShotId) return;
            if (target.IsDefeated && target.TryGetComponent<ArcherAnatomy>(out var anatomy))
                FatalityRequested?.Invoke(targetIndex, anatomy.Trace(impact));
            ReactionRequested?.Invoke(targetIndex, impact, target.IsDefeated, token);
        }

        // Resolves the first scenery contact as a miss without applying body damage.
        private void OnSceneryHit(RaycastHit hit) => OnExpired();

        // Resolves a flight expiry or scenery miss once, without reacting to later arrow cleanup.
        private void OnExpired()
        {
            if (Phase != DuelPhase.Flying) return;
            ClearArrow();
            NextTurn();
        }

        // Gives the other living archer the next turn after the current outcome has fully resolved.
        private void NextTurn()
        {
            ActiveArcher = 1 - ActiveArcher;
            SetPhase(DuelPhase.Aiming);
        }

        // Finishes the duel when the first archer's health reaches zero.
        private void FirstDefeated() => Finish(1);

        // Finishes the duel when the second archer's health reaches zero.
        private void SecondDefeated() => Finish(0);

        // Cancels pending shot/recovery work and announces a single winner immediately.
        private void Finish(int winner)
        {
            if (Phase == DuelPhase.Finished) return;
            ClearArrow();
            Winner = winner;
            SetPhase(DuelPhase.Finished);
            MatchFinished?.Invoke(winner);
        }

        // Publishes the current flow state for bow gates and match feedback.
        private void SetPhase(DuelPhase phase)
        {
            Phase = phase;
            Changed?.Invoke();
        }

        // Removes all listeners from the current shot before another outcome or turn can be accepted.
        private void ClearArrow()
        {
            if (pendingArrow != null)
            {
                pendingArrow.BodyHit -= OnBodyHit;
                pendingArrow.Hit -= OnSceneryHit;
                pendingArrow.Expired -= OnExpired;
            }
            pendingArrow = null;
        }

        // Releases old actor and arrow listeners when restarting or destroying the match host.
        private void Detach()
        {
            ClearArrow();
            if (archers[0] != null) archers[0].Defeated -= FirstDefeated;
            if (archers[1] != null) archers[1].Defeated -= SecondDefeated;
        }

        // Prevents destroyed match hosts from receiving late arrow or health callbacks.
        private void OnDestroy() => Detach();
    }
}
