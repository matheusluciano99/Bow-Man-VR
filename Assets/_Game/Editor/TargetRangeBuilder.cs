using BowMan;
using BowMan.Networking;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class TargetRangeBuilder
{
    // Reuses the online lobby, tracked rig and host/join controls for an equal-distance competition.
    [MenuItem("Bow Man VR/Build Target Archery Scene")]
    public static void Build()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var scene = EditorSceneManager.OpenScene("Assets/_Game/Scenes/PvPConnectionTest.unity");
        if (!EditorSceneManager.SaveScene(scene, "Assets/_Game/Scenes/TargetRange.unity"))
            throw new System.IO.IOException("Cannot save the target range scene.");
        var duel = Object.FindAnyObjectByType<PvPDuel>();
        var source = new SerializedObject(duel);
        var spawns = (ArcherSpawnPoints)source.FindProperty("spawns").objectReferenceValue;
        var first = spawns.For(ArcherRole.Archer1);
        var second = spawns.For(ArcherRole.Archer2);
        var host = duel.gameObject;
        host.name = "Target archery competition";
        var scenery = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/_Game/Resources/Arenas/WoodlandRange.prefab"));
        scenery.transform.SetPositionAndRotation((first.position + second.position) * 0.5f, first.rotation);

        // The woodland prefab groups its decorative target faces by material.
        foreach (var group in new[] { "straw", "red cloth" })
        {
            var decoration = scenery.transform.Find(group).gameObject;
            decoration.SetActive(false);
            PrefabUtility.RecordPrefabInstancePropertyModifications(decoration);
        }
        PrepareWoodland(scenery.transform, first);
        second.SetPositionAndRotation(first.position, first.rotation);
        RenderSettings.skybox = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Materials/Arenas/forest_grove.mat");
        var wood = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Materials/Arenas/medieval_wood.mat");
        var stand = new GameObject("Target stand").transform;
        stand.SetPositionAndRotation(first.position + first.forward * OnlineTargetMatch.DefaultDistance, first.rotation);
        Piece(stand, "Left support", PrimitiveType.Cube, new Vector3(-0.65f, 0.62f, 0.14f), new Vector3(0.12f, 1.24f, 0.12f), wood);
        Piece(stand, "Right support", PrimitiveType.Cube, new Vector3(0.65f, 0.62f, 0.14f), new Vector3(0.12f, 1.24f, 0.12f), wood);
        Piece(stand, "Wooden backing", PrimitiveType.Cube, new Vector3(0f, 1.5f, 0.11f), new Vector3(2.07f, 2.07f, 0.18f), wood);
        var face = Piece(stand, "Scoring face", PrimitiveType.Quad, Vector3.up * 1.5f, new Vector3(2f, 2f, 1f),
            AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Materials/TargetFace.mat"));

        var target = new SerializedObject(host.AddComponent<OnlineTargetMatch>());
        foreach (var field in new[] { "bow", "vrInput", "desktop", "feedback", "arrowPrefab", "connection", "rig", "firstPrefab", "secondPrefab" })
            target.FindProperty(field).objectReferenceValue = source.FindProperty(field).objectReferenceValue;
        target.FindProperty("face").objectReferenceValue = face.GetComponent<Collider>();
        target.FindProperty("stand").objectReferenceValue = first;
        target.ApplyModifiedPropertiesWithoutUndo();
        var connection = new SerializedObject(source.FindProperty("connection").objectReferenceValue);
        connection.FindProperty("targetCompetition").boolValue = true;
        connection.ApplyModifiedPropertiesWithoutUndo();
        var label = (TextMesh)source.FindProperty("feedback").objectReferenceValue;
        label.text = "Tiro ao alvo   •   10 arrows per archer";
        label.transform.Find("HUD backing").localScale = new Vector3(1.58f, .31f, 1f);
        label.transform.Find("HUD top rule").localScale = new Vector3(1.58f, .005f, 1f);
        Object.DestroyImmediate(duel);
        Object.DestroyImmediate(host.GetComponent<VRInjuryView>());
        EditorSceneManager.SaveScene(scene);
    }

    // Keeps the full competition lane on solid woodland ground and moves complete firs behind the longest target.
    public static void PrepareWoodland(Transform scenery, Transform firingLine)
    {
        int farthest = OnlineTargetMatch.Distances[OnlineTargetMatch.Distances.Count - 1];
        Vector3 start = scenery.InverseTransformPoint(firingLine.position);
        foreach (string name in new[] { "forest_ground_04", "forest_ground_04 collision" })
        {
            var ground = scenery.Find(name);
            ground.localPosition = new Vector3(ground.localPosition.x, ground.localPosition.y, start.z + farthest * .5f);
            PrefabUtility.RecordPrefabInstancePropertyModifications(ground);
        }
        foreach (string profile in new[] { "fir_sapling_medium_front", "fir_sapling_medium_side" })
        {
            var filter = scenery.Find(profile).GetComponent<MeshFilter>();
            var original = PrefabUtility.GetCorrespondingObjectFromSource(filter).sharedMesh;
            var mesh = Object.Instantiate(original);
            mesh.name = "TargetRange " + profile;
            var vertices = mesh.vertices;
            for (int i = 0; i < vertices.Length; i += 4)
            {
                var center = (vertices[i] + vertices[i + 3]) * .5f;
                if (Mathf.Abs(center.x - start.x) > 10f || center.z < start.z || center.z > start.z + farthest + 6f) continue;
                for (int corner = 0; corner < 4; corner++) vertices[i + corner].z += farthest;
            }
            mesh.vertices = vertices;
            mesh.RecalculateBounds();
            string path = $"Assets/_Game/Models/Arenas/{mesh.name}.asset";
            var saved = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (saved == null) AssetDatabase.CreateAsset(mesh, path);
            else
            {
                EditorUtility.CopySerialized(mesh, saved);
                Object.DestroyImmediate(mesh);
            }
            filter.sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            PrefabUtility.RecordPrefabInstancePropertyModifications(filter);
        }
    }

    // Places one solid target part in the firing line's local frame.
    private static GameObject Piece(Transform stand, string name, PrimitiveType shape, Vector3 position,
        Vector3 size, Material material)
    {
        var part = GameObject.CreatePrimitive(shape);
        part.name = name;
        part.transform.SetParent(stand, false);
        part.transform.localPosition = position;
        part.transform.localScale = size;
        part.GetComponent<Renderer>().sharedMaterial = material;
        return part;
    }
}
