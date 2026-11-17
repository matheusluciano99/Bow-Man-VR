using System;
using System.Collections.Generic;
using System.Linq;
using BowMan;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;
using static BowMan.ArcherLimbIK;

public static class InjuryAnimationBuilder
{
    // Rebuilds the pelvis-specific seated fall with both palms supporting the body from behind.
    [MenuItem("Bow Man VR/Injuries/Rebuild Pelvis")]
    public static void RebuildPelvis() => Rebuild(BodyRegion.Pelvis);

    // Rebuilds the abdomen-specific wound brace, both-knee descent and standing recovery.
    [MenuItem("Bow Man VR/Injuries/Rebuild Abdomen")]
    public static void RebuildAbdomen() => Rebuild(BodyRegion.Abdomen);

    // Rebuilds a left-knee injury with left-palm ground support and a standing recovery.
    [MenuItem("Bow Man VR/Injuries/Rebuild Left Leg")]
    public static void RebuildLeftLeg() => RebuildLeg(BodyRegion.LeftLeg);

    // Rebuilds the independent right-knee reaction with right-palm support and the left foot planted.
    [MenuItem("Bow Man VR/Injuries/Rebuild Right Leg")]
    public static void RebuildRightLeg() => RebuildLeg(BodyRegion.RightLeg);

