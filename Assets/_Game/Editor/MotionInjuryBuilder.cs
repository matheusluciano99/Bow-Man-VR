using System;
using System.Linq;
using BowMan;
using UnityEditor;
using UnityEngine;

public static class MotionInjuryBuilder
{
    private const string MotionFolder = "Assets/ThirdParty/MixamoAnimations/Motions/";

    // Authors a rapid loss of posture and relaxed head separately from the surviving chest sequence.
    [MenuItem("Bow Man VR/Injuries/Rebuild Head Collapse")]
    public static void RebuildHeadCollapse()
    {
        const BodyRegion region = BodyRegion.Head;
        const float sourceStart = 0.8f;
        const float duration = 1f;
        const float speed = 2.5f;
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
                        var blend = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(time / 0.06f));
                        for (var i = 0; i < bones.Length; i++)
                        {
                            bones[i].localPosition = Vector3.Lerp(positions[i], bones[i].localPosition, blend);
                            bones[i].localRotation = Quaternion.Slerp(rotations[i], bones[i].localRotation, blend);
                        }
                        animator.GetBoneTransform(HumanBodyBones.Head).localRotation *= Quaternion.Euler(12f * blend, 0f, 35f * blend);
                        var clearance = time > 0f && time < duration ? 0.004f : 0f;
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

    // Rebuilds the left-foot entry, supported balance, recovery and fatal continuation on both models.
    [MenuItem("Bow Man VR/Injuries/Rebuild Left Foot")]
    public static void RebuildLeftFoot() => RebuildFoot(BodyRegion.LeftFoot);

    // Rebuilds the right-foot entry, supported balance, recovery and fatal continuation on both models.
    [MenuItem("Bow Man VR/Injuries/Rebuild Right Foot")]
    public static void RebuildRightFoot() => RebuildFoot(BodyRegion.RightFoot);

