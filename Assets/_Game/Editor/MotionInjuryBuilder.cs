using System;
using System.Linq;
using BowMan;
using UnityEditor;
using UnityEngine;

public static class MotionInjuryBuilder
{
    private const string MotionFolder = "Assets/ThirdParty/MixamoAnimations/Motions/";

    // Bakes a recorded torso recoil with measured foot withdrawal, supported balance and a cautious landing.
    internal static void RebuildFoot(BodyRegion region)
    {
        var source = ImportMotion("Dying Backwards");
        var left = region == BodyRegion.LeftFoot;
        foreach (var name in new[] { "EnglishArcher", "ErikaArcher" })
        {
            var path = $"Assets/_Game/Prefabs/Characters/{name}.prefab";
            var root = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));
            try
            {
                var animator = root.GetComponentInChildren<Animator>();
                var bones = Enumerable.Range(0, (int)HumanBodyBones.LastBone)
                    .Select(i => animator.GetBoneTransform((HumanBodyBones)i)).Where(t => t != null).Distinct().ToArray();
                var positions = bones.Select(t => t.localPosition).ToArray();
                var rotations = bones.Select(t => t.localRotation).ToArray();
                var modelPosition = animator.transform.localPosition;
                var modelRotation = animator.transform.localRotation;
                var axis = root.transform;
                var hips = animator.GetBoneTransform(HumanBodyBones.Hips);
                var spine = animator.GetBoneTransform(HumanBodyBones.Spine);
                var head = animator.GetBoneTransform(HumanBodyBones.Head);
                var hipStart = hips.position;
                var feet = new[] { animator.GetBoneTransform(HumanBodyBones.LeftFoot), animator.GetBoneTransform(HumanBodyBones.RightFoot) };
                var footPositions = feet.Select(t => t.position).ToArray();
                var footRotations = feet.Select(t => t.rotation).ToArray();
                var hands = new[] { animator.GetBoneTransform(HumanBodyBones.LeftHand), animator.GetBoneTransform(HumanBodyBones.RightHand) };
                var handPositions = hands.Select(t => t.position).ToArray();
                Sample(animator, source, 0.45f, modelPosition, modelRotation);
                var motionStart = bones.Select(t => t.localRotation).ToArray();
                Restore(bones, positions, rotations);

                // Evaluates separate load, withdrawal and bracing channels without stretching the planted leg.
                void Pose(float load, float lift, float brace, float recoilTime, float balance)
                {
                    Restore(bones, positions, rotations);
                    var recoil = recoilTime <= 0f || recoilTime >= 0.72f ? 0f : Mathf.Pow(Mathf.Sin(Mathf.PI * recoilTime / 0.72f), 2f);
                    if (load == 0f && lift == 0f && brace == 0f && recoil == 0f && balance == 0f) return;
                    if (recoil > 0f)
                    {
                        Sample(animator, source, 0.45f + recoilTime / 0.72f * 0.35f, modelPosition, modelRotation);
                        var sampled = bones.Select(t => t.localRotation).ToArray();
                        Restore(bones, positions, rotations);
                        for (var i = 0; i < bones.Length; i++)
                            if (bones[i] == spine || bones[i].IsChildOf(spine))
                                bones[i].localRotation = Quaternion.Slerp(rotations[i],
                                    sampled[i] * Quaternion.Inverse(motionStart[i]) * rotations[i], recoil * 0.7f);
                    }
                    var healthy = footPositions[left ? 1 : 0];
                    hips.position = hipStart + axis.right * (Vector3.Dot(healthy - hipStart, axis.right) * 0.9f * load + balance * 0.003f)
                        - axis.up * (0.025f * load);
                    hips.rotation = Quaternion.AngleAxis((left ? -4f : 4f) * load, axis.forward) * hips.rotation;
                    spine.rotation = Quaternion.AngleAxis(18f * brace, axis.right) * spine.rotation;
                    head.rotation = Quaternion.AngleAxis(12f * brace, axis.right) * head.rotation;
                    var recoilWithdrawal = recoil * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.07f, 0.2f, recoilTime));
                    var withdrawal = axis.up * (0.16f * lift + 0.025f * recoilWithdrawal + balance * 0.002f)
                        + axis.forward * (0.06f * lift - 0.1f * recoilWithdrawal);
                    for (var i = 0; i < 2; i++)
                    {
                        var injured = i == (left ? 0 : 1);
                        ArcherLimbIK.Solve(animator, i == 0 ? HumanBodyBones.LeftUpperLeg : HumanBodyBones.RightUpperLeg,
                            i == 0 ? HumanBodyBones.LeftLowerLeg : HumanBodyBones.RightLowerLeg,
                            i == 0 ? HumanBodyBones.LeftFoot : HumanBodyBones.RightFoot,
                            footPositions[i] + (injured ? withdrawal : Vector3.zero), axis.forward);
                        feet[i].rotation = (injured ? Quaternion.AngleAxis(-12f * lift, axis.right) : Quaternion.identity) * footRotations[i];
                    }
                    var knee = animator.GetBoneTransform(left ? HumanBodyBones.LeftLowerLeg : HumanBodyBones.RightLowerLeg);
                    for (var i = 0; i < 2; i++)
                    {
                        var injured = i == (left ? 0 : 1);
                        var target = injured ? knee.position + axis.up * 0.11f + axis.forward * 0.08f
                            : handPositions[i] + axis.right * (i == 0 ? -0.07f : 0.07f) + axis.forward * 0.08f;
                        ArcherLimbIK.Solve(animator, i == 0 ? HumanBodyBones.LeftUpperArm : HumanBodyBones.RightUpperArm,
                            i == 0 ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm,
                            i == 0 ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand,
                            Vector3.Lerp(hands[i].position, target, brace), axis.right * (i == 0 ? -1f : 1f) - axis.up * 0.35f);
                        if (injured) ArcherLimbIK.PointHand(animator, i == 0, -axis.up + axis.forward * 0.25f, brace);
                    }
                }

                // Eases one physical stage independently of the other body channels.
                float Ease(float time, float start, float end) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(start, end, time));

                var entry = Record(root, bones, 0.9f, time =>
                    Pose(Ease(time, 0f, 0.38f), Ease(time, 0.07f, 0.42f), Ease(time, 0.1f, 0.72f), time, 0f));
                var hold = Record(root, bones, 3f, time =>
                    Pose(1f, 1f, 1f, 1f, Mathf.Sin(time / 3f * Mathf.PI * 2f) * Mathf.Pow(Mathf.Sin(time / 3f * Mathf.PI), 2f)));
                var recovery = Record(root, bones, 0.9f, time =>
                    Pose(1f - Ease(time, 0.5f, 0.9f), 1f - Ease(time, 0f, 0.6f), 1f - Ease(time, 0.3f, 0.9f), 1f, 0f));
                entry.name = region + " Entry";
                hold.name = region + " Balance";
                recovery.name = region + " Recovery";
                var folder = $"Assets/_Game/Animations/Injuries/{name}";
                entry = InjuryAnimationBuilder.SaveClip(entry, $"{folder}/{region}_Entry.anim");
                hold = InjuryAnimationBuilder.SaveClip(hold, $"{folder}/{region}_Hold.anim");
                recovery = InjuryAnimationBuilder.SaveClip(recovery, $"{folder}/{region}_Recovery.anim");
                var prefab = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    prefab.GetComponent<ArcherReactionAnimation>().SetSequence(region, entry, recovery, hold);
                    PrefabUtility.SaveAsPrefabAsset(prefab, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(prefab); }
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
        RebuildDeathFinishers(region);
        AssetDatabase.SaveAssets();
    }

    // Bakes each body region's own transition from its injured pose into a complete fatal collapse.
    [MenuItem("Bow Man VR/Injuries/Rebuild Body Death Finishers")]
    public static void RebuildDeathFinishers() => RebuildDeathFinishers(null);

    // Replaces only the requested region's collapse, or all body collapses for a complete rebuild.
    private static void RebuildDeathFinishers(BodyRegion? onlyRegion)
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
                var neutralPositions = bones.Select(t => t.localPosition).ToArray();
                var neutralRotations = bones.Select(t => t.localRotation).ToArray();
                var modelPosition = animator.transform.localPosition;
                var modelRotation = animator.transform.localRotation;
                var hips = animator.GetBoneTransform(HumanBodyBones.Hips);
                var player = root.GetComponent<ArcherReactionAnimation>();
                var clips = new AnimationClip[15];
                foreach (BodyRegion region in Enum.GetValues(typeof(BodyRegion)))
                {
                    if (onlyRegion.HasValue && region != onlyRegion.Value) continue;
                    if (region is BodyRegion.Head or BodyRegion.Neck) continue;
                    Restore(bones, neutralPositions, neutralRotations);
                    var entry = player.Entry(region);
                    if (entry == null) throw new InvalidOperationException("Missing injury entry for " + region);
                    entry.SampleAnimation(root, entry.length);
                    var positions = bones.Select(t => t.localPosition).ToArray();
                    var rotations = bones.Select(t => t.localRotation).ToArray();
                    var start = hips.position;
                    if (region is BodyRegion.Chest or BodyRegion.Pelvis)
                    {
                        var death = player.Entry(BodyRegion.Head);
                        death.SampleAnimation(root, death.length);
                        hips.position = new Vector3(start.x, hips.position.y, start.z) -
                            root.transform.forward * (region == BodyRegion.Chest ? 0f : 0.2f);
                    }
                    else
                    {
                        Sample(animator, prone, prone.length, modelPosition, modelRotation);
                        hips.rotation = Quaternion.AngleAxis(180f, root.transform.up) * hips.rotation;
                        hips.position = new Vector3(start.x, hips.position.y, start.z) + root.transform.forward * 0.2f;
                    }
                    Ground(root, hips, mesh);
                    var targetPositions = bones.Select(t => t.localPosition).ToArray();
                    var targetRotations = bones.Select(t => t.localRotation).ToArray();
                    var clip = Record(root, bones, 0.8f, time =>
                    {
                        var progress = Mathf.SmoothStep(0f, 1f, time / 0.8f);
                        for (var i = 0; i < bones.Length; i++)
                        {
                            bones[i].localPosition = Vector3.Lerp(positions[i], targetPositions[i], progress);
                            bones[i].localRotation = Quaternion.Slerp(rotations[i], targetRotations[i], progress);
                        }
                        if (time > 0f) Ground(root, hips, mesh);
                    });
                    clip.name = region + " Death";
                    clips[(int)region] = InjuryAnimationBuilder.SaveClip(clip,
                        $"Assets/_Game/Animations/Injuries/{name}/{region}_Death.anim");
                }
                var prefab = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    player = prefab.GetComponent<ArcherReactionAnimation>();
                    foreach (BodyRegion region in Enum.GetValues(typeof(BodyRegion)))
                        if (clips[(int)region] != null) player.SetDeathFinisher(region, clips[(int)region]);
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
                        Ground(root, hips, mesh);
                    });
                }
                finally { UnityEngine.Object.DestroyImmediate(mesh); }
                clip.name = "Chest Recovery";
                clip = InjuryAnimationBuilder.SaveClip(clip, $"Assets/_Game/Animations/Injuries/{name}/Chest_Recovery.anim");
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

    // Retargets the chest fall onto each owned skeleton and preserves its other injury clips.
    [MenuItem("Bow Man VR/Injuries/Rebuild Chest Fall")]
    public static void RebuildChestFall() => RebuildFall(BodyRegion.Chest, 0.5f, 2.7f, 1f);

    // Authors a rapid loss of posture and relaxed head separately from the surviving chest sequence.
    [MenuItem("Bow Man VR/Injuries/Rebuild Head Collapse")]
    public static void RebuildHeadCollapse() => RebuildFall(BodyRegion.Head, 0.8f, 1f, 2.5f);

    // Authors a wound reach followed by buckling knees and a fatal collapse, without a recovery clip.
    [MenuItem("Bow Man VR/Injuries/Rebuild Neck Collapse")]
    public static void RebuildNeckCollapse() => RebuildFall(BodyRegion.Neck, 0.8f, 2.4f, 1.3f);

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
                        var motionTime = region == BodyRegion.Neck ? Mathf.Max(0f, time - 0.55f) : time;
                        Sample(animator, source, sourceStart + motionTime * speed, modelPosition, modelRotation);
                        hips.position += offset;
                        var blend = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(time / (region == BodyRegion.Head ? 0.06f : 0.18f)));
                        for (var i = 0; i < bones.Length; i++)
                        {
                            bones[i].localPosition = Vector3.Lerp(positions[i], bones[i].localPosition, blend);
                            bones[i].localRotation = Quaternion.Slerp(rotations[i], bones[i].localRotation, blend);
                        }
                        if (region == BodyRegion.Head)
                            animator.GetBoneTransform(HumanBodyBones.Head).localRotation *= Quaternion.Euler(12f * blend, 0f, 35f * blend);
                        if (region == BodyRegion.Neck)
                        {
                            var neck = animator.GetBoneTransform(HumanBodyBones.Neck);
                            ArcherLimbIK.ReachNeck(animator, neck.position + root.transform.forward * 0.08f,
                                root.transform.forward, ArcherLimbIK.NeckReachWeight(time));
                        }
                        Ground(root, hips, mesh);
                    });
                }
                finally { UnityEngine.Object.DestroyImmediate(mesh); }
                clip.name = region + " Entry";
                var folder = $"Assets/_Game/Animations/Injuries/{name}";
                clip = InjuryAnimationBuilder.SaveClip(clip, $"{folder}/{region}_Entry.anim");
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

    // Raises only penetrating samples against the visible skin, including the model's attached equipment.
    private static void Ground(GameObject root, Transform hips, Mesh mesh)
    {
        var floor = float.PositiveInfinity;
        foreach (var skin in root.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            skin.BakeMesh(mesh, true);
            foreach (var vertex in mesh.vertices) floor = Mathf.Min(floor, skin.transform.TransformPoint(vertex).y);
            mesh.Clear();
        }
        if (floor < root.transform.position.y) hips.position += Vector3.up * (root.transform.position.y - floor);
    }

    // Bakes complete local position/quaternion groups at thirty samples per second on the actual target hierarchy.
    private static AnimationClip Record(GameObject root, Transform[] bones, float duration, Action<float> pose)
    {
        var clip = new AnimationClip { frameRate = 30f };
        var curves = bones.Select(_ => Enumerable.Range(0, 7).Select(i => new AnimationCurve()).ToArray()).ToArray();
        var frames = Mathf.RoundToInt(duration * 30f);
        for (var frame = 0; frame <= frames; frame++)
        {
            var time = frame / 30f;
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
            clip.SetCurve(AnimationUtility.CalculateTransformPath(bones[i], root.transform), typeof(Transform), properties[c], curves[i][c]);
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
}
