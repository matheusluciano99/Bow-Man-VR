using UnityEngine;
using UnityEngine.InputSystem;

namespace BowMan
{
    public sealed class ArenaShotTest : MonoBehaviour
    {
        [SerializeField] private ArrowFlight arrowPrefab;
        [SerializeField] private Transform firingArcher;
        [SerializeField] private Transform targetArcher;
        [SerializeField] private Vector3 launchVelocity = new Vector3(0f, 3.8f, 25f);
        private BodyRegion selectedRegion = BodyRegion.Chest;
        [SerializeField] private ArcherHealth targetHealth;
        [SerializeField, Min(1)] private int testDamage = 25;

        private int shots;
        private int hits;
        private int defeats;
        private string lastHit = "Waiting for impact";

        // Listens for the opposing test archer's defeat while this test panel is active.
        private void OnEnable()
        {
            if (targetHealth != null) targetHealth.Defeated += OnTargetDefeated;
        }

        // Removes the defeat listener when the test panel is disabled or its scene closes.
        private void OnDisable()
        {
            if (targetHealth != null) targetHealth.Defeated -= OnTargetDefeated;
        }

        // Fires the initial test arrow when the scene starts.
        private void Start() => Fire();

        // Checks once per frame whether Space was pressed to fire another test arrow.
        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame) Fire();
        }

        // Launches at the selected anatomical target, using the configured velocity when no target is assigned.
        public void Fire()
        {
            if (arrowPrefab == null) return;
            var arrow = Instantiate(arrowPrefab, transform.position, transform.rotation);
            arrow.Hit += OnHit;
            arrow.BodyHit += OnBodyHit;
            var velocity = transform.TransformDirection(launchVelocity);
            if (targetArcher != null)
            {
                foreach (var region in targetArcher.GetComponentsInChildren<BodyHitRegion>())
                {
                    if (region.Region != selectedRegion) continue;
                    const float flightTime = 0.8f;
                    velocity = (region.GetComponent<Collider>().bounds.center - transform.position) / flightTime
                        - Physics.gravity * (flightTime * 0.5f);
                    break;
                }
            }
            arrow.Launch(velocity, firingArcher);
            shots++;
        }

        // Counts and records the impact, then applies test damage if the struck object belongs to an archer.
        private void OnHit(RaycastHit hit)
        {
            hits++;
            if (!hit.collider.TryGetComponent<BodyHitRegion>(out _))
                lastHit = $"Scenery: {hit.collider.name} at {hit.point:F2}";
            Debug.Log($"Test arrow hit {lastHit}", hit.collider);
            var health = hit.collider.GetComponentInParent<ArcherHealth>();
            if (health != null) health.TakeDamage(testDamage);
        }

        // Reports the anatomical result produced by the arrow, independently of the selected test target.
        private void OnBodyHit(BodyImpact hit) =>
            lastHit = $"{hit.Region} ({hit.Side}) at {hit.Point:F2}\nDirection: {hit.Direction:F2}";

        // Draws the desktop test panel with shot counts, the last impact, and a fire button.
        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(16, 16, 430, 340), GUI.skin.box);
            GUILayout.Label("Body region test — Space to fire");
            selectedRegion = (BodyRegion)GUILayout.SelectionGrid((int)selectedRegion,
                System.Enum.GetNames(typeof(BodyRegion)), 3);
            GUILayout.Label($"Shots: {shots}   Hits: {hits}");
            GUILayout.Label(lastHit);
            if (targetHealth != null)
            {
                GUILayout.Label($"Target health: {targetHealth.CurrentHealth}/{targetHealth.MaxHealth}");
                GUILayout.Label($"{(targetHealth.IsDefeated ? "Defeated" : "Alive")}   Defeat events: {defeats}");
            }
            GUILayout.Label($"Test damage: {testDamage} (provisional)");
            if (GUILayout.Button("Fire test arrow")) Fire();
            GUILayout.EndArea();
        }

        // Counts the target's defeat event and reports it once in the Console.
        private void OnTargetDefeated()
        {
            defeats++;
            Debug.Log($"Test archer defeated: {targetHealth.name}", targetHealth);
        }
    }
}
