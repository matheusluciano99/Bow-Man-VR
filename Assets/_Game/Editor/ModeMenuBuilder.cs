using System.IO;
using System.Linq;
using BowMan;
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
        camera.backgroundColor = new Color(.027f, .039f, .041f);
        PrefabUtility.RecordPrefabInstancePropertyModifications(camera);
        floor.GetComponent<Renderer>().sharedMaterial = MenuMaterial(MaterialFolder + "/MenuFloor.mat", new Color(0.10f, 0.14f, 0.14f));
        var rightHand = rig.transform.Find("Camera Offset/Right Controller");

        var choices = new GameObject("Mode choices").transform;
        var layout = new GameObject("Menu layout").transform;
        layout.SetParent(choices, false);
        layout.localPosition = new Vector3(0f, 1.52f, 2.65f);
        ArcherInterface.Surface(layout, "Outer frame", new Vector3(0, 0, .055f), new Vector2(2.78f, 1.88f), ArcherInterface.Frame);
        ArcherInterface.Surface(layout, "Menu backing", new Vector3(0, 0, .045f), new Vector2(2.755f, 1.855f), ArcherInterface.Ink);
        ArcherInterface.Label(layout, "Bow Man VR", new Vector3(0, .679f, 0), .049f, true);
        ArcherInterface.Surface(layout, "Title rule", new Vector3(0, .535f, 0), new Vector2(.74f, .008f), ArcherInterface.Brass);

        var modeSize = new Vector2(1.19f, .35f);
        Card(layout, "PvE option", "AI duel", "Versus the CPU", -.63f, .307f, modeSize, GameMode.PvE);
        Card(layout, "PvP option", "Online duel", "Two players", +.63f, .307f, modeSize, GameMode.PvP);
        Card(layout, "Target option", "Tiro ao alvo", "Ten arrows · CPU or online", 0f, -.098f,
            new Vector2(2.45f, .35f), GameMode.Target, true);

        var archerOption = Card(layout, "Archer option", "English Archer", "", -.63f, -.478f, new Vector2(1.19f, .27f));
        var handOption = Card(layout, "Bow hand option", "Bow hand · Left", "", .63f, -.478f, new Vector2(1.19f, .27f));
        var handLabel = handOption.GetComponentInChildren<TextMesh>();
        var prompt = ArcherInterface.Label(layout, "Right trigger to select", new Vector3(-.36f, -.757f, 0), .019f);
        prompt.color = ArcherInterface.Quiet;
        var creditsOption = Card(layout, "Credits option", "Credits", "", .87f, -.757f, new Vector2(.65f, .175f));

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
        BuildCredits(credits, choices.gameObject, creditsOption,
            AssetDatabase.LoadAssetAtPath<Material>(PanelMaterialPath));
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
        ArcherInterface.Surface(pages, "Credits frame", new Vector3(0f, 1.48f, 2.56f), new Vector2(2.95f, 1.82f), ArcherInterface.Frame);
        ArcherInterface.Surface(pages, "Credits backing", new Vector3(0f, 1.48f, 2.55f), new Vector2(2.925f, 1.795f), ArcherInterface.Ink);
        var heading = ArcherInterface.Label(pages, "Credits", new Vector3(0f, 2.19f, 2.5f), .05f, true);
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

    // Builds a controller-sized card and keeps its supporting label subordinate to the action.
    private static ModeOption Card(Transform layout, string name, string title, string detail,
        float x, float y, Vector2 size, GameMode mode = GameMode.PvE, bool featured = false)
    {
        var root = new GameObject(name).transform;
        root.SetParent(layout, false);
        root.localPosition = new Vector3(x, y, 0);
        var color = featured ? ArcherInterface.Teal : ArcherInterface.Panel;
        var face = ArcherInterface.Surface(root, "Card face", Vector3.zero, size, color, true);
        ArcherInterface.Surface(root, "Card edge", new Vector3(0, 0, .014f), size + Vector2.one * .012f, ArcherInterface.Frame);
        ArcherInterface.Label(root, title, new Vector3(0, detail.Length == 0 ? 0 : .069f, -.015f), detail.Length == 0 ? .025f : .032f);
        if (detail.Length > 0)
        {
            var description = ArcherInterface.Label(root, detail, new Vector3(0, -.071f, -.015f), .018f);
            description.color = ArcherInterface.Quiet;
        }
        var option = root.gameObject.AddComponent<ModeOption>();
        option.Configure(mode, face, color);
        return option;
    }

    // Creates a menu label with the shared spatial type settings.
    private static TextMesh Label(Transform parent, string text, Vector3 position, float characterSize) =>
        ArcherInterface.Label(parent, text, position, characterSize);

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
