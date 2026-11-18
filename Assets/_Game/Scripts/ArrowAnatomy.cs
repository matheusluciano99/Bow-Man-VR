using System;
using UnityEngine;

namespace BowMan
{
    [RequireComponent(typeof(ArrowFlight))]
    public sealed class ArrowAnatomy : MonoBehaviour
    {
        private ArrowFlight flight;
        public AnatomicalImpact LastImpact { get; private set; }
        public event Action<AnatomicalImpact> Resolved;

        // Receives only the shared projectile's real anatomical entry, without applying another damage outcome.
        private void Awake()
        {
            flight = GetComponent<ArrowFlight>();
            flight.BodyHit += OnBodyHit;
        }

        // Resolves fitted anatomy if present; ordinary test colliders cannot fabricate internal organ damage.
        private void OnBodyHit(BodyImpact hit)
        {
            var anatomy = hit.Collider.GetComponentInParent<ArcherAnatomy>();
            if (anatomy == null) return;
            LastImpact = anatomy.Trace(hit);
            Resolved?.Invoke(LastImpact);
        }

        // Removes the projectile listener when its character or match is cleaned up.
        private void OnDestroy()
        {
            if (flight != null) flight.BodyHit -= OnBodyHit;
        }
    }
}
