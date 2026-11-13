using System;
using UnityEngine;

namespace BowMan
{
    public sealed class ArrowFlight : MonoBehaviour
    {
        [SerializeField, Min(0.01f)] private float lifetime = 8f;
        [SerializeField, Min(0.01f)] private float impactLifetime = 3f;
        [SerializeField] private LayerMask hitLayers = Physics.DefaultRaycastLayers;

        private Vector3 velocity;
        private Transform owner;
        private bool launched;
        private bool flying;

        public event Action<RaycastHit> Hit;

        // The root is the arrow tip; visual geometry extends behind it along local -Z.
        public void Launch(Vector3 initialVelocity, Transform firingArcher = null)
        {
            var speedSquared = initialVelocity.sqrMagnitude;
            if (float.IsNaN(speedSquared) || float.IsInfinity(speedSquared) || speedSquared <= 0f)
                throw new ArgumentException("Launch velocity must be finite and nonzero.", nameof(initialVelocity));
            if (launched)
                throw new InvalidOperationException("An arrow can only be launched once.");

            owner = firingArcher;
            velocity = initialVelocity;
            launched = flying = true;
            transform.rotation = Quaternion.LookRotation(velocity);
            Destroy(gameObject, lifetime);
        }

        private void FixedUpdate()
        {
            if (!flying) return;

            var dt = Time.fixedDeltaTime;
            var displacement = velocity * dt + Physics.gravity * (0.5f * dt * dt);
            velocity += Physics.gravity * dt;
            var distance = displacement.magnitude;
            var nearest = new RaycastHit { distance = float.PositiveInfinity };
            if (distance > 0f)
            {
                foreach (var hit in Physics.RaycastAll(transform.position, displacement / distance,
                    distance, hitLayers, QueryTriggerInteraction.Ignore))
                {
                    if (owner != null && hit.transform.IsChildOf(owner)) continue;
                    if (hit.transform.IsChildOf(transform)) continue;
                    if (hit.distance < nearest.distance) nearest = hit;
                }
            }

            if (nearest.collider != null)
            {
                flying = false;
                transform.position = nearest.point;
                transform.rotation = Quaternion.LookRotation(displacement);
                Destroy(gameObject, impactLifetime);
                Hit?.Invoke(nearest);
                return;
            }

            transform.position += displacement;
            if (velocity.sqrMagnitude > 0f) transform.rotation = Quaternion.LookRotation(velocity);
        }
    }
}
