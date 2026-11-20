using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

namespace BowMan
{
    [DefaultExecutionOrder(150)]
    public sealed class FatalCutaway : MonoBehaviour
    {
        private const float Duration = 3f;
        private const float Zoom = 1.35f;
        private static readonly int Ink = Shader.PropertyToID("_Color");
        private readonly List<(Transform source, Transform copy, Vector3 center, Vector3 size)> parts = new();
        private Material material;
        private Material pathMaterial;
        private Material backgroundMaterial;
        private Transform window;
        private Transform anchor;
        private Transform path;
        private Vector3 localStart;
        private Vector3 localEnd;
        private Vector3 localUp;
        private float opened;

        public string Caption { get; private set; }
        public bool IsVisible => window != null;

        // Opens a stereo close-up of the resolved path and its contacted geometry inside the defeated avatar.
        public void Show(AnatomicalImpact report)
        {
            Clear();
            if (material == null)
            {
                material = new Material(Resources.Load<Shader>("FatalCutaway"));
                backgroundMaterial = new Material(material) { renderQueue = 2990 };
                pathMaterial = new Material(material) { renderQueue = 3010 };
            }
            anchor = report.Entry.Volume.transform;
            localStart = anchor.InverseTransformPoint(report.Entry.Point);
            localEnd = anchor.InverseTransformPoint(report.Entry.PathEnd);
            var up = Vector3.ProjectOnPlane(anchor.root.up, report.Entry.Direction);
            if (up.sqrMagnitude < 0.001f) up = Vector3.ProjectOnPlane(anchor.root.right, report.Entry.Direction);
            localUp = anchor.InverseTransformDirection(up.normalized);
            opened = Time.unscaledTime;
            window = new GameObject("Fatal cutaway").transform;
            window.SetParent(transform, false);
            window.localPosition = new Vector3(0f, 0.04f, 1.25f);

            var background = Box("Cutaway background", new Color(0.035f, 0.065f, 0.1f, 0.97f));
            background.GetComponent<MeshRenderer>().sharedMaterial = backgroundMaterial;
            background.localPosition = new Vector3(0f, -0.17f, 0.32f);
            background.localScale = new Vector3(1.3f, 0.93f, 0.002f);
            foreach (var hit in report.Organs)
                foreach (var mesh in hit.Organ.Surfaces)
                {
                    var tissue = new GameObject(hit.Organ.DisplayName);
                    tissue.AddComponent<MeshFilter>().sharedMesh = mesh;
                    var renderer = tissue.AddComponent<MeshRenderer>();
                    Style(renderer, new Color(0.16f, 0.8f, 1f, 0.43f));
                    parts.Add((hit.Organ.transform, tissue.transform, Vector3.zero, Vector3.one));
                }
            foreach (var hit in report.Bones)
            {
                var color = hit.Fractured ? new Color(1f, 0.35f, 0.18f, 0.75f) : new Color(0.79f, 0.85f, 0.93f, 0.42f);
                var bone = Box(BoneName(hit.Bone), color);
                parts.Add((hit.Frame, bone, hit.LocalBounds.center, hit.LocalBounds.size));
            }
            path = Box("Arrow path", new Color(1f, 0.85f, 0.22f));
            path.GetComponent<MeshRenderer>().sharedMaterial = pathMaterial;
            var organs = report.Organs.Select(h => h.Organ.DisplayName);
            var fractures = report.Bones.Where(h => h.Fractured).Select(h => BoneName(h.Bone));
            Caption = $"X-ray   {Readable(report.Entry.Region)}\nOrgans: {Names(organs)}\nFractures: {Names(fractures)}";
            LateUpdate();
        }

        // Labels the head bone as the skull and preserves rig granularity for grouped segments.
        private static string BoneName(HumanBodyBones bone) => bone == HumanBodyBones.Head ? "Skull" : Readable(bone);

        // Separates the words of a Humanoid bone or region for the match feedback.
        private static string Readable(System.Enum value) => Regex.Replace(value.ToString(), "(?<=[a-z])(?=[A-Z])", " ");

        // Shows an explicit empty result when no structure in the category was struck.
        private static string Names(IEnumerable<string> names) => string.Join(", ", names.DefaultIfEmpty("none"));

        // Creates an inert visual for the embedded path or a contacted rig bone.
        private Transform Box(string label, Color color)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = label;
            var collider = box.GetComponent<Collider>();
            collider.enabled = false;
            Destroy(collider);
            Style(box.GetComponent<MeshRenderer>(), color);
            return box.transform;
        }

        // Uses one stereo material for the window and colors each part independently.
        private void Style(MeshRenderer renderer, Color color)
        {
            renderer.transform.SetParent(window, false);
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            var properties = new MaterialPropertyBlock();
            properties.SetColor(Ink, color);
            renderer.SetPropertyBlock(properties);
        }

        // Tracks the actual animated volume frames in a side view while revealing the finite path.
        private void LateUpdate()
        {
            if (!IsVisible) return;
            var elapsed = Time.unscaledTime - opened;
            if (anchor == null || elapsed >= Duration) { Clear(); return; }
            var start = anchor.TransformPoint(localStart);
            var end = anchor.TransformPoint(localEnd);
            var middle = (start + end) * 0.5f;
            var up = anchor.TransformDirection(localUp);
            var basis = Quaternion.Inverse(Quaternion.LookRotation(Vector3.Cross(end - start, up), up));
            foreach (var part in parts)
            {
                part.copy.localPosition = basis * (part.source.TransformPoint(part.center) - middle) * Zoom;
                part.copy.localRotation = basis * part.source.rotation;
                part.copy.localScale = Vector3.Scale(part.source.lossyScale, part.size) * Zoom;
            }
            var span = Vector3.Distance(start, end) * Zoom;
            var length = Mathf.Max(0.002f, span * Mathf.Clamp01(elapsed / 0.45f));
            path.localScale = new Vector3(length, 0.006f, 0.006f);
            path.localPosition = Vector3.right * ((length - span) * 0.5f);
        }

        // Removes the finishing view immediately on timeout, scene unload or match restart.
        public void Clear()
        {
            if (window != null) { window.gameObject.SetActive(false); Destroy(window.gameObject); }
            window = null;
            anchor = null;
            parts.Clear();
            Caption = null;
        }

        // Releases transient presentation objects and their material with the tracked camera.
        private void OnDestroy()
        {
            Clear();
            if (material != null) Destroy(material);
            if (pathMaterial != null) Destroy(pathMaterial);
            if (backgroundMaterial != null) Destroy(backgroundMaterial);
        }
    }
}
