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
        private float remainingLifetime;

        public event Action<RaycastHit> Hit;
        public event Action<BodyImpact> BodyHit;

        // Starts one timed flight with the given velocity, ignoring the firing archer.
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
            remainingLifetime = lifetime;
        }

        // Applies gravity each physics step, sweeps the tip's path, and stops at the first valid hit.
        private void FixedUpdate()
        {
            if (!flying) return;

            remainingLifetime -= Time.fixedDeltaTime;
            if (remainingLifetime <= 0f)
            {
                flying = false;
                Destroy(gameObject);
                return;
            }

            Physics.SyncTransforms();

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
                    if (owner != null && hit.collider.transform.IsChildOf(owner)) continue;
                    if (hit.collider.transform.IsChildOf(transform)) continue;
                    if (hit.distance < nearest.distance ||
                        hit.distance == nearest.distance && hit.collider.GetInstanceID() < nearest.collider.GetInstanceID())
                        nearest = hit;
                }
            }

            if (nearest.collider != null)
            {
                flying = false;
                transform.position = nearest.point;
                transform.rotation = Quaternion.LookRotation(displacement);
                if (nearest.collider.TryGetComponent<BodyHitRegion>(out var region))
                {
                    transform.SetParent(nearest.collider.transform, true);
                    BodyHit?.Invoke(new BodyImpact(region, nearest, displacement));
                }
                else Destroy(gameObject, Mathf.Min(impactLifetime, remainingLifetime));
                Hit?.Invoke(nearest);
                return;
            }

            transform.position += displacement;
            if (velocity.sqrMagnitude > 0f) transform.rotation = Quaternion.LookRotation(velocity);
        }
    }
}
