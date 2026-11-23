using UnityEngine;

namespace BowMan.Menu
{
    // A selectable mode panel that brightens while the pointer is on it.
    public sealed class ModeOption : MonoBehaviour
    {
        [SerializeField] private GameMode mode;
        [SerializeField] private Renderer panel;
        [SerializeField] private Color restingColor = new Color32(32, 43, 46, 255);

        public GameMode Mode => mode;
        public bool IsHighlighted { get; private set; }

        // Sets the mode and the renderer to tint, used by the scene builder and tests.
        public void Configure(GameMode chosenMode, Renderer panelRenderer, Color? idleColor = null)
        {
            mode = chosenMode;
            panel = panelRenderer;
            if (idleColor.HasValue) restingColor = idleColor.Value;
            SetHighlighted(false);
        }

        // Updates the resting tint used for a selected course.
        public void SetRestingColor(Color color)
        {
            restingColor = color;
            SetHighlighted(IsHighlighted);
        }

        // Starts every panel in its idle color.
        private void Start() => SetHighlighted(false);

        // Tints the panel for the hovered or idle state.
        public void SetHighlighted(bool highlighted)
        {
            IsHighlighted = highlighted;
            if (panel == null) return;
            ArcherInterface.Tint(panel, highlighted ? new Color32(100, 74, 36, 255) : restingColor);
        }
    }
}
