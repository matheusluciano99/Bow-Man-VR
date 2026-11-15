using BowMan;
using UnityEditor;
using UnityEngine;

public static class WoundBloodBuilder
{
    // Rebuilds the bounded wound emitter and connects it to the shared gameplay arrow prefab.
    [MenuItem("Bow Man VR/Injuries/Rebuild Wound Blood")]
    public static void Rebuild()
    {
        const string materialPath = "Assets/_Game/Materials/WoundBlood.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(material, materialPath);
        }
        material.SetColor("_BaseColor", new Color(0.32f, 0.005f, 0.002f));
        material.SetFloat("_Smoothness", 0.65f);
        var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        var source = new GameObject("Arrow wound blood");
        try
        {
            var system = source.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = system.main;
            main.duration = 2f;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.35f, 0.75f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.002f, 0.005f);
            main.gravityModifier = 1f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Shape;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            main.maxParticles = 80;
            main.playOnAwake = true;
            main.stopAction = ParticleSystemStopAction.Destroy;
            var emission = system.emission;
            emission.rateOverTime = 16f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 20) });
            var shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 25f;
            shape.radius = 0.004f;
            // Clears each droplet's collision radius while retaining the exact wound anchor.
            shape.position = Vector3.forward * 0.015f;
            var collision = system.collision;
            collision.enabled = true;
            collision.type = ParticleSystemCollisionType.World;
            collision.mode = ParticleSystemCollisionMode.Collision3D;
            collision.lifetimeLoss = 1f;
            collision.bounce = 0f;
            var renderer = system.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Mesh;
            renderer.mesh = sphere.GetComponent<MeshFilter>().sharedMesh;
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var prefab = PrefabUtility.SaveAsPrefabAsset(source, "Assets/_Game/Prefabs/ArrowWoundBlood.prefab");
            var arrow = PrefabUtility.LoadPrefabContents("Assets/_Game/Prefabs/TestArrow.prefab");
            try
            {
                var blood = arrow.GetComponent<ArrowWoundBlood>() ?? arrow.AddComponent<ArrowWoundBlood>();
                var settings = new SerializedObject(blood);
                settings.FindProperty("woundPrefab").objectReferenceValue = prefab.GetComponent<ParticleSystem>();
                settings.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(arrow, "Assets/_Game/Prefabs/TestArrow.prefab");
            }
            finally { PrefabUtility.UnloadPrefabContents(arrow); }
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssets();
        }
        finally
        {
            Object.DestroyImmediate(source);
            Object.DestroyImmediate(sphere);
        }
    }
}