    // Fits the injured knee and same-side support hand independently of the foot-lift reactions.
    public static void RebuildLeg(BodyRegion region)
    {
        if (region != BodyRegion.LeftLeg && region != BodyRegion.RightLeg)
            throw new ArgumentException("A leg region is required.");
        Rebuild(region);
    }

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
        MotionInjuryBuilder.RebuildFoot(region);
    }

    // Replaces only the requested region's per-model clip pair and retains every other configured sequence.
    private static void Rebuild(BodyRegion region)
    {
        foreach (var name in new[] { "EnglishArcher", "ErikaArcher" })
        {
            var path = $"Assets/_Game/Prefabs/Characters/{name}.prefab";
            var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));
            try
            {
                var animator = root.GetComponentInChildren<Animator>();
                var bones = Enumerable.Range(0, (int)HumanBodyBones.LastBone)
                    .Select(i => animator.GetBoneTransform((HumanBodyBones)i)).Where(t => t != null).Distinct().ToArray();
                var positions = bones.Select(t => t.localPosition).ToArray();
                var rotations = bones.Select(t => t.localRotation).ToArray();
                var folder = $"Assets/_Game/Animations/Injuries/{name}";
                System.IO.Directory.CreateDirectory(folder);
                var kneeHeights = Vector2.one * 0.08f;
                var seatHeight = 0.18f;
                if (region is BodyRegion.LeftLeg or BodyRegion.RightLeg)
                {
                    var side = region == BodyRegion.LeftLeg ? 0 : 1;
                    for (var fit = 0; fit < 3; fit++)
                    {
                        Restore(bones, positions, rotations);
                        LegPose(root, region == BodyRegion.LeftLeg, 1f, kneeHeights[side]);
                        var knee = animator.GetBoneTransform(region == BodyRegion.LeftLeg ? HumanBodyBones.LeftLowerLeg : HumanBodyBones.RightLowerLeg);
                        kneeHeights[side] -= NearbyFloor(root, knee.position) - root.transform.position.y;
                    }
                }
                if (region == BodyRegion.Abdomen)
                {
                    for (var fit = 0; fit < 3; fit++)
                    {
                        Restore(bones, positions, rotations);
                        AbdomenPose(root, 1f, kneeHeights);
                        kneeHeights.x -= NearbyFloor(root, animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg).position) - root.transform.position.y;
                        kneeHeights.y -= NearbyFloor(root, animator.GetBoneTransform(HumanBodyBones.RightLowerLeg).position) - root.transform.position.y;
                    }
                }
                if (region == BodyRegion.Pelvis)
                {
                    for (var fit = 0; fit < 3; fit++)
                    {
                        Restore(bones, positions, rotations);
                        PelvisPose(root, 1f, seatHeight);
                        seatHeight -= NearbyFloor(root, animator.GetBoneTransform(HumanBodyBones.Hips).position, true) - root.transform.position.y;
                    }
                }
                var entry = BuildClip(root, bones, positions, rotations, region, false, kneeHeights, seatHeight);
                var recovery = BuildClip(root, bones, positions, rotations, region, true, kneeHeights, seatHeight);
                entry = SaveClip(entry, $"{folder}/{region}_Entry.anim");
                recovery = SaveClip(recovery, $"{folder}/{region}_Recovery.anim");
                Restore(bones, positions, rotations);
                var prefab = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var player = prefab.GetComponent<ArcherReactionAnimation>();
                    if (player == null) player = prefab.AddComponent<ArcherReactionAnimation>();
                    player.SetSequence(region, entry, recovery);
                    PrefabUtility.SaveAsPrefabAsset(prefab, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(prefab); }
            }
            finally { Object.DestroyImmediate(root); }
        }
        AssetDatabase.Refresh();
    }

    // Records eased entry or return curves for changed bone channels at thirty samples per second.
    private static AnimationClip BuildClip(GameObject root, Transform[] bones, Vector3[] positions,
        Quaternion[] rotations, BodyRegion region, bool recovery, Vector2 kneeHeights, float seatHeight)
    {
        var clip = new AnimationClip { name = region + (recovery ? " Recovery" : " Entry"), frameRate = 30f };
        var curves = bones.Select(_ => Enumerable.Range(0, 7).Select(i => new AnimationCurve()).ToArray()).ToArray();
        const float duration = 0.9f;
        for (var sample = 0; sample <= 27; sample++)
        {
            Restore(bones, positions, rotations);
            var progress = Mathf.SmoothStep(0f, 1f, sample / 27f);
            var amount = recovery ? 1f - progress : progress;
            if (amount > 0f) ApplyPose(root, region, amount, kneeHeights, seatHeight);
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

    // Dispatches to the exact region's authoring motion instead of substituting a generic body reaction.
    private static void ApplyPose(GameObject root, BodyRegion region, float progress, Vector2 kneeHeights, float seatHeight)
    {
        switch (region)
        {
            case BodyRegion.Pelvis:
                PelvisPose(root, progress, seatHeight);
                break;
            case BodyRegion.Abdomen:
                AbdomenPose(root, progress, kneeHeights);
                break;
            case BodyRegion.LeftLeg: case BodyRegion.RightLeg:
                LegPose(root, region == BodyRegion.LeftLeg, progress, kneeHeights[region == BodyRegion.LeftLeg ? 0 : 1]);
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

    // Buckles into a reclined seated pose with raised knees and palms planted behind the hips.
    private static void PelvisPose(GameObject root, float progress, float seatHeight)
    {
        var a = root.GetComponentInChildren<Animator>();
        var hips = a.GetBoneTransform(HumanBodyBones.Hips);
        var feet = new[] { a.GetBoneTransform(HumanBodyBones.LeftFoot), a.GetBoneTransform(HumanBodyBones.RightFoot) };
        var positions = feet.Select(t => t.position).ToArray();
        var rotations = feet.Select(t => t.rotation).ToArray();
        hips.position += root.transform.up * ((root.transform.position.y + seatHeight - hips.position.y) * progress)
            - root.transform.forward * (0.15f * progress);
        hips.rotation = Quaternion.AngleAxis(-45f * progress, root.transform.right) * hips.rotation;
        for (var i = 0; i < 2; i++)
        {
            var target = positions[i] + root.transform.forward * (0.45f * progress) +
                root.transform.up * (0.012f * progress + 0.08f * Mathf.Sin(progress * Mathf.PI));
            Solve(a, i == 0 ? HumanBodyBones.LeftUpperLeg : HumanBodyBones.RightUpperLeg,
                i == 0 ? HumanBodyBones.LeftLowerLeg : HumanBodyBones.RightLowerLeg,
                i == 0 ? HumanBodyBones.LeftFoot : HumanBodyBones.RightFoot, target, root.transform.forward);
            feet[i].rotation = Quaternion.AngleAxis(-8f * progress, root.transform.right) * rotations[i];
        }
        GroundPalm(root, a, true, progress, -0.08f);
        GroundPalm(root, a, false, progress, -0.08f);
    }

    // Folds over the abdominal wound while placing both visible knees on their fitted support surfaces.
    private static void AbdomenPose(GameObject root, float progress, Vector2 kneeHeights)
    {
        var a = root.GetComponentInChildren<Animator>();
        var hips = a.GetBoneTransform(HumanBodyBones.Hips);
        var hipStart = hips.position;
        var thighs = new[] { a.GetBoneTransform(HumanBodyBones.LeftUpperLeg), a.GetBoneTransform(HumanBodyBones.RightUpperLeg) };
        var knees = new[] { a.GetBoneTransform(HumanBodyBones.LeftLowerLeg), a.GetBoneTransform(HumanBodyBones.RightLowerLeg) };
        var feet = new[] { a.GetBoneTransform(HumanBodyBones.LeftFoot), a.GetBoneTransform(HumanBodyBones.RightFoot) };
        var starts = feet.Select(t => t.position).ToArray();
        var rotations = feet.Select(t => t.rotation).ToArray();
        var upperLengths = Enumerable.Range(0, 2).Select(i => Vector3.Distance(thighs[i].position, knees[i].position)).ToArray();
        var lowerLengths = Enumerable.Range(0, 2).Select(i => Vector3.Distance(knees[i].position, feet[i].position)).ToArray();
        var finalHip = hipStart;
        finalHip.y = root.transform.position.y + upperLengths.Min() * 0.8f + 0.02f;
        var targets = new Vector3[2];
        var kneeTargets = new Vector3[2];
        for (var i = 0; i < 2; i++)
        {
            var thigh = finalHip + Quaternion.AngleAxis(20f, root.transform.right) * (thighs[i].position - hipStart);
            var kneeY = root.transform.position.y + kneeHeights[i];
            var kneeX = root.transform.position.x + (i == 0 ? -1f : 1f) * Mathf.Max(0.07f, Mathf.Abs(thigh.x - root.transform.position.x) - 0.04f);
            var kneeZ = thigh.z + Mathf.Sqrt(Mathf.Max(0f, upperLengths[i] * upperLengths[i] -
                Mathf.Pow(thigh.y - kneeY, 2f) - Mathf.Pow(thigh.x - kneeX, 2f)));
            kneeTargets[i] = new Vector3(kneeX, kneeY, kneeZ);
            targets[i] = new Vector3(kneeX, root.transform.position.y + 0.22f,
                kneeZ - Mathf.Sqrt(Mathf.Max(0f, lowerLengths[i] * lowerLengths[i] - Mathf.Pow(0.22f - kneeHeights[i], 2f))));
        }
        hips.position = Vector3.Lerp(hipStart, finalHip, progress);
        hips.rotation = Quaternion.AngleAxis(20f * progress, root.transform.right) * hips.rotation;
        var spine = a.GetBoneTransform(HumanBodyBones.Spine);
        spine.rotation = Quaternion.AngleAxis(25f * progress, root.transform.right) * spine.rotation;
        for (var i = 0; i < 2; i++)
        {
            Solve(a, i == 0 ? HumanBodyBones.LeftUpperLeg : HumanBodyBones.RightUpperLeg,
                i == 0 ? HumanBodyBones.LeftLowerLeg : HumanBodyBones.RightLowerLeg,
                i == 0 ? HumanBodyBones.LeftFoot : HumanBodyBones.RightFoot,
                Vector3.Lerp(starts[i], targets[i], progress) + root.transform.up * (0.03f * Mathf.Sin(progress * Mathf.PI)),
                kneeTargets[i] - thighs[i].position);
            feet[i].rotation = Quaternion.AngleAxis(45f * progress, root.transform.right) * rotations[i];
        }
        var volume = root.GetComponentsInChildren<BodyHitRegion>().Single(r => r.Region == BodyRegion.Abdomen).GetComponent<BoxCollider>();
        var point = volume.transform.TransformPoint(volume.center + Vector3.forward * (volume.size.z * 0.5f));
        ArcherLimbIK.ReachAbdomen(a, point, volume.transform.forward, progress);
    }

    // Lowers the injured knee while the other foot stays planted and the same-side palm supports the bent torso.
    private static void LegPose(GameObject root, bool left, float progress, float kneeHeight)
    {
        var a = root.GetComponentInChildren<Animator>();
        var hips = a.GetBoneTransform(HumanBodyBones.Hips);
        var thigh = a.GetBoneTransform(left ? HumanBodyBones.LeftUpperLeg : HumanBodyBones.RightUpperLeg);
        var knee = a.GetBoneTransform(left ? HumanBodyBones.LeftLowerLeg : HumanBodyBones.RightLowerLeg);
        var injured = a.GetBoneTransform(left ? HumanBodyBones.LeftFoot : HumanBodyBones.RightFoot);
        var healthy = a.GetBoneTransform(left ? HumanBodyBones.RightFoot : HumanBodyBones.LeftFoot);
        var injuredStart = injured.position;
        var healthyStart = healthy.position;
        var injuredRotation = injured.rotation;
        var healthyRotation = healthy.rotation;
        var upperLength = Vector3.Distance(thigh.position, knee.position);
        var lowerLength = Vector3.Distance(knee.position, injured.position);
        const float kneeForward = 0.15f;
        var thighOffset = thigh.position - hips.position;
        var finalThighOffset = Quaternion.AngleAxis(40f, root.transform.right) * thighOffset;
        var kneeReach = kneeForward + thighOffset.z - finalThighOffset.z;
        var targetHeight = root.transform.position.y + kneeHeight +
            Mathf.Sqrt(upperLength * upperLength - kneeReach * kneeReach) - finalThighOffset.y;
        var kneeTarget = new Vector3(thigh.position.x, root.transform.position.y + kneeHeight,
            thigh.position.z + kneeForward);
        var footHeight = Mathf.Max(injuredStart.y + 0.09f, root.transform.position.y + 0.22f);
        var footTarget = new Vector3(kneeTarget.x, footHeight,
            kneeTarget.z - Mathf.Sqrt(lowerLength * lowerLength - Mathf.Pow(footHeight - kneeTarget.y, 2f)));
        hips.position += root.transform.up * ((targetHeight - hips.position.y) * progress);
        hips.rotation = Quaternion.AngleAxis(40f * progress, root.transform.right) * hips.rotation;
        var spine = a.GetBoneTransform(HumanBodyBones.Spine);
        spine.rotation = Quaternion.AngleAxis(60f * progress, root.transform.right) * spine.rotation;
        Solve(a, left ? HumanBodyBones.LeftUpperLeg : HumanBodyBones.RightUpperLeg,
            left ? HumanBodyBones.LeftLowerLeg : HumanBodyBones.RightLowerLeg,
            left ? HumanBodyBones.LeftFoot : HumanBodyBones.RightFoot,
            Vector3.Lerp(injuredStart, footTarget, progress) + root.transform.up * (0.025f * Mathf.Sin(progress * Mathf.PI)),
            root.transform.forward);
        Solve(a, left ? HumanBodyBones.RightUpperLeg : HumanBodyBones.LeftUpperLeg,
            left ? HumanBodyBones.RightLowerLeg : HumanBodyBones.LeftLowerLeg,
            left ? HumanBodyBones.RightFoot : HumanBodyBones.LeftFoot, healthyStart, root.transform.forward);
        injured.rotation = Quaternion.AngleAxis(45f * progress, root.transform.right) * injuredRotation;
        healthy.rotation = healthyRotation;
        if (!left)
            healthy.rotation = Quaternion.AngleAxis(-4f * progress, root.transform.right) * healthyRotation;
        GroundPalm(root, a, left, progress, 0.08f);
        var other = a.GetBoneTransform(left ? HumanBodyBones.RightHand : HumanBodyBones.LeftHand);
        var rest = a.GetBoneTransform(left ? HumanBodyBones.RightLowerLeg : HumanBodyBones.LeftLowerLeg).position +
            root.transform.up * 0.08f + root.transform.forward * 0.025f;
        Solve(a, left ? HumanBodyBones.RightUpperArm : HumanBodyBones.LeftUpperArm,
            left ? HumanBodyBones.RightLowerArm : HumanBodyBones.LeftLowerArm,
            left ? HumanBodyBones.RightHand : HumanBodyBones.LeftHand,
            Vector3.Lerp(other.position, rest, progress), root.transform.right * (left ? 1f : -1f));
    }

    // Places a fitted support palm on the floor in front of or behind its shoulder.
    private static void GroundPalm(GameObject root, Animator a, bool left, float progress, float forwardOffset)
    {
        var hand = a.GetBoneTransform(left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
        var shoulder = a.GetBoneTransform(left ? HumanBodyBones.LeftUpperArm : HumanBodyBones.RightUpperArm);
        var support = shoulder.position + root.transform.right * (left ? -0.06f : 0.06f) + root.transform.forward * forwardOffset;
        support.y = root.transform.position.y + 0.03f;
        Solve(a, left ? HumanBodyBones.LeftUpperArm : HumanBodyBones.RightUpperArm,
            left ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm,
            left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand,
            Vector3.Lerp(hand.position, support, progress), root.transform.right * (left ? -1f : 1f));
        var middle = a.GetBoneTransform(left ? HumanBodyBones.LeftMiddleProximal : HumanBodyBones.RightMiddleProximal);
        var index = a.GetBoneTransform(left ? HumanBodyBones.LeftIndexProximal : HumanBodyBones.RightIndexProximal);
        var little = a.GetBoneTransform(left ? HumanBodyBones.LeftLittleProximal : HumanBodyBones.RightLittleProximal);
        var fingers = middle.position - hand.position;
        var normal = Vector3.Cross(fingers, index.position - little.position).normalized;
        // Anatomical handedness keeps the support palm from flipping as the arm bends.
        if (!left) normal = -normal;
        var palmRotation = Quaternion.LookRotation(root.transform.forward, root.transform.up) *
            Quaternion.Inverse(Quaternion.LookRotation(fingers, normal)) * hand.rotation;
        hand.rotation = Quaternion.Slerp(hand.rotation, palmRotation, progress);
        RaiseSupportHand(root, a, left);
    }

    // Fits the visible glove and fingertips above the support plane without stretching the arm or lifting the planted foot.
    private static void RaiseSupportHand(GameObject root, Animator animator, bool left)
    {
        var hand = animator.GetBoneTransform(left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
        for (var fit = 0; fit < 2; fit++)
        {
            var floor = NearbyFloor(root, hand.position);
            if (floor >= root.transform.position.y) return;
            var rotation = hand.rotation;
            Solve(animator, left ? HumanBodyBones.LeftUpperArm : HumanBodyBones.RightUpperArm,
                left ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm,
                left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand,
                hand.position + root.transform.up * (root.transform.position.y - floor + 0.001f),
                root.transform.right * (left ? -1f : 1f));
            hand.rotation = rotation;
        }
    }

    // Measures the visible support surface near a joint, including clothing and fingers.
    private static float NearbyFloor(GameObject root, Vector3 joint, bool seat = false)
    {
        var floor = float.PositiveInfinity;
        var radius = seat ? 0.25f : 0.18f;
        var mesh = new Mesh();
        try
        {
            foreach (var skin in root.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                skin.BakeMesh(mesh, true);
                foreach (var vertex in mesh.vertices)
                {
                    var point = skin.transform.TransformPoint(vertex);
                    var delta = seat ? Vector3.ProjectOnPlane(point - joint, root.transform.up) : point - joint;
                    if (delta.sqrMagnitude < radius * radius) floor = Mathf.Min(floor, point.y);
                }
                mesh.Clear();
            }
        }
        finally { Object.DestroyImmediate(mesh); }
        if (float.IsInfinity(floor)) throw new InvalidOperationException("No visible skin near the support joint.");
        return floor;
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

    // Replaces clip contents without changing existing asset GUIDs when authoring is rerun.
    internal static AnimationClip SaveClip(AnimationClip clip, string path)
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
