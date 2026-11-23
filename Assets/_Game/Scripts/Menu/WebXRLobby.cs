using BowMan.Networking;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BowMan.Menu
{
    // Lets an immersed browser player host or enter a Relay code with their right controller.
    public sealed class WebXRLobby : MonoBehaviour
    {
        private PvPConnectionController connection;
        private ControllerPointer pointer;
        private GameObject board, keypad;
        private ModeOption join, distance;
        private OnlineTargetMatch range;
        private TextMesh distanceLabel;
        private TextMesh prompt;
        private string code = "";

        // Installs the in-headset controls on the two scenes that offer Relay connections.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register()
        {
            SceneManager.sceneLoaded += EnterScene;
        }

        // Binds the scene's own tracking rig so the board follows its assigned network spawn.
        private static void EnterScene(Scene scene, LoadSceneMode mode)
        {
            var connection = FindAnyObjectByType<PvPConnectionController>();
            var rig = FindAnyObjectByType<LocalRigBinder>();
            if (connection == null || rig == null || rig.gameObject.scene != scene) return;
            var lobby = rig.gameObject.AddComponent<WebXRLobby>();
            lobby.Build(connection, rig);
        }

        // Lays out the host button and a six-column keyboard within seated pointing reach.
        private void Build(PvPConnectionController controller, LocalRigBinder rig)
        {
            connection = controller;
            board = new GameObject("Online lobby controls");
            board.transform.SetParent(rig.transform, false);
            board.transform.localPosition = new Vector3(0, 1.6f, 2);
            prompt = Label(board.transform, "Online match", new Vector3(0, .48f, 0), .042f);
            Key(board.transform, "Host", -.36f, .28f, .62f);
            join = Key(board.transform, "Join", .36f, .28f, .62f);
            if (controller.IsTargetCompetition)
            {
                range = FindAnyObjectByType<OnlineTargetMatch>();
                distance = Key(board.transform, "Host distance", 0, .73f, 1.36f);
                distanceLabel = distance.GetComponentInChildren<TextMesh>();
                distanceLabel.text = $"Host distance: {range.HostDistance} m";
            }
            keypad = new GameObject("Join code");
            keypad.transform.SetParent(board.transform, false);
            const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            for (int i = 0; i < alphabet.Length; i++)
                Key(keypad.transform, alphabet[i].ToString(), (i % 6 - 2.5f) * .23f, .08f - i / 6 * .15f, .21f);
            Key(keypad.transform, "Delete", 0f, -.83f, .62f);
            keypad.SetActive(false);
            pointer = rig.RightHand.gameObject.AddComponent<ControllerPointer>();
            pointer.Selected += Press;
            pointer.enabled = false;
            board.SetActive(false);
        }

        // Sizes a centered text mesh by its rendered line height in world units.
        private static TextMesh Label(Transform parent, string text, Vector3 position, float height)
        {
            var mesh = new GameObject(text).AddComponent<TextMesh>();
            mesh.transform.SetParent(parent, false);
            mesh.transform.localPosition = position;
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            mesh.fontSize = 64;
            mesh.characterSize = .02f;
            mesh.text = text;
            var renderer = mesh.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = mesh.font.material;
            mesh.transform.localScale *= height / Mathf.Max(.001f, renderer.bounds.size.y);
            return mesh;
        }

        // Gives each key a raycastable face and the same hover highlight as the mode menu.
        private static ModeOption Key(Transform parent, string caption, float x, float y, float width)
        {
            var key = new GameObject(caption);
            key.transform.SetParent(parent, false);
            key.transform.localPosition = new Vector3(x, y, 0);
            var face = GameObject.CreatePrimitive(PrimitiveType.Cube);
            face.transform.SetParent(key.transform, false);
            face.transform.localScale = new Vector3(width, .13f, .02f);
            Label(key.transform, caption, new Vector3(0, 0, -.016f), .072f);
            var option = key.AddComponent<ModeOption>();
            option.Configure(GameMode.PvP, face.GetComponent<Renderer>());
            return option;
        }

        // Edits the six-character code or starts the selected connection exactly once.
        private void Press(ModeOption key)
        {
            if (!board.activeSelf || !connection.Status.CanStart) return;
            if (distance != null && key == distance)
            {
                var options = OnlineTargetMatch.Distances;
                range.ChooseHostDistance(options[(options.IndexOf(range.HostDistance) + 1) % options.Count]);
                distanceLabel.text = $"Host distance: {range.HostDistance} m";
                return;
            }
            if (key.name == "Host") { _ = connection.HostAsync(); return; }
            if (key == join)
            {
                if (!keypad.activeSelf) keypad.SetActive(true);
                else if (code.Length == 6) { _ = connection.JoinAsync(code); return; }
            }
            else if (key.name == "Delete")
            {
                if (code.Length > 0) code = code.Substring(0, code.Length - 1);
            }
            else if (code.Length < 6) code += key.name;
            prompt.text = "Join code: " + code.PadRight(6, '_');
        }

        // Removes the keyboard and its solid hit areas before either bow is unlocked.
        private void Update()
        {
            bool show = VRBowInput.HeadsetActive && connection != null && connection.Status.CanStart;
            if (show == board.activeSelf) return;
            if (show && distanceLabel != null) distanceLabel.text = $"Host distance: {range.HostDistance} m";
            board.SetActive(show);
            pointer.enabled = show;
        }

        // Releases the rig's pointer subscription when the match scene unloads.
        private void OnDestroy()
        {
            if (pointer != null) pointer.Selected -= Press;
        }
    }
}
