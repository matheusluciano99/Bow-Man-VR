using UnityEngine;

namespace BowMan
{
    [RequireComponent(typeof(ArrowFlight))]
    public sealed class ArrowWoundBlood : MonoBehaviour
    {
        [SerializeField] private ParticleSystem woundPrefab;
        private ArrowFlight flight;

        // Listens only for a real anatomical impact, independently of damage and turn resolution.
        private void Awake()
        {
            flight = GetComponent<ArrowFlight>();
            flight.BodyHit += OnBodyHit;
        }

        // Anchors the emitter to the actual entry point and bone while spilled droplets move in world space.
        private void OnBodyHit(BodyImpact hit)
        {
            if (woundPrefab == null) throw new System.InvalidOperationException("Arrow blood requires its wound prefab.");
            var wound = Instantiate(woundPrefab, hit.Point, Quaternion.LookRotation(hit.Normal));
            wound.transform.SetParent(hit.Collider.transform, true);
        }

        // Releases the arrow listener without removing a wound that is still finishing on the character.
        private void OnDestroy()
        {
            if (flight != null) flight.BodyHit -= OnBodyHit;
        }
    }
}
