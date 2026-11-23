using System;
using BowMan.Networking;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace BowMan.Menu
{
    // Opens the scene for the mode chosen with the pointer or the numbered desktop shortcuts.
    public sealed class ModeMenu : MonoBehaviour
    {
        [SerializeField] private ControllerPointer pointer;

        [SerializeField] private ModeOption archerOption;
        [SerializeField] private TextMesh archerLabel;
        [SerializeField] private ModeOption handOption;
        [SerializeField] private TextMesh handLabel;
        [SerializeField] private MenuCredits credits;

        private Keyboard keyboard;
        private bool loading;

        public Action<string> Loader { get; set; } = name => SceneManager.LoadScene(name);

        // Applies -desktop before anything renders, since the menu is the first scene.
        private void Awake() => DesktopXR.ApplyCommandLine();

        // Listens to the pointer and to typed keys.
        private void OnEnable()
        {
            if (pointer != null) pointer.Selected += OnSelected;
            RefreshHandLabel();
            ShowArcher();
            keyboard = DesktopBowTest.DesktopAvailable ? Keyboard.current : null;
            if (keyboard != null) keyboard.onTextInput += OnTextInput;
        }

        // Stops listening to the pointer and keyboard.
        private void OnDisable()
        {
            if (pointer != null) pointer.Selected -= OnSelected;
            if (keyboard != null) keyboard.onTextInput -= OnTextInput;
            keyboard = null;
        }

        // Uses the given pointer for selection, replacing any previous one.
        public void Connect(ControllerPointer source)
        {
            if (pointer != null) pointer.Selected -= OnSelected;
            pointer = source;
            if (pointer != null && isActiveAndEnabled) pointer.Selected += OnSelected;
        }

        // Loads the mode's scene once; later choices are ignored while it loads.
        public bool Choose(GameMode mode)
        {
            if (loading || (credits != null && credits.IsOpen)) return false;
            loading = true;
            Loader(GameScenes.For(mode));
            return true;
        }

        // Chooses the mode of the panel the pointer confirmed.
        private void OnSelected(ModeOption option)
        {
            if (loading || (credits != null && credits.Select(option))) return;
            if (option == archerOption) SwitchArcher();
            else if (option == handOption) SwitchHand();
            else Choose(option.Mode);
        }

        // Saves the next archer and shows which character will enter the match.
        private void SwitchArcher()
        {
            ArcherSelection.Preferred = ArcherSelection.Other(ArcherSelection.Preferred);
            ShowArcher();
        }

        // Restores the character label when returning to the mode menu.
        private void ShowArcher()
        {
            if (archerLabel != null) archerLabel.text = "Archer: " + ArcherSelection.Name(ArcherSelection.Preferred);
        }

        // Switches which controller holds the bow and updates the visible menu choice.
        private void SwitchHand()
        {
            if (loading) return;
            VRBowInput.PreferredBowLeft = !VRBowInput.PreferredBowLeft;
            RefreshHandLabel();
        }

        // Describes both hand roles so a first-time player can choose their drawing hand.
        private void RefreshHandLabel()
        {
            if (handLabel != null) handLabel.text = VRBowInput.PreferredBowLeft
                ? "Bow in left hand  •  Draw with right"
                : "Bow in right hand  •  Draw with left";
        }

        // Navigates credits with the keyboard while the same panels remain available to the pointer.
        private void Update()
        {
            if (credits == null || !credits.IsOpen || keyboard == null) return;
            if (keyboard.escapeKey.wasPressedThisFrame) credits.Show(false);
            else if (keyboard.leftArrowKey.wasPressedThisFrame) credits.Turn(-1);
            else if (keyboard.rightArrowKey.wasPressedThisFrame) credits.Turn(1);
        }

        // Opens credits, switches bow hands or chooses a mode from the desktop shortcuts.
        private void OnTextInput(char key)
        {
            if (loading || (credits != null && credits.IsOpen)) return;
            if (credits != null && (key == 'c' || key == 'C')) { credits.Show(true); return; }
            if (key == 'h' || key == 'H') SwitchHand();
            if (key == 'g' || key == 'G') SwitchArcher();
            var mode = GameScenes.ModeForKey(key);
            if (mode.HasValue) Choose(mode.Value);
        }
    }
}
