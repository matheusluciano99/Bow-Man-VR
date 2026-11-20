using System.IO;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BowMan.Networking.Editor
{
    // Generates the PvP connection test scene, its networked player prefab and a Windows test player.
    public static class PvPConnectionSceneBuilder
    {
        private const string ScenePath = "Assets/_Game/Scenes/PvPConnectionTest.unity";
        private const string PrefabFolder = "Assets/_Game/Prefabs/Networking";
        private const string PresencePath = PrefabFolder + "/NetworkArcherPresence.prefab";
        private const string MarkerMaterialPath = PrefabFolder + "/OpponentMarker.mat";
        private const string XRPlayerPath = "Assets/_Game/Prefabs/XRPlayer.prefab";
        private const string PlayerPath = "Builds/PCVRPvP/BowManVR-PvP.exe";

        // Rebuilds the presence prefab and the PvP connection test scene, replacing earlier output.
        [MenuItem("Bow Man VR/PvP/Build Connection Test Scene")]
        public static void BuildScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var presence = BuildPresencePrefab();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            CreateLight();
            CreateFloor();
            var spawns = CreateSpawns();
            var label = CreateRig(spawns);
            var connection = CreateNetwork(presence, label);
            CreateDuel(spawns, label, connection);

            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log($"PvP connection test scene written to {ScenePath}");
        }

        // Builds only the PvP test scene into a Windows x86_64 player under Builds/PCVRPvP.
        [MenuItem("Bow Man VR/PvP/Build Windows Connection Player")]
        public static void BuildPlayer()
        {
            if (!File.Exists(ScenePath)) BuildScene();
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = PlayerPath,
                target = BuildTarget.StandaloneWindows64,
                targetGroup = BuildTargetGroup.Standalone,
                options = BuildOptions.None,
            });
            var ok = report.summary.result == BuildResult.Succeeded;
            if (ok) Debug.Log($"PvP connection player built at {PlayerPath}");
            else Debug.LogError($"PvP connection player build {report.summary.result} with {report.summary.totalErrors} errors");
        }

        // Saves a networked root with PlayerPresence and red head/hand markers as a prefab.
        private static GameObject BuildPresencePrefab()
        {
            EnsureFolder(PrefabFolder);
            var material = MarkerMaterial();
            var root = new GameObject("NetworkArcherPresence");
            root.AddComponent<NetworkObject>();
            var presence = root.AddComponent<PlayerPresence>();
            var serialized = new SerializedObject(presence);
            serialized.FindProperty("headMarker").objectReferenceValue = Marker(root.transform, "Head", 0.25f, material);
            serialized.FindProperty("leftMarker").objectReferenceValue = Marker(root.transform, "Left", 0.08f, material);
            serialized.FindProperty("rightMarker").objectReferenceValue = Marker(root.transform, "Right", 0.08f, material);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PresencePath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        // Creates a collider-free sphere marker of the given diameter.
        private static Transform Marker(Transform parent, string name, float size, Material material)
        {
            var marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            marker.name = name;
            Object.DestroyImmediate(marker.GetComponent<Collider>());
            marker.GetComponent<Renderer>().sharedMaterial = material;
            marker.transform.SetParent(parent, false);
            marker.transform.localScale = Vector3.one * size;
            return marker.transform;
        }

        // Loads or creates the red unlit material used for the opponent's markers.
        private static Material MarkerMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(MarkerMaterialPath);
            if (material != null) return material;
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            material = new Material(shader) { color = new Color(0.9f, 0.15f, 0.1f) };
            AssetDatabase.CreateAsset(material, MarkerMaterialPath);
            return material;
        }

        // Adds the sun light.
        private static void CreateLight()
        {
            var light = new GameObject("Directional Light").AddComponent<Light>();
            light.type = LightType.Directional;
            light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }

        // Adds a 12 by 28 metre floor whose top surface sits at Y=0.
        private static void CreateFloor()
        {
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "ArenaFloor";
            floor.transform.position = new Vector3(0f, -0.5f, 0f);
            floor.transform.localScale = new Vector3(12f, 1f, 28f);
        }

        // Adds Archer1Spawn and Archer2Spawn facing each other 20 metres apart.
        private static ArcherSpawnPoints CreateSpawns()
        {
            var spawns = new GameObject("ArcherSpawns").AddComponent<ArcherSpawnPoints>();
            var serialized = new SerializedObject(spawns);
            serialized.FindProperty("archer1").objectReferenceValue = Spawn(spawns.transform, "Archer1Spawn", -10f, 0f);
            serialized.FindProperty("archer2").objectReferenceValue = Spawn(spawns.transform, "Archer2Spawn", 10f, 180f);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return spawns;
        }

        // Creates one spawn transform on the floor at the given Z and facing.
        private static Transform Spawn(Transform parent, string name, float z, float yaw)
        {
            var spawn = new GameObject(name).transform;
            spawn.SetParent(parent, false);
            spawn.SetPositionAndRotation(new Vector3(0f, 0f, z), Quaternion.Euler(0f, yaw, 0f));
            return spawn;
        }

        // Instantiates XRPlayer, binds its tracked transforms and adds the headset status label.
        private static TextMesh CreateRig(ArcherSpawnPoints spawns)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(XRPlayerPath);
            var rig = (GameObject)PrefabUtility.InstantiatePrefab(source);
            var head = rig.transform.Find("Camera Offset/Main Camera");
            var binder = rig.AddComponent<LocalRigBinder>();
            var serialized = new SerializedObject(binder);
            serialized.FindProperty("rigRoot").objectReferenceValue = rig.transform;
            serialized.FindProperty("head").objectReferenceValue = head;
            serialized.FindProperty("leftHand").objectReferenceValue = rig.transform.Find("Camera Offset/Left Controller");
            serialized.FindProperty("rightHand").objectReferenceValue = rig.transform.Find("Camera Offset/Right Controller");
            serialized.FindProperty("spawns").objectReferenceValue = spawns;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            var label = new GameObject("HeadsetStatus").AddComponent<TextMesh>();
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.font = font;
            label.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            label.transform.SetParent(head, false);
            label.transform.localPosition = new Vector3(0f, -0.3f, 1.5f);
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            label.fontSize = 48;
            label.characterSize = 0.012f;
            label.color = Color.white;
            label.text = ConnectionStatus.Offline.Describe();
            return label;
        }

        // Adds NetworkManager, Unity Transport, the connection controller and the panel on one object.
        private static PvPConnectionController CreateNetwork(GameObject presence, TextMesh label)
        {
            var root = new GameObject("NetworkManager");
            var manager = root.AddComponent<NetworkManager>();
            var transport = root.AddComponent<UnityTransport>();
            if (manager.NetworkConfig == null) manager.NetworkConfig = new NetworkConfig();
            manager.NetworkConfig.NetworkTransport = transport;
            manager.NetworkConfig.PlayerPrefab = presence;
            manager.NetworkConfig.ConnectionApproval = true;
            manager.NetworkConfig.EnableSceneManagement = false;
            EditorUtility.SetDirty(manager);

            var controller = root.AddComponent<PvPConnectionController>();
            var controllerFields = new SerializedObject(controller);
            controllerFields.FindProperty("networkManager").objectReferenceValue = manager;
            controllerFields.FindProperty("transport").objectReferenceValue = transport;
            controllerFields.ApplyModifiedPropertiesWithoutUndo();

            var panel = root.AddComponent<ConnectionPanel>();
            var panelFields = new SerializedObject(panel);
            panelFields.FindProperty("controller").objectReferenceValue = controller;
            panelFields.FindProperty("headsetLabel").objectReferenceValue = label;
            panelFields.ApplyModifiedPropertiesWithoutUndo();
            return controller;
        }

        // Wires the saved online arena to the same characters, bow, tracked inputs and arrow used by PvE.
        private static void CreateDuel(ArcherSpawnPoints spawns, TextMesh label, PvPConnectionController connection)
        {
            var host = new GameObject("Online duel");
            var duel = host.AddComponent<PvPDuel>();
            var binder = label.GetComponentInParent<LocalRigBinder>();
            var bowObject = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Game/Prefabs/Bow.prefab"));
            var bow = bowObject.GetComponent<Bow>();
            var input = host.AddComponent<VRBowInput>();
            Set(input, "bow", bow);
            Set(input, "leftController", binder.LeftHand);
            Set(input, "rightController", binder.RightHand);
            var desktop = host.AddComponent<DesktopBowTest>();
            Set(desktop, "bow", bow);
            Set(desktop, "vrInput", input);
            var editor = new SerializedObject(desktop);
            editor.FindProperty("showPanel").boolValue = false;
            editor.ApplyModifiedPropertiesWithoutUndo();
            var view = host.AddComponent<VRInjuryView>();
            Set(view, "trackingOrigin", binder.transform);
            Set(duel, "firstPrefab", AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/Characters/EnglishArcher.prefab"));
            Set(duel, "secondPrefab", AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/Characters/ErikaArcher.prefab"));
            Set(duel, "arrowPrefab", AssetDatabase.LoadAssetAtPath<ArrowFlight>("Assets/_Game/Prefabs/TestArrow.prefab"));
            Set(duel, "bow", bow);
            Set(duel, "vrInput", input);
            Set(duel, "desktop", desktop);
            Set(duel, "injuryView", view);
            Set(duel, "rig", binder);
            Set(duel, "spawns", spawns);
            Set(duel, "connection", connection);
            Set(duel, "feedback", label);
        }

        // Saves one object reference on the authored component.
        private static void Set(Object component, string field, Object value)
        {
            var serialized = new SerializedObject(component);
            serialized.FindProperty(field).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        // Creates each missing folder along an Assets-relative path.
        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
