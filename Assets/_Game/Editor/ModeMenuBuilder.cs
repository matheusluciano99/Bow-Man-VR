using System.IO;
using System.Linq;
using BowMan.Menu;
using BowMan.Networking.Editor;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

public static class ModeMenuBuilder
{
    private const string ScenesFolder = "Assets/_Game/Scenes/";
    private const string MenuScenePath = ScenesFolder + GameScenes.MainMenu + ".unity";
    private const string MaterialFolder = "Assets/_Game/Materials/Menu";
    private const string PanelMaterialPath = MaterialFolder + "/ModePanel.mat";
    private const string RayMaterialPath = MaterialFolder + "/PointerRay.mat";
    private const string XRPlayerPath = "Assets/_Game/Prefabs/XRPlayer.prefab";
    private const string PlayerPath = "Builds/PCVRGame/BowManVR.exe";

    // Regenerates the local duel, PvP and menu scenes, then puts them first in the build list.
    [MenuItem("Bow Man VR/Build Mode Menu And Game Scenes")]
    public static void BuildAll()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        LocalDuelBuilder.Build();
        PvPConnectionSceneBuilder.BuildScene();
        BuildMenuScene();
        UseGameBuildList();
        AssetDatabase.SaveAssets();
        Debug.Log("Mode menu, local duel and PvP scenes generated; MainMenu is the first build scene.");
    }

    // Builds the menu, local duel and PvP scenes into one Windows x86_64 player.
    [MenuItem("Bow Man VR/Build Windows Game Player")]
    public static void BuildPlayer()
    {
        var scenes = GameScenes.BuildOrder.Select(ScenePath).ToArray();
        if (scenes.Any(path => !File.Exists(path)))
        {
            Debug.LogError("Run Bow Man VR > Build Mode Menu And Game Scenes before building the game player.");
            return;
        }
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = PlayerPath,
            target = BuildTarget.StandaloneWindows64,
            targetGroup = BuildTargetGroup.Standalone,
            options = BuildOptions.None,
        });
        if (report.summary.result == BuildResult.Succeeded) Debug.Log($"Game player built at {PlayerPath}");
        else Debug.LogError($"Game player build {report.summary.result} with {report.summary.totalErrors} errors");
    }

    // Creates the menu scene: rig, floor, title, two mode panels and the controller pointer.
    public static void BuildMenuScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var light = new GameObject("Directional Light").AddComponent<Light>();
        light.type = LightType.Directional;
        light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.name = "Floor";
        floor.transform.position = new Vector3(0f, -0.5f, 0f);
        floor.transform.localScale = new Vector3(8f, 1f, 8f);

        var rig = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(XRPlayerPath));
        var camera = rig.GetComponentInChildren<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.085f, 0.12f, 0.16f);
        PrefabUtility.RecordPrefabInstancePropertyModifications(camera);
        floor.GetComponent<Renderer>().sharedMaterial = MenuMaterial(MaterialFolder + "/MenuFloor.mat", new Color(0.10f, 0.14f, 0.14f));
        var rightHand = rig.transform.Find("Camera Offset/Right Controller");

        Label(null, "Bow Man VR", new Vector3(0f, 2.20f, 2.5f), 0.05f);
        Label(null, "Point and pull the right trigger to select  •  Desktop: click or press 1 / 2",
            new Vector3(0f, 1.93f, 2.5f), 0.018f);
        var panelMaterial = MenuMaterial(PanelMaterialPath, new Color(0.16f, 0.2f, 0.28f));
        Panel(GameMode.PvE, new Vector3(-0.8f, 1.45f, 2.5f), panelMaterial);
        Panel(GameMode.PvP, new Vector3(0.8f, 1.45f, 2.5f), panelMaterial);

        var handPosition = new Vector3(0f, 0.78f, 2.5f);
        var handOption = Panel(GameMode.PvE, handPosition, panelMaterial, true);
        var handLabel = handOption.GetComponentInChildren<TextMesh>();
        Label(null, "Select the hand panel or press H to switch", new Vector3(0f, 0.53f, 2.5f), 0.019f);

        var pointer = rightHand.gameObject.AddComponent<ControllerPointer>();
        var line = rightHand.gameObject.AddComponent<LineRenderer>();
        line.positionCount = 2;
        line.useWorldSpace = true;
        line.widthMultiplier = 0.004f;
        line.shadowCastingMode = ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.sharedMaterial = MenuMaterial(RayMaterialPath, new Color(1f, 0.85f, 0.4f));
        var pointerFields = new SerializedObject(pointer);
        pointerFields.FindProperty("rayOrigin").objectReferenceValue = rightHand;
        pointerFields.FindProperty("line").objectReferenceValue = line;
        pointerFields.FindProperty("desktopCamera").objectReferenceValue = camera;
        pointerFields.ApplyModifiedPropertiesWithoutUndo();

        var menu = new GameObject("Mode menu").AddComponent<ModeMenu>();
        var menuFields = new SerializedObject(menu);
        menuFields.FindProperty("pointer").objectReferenceValue = pointer;
        menuFields.FindProperty("handOption").objectReferenceValue = handOption;
        menuFields.FindProperty("handLabel").objectReferenceValue = handLabel;
        menuFields.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.SaveScene(scene, MenuScenePath);
    }

    // Creates one selectable panel: a solid board, its mode title and the highlight component.
    private static ModeOption Panel(GameMode mode, Vector3 position, Material material, bool handChoice = false)
    {
        var root = new GameObject(handChoice ? "Bow hand option" : mode + " option");
        root.transform.position = position;
        var board = GameObject.CreatePrimitive(PrimitiveType.Cube);
        board.name = "Board";
        board.transform.SetParent(root.transform, false);
        board.transform.localScale = handChoice ? new Vector3(2.9f, 0.34f, 0.05f) : new Vector3(1.3f, 0.72f, 0.05f);
        var renderer = board.GetComponent<Renderer>();
        renderer.sharedMaterial = material;
        Label(root.transform, handChoice ? "Bow in left hand  •  Draw with right" : GameScenes.Title(mode),
            new Vector3(0f, 0f, -0.04f), handChoice ? 0.024f : 0.032f);
        var option = root.AddComponent<ModeOption>();
        option.Configure(mode, renderer);
        EditorUtility.SetDirty(option);
        return option;
    }

    // Adds centered white text using the built-in font, readable from the player's side.
    private static void Label(Transform parent, string text, Vector3 position, float characterSize)
    {
        var label = new GameObject("Label").AddComponent<TextMesh>();
        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.font = font;
        var renderer = label.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = font.material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        label.transform.SetParent(parent, false);
        label.transform.localPosition = position;
        label.anchor = TextAnchor.MiddleCenter;
        label.alignment = TextAlignment.Center;
        label.fontSize = 64;
        label.characterSize = characterSize;
        label.color = Color.white;
        label.text = text;
    }

    // Loads or creates an unlit menu material with the given color.
    private static Material MenuMaterial(string path, Color color)
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null) return material;
        if (!AssetDatabase.IsValidFolder(MaterialFolder)) AssetDatabase.CreateFolder("Assets/_Game/Materials", "Menu");
        var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
        material = new Material(shader) { color = color };
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    // Puts MainMenu, LocalDuel and PvPConnectionTest first in the build list and keeps other scenes after them.
    private static void UseGameBuildList()
    {
        var game = GameScenes.BuildOrder.Select(name => new EditorBuildSettingsScene(ScenePath(name), true));
        var others = EditorBuildSettings.scenes.Where(scene => !GameScenes.BuildOrder.Select(ScenePath).Contains(scene.path));
        EditorBuildSettings.scenes = game.Concat(others).ToArray();
    }

    // Returns the asset path of a game scene by name.
    private static string ScenePath(string name) => ScenesFolder + name + ".unity";
}
