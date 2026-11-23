using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Rendering;

namespace BowMan
{
    [DefaultExecutionOrder(150)]
    public sealed class FatalCutaway : MonoBehaviour
    {
        private const float ImpactTime = 0.95f;
        private const float InspectTime = 2.75f;
        private const float Duration = 3.65f;
        private static readonly Vector3 StageOrigin = new Vector3(0f, -4096f, 0f);
        private static readonly int Tint = Shader.PropertyToID("_Color");

        private readonly List<Mesh> snapshots = new();
        private readonly List<Renderer> shell = new();
        private readonly List<(Transform part, Vector3 rest, Vector3 offset)> fragments = new();
        private Transform window, stage, arrow, anatomy;
        private Camera replayCamera;
        private RenderTexture picture;
        private Material screenMaterial, cutawayMaterial;
        private AudioSource sound;
        private Quaternion view;
        private Vector3 entry, pathEnd;
        private float started;
        private bool contactPlayed;

        public string Caption { get; private set; }
        public bool IsVisible => window != null;

        // Stages the real lethal hit once, leaving the tracked camera and duel simulation in place.
        public void Show(AnatomicalImpact report, ArrowFlight arrowPrefab)
        {
            Clear();
            entry = report.Entry.Point;
            var direction = report.Entry.Direction;
            var root = report.Entry.Collider.GetComponentInParent<ArcherHealth>();
            var up = Vector3.ProjectOnPlane(root.transform.up, direction);
            if (up.sqrMagnitude < .001f) up = Vector3.ProjectOnPlane(root.transform.right, direction);
            view = Quaternion.Inverse(Quaternion.LookRotation(Vector3.Cross(direction, up), up));
            pathEnd = view * (report.Entry.PathEnd - entry);
            started = Time.unscaledTime;
            Caption = $"{Readable(report.Entry.Region)}\nOrgans: {Names(report.Organs.Select(h => h.Organ.DisplayName))}\n" +
                $"Fractures: {Names(report.Bones.Where(h => h.Fractured).Select(h => h.Bone == HumanBodyBones.Head ? "Skull" : Readable(h.Bone)))}";

            BuildScreen();
            stage = new GameObject("Fatal replay stage").transform;
            stage.position = StageOrigin;
            replayCamera = new GameObject("Replay camera").AddComponent<Camera>();
            replayCamera.transform.SetParent(stage, false);
            replayCamera.stereoTargetEye = StereoTargetEyeMask.None;
            replayCamera.enabled = picture != null;
            replayCamera.targetTexture = picture;
            replayCamera.depth = -20;
            replayCamera.nearClipPlane = .015f;
            replayCamera.farClipPlane = 5f;
            replayCamera.clearFlags = CameraClearFlags.SolidColor;
            replayCamera.backgroundColor = new Color(.033f, .039f, .044f);
            replayCamera.allowHDR = false;

            cutawayMaterial = new Material(Resources.Load<Shader>("FatalCutaway"));
            foreach (var source in root.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                var frozen = new Mesh();
                source.BakeMesh(frozen);
                snapshots.Add(frozen);
                var skin = CopyMesh("Frozen archer", stage, frozen, source.sharedMaterials);
                Place(skin.transform, source.transform);
                shell.Add(skin);
            }

            anatomy = new GameObject("Contact anatomy").transform;
            anatomy.SetParent(stage, false);
            foreach (var hit in report.Organs)
                foreach (var surface in hit.Organ.Surfaces)
                {
                    var organ = CopyMesh(hit.Organ.DisplayName, anatomy, surface, new[] { cutawayMaterial });
                    Place(organ.transform, hit.Organ.transform);
                    Color(organ, new Color(.74f, .19f, .23f, .63f));
                }
            foreach (var hit in report.Bones) StageBone(hit);

            arrow = new GameObject("Replay arrow").transform;
            arrow.SetParent(stage, false);
            arrow.localRotation = Quaternion.LookRotation(Vector3.right, Vector3.up);
            foreach (var part in arrowPrefab.GetComponentsInChildren<MeshFilter>())
            {
                var prop = CopyMesh(part.name, arrow, part.sharedMesh, part.GetComponent<MeshRenderer>().sharedMaterials);
                prop.transform.localPosition = part.transform.localPosition;
                prop.transform.localRotation = part.transform.localRotation;
                prop.transform.localScale = part.transform.localScale;
            }
            Tick(0f);
        }

