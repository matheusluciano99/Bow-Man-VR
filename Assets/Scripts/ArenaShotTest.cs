using UnityEngine;
using UnityEngine.InputSystem;

namespace BowMan
{
    public sealed class ArenaShotTest : MonoBehaviour
    {
        [SerializeField] private ArrowFlight arrowPrefab;
        [SerializeField] private Transform firingArcher;
        [SerializeField] private Vector3 launchVelocity = new Vector3(0f, 3.8f, 25f);

        private int shots;
        private int hits;
        private string lastHit = "Waiting for impact";

        private void Start() => Fire();

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame) Fire();
        }

        public void Fire()
        {
            if (arrowPrefab == null) return;
            var arrow = Instantiate(arrowPrefab, transform.position, transform.rotation);
            arrow.Hit += OnHit;
            arrow.Launch(transform.TransformDirection(launchVelocity), firingArcher);
            shots++;
        }

        private void OnHit(RaycastHit hit)
        {
            hits++;
            lastHit = $"{hit.collider.name} at {hit.point:F2}";
            Debug.Log($"Test arrow hit {lastHit}", hit.collider);
        }

        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(16, 16, 340, 110), GUI.skin.box);
            GUILayout.Label("Arrow flight test — Space to fire");
            GUILayout.Label($"Shots: {shots}   Hits: {hits}");
            GUILayout.Label(lastHit);
            if (GUILayout.Button("Fire test arrow")) Fire();
            GUILayout.EndArea();
        }
    }
}
