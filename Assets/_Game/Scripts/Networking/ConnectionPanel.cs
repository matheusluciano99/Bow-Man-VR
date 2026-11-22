using UnityEngine;

namespace BowMan.Networking
{
    // Desktop Host/Join panel plus an in-headset status line for the PvP connection test.
    public sealed class ConnectionPanel : MonoBehaviour
    {
        [SerializeField] private PvPConnectionController controller;
        [SerializeField] private TextMesh headsetLabel;

        private const string MenuHint = "Menu button / Esc: back to modes";

        private string codeInput = string.Empty;

        // Mirrors every status change onto the headset label.
        private void OnEnable()
        {
            controller.StatusChanged += ShowInHeadset;
            ShowInHeadset(controller.Status);
        }

        // Stops mirroring status changes.
        private void OnDisable()
        {
            if (controller != null) controller.StatusChanged -= ShowInHeadset;
        }

        // Draws the status, Host, code field, Join and Leave controls in the desktop window.
        private void OnGUI()
        {
            var status = controller.Status;
            GUILayout.BeginArea(new Rect(16, 16, 360, Screen.height - 32));
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label(controller.IsTargetCompetition ? "Tiro ao alvo  •  Online" : "PvP connection test");
            GUILayout.Label(status.Describe());
            GUI.enabled = status.CanStart;
            if (GUILayout.Button("Host")) _ = controller.HostAsync();
            GUILayout.BeginHorizontal();
            codeInput = GUILayout.TextField(codeInput, 16, GUILayout.Width(200));
            if (GUILayout.Button("Join")) _ = controller.JoinAsync(codeInput);
            GUILayout.EndHorizontal();
            GUI.enabled = !status.CanStart;
            if (GUILayout.Button("Leave")) controller.Leave();
            GUI.enabled = true;
            GUILayout.Label(MenuHint);
            GUILayout.EndVertical();
            GUILayout.EndArea();
        }

        // Writes the status sentence on the label in front of the headset camera.
        private void ShowInHeadset(ConnectionStatus status)
        {
            if (headsetLabel != null) headsetLabel.text = status.Describe() + "\n" + MenuHint;
        }
    }
}