        // Places the replay on a fixed stereo surface where the player can look around it.
        private void BuildScreen()
        {
            window = new GameObject("Fatal cutaway").transform;
            window.SetParent(transform, false);
            window.localPosition = new Vector3(0f, .075f, 1.34f);
            ArcherInterface.Surface(window, "Replay frame", new Vector3(0, 0, .012f),
                new Vector2(1.2f, .705f), ArcherInterface.Brass);
            var screen = ArcherInterface.Surface(window, "Replay image", Vector3.zero,
                new Vector2(1.178f, .685f), ArcherInterface.Ink);
            if (SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null)
            {
                picture = new RenderTexture(640, 372, 24);
                screenMaterial = new Material(screen.sharedMaterial);
                screenMaterial.SetTexture("_BaseMap", picture);
                screenMaterial.SetColor("_BaseColor", UnityEngine.Color.white);
                screen.sharedMaterial = screenMaterial;
            }
            var heading = ArcherInterface.Label(window, "Final shot", new Vector3(0, .393f, 0), .017f, true);
            ArcherInterface.Surface(window, "Replay title backing", new Vector3(0, heading.transform.localPosition.y, .012f),
                new Vector2(.43f, .09f), ArcherInterface.Ink);
            sound = window.gameObject.AddComponent<AudioSource>();
            sound.PlayOneShot(Resources.Load<AudioClip>("DuelAudio/Flight"), .5f);
        }

        // Adds a rounded bone form at the game contact and lets only confirmed fractures separate.
        private void StageBone(BoneHit hit)
        {
            var size = hit.LocalBounds.size;
            var axis = size.x > size.y ? (size.x > size.z ? 0 : 2) : (size.y > size.z ? 1 : 2);
            var span = Vector3.zero;
            span[axis] = size[axis];
            var center = hit.LocalBounds.center;
            for (int i = 0; i < (hit.Fractured ? 2 : 1); i++)
            {
                float sign = i == 0 ? -1f : 1f;
                var segment = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                segment.name = hit.Bone.ToString();
                var collider = segment.GetComponent<Collider>();
                collider.enabled = false;
                Destroy(collider);
                segment.transform.SetParent(anatomy, false);
                segment.transform.localPosition = view * (hit.Frame.TransformPoint(center +
                    (hit.Fractured ? span * (.24f * sign) : Vector3.zero)) - entry);
                segment.transform.localRotation = view * hit.Frame.rotation;
                var dimensions = size;
                if (hit.Fractured) dimensions[axis] *= .58f;
                segment.transform.localScale = Vector3.Scale(hit.Frame.lossyScale, dimensions);
                var renderer = segment.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = cutawayMaterial;
                Color(renderer, new Color(.96f, .81f, .6f, .88f));
                if (hit.Fractured)
                    fragments.Add((segment.transform, segment.transform.localPosition,
                        view * hit.Frame.TransformDirection(span.normalized) * (.022f * sign)));
            }
        }

        // Copies render geometry without physics, weapons or gameplay scripts.
        private static MeshRenderer CopyMesh(string name, Transform parent, Mesh mesh, Material[] materials)
        {
            var copy = new GameObject(name);
            copy.transform.SetParent(parent, false);
            copy.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = copy.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = materials;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            return renderer;
        }