    // Fits the complete downloaded foot performance, with planted support and model-specific transitions.
    private static void RebuildFoot(BodyRegion region)
    {
        var source = ImportFootMotion();
        var left = region == BodyRegion.LeftFoot;
        foreach (var name in new[] { "EnglishArcher", "ErikaArcher" })
        {
            var path = $"Assets/_Game/Prefabs/Characters/{name}.prefab";
            var root = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));
            var contactMesh = new Mesh();
            try
            {
                var animator = root.GetComponentInChildren<Animator>();
                var humanBones = Enumerable.Range(0, (int)HumanBodyBones.LastBone)
                    .Select(i => animator.GetBoneTransform((HumanBodyBones)i)).Where(t => t != null).ToArray();
                var bones = animator.GetComponentsInChildren<Transform>().Where(t => humanBones.Contains(t)).ToArray();
                var positions = bones.Select(t => t.localPosition).ToArray();
                var rotations = bones.Select(t => t.localRotation).ToArray();
                var worldRotations = bones.Select(t => t.rotation).ToArray();
                var mirrored = bones.Select(t =>
                {
                    var human = (HumanBodyBones)Enumerable.Range(0, (int)HumanBodyBones.LastBone)
                        .First(i => animator.GetBoneTransform((HumanBodyBones)i) == t);
                    var opposite = human.ToString().StartsWith("Left") ? human.ToString().Replace("Left", "Right")
                        : human.ToString().StartsWith("Right") ? human.ToString().Replace("Right", "Left") : human.ToString();
                    return Array.IndexOf(bones, animator.GetBoneTransform(Enum.Parse<HumanBodyBones>(opposite)));
                }).ToArray();
                var modelPosition = animator.transform.localPosition;
                var modelRotation = animator.transform.localRotation;
                var hips = animator.GetBoneTransform(HumanBodyBones.Hips);
                var hipStart = hips.position;
                var feet = new[] { animator.GetBoneTransform(HumanBodyBones.LeftFoot), animator.GetBoneTransform(HumanBodyBones.RightFoot) };
                var footPositions = feet.Select(t => t.position).ToArray();
                var footRotations = feet.Select(t => t.rotation).ToArray();
                var injured = left ? 0 : 1;
                var healthy = 1 - injured;
                var skins = root.GetComponentsInChildren<SkinnedMeshRenderer>();
                var soleIndices = feet.Select(foot => skins.Select(skin =>
                {
                    var footBones = skin.bones.Select(bone => bone == foot || bone.IsChildOf(foot)).ToArray();
                    return skin.sharedMesh.boneWeights.Select((weight, index) => new
                    {
                        index, influence = (footBones[weight.boneIndex0] ? weight.weight0 : 0f)
                            + (footBones[weight.boneIndex1] ? weight.weight1 : 0f)
                            + (footBones[weight.boneIndex2] ? weight.weight2 : 0f)
                            + (footBones[weight.boneIndex3] ? weight.weight3 : 0f)
                    }).Where(value => value.influence > 0.5f).Select(value => value.index).ToArray();
                }).ToArray()).ToArray();

                // Measures only skin bound to this boot, including its full sole rather than a joint-radius proxy.
                float SoleFloor(int side)
                {
                    var minimum = float.PositiveInfinity;
                    for (var i = 0; i < skins.Length; i++)
                    {
                        if (soleIndices[side][i].Length == 0) continue;
                        skins[i].BakeMesh(contactMesh, true);
                        var vertices = contactMesh.vertices;
                        foreach (var index in soleIndices[side][i])
                            minimum = Mathf.Min(minimum, skins[i].transform.TransformPoint(vertices[index]).y);
                    }
                    if (float.IsInfinity(minimum)) throw new InvalidOperationException("No skinned boot surface on " + name);
                    return minimum;
                }

                // Samples the source, mirrors anatomical rotation deltas when needed, and fixes support drift.
                void Pose(float time, float blend, float lift, float landing)
                {
                    Restore(bones, positions, rotations);
                    Sample(animator, source, time, modelPosition, modelRotation);
                    for (var i = 0; i < bones.Length; i++)
                        if (bones[i] != hips) bones[i].localPosition = positions[i];
                    if (left)
                    {
                        var sampled = bones.Select(t => t.rotation).ToArray();
                        var offset = root.transform.InverseTransformVector(hips.position - hipStart);
                        offset.x = -offset.x;
                        Restore(bones, positions, rotations);
                        hips.position = hipStart + root.transform.TransformVector(offset);
                        for (var i = 0; i < bones.Length; i++)
                        {
                            var other = mirrored[i];
                            var delta = Quaternion.Inverse(root.transform.rotation) * sampled[other]
                                * Quaternion.Inverse(worldRotations[other]) * root.transform.rotation;
                            delta = new Quaternion(delta.x, -delta.y, -delta.z, delta.w);
                            bones[i].rotation = root.transform.rotation * delta
                                * Quaternion.Inverse(root.transform.rotation) * worldRotations[i];
                        }
                    }
                    hips.position += footPositions[healthy] - feet[healthy].position;
                    feet[healthy].rotation = footRotations[healthy];
                    var withdrawal = feet[injured].position;
                    var withdrawnRotation = feet[injured].rotation;
                    for (var i = 0; i < bones.Length; i++)
                    {
                        bones[i].localPosition = Vector3.Lerp(positions[i], bones[i].localPosition, blend);
                        bones[i].localRotation = Quaternion.Slerp(rotations[i], bones[i].localRotation, blend);
                    }
                    hips.position += footPositions[healthy] - feet[healthy].position;
                    feet[healthy].rotation = footRotations[healthy];
                    if (lift < 1f || landing > 0f)
                    {
                        var target = Vector3.Lerp(footPositions[injured], withdrawal, lift * (1f - landing));
                        ArcherLimbIK.Solve(animator, left ? HumanBodyBones.LeftUpperLeg : HumanBodyBones.RightUpperLeg,
                            left ? HumanBodyBones.LeftLowerLeg : HumanBodyBones.RightLowerLeg,
                            left ? HumanBodyBones.LeftFoot : HumanBodyBones.RightFoot, target, root.transform.forward);
                        feet[injured].rotation = Quaternion.Slerp(footRotations[injured], withdrawnRotation, lift * (1f - landing));
                    }
                    foreach (var toe in new[] { HumanBodyBones.LeftToes, HumanBodyBones.RightToes })
                    {
                        var index = Array.IndexOf(bones, animator.GetBoneTransform(toe));
                        bones[index].localRotation = rotations[index];
                    }
                    if (blend <= 0f) Restore(bones, positions, rotations);
                    else
                    {
                        FitBootContact(root, feet[injured], () => SoleFloor(injured), false);
                        FitBootContact(root, feet[healthy], () => SoleFloor(healthy), true);
                    }
                }

                // Eases an authored contact transition without replacing the source's body motion.
                float Ease(float time, float start, float end) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(start, end, time));

