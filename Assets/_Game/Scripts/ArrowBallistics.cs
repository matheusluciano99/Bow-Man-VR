using UnityEngine;

namespace BowMan
{
    public static class ArrowBallistics
    {
        // Finds the lower-time gravity-driven arc at a fixed speed, rejecting invalid or unreachable targets.
        public static bool TryVelocity(Vector3 origin, Vector3 target, float speed, out Vector3 velocity)
        {
            velocity = Vector3.zero;
            var offset = target - origin;
            var distanceSquared = offset.sqrMagnitude;
            var speedSquared = speed * speed;
            if (!(speed > 0f) || float.IsInfinity(speedSquared) || float.IsNaN(distanceSquared) ||
                float.IsInfinity(distanceSquared) || distanceSquared <= 0f) return false;
            var gravity = Physics.gravity;
            var a = gravity.sqrMagnitude * 0.25f;
            var b = speedSquared + Vector3.Dot(offset, gravity);
            var discriminant = b * b - 4f * a * distanceSquared;
            if (discriminant < 0f) return false;
            var timeSquared = a < 0.000001f ? distanceSquared / speedSquared :
                2f * distanceSquared / (b + Mathf.Sqrt(discriminant));
            if (!(timeSquared > 0f) || float.IsInfinity(timeSquared)) return false;
            velocity = (offset - gravity * (0.5f * timeSquared)) / Mathf.Sqrt(timeSquared);
            return true;
        }
    }
}
