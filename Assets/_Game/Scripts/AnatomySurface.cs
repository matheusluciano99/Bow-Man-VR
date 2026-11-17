using System;
using System.Collections.Generic;
using UnityEngine;

namespace BowMan
{
    public sealed class AnatomySurface
    {
        private static readonly Dictionary<Mesh, AnatomySurface> cache = new Dictionary<Mesh, AnatomySurface>();
        private readonly struct Chunk
        {
            public readonly Bounds Bounds;
            public readonly int First;
            public readonly int End;

            // Retains one bounded batch of source triangles for ray pruning.
            public Chunk(Bounds bounds, int first, int end) { Bounds = bounds; First = first; End = end; }
        }
        private readonly Vector3[] vertices;
        private readonly int[] triangles;
        private readonly Bounds bounds;
        private readonly Chunk[] chunks;

        // Shares immutable mesh geometry across both repeated shots and match restarts.
        public static AnatomySurface For(Mesh mesh)
        {
            if (mesh == null) throw new ArgumentException("An anatomical mesh is required.");
            if (!cache.TryGetValue(mesh, out var geometry)) cache[mesh] = geometry = new AnatomySurface(mesh);
            return geometry;
        }

        // Drops stale geometry before each play session or an editor mesh rebake.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void ClearCache() => cache.Clear();

        // Caches a readable anatomical surface once, keeping per-shot queries independent of physics layers.
        public AnatomySurface(Mesh mesh)
        {
            if (mesh == null || !mesh.isReadable || mesh.vertexCount == 0)
                throw new ArgumentException("Anatomy requires a readable nonempty surface mesh.");
            vertices = mesh.vertices;
            triangles = mesh.triangles;
            bounds = mesh.bounds;
            if (triangles.Length == 0) throw new ArgumentException("Anatomy requires surface triangles.");
            var groups = new List<Chunk>();
            for (var first = 0; first < triangles.Length; first += 384)
            {
                var end = Mathf.Min(first + 384, triangles.Length);
                var box = new Bounds(vertices[triangles[first]], Vector3.zero);
                for (var i = first; i < end; i++) box.Encapsulate(vertices[triangles[i]]);
                groups.Add(new Chunk(box, first, end));
            }
            chunks = groups.ToArray();
        }

        // Finds the first finite-segment contact, including a segment that starts entirely inside the tissue.
        public bool TryHit(Vector3 start, Vector3 end, out Vector3 point)
        {
            point = default;
            var direction = end - start;
            var length = direction.magnitude;
            if (!(length > 0f) || float.IsInfinity(length)) return false;
            var ray = new Ray(start, direction / length);
            var inBounds = bounds.Contains(start);
            if (!inBounds && (!bounds.IntersectRay(ray, out var distance) ||
                distance > length)) return false;
            var nearest = float.PositiveInfinity;
            var crossings = inBounds ? new List<float>() : null;
            foreach (var chunk in chunks)
            {
                if (!chunk.Bounds.Contains(start) && (!chunk.Bounds.IntersectRay(ray, out var entry) ||
                    !inBounds && entry > length)) continue;
                for (var i = chunk.First; i < chunk.End; i += 3)
                {
                    var t = Crossing(i, start, direction);
                    if (t >= 0f && t <= 1f && t < nearest) nearest = t;
                    if (t > 0.00001f) crossings?.Add(t);
                }
            }
            if (crossings != null && IsInside(crossings)) { point = start; return true; }
            if (!float.IsPositiveInfinity(nearest))
            {
                point = start + direction * nearest;
                return true;
            }
            return false;
        }

        // Tests closed-tissue containment with deduplicated crossings, retaining real holes and concavities.
        private static bool IsInside(List<float> crossings)
        {
            crossings.Sort();
            var count = 0;
            var previous = float.NegativeInfinity;
            foreach (var t in crossings)
            {
                if (t - previous <= 0.00001f) continue;
                count++;
                previous = t;
            }
            return count % 2 == 1;
        }

        // Intersects either triangle face without requiring outward winding or colliders on the hidden anatomy.
        private float Crossing(int triangle, Vector3 start, Vector3 direction)
        {
            var a = vertices[triangles[triangle]];
            var first = vertices[triangles[triangle + 1]] - a;
            var second = vertices[triangles[triangle + 2]] - a;
            var cross = Vector3.Cross(direction, second);
            var determinant = Vector3.Dot(first, cross);
            if (determinant * determinant <= 0.000000000001f * first.sqrMagnitude * second.sqrMagnitude * direction.sqrMagnitude)
                return float.NaN;
            var inverse = 1f / determinant;
            var offset = start - a;
            var u = Vector3.Dot(offset, cross) * inverse;
            if (u < -0.000001f || u > 1.000001f) return float.NaN;
            var other = Vector3.Cross(offset, first);
            var v = Vector3.Dot(direction, other) * inverse;
            if (v < -0.000001f || u + v > 1.000001f) return float.NaN;
            var t = Vector3.Dot(second, other) * inverse;
            return t >= -0.000001f ? Mathf.Max(0f, t) : float.NaN;
        }
    }
}
