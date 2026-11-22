using System;
using System.Linq;
using BowMan;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class OpponentBowBuilder
{
    // Fits the straight exchange from the recorded performance to both rigs and both bow hands.
    [MenuItem("Bow Man VR/Characters/Bake opponent bow action")]
    public static void Bake()
    {
        var source = AssetDatabase.LoadAllAssetsAtPath("Assets/ThirdParty/Rokoko/Hawkeye_Archery_mixamo.fbx")
            .OfType<AnimationClip>().Single(c => c.humanMotion && !c.name.StartsWith("__"));
        foreach (var model in new[] { "EnglishArcher", "ErikaArcher" })
            BakeModel(model, source);
        AssetDatabase.SaveAssets();
    }

    // Retargets upper-body joints into the archer's lane direction while keeping the standing legs planted.
    private static void BakeModel(string model, AnimationClip source)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/_Game/Prefabs/Characters/{model}.prefab");
        var root = Object.Instantiate(prefab);
        try
        {
            var rig = root.GetComponentInChildren<Animator>();
            var all = MotionInjuryBuilder.HumanoidBones(rig);
            var positions = all.Select(t => t.localPosition).ToArray();
            var rotations = all.Select(t => t.localRotation).ToArray();
            var world = all.Select(t => t.rotation).ToArray();
            var mirrored = MotionInjuryBuilder.OppositeBoneIndices(rig, all);
            var spine = rig.GetBoneTransform(HumanBodyBones.Spine);
            var upper = all.Where(t => t == spine || t.IsChildOf(spine)).ToArray();
            var hips = rig.GetBoneTransform(HumanBodyBones.Hips);
            var hipStart = hips.position;
            var rigPosition = rig.transform.localPosition;
            var rigRotation = rig.transform.localRotation;
            var left = rig.GetBoneTransform(HumanBodyBones.LeftHand);
            var right = rig.GetBoneTransform(HumanBodyBones.RightHand);
            bool erika = model == "ErikaArcher";
            source.SampleAnimation(rig.gameObject, erika ? 8.5f : 6.5f);
            var forward = Vector3.ProjectOnPlane(left.position - right.position, Vector3.up);
            var lane = Quaternion.AngleAxis(Vector3.SignedAngle(forward, root.transform.forward, Vector3.up), Vector3.up);
            var reaction = new SerializedObject(prefab.GetComponent<ArcherReactionAnimation>());
            foreach (var side in new[] { "Left", "Right" })
            {
                var clip = MotionInjuryBuilder.Record(root, upper, 2.75f, time =>
                {
                    rig.transform.SetLocalPositionAndRotation(rigPosition, rigRotation);
                    // Both draws reach the nock at 1.5 s and the cheek anchor at 2 s on the gameplay clock.
                    float sample = 4.5f + time;
                    if (erika) sample = time < 1.5f ? Mathf.Lerp(7.25f, 8f, time / 1.5f) : 8f + time - 1.5f;
                    source.SampleAnimation(rig.gameObject, sample);
                    var sampled = upper.Select(t => lane * t.rotation).ToArray();
                    rig.transform.SetLocalPositionAndRotation(rigPosition, rigRotation);
                    MotionInjuryBuilder.Restore(all, positions, rotations);
                    for (var i = 0; i < upper.Length; i++) upper[i].rotation = sampled[i];
                    if (side == "Right")
                        MotionInjuryBuilder.MirrorBody(root.transform, hips, hipStart, all, positions, rotations, world, mirrored);
                }, 16f);
                clip.name = model + " Bow " + side;
                var saved = MotionInjuryBuilder.SaveClip(clip, $"Assets/_Game/Animations/{model}Bow{side}.anim");
                reaction.FindProperty("bowIn" + side + "Hand").objectReferenceValue = saved;
            }
            reaction.ApplyModifiedPropertiesWithoutUndo();
        }
        finally { Object.DestroyImmediate(root); }
    }
}
