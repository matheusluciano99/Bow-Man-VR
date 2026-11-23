using System.Collections.Generic;
using BowMan.Networking;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BowMan.Menu
{
    // Presents the multiplayer and target options on a controller-accessible panel.
    public sealed class WebXRLobby : MonoBehaviour
    {
        private PvPConnectionController connection;
        private ControllerPointer pointer;
        private GameObject board, choices, keypad;
        private ModeOption host, join, cpu, connect, back, delete;
        private OnlineTargetMatch range;
        private TextMesh prompt, distanceHeading, guidance;
        private GameObject statusHud;
        private readonly Dictionary<ModeOption, int> distances = new();
        private string code = "";

        // Adds the spatial lobby after a game scene and its tracking rig load.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register() => SceneManager.sceneLoaded += EnterScene;

        // Builds controls on the lobby rig in the newly loaded scene.
        private static void EnterScene(Scene scene, LoadSceneMode mode)
        {
            var connection = FindAnyObjectByType<PvPConnectionController>();
            var rig = FindAnyObjectByType<LocalRigBinder>();
            if (connection == null || rig == null || rig.gameObject.scene != scene) return;
            var lobby = rig.gameObject.AddComponent<WebXRLobby>();
            lobby.Build(connection, rig);
        }

        // Lays out directly selectable distances and a separate join-code screen.
        private void Build(PvPConnectionController controller, LocalRigBinder rig)
        {
            connection = controller;
            statusHud = rig.Head.Find("HeadsetStatus")?.gameObject;
            board = new GameObject("Online lobby controls");
            board.transform.SetParent(rig.transform, false);
            board.transform.localPosition = new Vector3(0, 1.62f, 2.15f);
            ArcherInterface.Surface(board.transform, "Lobby frame", new Vector3(0, 0, .05f), new Vector2(1.99f, 1.37f), ArcherInterface.Frame);
            ArcherInterface.Surface(board.transform, "Lobby backing", new Vector3(0, 0, .035f), new Vector2(1.97f, 1.35f), ArcherInterface.Ink);
            choices = new GameObject("Match options");
            choices.transform.SetParent(board.transform, false);
            ArcherInterface.Label(choices.transform, controller.IsTargetCompetition ? "Tiro ao alvo" : "Online duel",
                new Vector3(0, .5f, 0), .034f, true);
            if (controller.IsTargetCompetition)
            {
                range = FindAnyObjectByType<OnlineTargetMatch>();
                distanceHeading = ArcherInterface.Label(choices.transform, "Target distance", new Vector3(0, .31f, 0), .019f);
                distanceHeading.color = ArcherInterface.Quiet;
                var options = OnlineTargetMatch.Distances;
                for (int i = 0; i < options.Count; i++)
                {
                    int metres = options[i];
                    distances.Add(Key(choices.transform, $"{metres} m", (i - 2) * .33f, .151f, .3f), metres);
                }
                cpu = Key(choices.transform, "Play against CPU", 0, -.081f, 1.62f, true);
            }
            host = Key(choices.transform, "Host online", -.415f, -.315f, .79f);
            join = Key(choices.transform, "Join with code", .415f, -.315f, .79f);
            guidance = ArcherInterface.Label(choices.transform, "Right trigger to select", new Vector3(0, -.52f, 0), .018f);
            guidance.color = ArcherInterface.Quiet;

            keypad = new GameObject("Join code");
            keypad.transform.SetParent(board.transform, false);
            ArcherInterface.Label(keypad.transform, "Join a match", new Vector3(0, .52f, 0), .031f, true);
            prompt = ArcherInterface.Label(keypad.transform, "", new Vector3(0, .377f, 0), .030f);
            const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            for (int i = 0; i < alphabet.Length; i++)
                Key(keypad.transform, alphabet[i].ToString(), (i % 6 - 2.5f) * .277f, .219f - i / 6 * .126f, .241f);
            back = Key(keypad.transform, "Back", -.55f, -.559f, .5f);
            delete = Key(keypad.transform, "Delete", 0f, -.559f, .5f);
            connect = Key(keypad.transform, "Join", .55f, -.559f, .5f);

            pointer = rig.RightHand.gameObject.AddComponent<ControllerPointer>();
            pointer.AddVisibleRay();
            pointer.Selected += Press;
            pointer.enabled = false;
            board.SetActive(false);
            SetCodeScreen(false);
            ShowDistance();
        }

        // Creates one high-contrast button with a physical controller target.
        private static ModeOption Key(Transform parent, string caption, float x, float y, float width, bool primary = false)
        {
            var key = new GameObject(caption).transform;
            key.SetParent(parent, false);
            key.localPosition = new Vector3(x, y, 0);
            var color = primary ? ArcherInterface.Teal : ArcherInterface.Panel;
            var size = new Vector2(width, width <= .25f ? .102f : .155f);
            var face = ArcherInterface.Surface(key, "Key face", Vector3.zero, size, color, true);
            ArcherInterface.Label(key, caption, new Vector3(0, 0, -.014f), .018f);
            var option = key.gameObject.AddComponent<ModeOption>();
            option.Configure(GameMode.PvP, face, color);
            return option;
        }

        // Updates code cells and enables confirmation when six characters are filled.
        private void ShowCode()
        {
            prompt.text = string.Join("   ", code.PadRight(6, '_').ToCharArray());
            connect.gameObject.SetActive(code.Length == 6);
        }

        // Switches between match choices and code entry without changing the active course.
        private void SetCodeScreen(bool entering)
        {
            keypad.SetActive(entering);
            choices.SetActive(!entering);
            if (entering) ShowCode();
        }

        // Keeps the chosen distance visible after the pointer moves away.
        private void ShowDistance()
        {
            if (range == null) return;
            distanceHeading.text = $"Target distance · {range.HostDistance} m";
            foreach (var option in distances)
                option.Key.SetRestingColor(option.Value == range.HostDistance ? ArcherInterface.Teal : ArcherInterface.Panel);
        }

        // Routes a button confirmation to the course, solo match or Relay lobby.
        private void Press(ModeOption key)
        {
            if (!board.activeSelf || !connection.Status.CanStart) return;
            if (keypad.activeSelf)
            {
                if (key == back) { SetCodeScreen(false); return; }
                if (key == connect && code.Length == 6) { _ = connection.JoinAsync(code); return; }
                if (key == delete && code.Length > 0) code = code.Substring(0, code.Length - 1);
                if (key.name.Length == 1 && code.Length < 6) code += key.name;
                ShowCode();
                return;
            }
            if (key == join) { SetCodeScreen(true); return; }
            if (key == host) { _ = connection.HostAsync(); return; }
            if (cpu != null && key == cpu) { range.StartCpuMatch(); return; }
            if (distances.TryGetValue(key, out int metres))
            {
                range.ChooseHostDistance(metres);
                ShowDistance();
            }
        }

        // Clears lobby controls from the firing lane during an active match or connection.
        private void Update()
        {
            bool show = VRBowInput.HeadsetActive && connection != null && connection.Status.CanStart && (range == null || !range.Playing);
            if (show == board.activeSelf) return;
            board.SetActive(show);
            if (statusHud != null) statusHud.SetActive(!show);
            pointer.enabled = show;
            if (show)
            {
                if (connection.Status.Phase == ConnectionPhase.Failed || connection.Status.Phase == ConnectionPhase.Disconnected)
                    SetCodeScreen(false);
                guidance.text = connection.Status.Phase switch
                {
                    ConnectionPhase.Failed => "Could not connect. Check the code or host again.",
                    ConnectionPhase.Disconnected => "Connection ended. Host or join again.",
                    _ => "Right trigger to select"
                };
                ShowDistance();
            }
        }

        // Releases the pointer subscription when the player leaves the scene.
        private void OnDestroy()
        {
            if (pointer != null) pointer.Selected -= Press;
        }
    }
}
