using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BowMan
{
    public readonly struct OrganHit
    {
        public AnatomyOrgan Organ { get; }
        public Vector3 LocalPoint { get; }
        public Vector3 Point => Organ.transform.TransformPoint(LocalPoint);

        // Keeps the actual tissue contact attached to its own moving anatomical bone.
        public OrganHit(AnatomyOrgan organ, Vector3 point)
        {
            Organ = organ;
            LocalPoint = organ.transform.InverseTransformPoint(point);
        }
    }

    public sealed class AnatomicalImpact
    {
        public BodyImpact Entry { get; }
        public IReadOnlyList<OrganHit> Organs { get; }

        // Associates tissue contacts with the real entry region, side and finite internal arrow path.
        public AnatomicalImpact(BodyImpact entry, OrganHit[] organs) { Entry = entry; Organs = organs; }
    }

    public sealed class ArcherAnatomy : MonoBehaviour
    {
        private AnatomyOrgan[] organs;

        // Traces fitted tissue surfaces at impact time and deduplicates organs split across animated bones.
        public AnatomicalImpact Trace(BodyImpact entry)
        {
            if (entry.Collider == null || !entry.Collider.transform.IsChildOf(transform))
                throw new ArgumentException("The entry must belong to this archer.");
            organs ??= GetComponentsInChildren<AnatomyOrgan>();
            var hits = new List<OrganHit>();
            foreach (var organ in organs)
                if (organ.TryHit(entry.Point, entry.PathEnd, out var point)) hits.Add(new OrganHit(organ, point));
            var ordered = hits.OrderBy(h => (h.Point - entry.Point).sqrMagnitude)
                .GroupBy(h => (h.Organ.Kind, h.Organ.Side)).Select(g => g.First()).ToArray();
            return new AnatomicalImpact(entry, ordered);
        }
    }
}