                const float entrySourceEnd = 0.8f;
                var entry = Record(root, bones, 0.3f + entrySourceEnd, time =>
                    Pose(Mathf.Max(0f, time - 0.3f), Ease(time, 0f, 0.3f), Ease(time, 0.1f, 0.3f), 0f));
                var hold = Record(root, bones, 3f, time => Pose(entrySourceEnd + time, 1f, 1f, 0f));
                var tail = source.length - entrySourceEnd - 3f;
                var recovery = Record(root, bones, tail + 0.9f, time =>
                    Pose(Mathf.Min(source.length, entrySourceEnd + 3f + time), 1f - Ease(time, tail + 0.4f, tail + 0.9f),
                        1f, Ease(time, tail, tail + 0.4f)));
                entry.name = region + " Entry";
                hold.name = region + " Balance";
                recovery.name = region + " Recovery";
                var folder = $"Assets/_Game/Animations/Injuries/{name}";
                entry = SaveClip(entry, $"{folder}/{region}_Entry.anim");
                hold = SaveClip(hold, $"{folder}/{region}_Hold.anim");
                recovery = SaveClip(recovery, $"{folder}/{region}_Recovery.anim");
                var prefab = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    prefab.GetComponent<ArcherReactionAnimation>().SetSequence(region, entry, recovery, hold);
                    PrefabUtility.SaveAsPrefabAsset(prefab, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(prefab); }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(contactMesh);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
        RebuildFootFinisher(region);
        AssetDatabase.SaveAssets();
    }

    // Fits the real boot surface against the floor without moving its joint or stretching the leg.
    private static void FitBootContact(GameObject root, Transform foot, Func<float> floorHeight, bool planted)
    {
        for (var iteration = 0; iteration < 5; iteration++)
        {
            var floor = floorHeight();
            var correction = root.transform.position.y - floor;
            if (Mathf.Abs(correction) < 0.00025f || (!planted && correction <= 0f)) return;
            var rotation = foot.rotation;
            foot.rotation = Quaternion.AngleAxis(1f, root.transform.right) * rotation;
            var slope = floorHeight() - floor;
            foot.rotation = rotation;
            if (Mathf.Abs(slope) < 0.00001f) return;
            foot.rotation = Quaternion.AngleAxis(Mathf.Clamp(correction / slope, -4f, 4f), root.transform.right) * rotation;
        }
    }

    // Bakes the transition from the injured foot pose to the grounded fatal pose.
    private static void RebuildFootFinisher(BodyRegion region)
    {
        var prone = ImportMotion("Standing Death Backward 01");
        foreach (var name in new[] { "EnglishArcher", "ErikaArcher" })
        {
            var path = $"Assets/_Game/Prefabs/Characters/{name}.prefab";
            var root = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));
            var mesh = new Mesh();
            try
            {
                var animator = root.GetComponentInChildren<Animator>();
                var bones = animator.GetComponentsInChildren<Transform>().Where(t => t != animator.transform).ToArray();
                var modelPosition = animator.transform.localPosition;
                var modelRotation = animator.transform.localRotation;
                var hips = animator.GetBoneTransform(HumanBodyBones.Hips);
                var entry = root.GetComponent<ArcherReactionAnimation>().Entry(region);
                entry.SampleAnimation(root, entry.length);
                var positions = bones.Select(t => t.localPosition).ToArray();
                var rotations = bones.Select(t => t.localRotation).ToArray();
                var start = hips.position;

                Sample(animator, prone, prone.length, modelPosition, modelRotation);
                hips.rotation = Quaternion.AngleAxis(180f, root.transform.up) * hips.rotation;
                hips.position = new Vector3(start.x, hips.position.y, start.z) + root.transform.forward * 0.2f;
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
                clip.name = region + " Death";
                clip = SaveClip(clip, $"Assets/_Game/Animations/Injuries/{name}/{region}_Death.anim");
                var prefab = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    prefab.GetComponent<ArcherReactionAnimation>().SetDeathFinisher(region, clip);
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
    }

    // Imports the prepared neutral reference and excludes frame zero from the complete original performance.
    private static AnimationClip ImportFootMotion()
    {
        const string path = "Assets/ThirdParty/CosindraAnimations/Derived/RightFootPain_Humanoid.fbx";
        var importer = AssetImporter.GetAtPath(path) as ModelImporter;
        if (importer == null) throw new InvalidOperationException("Run SourceAssets/CosindraAnimations/PrepareFoot.py first");
        importer.sourceAvatar = null;
        importer.animationType = ModelImporterAnimationType.Generic;
        importer.avatarSetup = ModelImporterAvatarSetup.NoAvatar;
        importer.clipAnimations = new[] { new ModelImporterClipAnimation
        {
            name = "Right Foot Pain", takeName = "Scene", firstFrame = 1f, lastFrame = 120f, loopTime = false
        } };
        importer.SaveAndReimport();
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        var mapping = new System.Collections.Generic.Dictionary<string, string>
        {
            { "Hips", "pelvis" }, { "Spine", "spine_01" }, { "Chest", "spine_03" },
            { "UpperChest", "spine_05" }, { "Neck", "neck_01" }, { "Head", "head" }
        };
        foreach (var side in new[] { "Left", "Right" })
        {
            var suffix = side == "Left" ? "_l" : "_r";
            var human = new[] { "Shoulder", "UpperArm", "LowerArm", "Hand", "UpperLeg", "LowerLeg", "Foot", "Toes" };
            var source = new[] { "clavicle", "upperarm", "lowerarm", "hand", "thigh", "calf", "foot", "ball" };
            for (var i = 0; i < human.Length; i++) mapping.Add(side + human[i], source[i] + suffix);
            var fingers = new[] { "Thumb", "Index", "Middle", "Ring", "Little" };
            var names = new[] { "thumb", "index", "middle", "ring", "pinky" };
            var joints = new[] { "Proximal", "Intermediate", "Distal" };
            for (var finger = 0; finger < fingers.Length; finger++)
            for (var joint = 0; joint < joints.Length; joint++)
                mapping.Add(side + fingers[finger] + joints[joint], names[finger] + "_" + (joint + 1).ToString("00") + suffix);
        }
        importer.animationType = ModelImporterAnimationType.Human;
        importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        importer.humanDescription = new HumanDescription
        {
            human = mapping.Select(pair => new HumanBone
            {
                humanName = pair.Key, boneName = pair.Value, limit = new HumanLimit { useDefaultValues = true }
            }).ToArray(),
            skeleton = model.GetComponentsInChildren<Transform>().Select(t => new SkeletonBone
            {
                name = t.name, position = t.localPosition, rotation = t.localRotation, scale = t.localScale
            }).ToArray(),
            armStretch = 0f, legStretch = 0f, upperArmTwist = 0.5f, lowerArmTwist = 0.5f,
            upperLegTwist = 0.5f, lowerLegTwist = 0.5f, hasTranslationDoF = false
        };
        importer.animationCompression = ModelImporterAnimationCompression.Off;
        importer.SaveAndReimport();
        var avatar = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Avatar>().Single();
        if (!avatar.isValid || !avatar.isHuman) throw new InvalidOperationException("Invalid foot-motion Humanoid reference");
        return AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Single(c => c.name == "Right Foot Pain");
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
