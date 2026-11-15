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

    // Authors separate foot clips using each model's own limb lengths and planted support foot.
    public static void RebuildFoot(BodyRegion region)
    {
        if (region != BodyRegion.LeftFoot && region != BodyRegion.RightFoot)
            throw new ArgumentException("A foot region is required.");
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
            FootPose(root, region == BodyRegion.LeftFoot, recovery ? 1f - progress : progress);
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
            if (!changed) continue;
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
