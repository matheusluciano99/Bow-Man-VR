using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

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
        private readonly List<(Transform part, Vector3 rest, Vector3 offset)> fractures = new();
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
            replayCamera.GetUniversalAdditionalCameraData().allowXRRendering = false;
            replayCamera.enabled = picture != null;
            if (replayCamera.TryGetComponent<AudioListener>(out var listener)) listener.enabled = false;
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
            foreach (var hit in report.Bones) StageBone(hit, root);

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

        // Fits archived bone geometry into the contact's animated frame.
        private void StageBone(BoneHit hit, ArcherHealth archer)
        {
            var name = hit.Bone switch
            {
                HumanBodyBones.Head => "Skull",
                HumanBodyBones.Chest or HumanBodyBones.UpperChest => "Ribs",
                HumanBodyBones.Neck or HumanBodyBones.Spine => "Vertebra",
                HumanBodyBones.Hips => "Pelvis",
                HumanBodyBones.LeftShoulder or HumanBodyBones.RightShoulder => "Shoulder",
                HumanBodyBones.LeftUpperArm or HumanBodyBones.RightUpperArm => "Humerus",
                HumanBodyBones.LeftLowerArm or HumanBodyBones.RightLowerArm => "Forearm",
                HumanBodyBones.LeftUpperLeg or HumanBodyBones.RightUpperLeg => "Femur",
                HumanBodyBones.LeftLowerLeg or HumanBodyBones.RightLowerLeg => "Tibia",
                _ => null
            };
            if (name == null) return;
            var prefab = Resources.Load<GameObject>("ReplayBones/" + name);
            if (prefab == null) return;
            var mesh = prefab.GetComponentInChildren<MeshFilter>().sharedMesh;
            var bone = CopyMesh(name, anatomy, mesh, new[] { cutawayMaterial });
            Place(bone.transform, hit.Frame);
            var center = hit.LocalBounds.center;
            var size = hit.LocalBounds.size;
            if (hit.Frame.TryGetComponent<BoxCollider>(out var box)) size = box.size * .94f;
            if (hit.Bone == HumanBodyBones.Head) FitSkull(archer, hit.Frame, ref center, ref size);
            bone.transform.localPosition = view * (hit.Frame.TransformPoint(center) - entry);
            var bounds = mesh.bounds.size;
            bone.transform.localScale = Vector3.Scale(hit.Frame.lossyScale,
                new Vector3(size.x / bounds.x, size.y / bounds.y, size.z / bounds.z));
            Color(bone, hit.Fractured ? new Color(.94f, .75f, .50f, .53f) : new Color(.79f, .88f, .90f, .48f));
            if (hit.Fractured) fractures.Add((bone.transform, bone.transform.localPosition, Vector3.right * .016f));
        }

        // Aligns the cranial cavity to the fitted brain rather than the front-biased head hitbox.
        private static void FitSkull(ArcherHealth actor, Transform frame, ref Vector3 center, ref Vector3 size)
        {
            var brain = actor.GetComponentsInChildren<AnatomyOrgan>().FirstOrDefault(part => part.Kind == OrganKind.Brain);
            if (brain == null) return;
            Bounds fitted = new Bounds(frame.InverseTransformPoint(brain.transform.TransformPoint(brain.Surfaces[0].bounds.center)), Vector3.zero);
            foreach (var surface in brain.Surfaces)
            {
                var bounds = surface.bounds;
                for (int x = -1; x <= 1; x += 2)
                    for (int y = -1; y <= 1; y += 2)
                        for (int z = -1; z <= 1; z += 2)
                        {
                            var corner = bounds.center + Vector3.Scale(bounds.extents, new Vector3(x, y, z));
                            fitted.Encapsulate(frame.InverseTransformPoint(brain.transform.TransformPoint(corner)));
                        }
            }
            center = fitted.center + new Vector3(0f, -fitted.size.y * .20f, fitted.size.z * .09f);
            size = Vector3.Max(size, Vector3.Scale(fitted.size, new Vector3(1.18f, 1.57f, 1.28f)));
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
            foreach (var fracture in fractures)
                fracture.part.localPosition = fracture.rest + fracture.offset * Mathf.Sin(entryTime * Mathf.PI);

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
            fractures.Clear();
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
