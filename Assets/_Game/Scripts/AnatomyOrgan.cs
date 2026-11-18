using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BowMan
{
    public enum OrganKind { Brain, Heart, Lung, Liver, Stomach, Spleen, Pancreas, Kidney, SmallIntestine, LargeIntestine, Bladder }

    public sealed class AnatomyOrgan : MonoBehaviour
    {
        // shortcut: Tissue groups follow rigid bones; add deforming anatomy if a reaction exposes a visible fit error.
        [SerializeField] private OrganKind kind;
        [SerializeField] private BodySide side;
        [SerializeField] private BodyRegion region;
        [SerializeField] private Mesh[] surfaces;
        private AnatomySurface[] geometry;

        public OrganKind Kind => kind;
        public BodySide Side => side;
        public BodyRegion Region => region;
        public IReadOnlyList<Mesh> Surfaces => surfaces;
        public string DisplayName => (side == BodySide.Center ? "" : side + " ") + (kind switch
        {
            OrganKind.SmallIntestine => "Small intestine",
            OrganKind.LargeIntestine => "Large intestine",
            _ => kind.ToString()
        });

        // Prepares shared readable tissue geometry at actor creation rather than during the first arrow impact.
        private void Awake() => Prepare();

        // Resolves each atomic surface through the session cache, preserving per-character bone transforms.
        private void Prepare()
        {
            if (geometry != null) return;
            if (surfaces == null || surfaces.Length == 0) throw new InvalidOperationException("Missing anatomy surfaces.");
            geometry = surfaces.Select(AnatomySurface.For).ToArray();
        }

        // Finds the nearest actual tissue contact across the atomic surfaces that form this organ group.
        public bool TryHit(Vector3 start, Vector3 end, out Vector3 point)
        {
            Prepare();
            point = default;
            var first = transform.InverseTransformPoint(start);
            var last = transform.InverseTransformPoint(end);
            var nearest = float.PositiveInfinity;
            foreach (var surface in geometry)
            {
                if (!surface.TryHit(first, last, out var contact)) continue;
                var distance = (contact - first).sqrMagnitude;
                if (distance >= nearest) continue;
                nearest = distance;
                point = transform.TransformPoint(contact);
            }
            return !float.IsPositiveInfinity(nearest);
        }
    }
}
