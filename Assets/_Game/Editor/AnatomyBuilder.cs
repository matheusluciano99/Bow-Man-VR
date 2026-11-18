using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using BowMan;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

public static class AnatomyBuilder
{
    [Serializable] private sealed class Inventory { public OrganSource[] organs; public SourceFile[] files; }
    [Serializable] private sealed class OrganSource { public string kind, side, region, bone, fma; public string[] ids; }
    [Serializable] private sealed class SourceFile { public string id, filename; }
    private sealed class Surface { public Vector3[] vertices; public int[] triangles; }

    // Fits the verified atlas to each existing Humanoid and stores bone-bound tissue without adding hit colliders.
    [MenuItem("Bow Man VR/Injuries/Rebuild Organ Anatomy")]
    public static void Rebuild()
    {
        var inventory = JsonUtility.FromJson<Inventory>(File.ReadAllText("SourceAssets/BodyParts3D/Sources.json"));
        var sources = inventory.files.ToDictionary(f => f.id, f => ReadSurface(
            "Assets/ThirdParty/BodyParts3D/Models/" + f.filename));
        foreach (var character in new[] { "EnglishArcher", "ErikaArcher" })
        {
            var path = "Assets/_Game/Prefabs/Characters/" + character + ".prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                foreach (var organ in root.GetComponentsInChildren<AnatomyOrgan>()) Object.DestroyImmediate(organ.gameObject);
                if (root.GetComponent<ArcherAnatomy>() == null) root.AddComponent<ArcherAnatomy>();
                var animator = root.GetComponentInChildren<Animator>();
                var eyes = animator.GetBoneTransform(HumanBodyBones.Head).Find("EyeAnchor").position;
                var sourceEye = (Mean(sources["FJ1331"].vertices) + Mean(sources["FJ1382"].vertices)) * 0.5f;
                var sourceHip = (Hip(sources["FJ3365"]) + Hip(sources["FJ3259"])) * 0.5f;
                var hips = (animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg).position +
                    animator.GetBoneTransform(HumanBodyBones.RightUpperLeg).position) * 0.5f;
                var height = (eyes.y - hips.y) / (sourceEye.y - sourceHip.y);
                if (!(height > 0f) || float.IsInfinity(height)) throw new InvalidOperationException("Invalid anatomical landmarks.");
                var volumes = root.GetComponentsInChildren<BodyHitRegion>();
                var torsoHalfWidth = volumes.Where(v => v.Region is BodyRegion.Chest or BodyRegion.Abdomen or BodyRegion.Pelvis)
                    .Select(v => BoxBounds(v.GetComponent<BoxCollider>()).extents.x).Min() * 0.9f;
                var sourceTorsoWidth = inventory.organs.Where(o => o.kind != "Brain").SelectMany(o => o.ids)
                    .SelectMany(id => sources[id].vertices).Max(p => Mathf.Abs(p.x - sourceHip.x));
                var bodyWidth = torsoHalfWidth / sourceTorsoWidth;
                var headBounds = BoxBounds(volumes.First(v => v.Region == BodyRegion.Head).GetComponent<BoxCollider>());
                headBounds.Expand(-0.012f);
                headBounds.SetMinMax(new Vector3(headBounds.min.x, animator.GetBoneTransform(HumanBodyBones.Neck).position.y,
                    headBounds.min.z), headBounds.max);
                var depth = CranialDepth(path);
                headBounds.SetMinMax(new Vector3(headBounds.min.x, headBounds.min.y, depth.x + 0.006f),
                    new Vector3(headBounds.max.x, headBounds.max.y, depth.y - 0.006f));

                // Aligns the source hip and eye references while retaining the character's torso depth and lean.
                Vector3 Map(Vector3 p, bool head)
                {
                    var t = (p.y - sourceHip.y) / (sourceEye.y - sourceHip.y);
                    var width = head ? height : bodyWidth;
                    var x = p.x * width + Mathf.LerpUnclamped(hips.x - sourceHip.x * width, eyes.x - sourceEye.x * width, t);
                    var z = p.z * height + Mathf.LerpUnclamped(hips.z - sourceHip.z * height, eyes.z - sourceEye.z * height, t);
                    return new Vector3(x, hips.y + (p.y - sourceHip.y) * height, z);
                }

                var brainPoints = inventory.organs.Where(o => o.kind == "Brain").SelectMany(o => o.ids)
                    .SelectMany(id => sources[id].vertices).Select(p => Map(p, true)).ToArray();
                var brainBounds = new Bounds(brainPoints[0], Vector3.zero);
                foreach (var point in brainPoints) brainBounds.Encapsulate(point);
                var brainScale = Mathf.Min(1f, headBounds.size.x / brainBounds.size.x,
                    headBounds.size.y / brainBounds.size.y, headBounds.size.z / brainBounds.size.z);
                if (!(brainScale > 0.1f)) throw new InvalidOperationException("The brain cannot fit the character's head reference.");
                var brainExtents = brainBounds.extents * brainScale;
                var brainCentre = new Vector3(
                    Mathf.Clamp(brainBounds.center.x, headBounds.min.x + brainExtents.x, headBounds.max.x - brainExtents.x),
                    Mathf.Clamp(brainBounds.center.y, headBounds.min.y + brainExtents.y, headBounds.max.y - brainExtents.y),
                    Mathf.Clamp(brainBounds.center.z, headBounds.min.z + brainExtents.z, headBounds.max.z - brainExtents.z));
                foreach (var organ in inventory.organs)
                {
                    var key = organ.kind + "_" + organ.side + "_" + organ.bone;
                    var meshes = organ.ids.Select(id => MeshFor(id, sources[id], p => organ.kind == "Brain"
                        ? brainCentre + (Map(p, true) - brainBounds.center) * brainScale : Map(p, false))).ToArray();
                    var folder = "Assets/_Game/Models/Anatomy/" + character;
                    EnsureFolder(folder);
                    var saved = SaveMeshes(folder + "/" + key + ".asset", meshes);
                    var obj = new GameObject("Anatomy_" + key);
                    obj.transform.SetParent(animator.GetBoneTransform((HumanBodyBones)Enum.Parse(typeof(HumanBodyBones), organ.bone)), true);
                    var component = obj.AddComponent<AnatomyOrgan>();
                    var settings = new SerializedObject(component);
                    settings.FindProperty("kind").enumValueIndex = (int)Enum.Parse(typeof(OrganKind), organ.kind);
                    settings.FindProperty("side").enumValueIndex = (int)Enum.Parse(typeof(BodySide), organ.side);
                    settings.FindProperty("region").enumValueIndex = (int)Enum.Parse(typeof(BodyRegion), organ.region);
                    var surfaces = settings.FindProperty("surfaces");
                    surfaces.arraySize = organ.ids.Length;
                    for (var i = 0; i < organ.ids.Length; i++) surfaces.GetArrayElementAtIndex(i).objectReferenceValue = saved[organ.ids[i]];
                    settings.ApplyModifiedPropertiesWithoutUndo();
                }
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        var arrow = PrefabUtility.LoadPrefabContents("Assets/_Game/Prefabs/TestArrow.prefab");
        try
        {
            if (arrow.GetComponent<ArrowAnatomy>() == null) arrow.AddComponent<ArrowAnatomy>();
            PrefabUtility.SaveAsPrefabAsset(arrow, "Assets/_Game/Prefabs/TestArrow.prefab");
        }
        finally { PrefabUtility.UnloadPrefabContents(arrow); }
        AssetDatabase.SaveAssets();
        AnatomySurface.ClearCache();
    }

