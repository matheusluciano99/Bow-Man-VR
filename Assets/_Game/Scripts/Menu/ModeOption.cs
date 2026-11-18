using UnityEngine;

namespace BowMan.Menu
{
    // A selectable mode panel that brightens while the pointer is on it.
    public sealed class ModeOption : MonoBehaviour
    {
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly int LegacyColor = Shader.PropertyToID("_Color");
        private static readonly Color Idle = new Color(0.16f, 0.2f, 0.28f);
        private static readonly Color Highlight = new Color(0.85f, 0.55f, 0.12f);

        [SerializeField] private GameMode mode;
        [SerializeField] private Renderer panel;

        public GameMode Mode => mode;
        public bool IsHighlighted { get; private set; }

        // Sets the mode and the renderer to tint, used by the scene builder and tests.
        public void Configure(GameMode chosenMode, Renderer panelRenderer)
        {
            mode = chosenMode;
            panel = panelRenderer;
            SetHighlighted(false);
        }

        // Starts every panel in its idle color.
        private void Start() => SetHighlighted(false);

        // Tints the panel for the hovered or idle state.
        public void SetHighlighted(bool highlighted)
        {
            IsHighlighted = highlighted;
            if (panel == null) return;
            var block = new MaterialPropertyBlock();
            panel.GetPropertyBlock(block);
            var color = highlighted ? Highlight : Idle;
            block.SetColor(BaseColor, color);
            block.SetColor(LegacyColor, color);
            panel.SetPropertyBlock(block);
        }
    }
}
