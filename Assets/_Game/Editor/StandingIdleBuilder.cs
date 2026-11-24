using System;
using System.Linq;
using BowMan;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class StandingIdleBuilder
{
    private const string Source = "Assets/ThirdParty/Rokoko/Regular_Light_mixamo.fbx";
    private const float Duration = 8f;
    // Samples a relaxed standing take with weight transfer through the pelvis and both supporting legs.
    [MenuItem("Bow Man VR/Characters/Bake standing idle")]
    public static void Bake()
    {
        var take = AssetDatabase.LoadAllAssetsAtPath(Source).OfType<AnimationClip>()
            .Single(c => !c.name.StartsWith("__"));
        var source = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Source));
        try
        {
            if (take.length < Duration + 35f) throw new InvalidOperationException("Standing take is too short.");
            foreach (var model in new[] { "EnglishArcher", "ErikaArcher" })
                BakeModel(model, source, take);
        }
        finally { Object.DestroyImmediate(source); }
        AssetDatabase.SaveAssets();
    }

    // Transfers world-space motion relative to the first pose, leaving the target's planted boots and grip intact.
    private static void BakeModel(string model, GameObject source, AnimationClip take)
    {
        var path = $"Assets/_Game/Prefabs/Characters/{model}.prefab";
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        var root = Object.Instantiate(prefab);
        try
        {
            var rig = root.GetComponentInChildren<Animator>();
            var ids = new[] { HumanBodyBones.Spine, HumanBodyBones.Chest, HumanBodyBones.UpperChest,
                HumanBodyBones.Neck, HumanBodyBones.Head, HumanBodyBones.LeftShoulder, HumanBodyBones.LeftUpperArm,
                HumanBodyBones.LeftLowerArm, HumanBodyBones.RightShoulder, HumanBodyBones.RightUpperArm,
                HumanBodyBones.RightLowerArm };
            var names = new[] { "Spine", "Spine1", "Spine2", "Neck", "Head", "LeftShoulder", "LeftArm",
                "LeftForeArm", "RightShoulder", "RightArm", "RightForeArm" };
            var skeleton = source.GetComponentsInChildren<Transform>();
            var recorded = names.Select(n => skeleton.Single(t => t.name == "mixamorig:" + n)).ToArray();
            var bones = ids.Select(rig.GetBoneTransform).ToArray();
            float startTime = model == "ErikaArcher" ? 35f : 1f;
            take.SampleAnimation(source, startTime);
            var first = recorded.Select(b => b.rotation).ToArray();
            var across = recorded[5].position - recorded[8].position;
            var align = Quaternion.AngleAxis(Vector3.SignedAngle(Vector3.ProjectOnPlane(across, Vector3.up),
                Vector3.ProjectOnPlane(bones[5].position - bones[8].position, Vector3.up), Vector3.up), Vector3.up);
            // Fit the relaxed arm directions to each rig while retaining clearance for the quiver and belt.
            foreach (var upper in new[] { 6, 9 })
            {
                var left = upper == 6;
                var hand = rig.GetBoneTransform(left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
                var recordedHand = skeleton.Single(t => t.name == (left ? "mixamorig:LeftHand" : "mixamorig:RightHand"));
                FitDirection(bones[upper], bones[upper + 1], align * (recorded[upper + 1].position - recorded[upper].position));
                FitDirection(bones[upper + 1], hand, align * (recordedHand.position - recorded[upper + 1].position));
            }
            var neutral = bones.Select(b => b.rotation).ToArray();
            var hips = rig.GetBoneTransform(HumanBodyBones.Hips);
            var sourceHips = skeleton.Single(t => t.name == "mixamorig:Hips");
            var hipStart = hips.position;
            var hipRotation = hips.rotation;
            var sourceStart = sourceHips.position;
            var sourceRotation = sourceHips.rotation;
            var stance = new PlantedArcherStance(rig, root.transform);
            var baked = bones.Concat(new[] { hips }).Concat(stance.Bones).ToArray();
            var sourceFoot = skeleton.Single(t => t.name == "mixamorig:LeftFoot");
            var scale = (hipStart.y - stance.Bones[2].position.y) / (sourceStart.y - sourceFoot.position.y);
            var clip = MotionInjuryBuilder.Record(root, baked, Duration, time =>
            {
                take.SampleAnimation(source, time + startTime);
                var edge = Mathf.SmoothStep(0f, 1f, Mathf.Min(time, Duration - time) / 0.8f);
                var shift = Vector3.ClampMagnitude(align * (sourceHips.position - sourceStart) * scale, 0.065f);
                // Let the supporting knees flex as the performer settles over a boot.
                shift.y = Mathf.Min(0f, shift.y) - Mathf.Min(0.028f, shift.magnitude * 0.7f);
                hips.position = hipStart + shift * edge;
                var tilt = align * sourceHips.rotation * Quaternion.Inverse(sourceRotation) * Quaternion.Inverse(align);
                hips.rotation = Quaternion.Slerp(Quaternion.identity, tilt, edge) * hipRotation;
                stance.Plant();
                for (var i = 0; i < bones.Length; i++)
                {
                    var delta = align * recorded[i].rotation * Quaternion.Inverse(first[i]) * Quaternion.Inverse(align);
                    bones[i].rotation = Quaternion.Slerp(Quaternion.identity, delta, edge) * neutral[i];
                }
            }, 15f);
            clip.name = model + " Standing Idle";
            clip.wrapMode = WrapMode.Loop;
            var saved = MotionInjuryBuilder.SaveClip(clip, $"Assets/_Game/Animations/{model}StandingIdle.anim");
            var reaction = new SerializedObject(prefab.GetComponent<ArcherReactionAnimation>());
            reaction.FindProperty("standingIdle").objectReferenceValue = saved;
            reaction.ApplyModifiedPropertiesWithoutUndo();
        }
        finally { Object.DestroyImmediate(root); }
    }

    // Retains some clearance from the rig's clothing when its proportions differ from the performer.
    private static void FitDirection(Transform joint, Transform end, Vector3 direction)
    {
        var fit = Quaternion.FromToRotation(end.position - joint.position, direction);
        joint.rotation = Quaternion.Slerp(Quaternion.identity, fit, 0.8f) * joint.rotation;
    }

}