        // Converts a pose at the moment of impact into coordinates in the replay stage.
        private void Place(Transform copy, Transform source)
        {
            copy.localPosition = view * (source.position - entry);
            copy.localRotation = view * source.rotation;
            copy.localScale = source.lossyScale;
        }

        // Sets a layer's color without mutating shared source materials.
        private static void Color(Renderer renderer, Color color)
        {
            var block = new MaterialPropertyBlock();
            block.SetColor(Tint, color);
            renderer.SetPropertyBlock(block);
        }

        // Runs the replay on unscaled time while leaving head and controller tracking live.
        private void LateUpdate()
        {
            if (!IsVisible) return;
            var elapsed = Time.unscaledTime - started;
            if (elapsed >= Duration) { Clear(); return; }
            Tick(elapsed);
        }

        // Cuts from exterior approach to internal impact, then withdraws and hands back to the arena.
        private void Tick(float elapsed)
        {
            bool inside = elapsed >= ImpactTime;
            float entryTime = Mathf.Clamp01((elapsed - ImpactTime) / 1.15f);
            if (inside && !contactPlayed)
            {
                contactPlayed = true;
                sound.PlayOneShot(Resources.Load<AudioClip>("DuelAudio/Body"), .62f);
                foreach (var renderer in shell)
                {
                    renderer.sharedMaterials = Enumerable.Repeat(cutawayMaterial, renderer.sharedMaterials.Length).ToArray();
                    Color(renderer, new Color(.22f, .36f, .40f, .085f));
                }
            }
            anatomy.gameObject.SetActive(inside);
            float approach = Mathf.Clamp01(elapsed / ImpactTime);
            arrow.localPosition = inside ? pathEnd * Mathf.SmoothStep(0, 1, entryTime) :
                Vector3.left * Mathf.Lerp(.66f, 0f, 1f - (1f - approach) * (1f - approach));
            foreach (var fragment in fragments)
                fragment.part.localPosition = fragment.rest + fragment.offset * Mathf.SmoothStep(0, 1, entryTime);

            float exit = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(InspectTime, Duration, elapsed));
            var aim = inside ? pathEnd * .44f : new Vector3(Mathf.Lerp(-.26f, .025f, approach), .065f, 0);
            var offset = inside ? new Vector3(.09f, .035f, -.52f - exit * .48f) :
                new Vector3(-.28f + approach * .15f, .12f, -.85f);
            replayCamera.transform.localPosition = aim + offset;
            replayCamera.transform.rotation = Quaternion.LookRotation(stage.TransformPoint(aim) - replayCamera.transform.position);
            replayCamera.fieldOfView = 40f;
            window.localScale = Vector3.one * Mathf.Lerp(1f, .72f, exit);
        }

        // Turns the rig label into readable words for the optional anatomy caption.
        private static string Readable(System.Enum value) => Regex.Replace(value.ToString(), "(?<=[a-z])(?=[A-Z])", " ");

        // Formats the observed contacts without guessing missing tissue.
        private static string Names(IEnumerable<string> names) => string.Join(", ", names.DefaultIfEmpty("none"));

        // Disposes the replay, textures and frozen pose on timeout, rematch or scene exit.
        public void Clear()
        {
            if (window != null) { window.gameObject.SetActive(false); Destroy(window.gameObject); }
            if (stage != null) { stage.gameObject.SetActive(false); Destroy(stage.gameObject); }
            if (picture != null) { picture.Release(); Destroy(picture); }
            if (screenMaterial != null) Destroy(screenMaterial);
            if (cutawayMaterial != null) Destroy(cutawayMaterial);
            foreach (var mesh in snapshots) Destroy(mesh);
            snapshots.Clear();
            fragments.Clear();
            shell.Clear();
            window = null;
            stage = null;
            picture = null;
            screenMaterial = null;
            cutawayMaterial = null;
            contactPlayed = false;
            Caption = null;
        }

        // Cleans up the offscreen stage when the player leaves.
        private void OnDestroy() => Clear();
    }
}
