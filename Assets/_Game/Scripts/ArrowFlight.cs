using System;
using UnityEngine;

namespace BowMan
{
    public sealed class ArrowFlight : MonoBehaviour
    {
        [SerializeField, Min(0.01f)] private float lifetime = 8f;
        [SerializeField, Min(0.01f)] private float impactLifetime = 3f;
        [SerializeField] private LayerMask hitLayers = Physics.DefaultRaycastLayers;
        [SerializeField, Min(0.01f)] private float bodyPenetration = 0.25f;

        private Vector3 velocity;
        private Transform owner;
        private bool launched;
        private bool flying;
        private bool useGravity;
        private float remainingLifetime;
        private float shotStrength;

        public event Action<RaycastHit> Hit;
        public event Action<BodyImpact> BodyHit;
        public event Action Expired;
        public bool IsFlying => flying;

        // Starts one timed flight with the given velocity, ignoring the firing archer.
        // In flight the root is the tip; an embedded root anchors the entry while geometry penetrates ahead.
        public void Launch(Vector3 initialVelocity, Transform firingArcher = null, bool useGravity = true, float shotStrength = 1f)
        {
            var speedSquared = initialVelocity.sqrMagnitude;
            if (float.IsNaN(speedSquared) || float.IsInfinity(speedSquared) || speedSquared <= 0f)
                throw new ArgumentException("Launch velocity must be finite and nonzero.", nameof(initialVelocity));
            if (!(shotStrength >= 0f && shotStrength <= 1f))
                throw new ArgumentOutOfRangeException(nameof(shotStrength));
            if (launched)
                throw new InvalidOperationException("An arrow can only be launched once.");
            if (!(bodyPenetration > 0f) || float.IsInfinity(bodyPenetration))
                throw new InvalidOperationException("Body penetration must be finite and positive.");

            owner = firingArcher;
            velocity = initialVelocity;
            this.useGravity = useGravity;
            this.shotStrength = shotStrength;
            launched = flying = true;
            transform.rotation = Quaternion.LookRotation(velocity);
            remainingLifetime = lifetime;
        }

        // Sweeps the arrow's tip along its trajectory and stops at the first valid hit.
        private void FixedUpdate()
        {
            if (!flying) return;

            remainingLifetime -= Time.fixedDeltaTime;
            if (remainingLifetime <= 0f)
            {
                flying = false;
                Expired?.Invoke();
                Destroy(gameObject);
                return;
            }

            Physics.SyncTransforms();

            var dt = Time.fixedDeltaTime;
            var gravity = useGravity ? Physics.gravity : Vector3.zero;
            var displacement = velocity * dt + gravity * (0.5f * dt * dt);
            velocity += gravity * dt;
            var nearest = FirstImpact(transform.position, displacement, owner);

            if (nearest.collider != null)
            {
                flying = false;
                transform.position = nearest.point;
                transform.rotation = Quaternion.LookRotation(displacement);
                if (nearest.collider.TryGetComponent<BodyHitRegion>(out var region))
                {
                    transform.SetParent(nearest.collider.transform, true);
                    foreach (Transform visual in transform) visual.position += transform.forward * bodyPenetration;
                    BodyHit?.Invoke(new BodyImpact(region, nearest, displacement, bodyPenetration, shotStrength));
                }
                else Destroy(gameObject, Mathf.Min(impactLifetime, remainingLifetime));
                Hit?.Invoke(nearest);
                return;
            }

            transform.position += displacement;
            if (velocity.sqrMagnitude > 0f) transform.rotation = Quaternion.LookRotation(velocity);
        }

        // Finds the first solid impact with the same filtering for preview aim and live flight.
        internal RaycastHit FirstImpact(Vector3 origin, Vector3 displacement, Transform firingArcher)
        {
            var nearest = new RaycastHit { distance = float.PositiveInfinity };
            var distance = displacement.magnitude;
            if (distance <= 0f) return nearest;

            foreach (var hit in Physics.RaycastAll(origin, displacement / distance,
                distance, hitLayers, QueryTriggerInteraction.Ignore))
            {
                if (firingArcher != null && hit.collider.transform.IsChildOf(firingArcher)) continue;
                if (hit.collider.transform.IsChildOf(transform)) continue;
                if (hit.distance < nearest.distance ||
                    hit.distance == nearest.distance && hit.collider.GetInstanceID() < nearest.collider.GetInstanceID())
                    nearest = hit;
            }
            return nearest;
        }
    }
}
