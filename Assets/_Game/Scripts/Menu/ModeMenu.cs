using System;
using BowMan.Networking;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace BowMan.Menu
{
    // Opens the scene for the mode chosen with the pointer or the desktop keys 1 and 2.
    public sealed class ModeMenu : MonoBehaviour
    {
        [SerializeField] private ControllerPointer pointer;

        private Keyboard keyboard;
        private bool loading;

        public Action<string> Loader { get; set; } = name => SceneManager.LoadScene(name);

        // Applies -desktop before anything renders, since the menu is the first scene.
        private void Awake() => DesktopXR.ApplyCommandLine();

        // Listens to the pointer and to typed keys.
        private void OnEnable()
        {
            if (pointer != null) pointer.Selected += OnSelected;
            keyboard = Keyboard.current;
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
            if (loading) return false;
            loading = true;
            Loader(GameScenes.For(mode));
            return true;
        }

        // Chooses the mode of the panel the pointer confirmed.
        private void OnSelected(ModeOption option) => Choose(option.Mode);

        // Chooses PvE or PvP from the typed digits 1 and 2.
        private void OnTextInput(char key)
        {
            var mode = GameScenes.ModeForKey(key);
            if (mode.HasValue) Choose(mode.Value);
        }
    }
}