    // Converts verified OBJ positions from patient-left/mm/Z-up to Unity-right/metres/Y-up and reverses winding.
    private static Surface ReadSurface(string path)
    {
        var vertices = new List<Vector3>();
        var triangles = new List<int>();
        foreach (var line in File.ReadLines(path))
        {
            if (!line.StartsWith("v ") && !line.StartsWith("f ")) continue;
            var fields = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (fields[0] == "v")
            {
                var p = new Vector3(-float.Parse(fields[1], CultureInfo.InvariantCulture),
                    float.Parse(fields[3], CultureInfo.InvariantCulture), -float.Parse(fields[2], CultureInfo.InvariantCulture)) * 0.001f;
                if (float.IsNaN(p.sqrMagnitude) || float.IsInfinity(p.sqrMagnitude)) throw new InvalidDataException(path);
                vertices.Add(p);
            }
            else
            {
                var face = fields.Skip(1).Select(f => int.Parse(f.Split('/')[0], CultureInfo.InvariantCulture))
                    .Select(i => i > 0 ? i - 1 : vertices.Count + i).ToArray();
                if (face.Length < 3 || face.Any(i => i < 0 || i >= vertices.Count)) throw new InvalidDataException(path);
                for (var i = 1; i < face.Length - 1; i++) triangles.AddRange(new[] { face[0], face[i + 1], face[i] });
            }
        }
        if (vertices.Count == 0 || triangles.Count == 0) throw new InvalidDataException(path);
        return new Surface { vertices = vertices.ToArray(), triangles = triangles.ToArray() };
    }

