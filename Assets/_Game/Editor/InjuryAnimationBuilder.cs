using System;
using System.Collections.Generic;
using System.Linq;
using BowMan;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class InjuryAnimationBuilder
{
    // Rebuilds the left-foot entry/recovery on both prepared character prefabs.
    [MenuItem("Bow Man VR/Injuries/Rebuild Left Foot")]
    public static void RebuildLeftFoot() => RebuildFoot(BodyRegion.LeftFoot);

    // Rebuilds the independent right-foot entry/recovery with the left leg providing support.
    [MenuItem("Bow Man VR/Injuries/Rebuild Right Foot")]
    public static void RebuildRightFoot() => RebuildFoot(BodyRegion.RightFoot);

    // Rebuilds the left-arm recoil and opposite-hand support on both prepared characters.
    [MenuItem("Bow Man VR/Injuries/Rebuild Left Arm")]
    public static void RebuildLeftArm() => RebuildArm(BodyRegion.LeftArm);

    // Rebuilds the independent right-arm recoil with support from the left hand.
    [MenuItem("Bow Man VR/Injuries/Rebuild Right Arm")]
    public static void RebuildRightArm() => RebuildArm(BodyRegion.RightArm);

    // Rebuilds a left-shoulder recoil with the right palm cupping the shoulder cap.
    [MenuItem("Bow Man VR/Injuries/Rebuild Left Shoulder")]
    public static void RebuildLeftShoulder() => RebuildShoulder(BodyRegion.LeftShoulder);

    // Rebuilds the right-shoulder recoil with left-palm support in front of the chest.
    [MenuItem("Bow Man VR/Injuries/Rebuild Right Shoulder")]
    public static void RebuildRightShoulder() => RebuildShoulder(BodyRegion.RightShoulder);

    // Rebuilds the left-hand recoil with open fingers and right-hand wrist support.
    [MenuItem("Bow Man VR/Injuries/Rebuild Left Hand")]
    public static void RebuildLeftHand() => RebuildHand(BodyRegion.LeftHand);

    // Rebuilds the independent right-hand recoil with open fingers and left-hand wrist support.
    [MenuItem("Bow Man VR/Injuries/Rebuild Right Hand")]
    public static void RebuildRightHand() => RebuildHand(BodyRegion.RightHand);

    // Authors hand-specific clips independently of arm and shoulder motions.
    public static void RebuildHand(BodyRegion region)
    {
        if (region != BodyRegion.LeftHand && region != BodyRegion.RightHand)
            throw new ArgumentException("A hand region is required.");
        Rebuild(region);
    }

    // Authors shoulder support independently of the lower arm and wrist reactions.
    public static void RebuildShoulder(BodyRegion region)
    {
        if (region != BodyRegion.LeftShoulder && region != BodyRegion.RightShoulder)
            throw new ArgumentException("A shoulder region is required.");
        Rebuild(region);
    }

    // Authors an arm-specific recoil and cradle while preserving independent shoulders and hands.
    public static void RebuildArm(BodyRegion region)
    {
        if (region != BodyRegion.LeftArm && region != BodyRegion.RightArm)
            throw new ArgumentException("An arm region is required.");
        Rebuild(region);
    }

    // Authors separate foot clips using each model's own limb lengths and planted support foot.
    public static void RebuildFoot(BodyRegion region)
    {
        if (region != BodyRegion.LeftFoot && region != BodyRegion.RightFoot)
            throw new ArgumentException("A foot region is required.");
        Rebuild(region);
    }

    // Replaces only the requested region's per-model clip pair and retains every other configured sequence.
    private static void Rebuild(BodyRegion region)
    {
        foreach (var name in new[] { "EnglishArcher", "ErikaArcher" })
        {
            var path = $"Assets/_Game/Prefabs/Characters/{name}.prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var animator = root.GetComponentInChildren<Animator>();
                var bones = Enumerable.Range(0, (int)HumanBodyBones.LastBone)
                    .Select(i => animator.GetBoneTransform((HumanBodyBones)i)).Where(t => t != null).Distinct().ToArray();
                var positions = bones.Select(t => t.localPosition).ToArray();
                var rotations = bones.Select(t => t.localRotation).ToArray();
                var folder = $"Assets/_Game/Animations/Injuries/{name}";
                System.IO.Directory.CreateDirectory(folder);
                var entry = BuildClip(root, bones, positions, rotations, region, false);
                var recovery = BuildClip(root, bones, positions, rotations, region, true);
                entry = SaveClip(entry, $"{folder}/{region}_Entry.anim");
                recovery = SaveClip(recovery, $"{folder}/{region}_Recovery.anim");
                Restore(bones, positions, rotations);
                var player = root.GetComponent<ArcherReactionAnimation>();
                if (player == null) player = root.AddComponent<ArcherReactionAnimation>();
                player.SetSequence(region, entry, recovery);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        AssetDatabase.Refresh();
    }

    // Records eased entry or return curves for changed bone channels at thirty samples per second.
    private static AnimationClip BuildClip(GameObject root, Transform[] bones, Vector3[] positions,
        Quaternion[] rotations, BodyRegion region, bool recovery)
    {
        var clip = new AnimationClip { name = region + (recovery ? " Recovery" : " Entry"), frameRate = 30f };
        var curves = bones.Select(_ => Enumerable.Range(0, 7).Select(i => new AnimationCurve()).ToArray()).ToArray();
        const float duration = 0.9f;
        for (var sample = 0; sample <= 27; sample++)
        {
            Restore(bones, positions, rotations);
            var progress = Mathf.SmoothStep(0f, 1f, sample / 27f);
            ApplyPose(root, region, recovery ? 1f - progress : progress);
            for (var i = 0; i < bones.Length; i++)
            {
                var p = bones[i].localPosition;
                var q = bones[i].localRotation;
                var values = new[] { p.x, p.y, p.z, q.x, q.y, q.z, q.w };
                for (var c = 0; c < 7; c++) curves[i][c].AddKey(sample / 30f, values[c]);
            }
        }
        var properties = new[] { "localPosition.x", "localPosition.y", "localPosition.z",
            "localRotation.x", "localRotation.y", "localRotation.z", "localRotation.w" };
        for (var i = 0; i < bones.Length; i++)
        for (var group = 0; group < 2; group++)
        {
            var start = group == 0 ? 0 : 3;
            var count = group == 0 ? 3 : 4;
            var changed = Enumerable.Range(start, count).Any(c => curves[i][c].keys.Any(k =>
                Mathf.Abs(k.value - curves[i][c].keys[0].value) >= 0.000001f));
            var openFingers = group == 1 && (region == BodyRegion.LeftHand || region == BodyRegion.RightHand) &&
                bones[i].name.Contains(region == BodyRegion.LeftHand ? "LeftHand" : "RightHand");
            if (!changed && !openFingers) continue;
            for (var c = start; c < start + count; c++)
                clip.SetCurve(AnimationUtility.CalculateTransformPath(bones[i], root.transform), typeof(Transform), properties[c], curves[i][c]);
        }
        clip.EnsureQuaternionContinuity();
        if (Mathf.Abs(clip.length - duration) > 0.001f) throw new InvalidOperationException("Injury duration was not baked.");
        return clip;
    }

    // Restores the exact neutral bone values before independently evaluating the next authored sample.
    private static void Restore(Transform[] bones, Vector3[] positions, Quaternion[] rotations)
    {
        for (var i = 0; i < bones.Length; i++)
        {
            bones[i].localPosition = positions[i];
            bones[i].localRotation = rotations[i];
        }
    }

    // Shifts weight over the healthy leg while lifting the injured foot and opening the arms for balance.
    private static void FootPose(GameObject root, bool left, float progress)
    {
        var a = root.GetComponentInChildren<Animator>();
        var hips = a.GetBoneTransform(HumanBodyBones.Hips);
        var leftFoot = a.GetBoneTransform(HumanBodyBones.LeftFoot);
        var rightFoot = a.GetBoneTransform(HumanBodyBones.RightFoot);
        var leftTarget = leftFoot.position;
        var rightTarget = rightFoot.position;
        var leftRotation = leftFoot.rotation;
        var rightRotation = rightFoot.rotation;
        var healthy = left ? rightTarget : leftTarget;
        hips.position += root.transform.right * ((healthy.x - hips.position.x) * 0.75f * progress)
            - root.transform.up * (0.04f * progress);
        var lifted = root.transform.up * (0.14f * progress) + root.transform.forward * (0.04f * progress);
        Solve(a, HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot,
            leftTarget + (left ? lifted : Vector3.zero), root.transform.forward);
        Solve(a, HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot,
            rightTarget + (!left ? lifted : Vector3.zero), root.transform.forward);
        leftFoot.rotation = leftRotation;
        rightFoot.rotation = rightRotation;
        var armLeft = a.GetBoneTransform(HumanBodyBones.LeftUpperArm);
        var armRight = a.GetBoneTransform(HumanBodyBones.RightUpperArm);
        armLeft.rotation = Quaternion.AngleAxis(-20f * progress, root.transform.forward) * armLeft.rotation;
        armRight.rotation = Quaternion.AngleAxis(20f * progress, root.transform.forward) * armRight.rotation;
    }

    // Dispatches to the exact region's authoring motion instead of substituting a generic body reaction.
    private static void ApplyPose(GameObject root, BodyRegion region, float progress)
    {
        switch (region)
        {
            case BodyRegion.LeftFoot: case BodyRegion.RightFoot:
                FootPose(root, region == BodyRegion.LeftFoot, progress);
                break;
            case BodyRegion.LeftArm: case BodyRegion.RightArm:
                ArmPose(root, region == BodyRegion.LeftArm, progress);
                break;
            case BodyRegion.LeftShoulder: case BodyRegion.RightShoulder:
                ShoulderPose(root, region == BodyRegion.LeftShoulder, progress);
                break;
            case BodyRegion.LeftHand: case BodyRegion.RightHand:
                HandPose(root, region == BodyRegion.LeftHand, progress);
                break;
            default: throw new ArgumentException("This region has no authored motion.");
        }
    }

    // Recoils the injured side and bends its arm into the opposite hand's support while both feet stay planted.
    private static void ArmPose(GameObject root, bool left, float progress)
    {
        var a = root.GetComponentInChildren<Animator>();
        var hips = a.GetBoneTransform(HumanBodyBones.Hips);
        var chest = a.GetBoneTransform(HumanBodyBones.UpperChest);
        chest.rotation = Quaternion.AngleAxis((left ? 12f : -12f) * progress, root.transform.up) * chest.rotation;
        var injuredHand = a.GetBoneTransform(left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
        var healthyHand = a.GetBoneTransform(left ? HumanBodyBones.RightHand : HumanBodyBones.LeftHand);
        var target = hips.position + root.transform.up * 0.25f + root.transform.forward * 0.23f
            + root.transform.right * (left ? -0.06f : 0.06f);
        var handTarget = Vector3.Lerp(injuredHand.position, target, progress);
        Solve(a, left ? HumanBodyBones.LeftUpperArm : HumanBodyBones.RightUpperArm,
            left ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm,
            left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand,
            handTarget, root.transform.right * (left ? -1f : 1f) - root.transform.up * 0.3f);
        PointHand(a, left, root.transform.right * (left ? 1f : -1f) + root.transform.up * 0.15f, progress);
        var elbow = a.GetBoneTransform(left ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm);
        var support = Vector3.Lerp(elbow.position, injuredHand.position, 0.65f) - root.transform.up * 0.03f;
        Solve(a, left ? HumanBodyBones.RightUpperArm : HumanBodyBones.LeftUpperArm,
            left ? HumanBodyBones.RightLowerArm : HumanBodyBones.LeftLowerArm,
            left ? HumanBodyBones.RightHand : HumanBodyBones.LeftHand,
            Vector3.Lerp(healthyHand.position, support, progress),
            root.transform.right * (left ? 1f : -1f) - root.transform.up * 0.4f);
        PointHand(a, !left, elbow.position - healthyHand.position, progress);
    }

    // Aims the open fingers toward the contact direction without translating the supported wrist.
    private static void PointHand(Animator animator, bool left, Vector3 direction, float progress)
    {
        var hand = animator.GetBoneTransform(left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
        var finger = animator.GetBoneTransform(left ? HumanBodyBones.LeftMiddleDistal : HumanBodyBones.RightMiddleDistal);
        var rotation = Quaternion.FromToRotation(finger.position - hand.position, direction) * hand.rotation;
        hand.rotation = Quaternion.Slerp(hand.rotation, rotation, progress);
    }

    // Turns away from the injured shoulder and places the opposite palm over its cap while the injured arm recoils.
    private static void ShoulderPose(GameObject root, bool left, float progress)
    {
        var a = root.GetComponentInChildren<Animator>();
        var hips = a.GetBoneTransform(HumanBodyBones.Hips);
        var chest = a.GetBoneTransform(HumanBodyBones.UpperChest);
        chest.rotation = Quaternion.AngleAxis((left ? 20f : -20f) * progress, root.transform.up) * chest.rotation;
        var healthyShoulder = a.GetBoneTransform(left ? HumanBodyBones.RightShoulder : HumanBodyBones.LeftShoulder);
        healthyShoulder.rotation = Quaternion.AngleAxis((left ? -40f : 42f) * progress, root.transform.up) * healthyShoulder.rotation;
        var injuredHand = a.GetBoneTransform(left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
        var healthyHand = a.GetBoneTransform(left ? HumanBodyBones.RightHand : HumanBodyBones.LeftHand);
        var injuredTarget = hips.position + root.transform.up * 0.12f + root.transform.forward * 0.13f
            + root.transform.right * (left ? -0.18f : 0.18f);
        Solve(a, left ? HumanBodyBones.LeftUpperArm : HumanBodyBones.RightUpperArm,
            left ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm,
            left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand,
            Vector3.Lerp(injuredHand.position, injuredTarget, progress), root.transform.right * (left ? -1f : 1f));
        var shoulder = a.GetBoneTransform(left ? HumanBodyBones.LeftUpperArm : HumanBodyBones.RightUpperArm);
        var support = shoulder.position + root.transform.up * 0.10f + root.transform.forward * 0.11f;
        Solve(a, left ? HumanBodyBones.RightUpperArm : HumanBodyBones.LeftUpperArm,
            left ? HumanBodyBones.RightLowerArm : HumanBodyBones.LeftLowerArm,
            left ? HumanBodyBones.RightHand : HumanBodyBones.LeftHand,
            Vector3.Lerp(healthyHand.position, support, progress), root.transform.forward - root.transform.up * 0.5f);
        PointHand(a, !left, shoulder.position + root.transform.forward * 0.055f - healthyHand.position, progress);
    }

    // Jerks the injured hand back with open fingers while the opposite palm supports its wrist.
    private static void HandPose(GameObject root, bool left, float progress)
    {
        var a = root.GetComponentInChildren<Animator>();
        var hips = a.GetBoneTransform(HumanBodyBones.Hips);
        var chest = a.GetBoneTransform(HumanBodyBones.UpperChest);
        chest.rotation = Quaternion.AngleAxis((left ? 8f : -8f) * progress, root.transform.up) * chest.rotation;
        var injured = a.GetBoneTransform(left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
        var healthy = a.GetBoneTransform(left ? HumanBodyBones.RightHand : HumanBodyBones.LeftHand);
        var target = hips.position + root.transform.up * 0.38f + root.transform.forward * 0.20f
            + root.transform.right * (left ? -0.08f : 0.08f);
        Solve(a, left ? HumanBodyBones.LeftUpperArm : HumanBodyBones.RightUpperArm,
            left ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm,
            left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand,
            Vector3.Lerp(injured.position, target, progress), root.transform.right * (left ? -1f : 1f) + root.transform.forward * 0.3f);
        PointHand(a, left, root.transform.up + root.transform.forward * 0.1f, progress);
        var support = injured.position - root.transform.up * 0.06f - root.transform.forward * 0.015f;
        Solve(a, left ? HumanBodyBones.RightUpperArm : HumanBodyBones.LeftUpperArm,
            left ? HumanBodyBones.RightLowerArm : HumanBodyBones.LeftLowerArm,
            left ? HumanBodyBones.RightHand : HumanBodyBones.LeftHand,
            Vector3.Lerp(healthy.position, support, progress), root.transform.forward - root.transform.up * 0.4f);
        var elbow = a.GetBoneTransform(left ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm);
        PointHand(a, !left, elbow.position - healthy.position, progress);
    }

    // Places a two-bone chain using its measured lengths and a projected bend hint, without changing bone lengths.
    private static void Solve(Animator animator, HumanBodyBones upperId, HumanBodyBones lowerId,
        HumanBodyBones endId, Vector3 target, Vector3 hint)
    {
        var upper = animator.GetBoneTransform(upperId);
        var lower = animator.GetBoneTransform(lowerId);
        var end = animator.GetBoneTransform(endId);
        var a = Vector3.Distance(upper.position, lower.position);
        var b = Vector3.Distance(lower.position, end.position);
        var direction = (target - upper.position).normalized;
        var distance = Mathf.Clamp(Vector3.Distance(upper.position, target), Mathf.Abs(a - b) + 0.00001f, a + b - 0.00001f);
        var along = (a * a - b * b + distance * distance) / (2f * distance);
        var bend = Vector3.ProjectOnPlane(hint, direction).normalized;
        var joint = upper.position + direction * along + bend * Mathf.Sqrt(Mathf.Max(0f, a * a - along * along));
        upper.rotation = Quaternion.FromToRotation(lower.position - upper.position, joint - upper.position) * upper.rotation;
        lower.rotation = Quaternion.FromToRotation(end.position - lower.position, target - lower.position) * lower.rotation;
    }

    // Replaces clip contents without changing existing asset GUIDs when authoring is rerun.
    private static AnimationClip SaveClip(AnimationClip clip, string path)
    {
        var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (existing == null) { AssetDatabase.CreateAsset(clip, path); return clip; }
        EditorUtility.CopySerialized(clip, existing);
        Object.DestroyImmediate(clip);
        EditorUtility.SetDirty(existing);
        AssetDatabase.SaveAssets();
        return existing;
    }
}
