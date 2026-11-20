using UnityEngine;

namespace BowMan
{
    public enum ArenaSetting : byte { CastleCourtyard, WoodlandRange, VillageYard }

    public sealed class DuelEnvironment : MonoBehaviour
    {
        [SerializeField] private Material sky;
        private Material previousSky;

        // Chooses once per new local match or host-authorized online round.
        public static ArenaSetting Choose() => (ArenaSetting)Random.Range(0, 3);

        // Places the authored setting around the midpoint of the two level spawns.
        public static void Place(ArenaSetting setting, Transform round, Transform first, Transform second)
        {
            var prefab = Resources.Load<DuelEnvironment>("Arenas/" + setting);
            var direction = Vector3.ProjectOnPlane(second.position - first.position, Vector3.up);
            Instantiate(prefab, (first.position + second.position) * 0.5f,
                Quaternion.LookRotation(direction, Vector3.up), round);
        }

        // Uses the setting's panorama while the round is visible.
        private void OnEnable()
        {
            previousSky = RenderSettings.skybox;
            RenderSettings.skybox = sky;
        }

        // Restores the scene's sky when the round is replaced or the player returns to the menu.
        private void OnDisable()
        {
            if (RenderSettings.skybox == sky) RenderSettings.skybox = previousSky;
        }
    }
}
