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

    // Regenerates the playable scenes and puts them first in the build list.
    [MenuItem("Bow Man VR/Build Mode Menu And Game Scenes")]
    public static void BuildAll()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        LocalDuelBuilder.Build();
        PvPConnectionSceneBuilder.BuildScene();
        TargetRangeBuilder.Build();
        BuildMenuScene();
        UseGameBuildList();
        AssetDatabase.SaveAssets();
        Debug.Log("Playable scenes generated. MainMenu is the first build scene.");
    }

    // Builds all playable modes into the Windows x86_64 player.
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

    // Creates the menu scene: rig, floor, game choices and controller pointer.
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

        var choices = new GameObject("Mode choices").transform;
        Label(choices, "Bow Man VR", new Vector3(0f, 2.20f, 2.5f), 0.05f);
        Label(choices, "Point and pull the right trigger to select  •  Desktop: click or press 1 / 2 / 3",
            new Vector3(0f, 1.93f, 2.5f), 0.018f);
        var panelMaterial = MenuMaterial(PanelMaterialPath, new Color(0.16f, 0.2f, 0.28f));
        var modeSize = new Vector2(1.3f, 0.42f);
        Panel(choices, "PvE option", GameScenes.Title(GameMode.PvE), -0.8f, 1.63f, modeSize, 0.026f, panelMaterial);
        Panel(choices, "PvP option", GameScenes.Title(GameMode.PvP), 0.8f, 1.63f, modeSize, 0.026f, panelMaterial, GameMode.PvP);
        Panel(choices, "Target option", GameScenes.Title(GameMode.Target), 0f, 1.17f,
            new Vector2(2.9f, 0.35f), 0.025f, panelMaterial, GameMode.Target);
        var handOption = Panel(choices, "Bow hand option", "Bow in left hand  •  Draw with right", -0.49f, 0.78f,
            new Vector2(1.92f, 0.34f), 0.018f, panelMaterial);
        var handLabel = handOption.GetComponentInChildren<TextMesh>();
        var creditsOption = Panel(choices, "Credits option", "Credits", 1.02f, 0.78f,
            new Vector2(0.86f, 0.34f), 0.024f, panelMaterial);
        var archerOption = Panel(choices, "Archer option", "Archer: English Archer", 0f, 0.39f,
            new Vector2(2.9f, 0.34f), 0.022f, panelMaterial);
        Label(choices, "G: change archer   •   H: bow hand   •   C: credits", new Vector3(0f, 0.11f, 2.5f), 0.019f);

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
        var credits = menu.gameObject.AddComponent<MenuCredits>();
        BuildCredits(credits, choices.gameObject, creditsOption, panelMaterial);
        var menuFields = new SerializedObject(menu);
        menuFields.FindProperty("archerOption").objectReferenceValue = archerOption;
        menuFields.FindProperty("archerLabel").objectReferenceValue = archerOption.GetComponentInChildren<TextMesh>();
        menuFields.FindProperty("credits").objectReferenceValue = credits;
        menuFields.FindProperty("pointer").objectReferenceValue = pointer;
        menuFields.FindProperty("handOption").objectReferenceValue = handOption;
        menuFields.FindProperty("handLabel").objectReferenceValue = handLabel;
        menuFields.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.SaveScene(scene, MenuScenePath);
    }

    // Builds the credits at the same reading distance as the main menu and wires its pointer controls.
    private static void BuildCredits(MenuCredits credits, GameObject choices, ModeOption entry, Material material)
    {
        var pages = new GameObject("Release credits").transform;
        var heading = Label(pages, "Credits", new Vector3(0f, 2.20f, 2.5f), 0.035f);
        heading.name = "Credit heading";
        var body = Label(pages, "", new Vector3(0f, 1.55f, 2.5f), 0.022f);
        body.name = "Credit text";
        var number = Label(pages, "", new Vector3(0f, 0.99f, 2.5f), 0.017f);
        number.name = "Credit page";
        number.color = new Color(0.90f, 0.75f, 0.50f);
        var size = new Vector2(0.64f, 0.30f);
        var back = Panel(pages, "Return to menu", "Menu", -1.10f, 0.70f, size, 0.022f, material);
        var prev = Panel(pages, "Previous credit page", "Previous", -0.37f, 0.70f, size, 0.020f, material);
        var next = Panel(pages, "Next credit page", "Next", 0.37f, 0.70f, size, 0.022f, material);
        var notices = Panel(pages, "Source notices", "Notices", 1.10f, 0.70f, size, 0.022f, material);
        credits.Configure(choices, pages.gameObject, entry, prev, next, back, notices, heading, body, number,
            AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/_Game/Content/ReleaseCredits.txt"));
        pages.gameObject.SetActive(false);
    }

    // Creates a board selectable by either the tracked pointer or the desktop mouse.
    private static ModeOption Panel(Transform parent, string name, string caption, float x, float y,
        Vector2 size, float textSize, Material material, GameMode mode = GameMode.PvE)
    {
        var root = new GameObject(name);
        root.transform.SetParent(parent, false);
        root.transform.localPosition = new Vector3(x, y, 2.5f);
        var board = GameObject.CreatePrimitive(PrimitiveType.Cube);
        board.name = "Board";
        board.transform.SetParent(root.transform, false);
        board.transform.localScale = new Vector3(size.x, size.y, 0.05f);
        var renderer = board.GetComponent<Renderer>();
        renderer.sharedMaterial = material;
        Label(root.transform, caption, new Vector3(0f, 0f, -0.04f), textSize);
        var option = root.AddComponent<ModeOption>();
        option.Configure(mode, renderer);
        return option;
    }

    // Adds centered white text using the built-in font, readable from the player's side.
    private static TextMesh Label(Transform parent, string text, Vector3 position, float characterSize)
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
        return label;
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

    // Lists each playable scene once before the project’s remaining scenes.
    private static void UseGameBuildList()
    {
        var game = GameScenes.BuildOrder.Select(name => new EditorBuildSettingsScene(ScenePath(name), true));
        var others = EditorBuildSettings.scenes.Where(scene => !GameScenes.BuildOrder.Select(ScenePath).Contains(scene.path));
        EditorBuildSettings.scenes = game.Concat(others).ToArray();
    }

    // Returns the asset path of a game scene by name.
    private static string ScenePath(string name) => ScenesFolder + name + ".unity";
}
