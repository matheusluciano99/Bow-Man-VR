using UnityEngine;
using UnityEngine.InputSystem;

namespace BowMan
{
    public sealed class ArenaShotTest : MonoBehaviour
    {
        [SerializeField] private ArrowFlight arrowPrefab;
        [SerializeField] private Transform firingArcher;
        [SerializeField] private Vector3 launchVelocity = new Vector3(0f, 3.8f, 25f);
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

        // Creates an arrow, listens for its impact, and launches it in the launcher's local direction.
        public void Fire()
        {
            if (arrowPrefab == null) return;
            var arrow = Instantiate(arrowPrefab, transform.position, transform.rotation);
            arrow.Hit += OnHit;
            arrow.Launch(transform.TransformDirection(launchVelocity), firingArcher);
            shots++;
        }

        // Counts and records the impact, then applies test damage if the struck object belongs to an archer.
        private void OnHit(RaycastHit hit)
        {
            hits++;
            lastHit = $"{hit.collider.name} at {hit.point:F2}";
            Debug.Log($"Test arrow hit {lastHit}", hit.collider);
            var health = hit.collider.GetComponentInParent<ArcherHealth>();
            if (health != null) health.TakeDamage(testDamage);
        }

        // Counts the target's defeat event and reports it once in the Console.
        private void OnTargetDefeated()
        {
            defeats++;
            Debug.Log($"Test archer defeated: {targetHealth.name}", targetHealth);
        }

        // Draws desktop shot, health, and defeat feedback with a button to repeat the test shot.
        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(16, 16, 380, 210), GUI.skin.box);
            GUILayout.Label("Arrow flight test — Space to fire");
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
    }
}
