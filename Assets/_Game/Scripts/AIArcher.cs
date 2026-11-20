using System;
using UnityEngine;

namespace BowMan
{
    public sealed class AIArcher : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float thinkDelay = 0.8f;
        [SerializeField, Min(0f)] private float aimError = 0.20f;
        [SerializeField, Min(0.01f)] private float launchSpeed = 25f;
        private DuelTurnFlow flow;
        private ArcherHealth owner;
        private ArcherHealth opponent;
        private ArrowFlight arrowPrefab;
        private int archerIndex;
        private bool waiting;
        private float fireAt;
        public int ShotsFired { get; private set; }

        // Connects this opponent to the existing duel authority and real arrow prefab.
        public void Initialize(DuelTurnFlow match, int index, ArcherHealth archer, ArcherHealth target, ArrowFlight prefab)
        {
            if (match == null || archer == null || target == null || archer == target || prefab == null ||
                index < 0 || index > 1) throw new ArgumentException("AI requires a valid duel, actor, opponent and arrow.");
            if (!(launchSpeed > 0f) || float.IsInfinity(launchSpeed) || !(thinkDelay >= 0f) ||
                float.IsInfinity(thinkDelay) || !(aimError >= 0f) || float.IsInfinity(aimError))
                throw new InvalidOperationException("AI speed, delay and aim error must be finite and in range.");
            if (flow != null) flow.Changed -= OnFlowChanged;
            flow = match;
            archerIndex = index;
            owner = archer;
            opponent = target;
            arrowPrefab = prefab;
            ShotsFired = 0;
            flow.Changed += OnFlowChanged;
            OnFlowChanged();
        }

        // Schedules one thought delay only when this AI owns a recovered, living turn.
        private void OnFlowChanged()
        {
            waiting = flow != null && flow.CanShoot(archerIndex);
            fireAt = Time.time + thinkDelay;
        }

        // Fires once when ready; locked turns, pending reactions and defeat keep the opponent idle.
        private void Update()
        {
            if (!waiting || Time.time < fireAt || !flow.CanShoot(archerIndex)) return;
            waiting = false;
            Fire();
        }

        // Aims with bounded error and launches a legal fixed-speed arc through the shared arrow/turn systems.
        private void Fire()
        {
            Physics.SyncTransforms();
            var point = opponent.transform.position + Vector3.up * 1.3f;
            foreach (var region in opponent.GetComponentsInChildren<BodyHitRegion>())
            {
                if (region.Region != BodyRegion.Chest) continue;
                point = region.GetComponent<Collider>().bounds.center;
                break;
            }
            var error = UnityEngine.Random.insideUnitCircle * aimError;
            point += opponent.transform.right * error.x + Vector3.up * error.y;
            var start = owner.transform.position + Vector3.up * 1.4f + owner.transform.forward;
            if (!ArrowBallistics.TryVelocity(start, point, launchSpeed, out var velocity))
                velocity = (point - start).normalized * launchSpeed;
            var arrow = Instantiate(arrowPrefab, start, Quaternion.identity);
            arrow.transform.SetParent(owner.transform.parent, true);
            arrow.Launch(velocity, owner.transform);
            if (flow.TryTrackShot(archerIndex, arrow)) ShotsFired++;
            else Destroy(arrow.gameObject);
        }

        // Detaches the AI from match notifications when its host is removed.
        private void OnDestroy()
        {
            if (flow != null) flow.Changed -= OnFlowChanged;
        }
    }
}
