using BowMan;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class LocalDuelBuilder
{
    // Combines the existing arena, tracked rig, bow and prepared character prefabs into the lab duel scene.
    [MenuItem("Bow Man VR/Build Local Duel Scene")]
    public static void Build()
    {
        var scene = EditorSceneManager.OpenScene("Assets/_Game/Scenes/DuelArena.unity");
        Object.DestroyImmediate(GameObject.Find("ArrowFlightTest"));
        Object.DestroyImmediate(GameObject.Find("Main Camera"));
        var first = GameObject.Find("Archer1Spawn").transform;
        var second = GameObject.Find("Archer2Spawn").transform;
        foreach (var spawn in new[] { first, second })
        {
            Object.DestroyImmediate(spawn.GetComponent<ArcherHealth>());
            for (var i = spawn.childCount - 1; i >= 0; i--)
            {
                var child = spawn.GetChild(i);
                if (child.name != "PositionMarker" && child.name != "FacingMarker") Object.DestroyImmediate(child.gameObject);
            }
        }
        var rig = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/_Game/Prefabs/XRPlayer.prefab"));
        rig.transform.SetPositionAndRotation(first.position, first.rotation);
        var head = rig.GetComponentInChildren<Camera>().transform;
        var bow = ((GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/_Game/Prefabs/Bow.prefab"))).GetComponent<Bow>();
        bow.transform.position = first.position + new Vector3(-0.45f, 1.3f, 0.6f);
        var host = new GameObject("Local duel");
        var view = host.AddComponent<VRInjuryView>();
        Set(view, "trackingOrigin", rig.transform);
        var input = host.AddComponent<VRBowInput>();
        Set(input, "bow", bow);
        Set(input, "leftController", rig.transform.Find("Camera Offset/Left Controller"));
        Set(input, "rightController", rig.transform.Find("Camera Offset/Right Controller"));
        var desktop = host.AddComponent<DesktopBowTest>();
        Set(desktop, "bow", bow);
        Set(desktop, "vrInput", input);
        var settings = new SerializedObject(desktop);
        settings.FindProperty("showPanel").boolValue = false;
        settings.ApplyModifiedPropertiesWithoutUndo();
        var label = new GameObject("Match feedback").AddComponent<TextMesh>();
        label.transform.SetParent(head, false);
        label.transform.localPosition = new Vector3(0f, -0.27f, 1.3f);
        label.anchor = TextAnchor.MiddleCenter;
        label.alignment = TextAlignment.Center;
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.fontSize = 64;
        label.characterSize = 0.02f;
        label.text = "Your turn     You 100  |  Opponent 100\nBow: left hand   Draw: 0%\nTrigger: draw/release   B: switch bow hand";
        var renderer = label.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = label.font.material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        label.transform.localScale = Vector3.one * (1.15f / renderer.bounds.size.x);
        var duel = host.AddComponent<LocalDuel>();
        Set(duel, "localArcherPrefab", AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/Characters/EnglishArcher.prefab"));
        Set(duel, "opponentPrefab", AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/Characters/ErikaArcher.prefab"));
        Set(duel, "localSpawn", first);
        Set(duel, "opponentSpawn", second);
        Set(duel, "head", head);
        Set(duel, "bow", bow);
        Set(duel, "vrInput", input);
        Set(duel, "desktopInput", desktop);
        Set(duel, "injuryView", view);
        Set(duel, "arrowPrefab", AssetDatabase.LoadAssetAtPath<ArrowFlight>("Assets/_Game/Prefabs/TestArrow.prefab"));
        Set(duel, "feedback", label);
        EditorSceneManager.SaveScene(scene, "Assets/_Game/Scenes/LocalDuel.unity");
    }

    // Stores scene references in serialized fields without adding runtime authoring APIs.
    private static void Set(Object target, string field, Object value)
    {
        var settings = new SerializedObject(target);
        settings.FindProperty(field).objectReferenceValue = value;
        settings.ApplyModifiedPropertiesWithoutUndo();
    }
}
