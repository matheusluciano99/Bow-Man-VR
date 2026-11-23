using UnityEngine;

namespace BowMan.Networking
{
    public sealed class TargetImpactDisplay : MonoBehaviour
    {
        private Transform reticle;

        // Copies the scoring face into the upper-right HUD and builds an open-center mark with contrasting edges.
        public void Build(Transform head, Transform scoringFace)
        {
            transform.SetParent(head, false);
            transform.localPosition = new Vector3(.72f, .39f, 1.7f);
            transform.localScale = Vector3.one * .42f;
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
            Mark(null);
        }

        // Keeps the registered impact in face-local coordinates until the next result.
        public void Mark(Vector2? contact)
        {
            reticle.gameObject.SetActive(contact.HasValue);
            if (contact.HasValue) reticle.localPosition = contact.Value;
        }
    }
}
