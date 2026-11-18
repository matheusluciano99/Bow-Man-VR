using System;
using System.Linq;
using BowMan;
using UnityEditor;
using UnityEngine;

public static class MotionInjuryBuilder
{
    private const string MotionFolder = "Assets/ThirdParty/MixamoAnimations/Motions/";

    // Retargets the chest fall onto each owned skeleton and preserves its other injury clips.
    [MenuItem("Bow Man VR/Injuries/Rebuild Chest Fall")]
    public static void RebuildChestFall()
    {
        RebuildFall(BodyRegion.Chest, 0.5f, 2.7f, 1f);
        RebuildChestFinisher();
    }

    // Authors a rapid loss of posture and relaxed head separately from the surviving chest sequence.
    [MenuItem("Bow Man VR/Injuries/Rebuild Head Collapse")]
    public static void RebuildHeadCollapse() => RebuildFall(BodyRegion.Head, 0.8f, 1f, 2.5f);

    // Bakes the region's own fall timing and head posture onto each target skeleton.
    private static void RebuildFall(BodyRegion region, float sourceStart, float duration, float speed)
    {
        var source = ImportMotion("Dying Backwards");
        foreach (var name in new[] { "EnglishArcher", "ErikaArcher" })
        {
            var path = $"Assets/_Game/Prefabs/Characters/{name}.prefab";
            var root = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));
            try
            {
                var animator = root.GetComponentInChildren<Animator>();
                var bones = animator.GetComponentsInChildren<Transform>().Where(t => t != animator.transform).ToArray();
                var positions = bones.Select(t => t.localPosition).ToArray();
                var rotations = bones.Select(t => t.localRotation).ToArray();
                var modelPosition = animator.transform.localPosition;
                var modelRotation = animator.transform.localRotation;
                var hips = animator.GetBoneTransform(HumanBodyBones.Hips);
                var neutralHips = hips.position;
                Sample(animator, source, sourceStart, modelPosition, modelRotation);
                var offset = Vector3.ProjectOnPlane(neutralHips - hips.position, Vector3.up);
                Restore(bones, positions, rotations);
                var mesh = new Mesh();
                AnimationClip clip;
                try
                {
                    clip = Record(root, bones, duration, time =>
                    {
                        Restore(bones, positions, rotations);
                        Sample(animator, source, sourceStart + time * speed, modelPosition, modelRotation);
                        hips.position += offset;
                        var blend = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(time / (region == BodyRegion.Head ? 0.06f : 0.18f)));
                        for (var i = 0; i < bones.Length; i++)
                        {
                            bones[i].localPosition = Vector3.Lerp(positions[i], bones[i].localPosition, blend);
                            bones[i].localRotation = Quaternion.Slerp(rotations[i], bones[i].localRotation, blend);
                        }
                        if (region == BodyRegion.Head)
                            animator.GetBoneTransform(HumanBodyBones.Head).localRotation *= Quaternion.Euler(12f * blend, 0f, 35f * blend);
                        var clearance = region == BodyRegion.Head && time > 0f && time < duration ? 0.004f : 0f;
                        Ground(root, hips, mesh, clearance);
                    });
                }
                finally { UnityEngine.Object.DestroyImmediate(mesh); }
                clip.name = region + " Entry";
                var folder = $"Assets/_Game/Animations/Injuries/{name}";
                clip = SaveClip(clip, $"{folder}/{region}_Entry.anim");
                Restore(bones, positions, rotations);
                var prefab = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var player = prefab.GetComponent<ArcherReactionAnimation>();
                    player.SetSequence(region, clip, player.Recovery(region));
                    PrefabUtility.SaveAsPrefabAsset(prefab, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(prefab); }
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
        AssetDatabase.SaveAssets();
    }

    // Fits a back-to-standing motion from the exact landing pose before restoring the archer's ready stance.
    [MenuItem("Bow Man VR/Injuries/Rebuild Chest Recovery")]
    public static void RebuildChestRecovery()
    {
        var source = ImportMotion("Getting Up");
        foreach (var name in new[] { "EnglishArcher", "ErikaArcher" })
        {
            var path = $"Assets/_Game/Prefabs/Characters/{name}.prefab";
            var root = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));
            try
            {
                var animator = root.GetComponentInChildren<Animator>();
                var bones = animator.GetComponentsInChildren<Transform>().Where(t => t != animator.transform).ToArray();
                var neutralPositions = bones.Select(t => t.localPosition).ToArray();
                var neutralRotations = bones.Select(t => t.localRotation).ToArray();
                var modelPosition = animator.transform.localPosition;
                var modelRotation = animator.transform.localRotation;
                var hips = animator.GetBoneTransform(HumanBodyBones.Hips);
                var pelvis = root.GetComponentsInChildren<BodyHitRegion>()
                    .Single(part => part.Region == BodyRegion.Pelvis).transform;
                var neutralHips = hips.position;
                var player = root.GetComponent<ArcherReactionAnimation>();
                var entry = player.Entry(BodyRegion.Chest);
                if (entry == null) throw new InvalidOperationException("Build the chest fall before its recovery.");
                entry.SampleAnimation(root, entry.length);
                var fallenPositions = bones.Select(t => t.localPosition).ToArray();
                var fallenRotations = bones.Select(t => t.localRotation).ToArray();
                var fallenHips = hips.position;
                Sample(animator, source, 2f, modelPosition, modelRotation);
                var startOffset = Vector3.ProjectOnPlane(fallenHips - hips.position, Vector3.up);
                Sample(animator, source, 6.8f, modelPosition, modelRotation);
                var endOffset = Vector3.ProjectOnPlane(neutralHips - hips.position, Vector3.up);
                var mesh = new Mesh();
                AnimationClip clip;
                try
                {
                    clip = Record(root, bones, 5.4f, time =>
                    {
                        Restore(bones, neutralPositions, neutralRotations);
                        Sample(animator, source, Mathf.Min(time + 2f, 6.8f), modelPosition, modelRotation);
                        hips.position += Vector3.Lerp(startOffset, endOffset, Mathf.SmoothStep(0f, 1f, time / 4.8f));
                        var arrival = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(time / 0.35f));
                        var ready = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((time - 4.8f) / 0.6f));
                        for (var i = 0; i < bones.Length; i++)
                        {
                            bones[i].localPosition = Vector3.Lerp(fallenPositions[i], bones[i].localPosition, arrival);
                            bones[i].localRotation = Quaternion.Slerp(fallenRotations[i], bones[i].localRotation, arrival);
                            bones[i].localPosition = Vector3.Lerp(bones[i].localPosition, neutralPositions[i], ready);
                            bones[i].localRotation = Quaternion.Slerp(bones[i].localRotation, neutralRotations[i], ready);
                        }
                        FitChestRecoveryLimbs(animator, pelvis, time);
                        Ground(root, hips, mesh, 0f);
                    });
                }
                finally { UnityEngine.Object.DestroyImmediate(mesh); }
                clip.name = "Chest Recovery";
                clip = SaveClip(clip, $"Assets/_Game/Animations/Injuries/{name}/Chest_Recovery.anim");
                var prefab = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    player = prefab.GetComponent<ArcherReactionAnimation>();
                    player.SetSequence(BodyRegion.Chest, player.Entry(BodyRegion.Chest), clip);
                    PrefabUtility.SaveAsPrefabAsset(prefab, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(prefab); }
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
        AssetDatabase.SaveAssets();
    }

    // Guides the supporting limbs around the torso without shortening their measured bone lengths.
    private static void FitChestRecoveryLimbs(Animator animator, Transform pelvis, float time)
    {
        Move(HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand,
            -pelvis.right * (0.155f * Weight(0.16f, 0.275f, 0.53f, 0.79f)));
        Move(HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot,
            (pelvis.root.right * .018f + pelvis.root.forward * .028f) * Weight(2.52f, 2.67f, 2.87f, 3.04f));
        Move(HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand,
            (pelvis.root.right * .030f + pelvis.root.forward * .040f) * Weight(3.68f, 3.82f, 4.04f, 4.22f));

        // Eases in and out on the authored contact interval.
        float Weight(float start, float enter, float leave, float end) =>
            Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(start, enter, time)) *
            Mathf.SmoothStep(1f, 0f, Mathf.InverseLerp(leave, end, time));

        // Shifts the wrist or ankle while preserving the captured palm or boot rotation.
        void Move(HumanBodyBones upperId, HumanBodyBones lowerId, HumanBodyBones endId, Vector3 offset)
        {
            if (offset == Vector3.zero) return;
            var upper = animator.GetBoneTransform(upperId);
            var lower = animator.GetBoneTransform(lowerId);
            var end = animator.GetBoneTransform(endId);
            var rotation = end.rotation;
            ArcherLimbIK.Solve(animator, upperId, lowerId, endId, end.position + offset, lower.position - upper.position);
            end.rotation = rotation;
        }
    }

    // Relaxes the grounded archer from the chest landing into the final back-down pose.
    private static void RebuildChestFinisher()
    {
        foreach (var name in new[] { "EnglishArcher", "ErikaArcher" })
        {
            var path = $"Assets/_Game/Prefabs/Characters/{name}.prefab";
            var root = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));
            var mesh = new Mesh();
            try
            {
                var animator = root.GetComponentInChildren<Animator>();
                var bones = animator.GetComponentsInChildren<Transform>().Where(t => t != animator.transform).ToArray();
                var hips = animator.GetBoneTransform(HumanBodyBones.Hips);
                var player = root.GetComponent<ArcherReactionAnimation>();
                var entry = player.Entry(BodyRegion.Chest);
                entry.SampleAnimation(root, entry.length);
                var positions = bones.Select(t => t.localPosition).ToArray();
                var rotations = bones.Select(t => t.localRotation).ToArray();
                var start = hips.position;

                var collapse = player.Entry(BodyRegion.Head);
                collapse.SampleAnimation(root, collapse.length);
                hips.position = new Vector3(start.x, hips.position.y, start.z);
                Ground(root, hips, mesh, 0f);
                var targetPositions = bones.Select(t => t.localPosition).ToArray();
                var targetRotations = bones.Select(t => t.localRotation).ToArray();
                const float duration = 0.8f;
                var clip = Record(root, bones, duration, time =>
                {
                    var progress = Mathf.SmoothStep(0f, 1f, time / duration);
                    for (var i = 0; i < bones.Length; i++)
                    {
                        bones[i].localPosition = Vector3.Lerp(positions[i], targetPositions[i], progress);
                        bones[i].localRotation = Quaternion.Slerp(rotations[i], targetRotations[i], progress);
                    }
                    if (time > 0f) Ground(root, hips, mesh, 0f);
                });
                clip.name = "Chest Death";
                clip = SaveClip(clip, $"Assets/_Game/Animations/Injuries/{name}/Chest_Death.anim");
                var prefab = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    prefab.GetComponent<ArcherReactionAnimation>().SetDeathFinisher(BodyRegion.Chest, clip);
                    PrefabUtility.SaveAsPrefabAsset(prefab, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(prefab); }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(mesh);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
        AssetDatabase.SaveAssets();
    }

    // Uses the source T-pose in metre units, rather than the first animated frame or the centimetre derivative.
    private static AnimationClip ImportMotion(string name)
    {
        var path = MotionFolder + name + ".fbx";
        var importer = AssetImporter.GetAtPath(path) as ModelImporter;
        if (importer == null) throw new InvalidOperationException("Missing official motion source: " + path);
        var original = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ThirdParty/ErikaArcher/Models/Erika Archer.fbx");
        var avatar = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/Characters/ErikaArcher.prefab")
            .GetComponentInChildren<Animator>().avatar;
        var description = avatar.humanDescription;
        description.skeleton = original.GetComponentsInChildren<Transform>().Select(t => new SkeletonBone
        {
            name = t == original.transform ? name : t.name,
            position = t.localPosition, rotation = t.localRotation, scale = t.localScale
        }).ToArray();
        importer.sourceAvatar = null;
        importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        importer.animationType = ModelImporterAnimationType.Human;
        importer.humanDescription = description;
        importer.animationCompression = ModelImporterAnimationCompression.Off;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Single(c => !c.name.StartsWith("__"));
    }

    // Transfers sampled root motion into the hips so runtime playback never moves a model away from its prefab root.
    private static void Sample(Animator animator, AnimationClip source, float time, Vector3 position, Quaternion rotation)
    {
        animator.transform.localPosition = position;
        animator.transform.localRotation = rotation;
        source.SampleAnimation(animator.gameObject, time);
        var hips = animator.GetBoneTransform(HumanBodyBones.Hips);
        var hipPosition = hips.position;
        var hipRotation = hips.rotation;
        animator.transform.localPosition = position;
        animator.transform.localRotation = rotation;
        hips.SetPositionAndRotation(hipPosition, hipRotation);
    }

    // Holds a small clearance during interpolation and grounds both ends against the visible skin.
    private static void Ground(GameObject root, Transform hips, Mesh mesh, float clearance)
    {
        var floor = float.PositiveInfinity;
        foreach (var skin in root.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            skin.BakeMesh(mesh, true);
            foreach (var vertex in mesh.vertices) floor = Mathf.Min(floor, skin.transform.TransformPoint(vertex).y);
            mesh.Clear();
        }
        var groundedHeight = root.transform.position.y + clearance;
        if (floor < groundedHeight) hips.position += Vector3.up * (groundedHeight - floor);
    }

    // Bakes complete local position/quaternion groups at sixty samples per second on the actual target hierarchy.
    private static AnimationClip Record(GameObject root, Transform[] bones, float duration, Action<float> pose)
    {
        const float sampleRate = 60f;
        var clip = new AnimationClip { frameRate = sampleRate };
        var curves = bones.Select(_ => Enumerable.Range(0, 7).Select(i => new AnimationCurve()).ToArray()).ToArray();
        var frames = Mathf.RoundToInt(duration * sampleRate);
        for (var frame = 0; frame <= frames; frame++)
        {
            var time = frame / sampleRate;
            pose(time);
            for (var i = 0; i < bones.Length; i++)
            {
                var p = bones[i].localPosition;
                var q = bones[i].localRotation;
                var values = new[] { p.x, p.y, p.z, q.x, q.y, q.z, q.w };
                for (var c = 0; c < 7; c++) curves[i][c].AddKey(time, values[c]);
            }
        }
        var properties = new[] { "localPosition.x", "localPosition.y", "localPosition.z",
            "localRotation.x", "localRotation.y", "localRotation.z", "localRotation.w" };
        for (var i = 0; i < bones.Length; i++)
        for (var c = 0; c < 7; c++)
        {
            var curve = curves[i][c];
            for (var key = 0; key < curve.length; key++)
            {
                AnimationUtility.SetKeyLeftTangentMode(curve, key, AnimationUtility.TangentMode.Linear);
                AnimationUtility.SetKeyRightTangentMode(curve, key, AnimationUtility.TangentMode.Linear);
            }
            clip.SetCurve(AnimationUtility.CalculateTransformPath(bones[i], root.transform), typeof(Transform), properties[c], curve);
        }
        clip.EnsureQuaternionContinuity();
        return clip;
    }

    // Restores the target's neutral local bone values before an independent motion sample.
    private static void Restore(Transform[] bones, Vector3[] positions, Quaternion[] rotations)
    {
        for (var i = 0; i < bones.Length; i++)
        {
            bones[i].localPosition = positions[i];
            bones[i].localRotation = rotations[i];
        }
    }
    // Replaces clip contents without changing existing asset GUIDs when authoring is rerun.
    private static AnimationClip SaveClip(AnimationClip clip, string path)
    {
        var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (existing == null) { AssetDatabase.CreateAsset(clip, path); return clip; }
        EditorUtility.CopySerialized(clip, existing);
        UnityEngine.Object.DestroyImmediate(clip);
        EditorUtility.SetDirty(existing);
        AssetDatabase.SaveAssets();
        return existing;
    }
}