    // Measures the paired proximal femoral head landmarks instead of assuming the source origin is its floor.
    private static Vector3 Hip(Surface surface)
    {
        var top = surface.vertices.Max(v => v.y);
        var centre = surface.vertices.Average(v => Mathf.Abs(v.x));
        return Mean(surface.vertices.Where(v => v.y > top - 0.04f && Mathf.Abs(v.x) < centre).ToArray());
    }

    // Computes the geometric reference centre from actual atlas points.
    private static Vector3 Mean(Vector3[] points)
    {
        if (points.Length == 0) throw new InvalidDataException("Empty anatomical landmark.");
        var total = Vector3.zero;
        foreach (var point in points) total += point;
        return total / points.Length;
    }

    // Measures the actual cranial skin on a disposable rig, separate from the face-inclusive head hit volume.
    private static Vector2 CranialDepth(string prefab)
    {
        var clone = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(prefab));
        var points = new List<Vector3>();
        try
        {
            var animator = clone.GetComponentInChildren<Animator>();
            var head = animator.GetBoneTransform(HumanBodyBones.Head);
            var eyes = head.Find("EyeAnchor").position;
            foreach (var skin in clone.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                var index = Array.IndexOf(skin.bones, head);
                if (index < 0) continue;
                var mesh = new Mesh();
                try
                {
                    skin.BakeMesh(mesh, true);
                    var vertices = mesh.vertices;
                    var weights = skin.sharedMesh.boneWeights;
                    for (var i = 0; i < vertices.Length; i++)
                    {
                        var w = weights[i];
                        var weight = (w.boneIndex0 == index ? w.weight0 : 0f) + (w.boneIndex1 == index ? w.weight1 : 0f) +
                            (w.boneIndex2 == index ? w.weight2 : 0f) + (w.boneIndex3 == index ? w.weight3 : 0f);
                        if (weight < 0.4f) continue;
                        var p = skin.transform.TransformPoint(vertices[i]);
                        if (Mathf.Abs(p.x - eyes.x) < 0.05f && p.y > eyes.y + 0.03f && p.y < eyes.y + 0.12f) points.Add(p);
                    }
                }
                finally { Object.DestroyImmediate(mesh); }
            }
            if (points.Count == 0) throw new InvalidDataException("No cranial skin around the fitted eye reference.");
            return new Vector2(points.Min(p => p.z), points.Max(p => p.z));
        }
        finally { Object.DestroyImmediate(clone); }
    }

    // Measures the authored world-space box envelope without relying on preview-scene physics registration.
    private static Bounds BoxBounds(BoxCollider box)
    {
        var bounds = new Bounds(box.transform.TransformPoint(box.center), Vector3.zero);
        foreach (var x in new[] { -1f, 1f })
        foreach (var y in new[] { -1f, 1f })
        foreach (var z in new[] { -1f, 1f })
            bounds.Encapsulate(box.transform.TransformPoint(box.center + Vector3.Scale(box.size * 0.5f, new Vector3(x, y, z))));
        return bounds;
    }

    // Creates one fitted readable tissue surface in the character's neutral root coordinates.
    private static Mesh MeshFor(string name, Surface source, Func<Vector3, Vector3> map)
    {
        var mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt32, vertices = source.vertices.Select(map).ToArray(),
            triangles = source.triangles };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    // Creates missing project-owned asset directories with stable Unity folder metadata.
    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        var parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }

    // Preserves the main mesh and atomic subasset identities when anatomy is rebuilt.
    private static Dictionary<string, Mesh> SaveMeshes(string path, Mesh[] surfaces)
    {
        var existing = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Mesh>().ToDictionary(m => m.name);
        foreach (var mesh in surfaces)
        {
            var name = mesh.name;
            if (existing.TryGetValue(name, out var saved))
            {
                EditorUtility.CopySerialized(mesh, saved);
                EditorUtility.SetDirty(saved);
                Object.DestroyImmediate(mesh);
            }
            else
            {
                if (existing.Count == 0) AssetDatabase.CreateAsset(mesh, path);
                else AssetDatabase.AddObjectToAsset(mesh, path);
                mesh.name = name;
                EditorUtility.SetDirty(mesh);
                existing[name] = mesh;
            }
        }
        return existing;
    }
}
