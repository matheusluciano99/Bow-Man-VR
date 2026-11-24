using UnityEngine;
using UnityEngine.Rendering;

namespace BowMan
{
    public static class ArcherInterface
    {
        public static readonly Color Panel = new Color32(32, 43, 46, 255);
        public static readonly Color Ink = new Color32(19, 29, 32, 255);
        public static readonly Color Frame = new Color32(110, 89, 59, 255);
        public static readonly Color Brass = new Color32(236, 198, 125, 255);
        public static readonly Color Paper = new Color32(250, 242, 223, 255);
        public static readonly Color Quiet = new Color32(189, 203, 195, 255);
        public static readonly Color Teal = new Color32(31, 60, 54, 255);

        // Applies the atlas material and typography used by the spatial interface.
        public static void Style(TextMesh text, bool title = false)
        {
            var font = Resources.Load<Font>(title ? "Interface/Cinzel" : "Interface/SourceSans3-Semibold")
                ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.font = font;
            text.color = title ? Brass : Paper;
            var renderer = text.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = font.material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        // Creates spatial UI geometry with an unlit color for consistent contrast in arenas.
        public static Renderer Surface(Transform parent, string name, Vector3 position, Vector2 size, Color color,
            bool clickable = false)
        {
            var surface = GameObject.CreatePrimitive(clickable ? PrimitiveType.Cube : PrimitiveType.Quad);
            surface.name = name;
            var collider = surface.GetComponent<Collider>();
            if (!clickable) collider.enabled = false;
            surface.transform.SetParent(parent, false);
            surface.transform.localPosition = position;
            surface.transform.localScale = new Vector3(size.x, size.y, clickable ? .016f : 1f);
            var renderer = surface.GetComponent<MeshRenderer>();
            var material = color == Ink ? "Ink" : color == Frame ? "Frame" :
                color == Brass ? "Brass" : color == Teal ? "Teal" : "Panel";
            renderer.sharedMaterial = Resources.Load<Material>("Interface/" + material);
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return renderer;
        }

        // Tints shared material instances per element to avoid changing other buttons.
        public static void Tint(Renderer renderer, Color color)
        {
            var block = new MaterialPropertyBlock();
            block.SetColor("_BaseColor", color);
            block.SetColor("_Color", color);
            renderer.SetPropertyBlock(block);
        }

        // Creates a centered text line with a known world-space scale.
        public static TextMesh Label(Transform parent, string text, Vector3 position, float size, bool title = false)
        {
            var label = new GameObject("Label").AddComponent<TextMesh>();
            label.transform.SetParent(parent, false);
            label.transform.localPosition = position;
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            label.fontSize = 64;
            label.characterSize = size;
            label.text = text;
            Style(label, title);
            return label;
        }

        // Places the readable caption and a quiet brass rule below the aiming line.
        public static void MatchPanel(TextMesh label, float width)
        {
            Style(label);
            label.transform.localPosition = new Vector3(0f, -.44f, 1.48f);
            label.transform.localScale = Vector3.one;
            label.characterSize = .0145f;
            Surface(label.transform, "HUD backing", new Vector3(0, 0, .015f), new Vector2(width, .31f), Ink);
            Surface(label.transform, "HUD top rule", new Vector3(0, .154f, .013f), new Vector2(width, .005f), Brass);
        }
    }
}
