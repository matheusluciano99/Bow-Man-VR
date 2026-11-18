using UnityEngine;

namespace BowMan.Networking
{
    // Holds the two arena spawn transforms used for each archer role.
    public sealed class ArcherSpawnPoints : MonoBehaviour
    {
        [SerializeField] private Transform archer1;
        [SerializeField] private Transform archer2;

        // Returns the spawn for a role, or null when the role is None.
        public Transform For(ArcherRole role)
        {
            switch (role)
            {
                case ArcherRole.Archer1: return archer1;
                case ArcherRole.Archer2: return archer2;
                default: return null;
            }
        }
    }
}
