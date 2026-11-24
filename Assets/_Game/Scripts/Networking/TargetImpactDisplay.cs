using UnityEngine;

namespace BowMan.Networking
{
    public sealed class TargetImpactDisplay : MonoBehaviour
    {
        private Transform reticle;
        private TextMesh caption;

        // Copies the scoring face into the upper-right HUD and builds an open-center mark with contrasting edges.
        public void Build(Transform head, Transform scoringFace)
        {
            transform.SetParent(head, false);
            transform.localPosition = new Vector3(.67f, .43f, 1.7f);
            transform.localScale = Vector3.one * .38f;
            ArcherInterface.Surface(transform, "Impact frame", new Vector3(0, .084f, .028f), new Vector2(1.19f, 1.31f), ArcherInterface.Frame);
            ArcherInterface.Surface(transform, "Impact backing", new Vector3(0, .084f, .019f), new Vector2(1.16f, 1.28f), ArcherInterface.Ink);
            caption = ArcherInterface.Label(transform, "Last shot", new Vector3(0, .586f, -.015f), .039f);
            var mesh = scoringFace.GetComponent<MeshFilter>().sharedMesh;
            var material = scoringFace.GetComponent<Renderer>().sharedMaterial;
            gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            gameObject.AddComponent<MeshRenderer>().sharedMaterial = material;
            reticle = new GameObject("Last impact").transform;
            reticle.SetParent(transform, false);
            foreach (var axis in new[] { Vector2.up, Vector2.down, Vector2.left, Vector2.right })
            {
                var size = new Vector2(Mathf.Abs(axis.x), Mathf.Abs(axis.y)) * .05f;
                for (int layer = 0; layer < 2; layer++)
                {
                    var tick = new GameObject("Reticle tick").transform;
                    tick.SetParent(reticle, false);
                    tick.localPosition = new Vector3(axis.x * .052f, axis.y * .052f, -.004f * (layer + 1));
                    var dimensions = size + Vector2.one * (layer == 0 ? .028f : .012f);
                    tick.localScale = new Vector3(dimensions.x, dimensions.y, 1f);
                    tick.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
                    var renderer = tick.gameObject.AddComponent<MeshRenderer>();
                    renderer.sharedMaterial = material;
                    var tint = new MaterialPropertyBlock();
                    tint.SetTexture("_BaseMap", Texture2D.whiteTexture);
                    tint.SetColor("_BaseColor", layer == 0 ? Color.black : Color.white);
                    renderer.SetPropertyBlock(tint);
                }
            }
            Mark(null, -1);
        }

        // Keeps the registered impact in face-local coordinates until the next result.
        public void Mark(Vector2? contact, int points)
        {
            reticle.gameObject.SetActive(contact.HasValue);
            caption.text = points < 0 ? "Last shot" : points == 0 ? "Miss" : $"+{points} points";
            if (contact.HasValue) reticle.localPosition = contact.Value;
        }
    }
}
