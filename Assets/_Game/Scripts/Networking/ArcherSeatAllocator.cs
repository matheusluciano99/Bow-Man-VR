using System.Collections.Generic;

namespace BowMan.Networking
{
    // Server-side record of which client holds each of the two archer seats.
    public sealed class ArcherSeatAllocator
    {
        public const string FullReason = "Match is full";

        private static readonly ArcherRole[] SeatOrder = { ArcherRole.Archer1, ArcherRole.Archer2 };
        private readonly Dictionary<ulong, ArcherRole> roles = new Dictionary<ulong, ArcherRole>();

        public int Occupied => roles.Count;

        // Gives the client its existing seat or the lowest free one; fails when both are taken.
        public bool TryAssign(ulong clientId, out ArcherRole role)
        {
            if (roles.TryGetValue(clientId, out role)) return true;
            foreach (var seat in SeatOrder)
            {
                if (roles.ContainsValue(seat)) continue;
                roles[clientId] = seat;
                role = seat;
                return true;
            }
            role = ArcherRole.None;
            return false;
        }

        // Frees the client's seat; returns false when the client held none.
        public bool Release(ulong clientId) => roles.Remove(clientId);

        // Returns the client's seat, or None when the client has no seat.
        public ArcherRole RoleOf(ulong clientId) =>
            roles.TryGetValue(clientId, out var role) ? role : ArcherRole.None;

        // Frees every seat, used when a session ends.
        public void Clear() => roles.Clear();
    }
}
