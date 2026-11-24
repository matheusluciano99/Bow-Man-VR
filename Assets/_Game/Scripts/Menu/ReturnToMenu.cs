using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace BowMan.Menu
{
    // Goes back to the mode menu from a match with the left controller's Menu button or Esc.
    public sealed class ReturnToMenu : MonoBehaviour
    {
        public const string Hint = "Menu button: back to modes";

        private InputAction back;
        private bool leaving;

        public Action<string> Loader { get; set; } = name => SceneManager.LoadScene(name);
        public Func<string, bool> CanLoad { get; set; } = Application.CanStreamedLevelBeLoaded;

        // Creates the Menu-button and Esc action.
        private void OnEnable()
        {
            back = new InputAction("Return to menu", InputActionType.Button, "<XRController>{LeftHand}/menu");
            if (DesktopBowTest.DesktopAvailable) back.AddBinding("<Keyboard>/escape");
            back.AddBinding("<WebXRController>{LeftHand}/thumbstickPressed");
            back.Enable();
        }

        // Releases the action.
        private void OnDisable()
        {
            back?.Dispose();
            back = null;
        }

        // Returns to the menu when the action is pressed.
        private void Update()
        {
            if (back != null && back.WasPressedThisFrame()) Return();
        }

        // Loads the menu scene once; warns instead when the menu is not in the build list.
        public bool Return()
        {
            if (leaving) return false;
            if (!CanLoad(GameScenes.MainMenu))
            {
                Debug.LogWarning("MainMenu is not in the build list. Run Bow Man VR > Build Mode Menu And Game Scenes.");
                return false;
            }
            leaving = true;
            Loader(GameScenes.MainMenu);
            return true;
        }
    }
}
