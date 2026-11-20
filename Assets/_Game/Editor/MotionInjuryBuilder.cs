using System;
using System.Linq;
using BowMan;
using UnityEditor;
using UnityEngine;

public static class MotionInjuryBuilder
{
    private const string MotionFolder = "Assets/ThirdParty/MixamoAnimations/Motions/";

    // Bakes Azumi's short recoil, forearm cradle and release on both character rigs.
    [MenuItem("Bow Man VR/Injuries/Rebuild Captured Arm Reactions")]
    public static void RebuildArmReactions()
    {
        var source = AssetDatabase.LoadAllAssetsAtPath("Assets/ThirdParty/EyesJapan/accident-05-damage right arm-azumi.fbx")
            .OfType<AnimationClip>().Single(c => !c.name.StartsWith("__"));
        foreach (var region in new[] { BodyRegion.RightArm, BodyRegion.LeftArm })
        {
            foreach (var model in new[] { "EnglishArcher", "ErikaArcher" })
                BakeArmGesture(model, source, region);
        }
        AssetDatabase.SaveAssets();
    }

    // Keeps the performer's upper-body movement and plants the boots in the duel stance.
    private static void BakeArmGesture(string model, AnimationClip source, BodyRegion region)
    {
        var path = $"Assets/_Game/Prefabs/Characters/{model}.prefab";
        var root = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));
        try
        {
            var rig = root.GetComponentInChildren<Animator>();
            var left = region == BodyRegion.LeftArm;
            var bones = HumanoidBones(rig);
            var neutral = bones.Select(b => b.localRotation).ToArray();
            var positions = bones.Select(b => b.localPosition).ToArray();
            var world = bones.Select(b => b.rotation).ToArray();
            var opposite = OppositeBoneIndices(rig, bones);
            var modelPosition = rig.transform.localPosition;
            var modelRotation = rig.transform.localRotation;
            var hips = rig.GetBoneTransform(HumanBodyBones.Hips);
            var start = hips.position;
            var leftFoot = rig.GetBoneTransform(HumanBodyBones.LeftFoot);
            var rightFoot = rig.GetBoneTransform(HumanBodyBones.RightFoot);
            var feet = new[] { leftFoot.position, rightFoot.position };
            var bootRotations = new[] { leftFoot.rotation, rightFoot.rotation };
            var regions = root.GetComponentsInChildren<BodyHitRegion>();
            var core = CoreVolumes(regions);
            foreach (var box in core.OfType<BoxCollider>())
                box.size += Vector3.one * 0.005f;
            var leftArm = ForearmVolumes(regions, rig.GetBoneTransform(HumanBodyBones.LeftLowerArm), rig.GetBoneTransform(HumanBodyBones.LeftHand));
            var rightArm = ForearmVolumes(regions, rig.GetBoneTransform(HumanBodyBones.RightLowerArm), rig.GetBoneTransform(HumanBodyBones.RightHand));
            var chest = regions.Single(r => r.Region == BodyRegion.Chest).transform;
            var supportUpper = left ? HumanBodyBones.RightUpperArm : HumanBodyBones.LeftUpperArm;
            var supportLower = left ? HumanBodyBones.RightLowerArm : HumanBodyBones.LeftLowerArm;
            var supportHand = left ? HumanBodyBones.RightHand : HumanBodyBones.LeftHand;
            var shoulder = rig.GetBoneTransform(supportUpper);
            var elbow = rig.GetBoneTransform(supportLower);
            var hand = rig.GetBoneTransform(supportHand);
            var handStart = hand.position;
            var elbowStart = elbow.position;
            var rotationStart = hand.rotation;
            var shoulderRest = shoulder.localRotation;
            var elbowRest = elbow.localRotation;
            var neutralAcross = Across();
            Sample(rig, source, 25.12f, modelPosition, modelRotation);
            var sourceOrigin = hips.position;
            var yaw = Quaternion.AngleAxis(Vector3.SignedAngle(Across(), neutralAcross, Vector3.up), Vector3.up);
            Pose(25.44f, 1f);
            var handEnd = hand.position;
            var elbowEnd = elbow.position;
            var rotationEnd = hand.rotation;
            var entry = Bake("Entry", 0.9f, t =>
            {
                var weight = Ease(t, 0.05f, 0.85f);
                Pose(Mathf.Lerp(25.12f, 25.44f, t / 0.9f), Ease(t, 0f, 0.6f));
                if (t <= 0f || t >= 0.9f) return;
                var upperRecorded = shoulder.localRotation;
                var lowerRecorded = elbow.localRotation;
                var wristRecorded = hand.localRotation;
                var arc = Mathf.Sin(Mathf.PI * weight);
                var target = Vector3.Lerp(handStart, handEnd, weight) + root.transform.forward * (0.16f * arc);
                var bend = Vector3.Lerp(elbowStart, elbowEnd, weight) + root.transform.forward * (0.12f * arc) +
                    root.transform.right * ((left ? 0.09f : -0.09f) * arc);
                var forward = root.transform.forward;
                var ahead = float.NegativeInfinity;
                foreach (var volume in core.OfType<BoxCollider>())
                {
                    if (volume.GetComponent<BodyHitRegion>().Region is BodyRegion.Neck or BodyRegion.Head) continue;
                    var size = volume.size * 0.5f;
                    var extent = Mathf.Abs(Vector3.Dot(forward, volume.transform.TransformVector(Vector3.right * size.x))) +
                        Mathf.Abs(Vector3.Dot(forward, volume.transform.TransformVector(Vector3.up * size.y))) +
                        Mathf.Abs(Vector3.Dot(forward, volume.transform.TransformVector(Vector3.forward * size.z)));
                    ahead = Mathf.Max(ahead, Vector3.Dot(forward, volume.transform.TransformPoint(volume.center)) + extent);
                }
                var crossing = Ease(t, 0.04f, 0.22f) * (1f - Ease(t, 0.68f, 0.90f));
                target += forward * (Mathf.Max(0f, ahead + 0.12f - Vector3.Dot(forward, target)) * crossing);
                bend += forward * (Mathf.Max(0f, ahead + 0.10f - Vector3.Dot(forward, bend)) * crossing);
                shoulder.localRotation = shoulderRest;
                elbow.localRotation = elbowRest;
                ArcherLimbIK.Solve(rig, supportUpper, supportLower, supportHand, target, bend - shoulder.position);
                hand.rotation = Quaternion.Slerp(rotationStart, rotationEnd, weight);
                var settle = Ease(t, 0.68f, 0.9f);
                shoulder.localRotation = Quaternion.Slerp(shoulder.localRotation, upperRecorded, settle);
                elbow.localRotation = Quaternion.Slerp(elbow.localRotation, lowerRecorded, settle);
                hand.localRotation = Quaternion.Slerp(hand.localRotation, wristRecorded, settle);
            });
            var hold = Bake("Hold", 0.30f, t => Pose(25.44f + t, 1f));
            var recovery = Bake("Recovery", 0.95f, t =>
                Pose(25.74f + 0.42f * t + 0.53f * Ease(t, 0.49f, 0.95f), 1f - Ease(t, 0.38f, 0.95f)));
            var prefab = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var actor = prefab.GetComponent<ArcherReactionAnimation>();
                actor.SetSequence(region, entry, recovery, hold);
                actor.SetDeathFinisher(region, actor.Entry(BodyRegion.Head));
                PrefabUtility.SaveAsPrefabAsset(prefab, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(prefab); }

            // Measures facing from the line between the shoulder joints.
            Vector3 Across() => Vector3.ProjectOnPlane(rig.GetBoneTransform(HumanBodyBones.RightUpperArm).position -
                rig.GetBoneTransform(HumanBodyBones.LeftUpperArm).position, Vector3.up);

            // Records one contiguous phase on the runtime character root.
            AnimationClip Bake(string phase, float duration, Action<float> sample)
            {
                var clip = Record(root, bones, duration, sample);
                clip.name = region + " " + phase;
                return SaveClip(clip, $"Assets/_Game/Animations/Injuries/{model}/{region}_{phase}.anim");
            }

            // Aligns the original performance to the opponent and limits contact correction to the cradle.
            void Pose(float seconds, float blend)
            {
                Restore(bones, positions, neutral);
                if (blend <= 0f) return;
                Sample(rig, source, seconds, modelPosition, modelRotation);
                var delta = yaw * (hips.position - sourceOrigin);
                hips.position = start + new Vector3(delta.x * 0.22f, Mathf.Clamp(delta.y, -0.10f, 0f) - 0.036f, delta.z * 0.22f);
                hips.rotation = yaw * hips.rotation;
                if (left) MirrorBody(root.transform, hips, start, bones, positions, neutral, world, opposite);
                for (var i = 0; i < bones.Length; i++)
                {
                    bones[i].localPosition = Vector3.Lerp(positions[i], bones[i].localPosition, blend);
                    bones[i].localRotation = Quaternion.Slerp(neutral[i], bones[i].localRotation, blend);
                }
                Plant(HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot, 0);
                Plant(HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot, 1);
                var reach = blend * (1f - Ease(seconds, 25.75f, 25.94f));
                if (reach > 0f)
                {
                    var clearance = (chest.forward * 0.115f + Vector3.up * 0.165f) * reach;
                    ClearEquipment(true, clearance);
                    ClearEquipment(false, clearance);
                    var elbow = rig.GetBoneTransform(left ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm);
                    var wrist = rig.GetBoneTransform(left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
                    var handId = left ? HumanBodyBones.RightHand : HumanBodyBones.LeftHand;
                    var lowerId = left ? HumanBodyBones.RightLowerArm : HumanBodyBones.LeftLowerArm;
                    var upperId = left ? HumanBodyBones.RightUpperArm : HumanBodyBones.LeftUpperArm;
                    var hand = rig.GetBoneTransform(handId);
                    var middle = rig.GetBoneTransform(left ? HumanBodyBones.RightMiddleProximal : HumanBodyBones.LeftMiddleProximal);
                    var index = rig.GetBoneTransform(left ? HumanBodyBones.RightIndexProximal : HumanBodyBones.LeftIndexProximal);
                    var little = rig.GetBoneTransform(left ? HumanBodyBones.RightLittleProximal : HumanBodyBones.LeftLittleProximal);
                    var normal = Vector3.Cross(middle.position - hand.position, index.position - little.position).normalized * (left ? -1f : 1f);
                    var turn = Quaternion.FromToRotation(normal, Vector3.up);
                    hand.rotation = Quaternion.Slerp(Quaternion.identity, turn, 0.65f * reach) * hand.rotation;
                    var palm = (hand.position + middle.position) * 0.5f;
                    var contact = Vector3.Lerp(elbow.position, wrist.position, 0.55f) - Vector3.up * 0.032f;
                    var rotation = hand.rotation;
                    ArcherLimbIK.Solve(rig, upperId, lowerId, handId, hand.position + (contact - palm) * reach + chest.forward * (0.25f * Mathf.Sin(Mathf.PI * reach)),
                        rig.GetBoneTransform(lowerId).position - rig.GetBoneTransform(upperId).position +
                        (chest.forward * 0.27f + chest.right * (left ? 0.22f : -0.22f)) * Mathf.Sin(Mathf.PI * reach));
                    hand.rotation = rotation;
                }
                FitForearm(true, leftArm);
                FitForearm(false, rightArm);
            }

            // Uses the smallest local arm swing that clears the fitted torso during the crossing reach.
            void FitForearm(bool onLeft, Collider[] volumes)
            {
                ArcherLimbIK.ClearForearm(rig, onLeft, volumes, core, 14);
                var depth = ArcherLimbIK.DeepestOverlap(volumes, core, out _);
                if (depth <= 0.0002f) return;
                var arm = rig.GetBoneTransform(onLeft ? HumanBodyBones.LeftUpperArm : HumanBodyBones.RightUpperArm);
                var elbow = rig.GetBoneTransform(onLeft ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm);
                var wrist = rig.GetBoneTransform(onLeft ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
                var handRotation = wrist.rotation;
                var axes = new[] { chest.right, chest.up, chest.forward };
                for (var step = 0; step < 18 && depth > 0.0002f; step++)
                {
                    Transform bestJoint = null;
                    var bestRotation = Quaternion.identity;
                    foreach (var joint in new[] { arm, elbow })
                    {
                        var original = joint.rotation;
                        foreach (var axis in axes)
                        foreach (var angle in new[] { -2f, 2f })
                        {
                            joint.rotation = Quaternion.AngleAxis(angle, axis) * original;
                            wrist.rotation = handRotation;
                            var candidate = ArcherLimbIK.DeepestOverlap(volumes, core, out _);
                            if (candidate >= depth) continue;
                            depth = candidate;
                            bestJoint = joint;
                            bestRotation = joint.rotation;
                        }
                        joint.rotation = original;
                        wrist.rotation = handRotation;
                    }
                    if (bestJoint == null) break;
                    bestJoint.rotation = bestRotation;
                    wrist.rotation = handRotation;
                }
            }

            // Moves the recorded cradle in front of the fitted clothing while preserving the elbow direction.
            void ClearEquipment(bool onLeft, Vector3 offset)
            {
                var upper = onLeft ? HumanBodyBones.LeftUpperArm : HumanBodyBones.RightUpperArm;
                var lower = onLeft ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm;
                var end = onLeft ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand;
                var wrist = rig.GetBoneTransform(end);
                var rotation = wrist.rotation;
                ArcherLimbIK.Solve(rig, upper, lower, end, wrist.position + offset,
                    rig.GetBoneTransform(lower).position - rig.GetBoneTransform(upper).position);
                wrist.rotation = rotation;
            }

            // Retains the performed knee bend while locking each boot to its original contact.
            void Plant(HumanBodyBones thigh, HumanBodyBones shin, HumanBodyBones boot, int index)
            {
                ArcherLimbIK.Solve(rig, thigh, shin, boot, feet[index], rig.GetBoneTransform(shin).position - rig.GetBoneTransform(thigh).position);
                rig.GetBoneTransform(boot).rotation = bootRotations[index];
            }
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    // Retargets the kneeling and standing recordings to the injured knee and same-side palm.
    [MenuItem("Bow Man VR/Injuries/Rebuild Captured Leg Reactions")]
    public static void RebuildCapturedLegs()
    {
        var kneel = ImportMotion("Kneeling Down");
        var stand = ImportMotion("Standing");
        foreach (var side in new[] { BodyRegion.LeftLeg, BodyRegion.RightLeg })
        {
            foreach (var name in new[] { "EnglishArcher", "ErikaArcher" })
                BakeCapturedLeg(name, side, kneel, stand);
            RebuildProneFinisher(side);
        }
        AssetDatabase.SaveAssets();
    }

    // Preserves the captured weight transfer and torso movement while fitting the floor contacts.
    private static void BakeCapturedLeg(string model, BodyRegion region, AnimationClip down, AnimationClip up)
    {
        var path = $"Assets/_Game/Prefabs/Characters/{model}.prefab";
        var root = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));
        try
        {
            var rig = root.GetComponentInChildren<Animator>();
            var left = region == BodyRegion.LeftLeg;
            var bones = HumanoidBones(rig);
            var neutral = bones.Select(b => b.localRotation).ToArray();
            var positions = bones.Select(b => b.localPosition).ToArray();
            var world = bones.Select(b => b.rotation).ToArray();
            var opposite = OppositeBoneIndices(rig, bones);
            var hips = rig.GetBoneTransform(HumanBodyBones.Hips);
            var start = hips.position;
            var modelPosition = rig.transform.localPosition;
            var modelRotation = rig.transform.localRotation;
            var spine = rig.GetBoneTransform(HumanBodyBones.Spine);
            var upper = bones.Where(b => b.IsChildOf(spine)).ToArray();
            var knee = rig.GetBoneTransform(left ? HumanBodyBones.LeftLowerLeg : HumanBodyBones.RightLowerLeg);
            var regions = root.GetComponentsInChildren<BodyHitRegion>();
            var core = CoreVolumes(regions);
            var leftArm = ForearmVolumes(regions, rig.GetBoneTransform(HumanBodyBones.LeftLowerArm), rig.GetBoneTransform(HumanBodyBones.LeftHand));
            var rightArm = ForearmVolumes(regions, rig.GetBoneTransform(HumanBodyBones.RightLowerArm), rig.GetBoneTransform(HumanBodyBones.RightHand));
            var kneeHeight = 0.08f;
            for (var pass = 0; pass < 3; pass++)
            {
                Restore(bones, positions, neutral);
                LegPose(root, left, 1f, kneeHeight);
                kneeHeight -= NearbyFloor(root, knee.position) - root.transform.position.y;
            }
            var folder = $"Assets/_Game/Animations/Injuries/{model}/{region}";
            var entry = Bake(false);
            var recovery = Bake(true);
            var prefab = PrefabUtility.LoadPrefabContents(path);
            try
            {
                prefab.GetComponent<ArcherReactionAnimation>().SetSequence(region, entry, recovery);
                PrefabUtility.SaveAsPrefabAsset(prefab, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(prefab); }

            // Uses the performer's pelvis height to time the approach to the measured knee and palm contacts.
            AnimationClip Bake(bool recovering)
            {
                var source = recovering ? up : down;
                var first = recovering ? 0.45f : 0f;
                var last = recovering ? 2.12f : Mathf.Min(1.5f, down.length);
                Restore(bones, positions, neutral);
                Sample(rig, source, first, modelPosition, modelRotation);
                var firstHeight = hips.position.y;
                Sample(rig, source, last, modelPosition, modelRotation);
                var lastHeight = hips.position.y;
                const float duration = 0.95f;
                var clip = Record(root, bones, duration, time =>
                {
                    var u = Mathf.Clamp01(time / duration);
                    Restore(bones, positions, neutral);
                    Sample(rig, source, Mathf.Lerp(first, last, u), modelPosition, modelRotation);
                    var heightPhase = Mathf.InverseLerp(firstHeight, lastHeight, hips.position.y);
                    if (left) MirrorBody(root.transform, hips, start, bones, positions, neutral, world, opposite);
                    var recorded = upper.Select(b => b.localRotation).ToArray();
                    var progress = Mathf.Lerp(Mathf.SmoothStep(0f, 1f, u), heightPhase, 0.45f);
                    if (recovering) progress = 1f - progress;
                    Restore(bones, positions, neutral);
                    LegPose(root, left, progress, kneeHeight);
                    var weight = Ease(u, 0f, 0.20f) * (1f - Ease(u, 0.68f, 1f));
                    for (var i = 0; i < upper.Length; i++)
                        upper[i].localRotation = Quaternion.Slerp(upper[i].localRotation, recorded[i], 0.65f * weight);
                    ArcherLimbIK.ClearForearm(rig, true, leftArm, core);
                    ArcherLimbIK.ClearForearm(rig, false, rightArm, core);
                });
                clip.name = region + (recovering ? " Recovery" : " Entry");
                return SaveClip(clip, folder + (recovering ? "_Recovery.anim" : "_Entry.anim"));
            }
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
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
        ArcherLimbIK.Solve(a, left ? HumanBodyBones.LeftUpperLeg : HumanBodyBones.RightUpperLeg,
            left ? HumanBodyBones.LeftLowerLeg : HumanBodyBones.RightLowerLeg,
            left ? HumanBodyBones.LeftFoot : HumanBodyBones.RightFoot,
            Vector3.Lerp(injuredStart, footTarget, progress) + root.transform.up * (0.025f * Mathf.Sin(progress * Mathf.PI)),
            root.transform.forward);
        ArcherLimbIK.Solve(a, left ? HumanBodyBones.RightUpperLeg : HumanBodyBones.LeftUpperLeg,
            left ? HumanBodyBones.RightLowerLeg : HumanBodyBones.LeftLowerLeg,
            left ? HumanBodyBones.RightFoot : HumanBodyBones.LeftFoot, healthyStart, root.transform.forward);
        injured.rotation = Quaternion.AngleAxis(45f * progress, root.transform.right) * injuredRotation;
        healthy.rotation = healthyRotation;
        if (!left)
            healthy.rotation = Quaternion.AngleAxis(-4f * progress, root.transform.right) * healthyRotation;
        GroundPalm(root, a, left, progress, 0.08f, 0.105f);
        var other = a.GetBoneTransform(left ? HumanBodyBones.RightHand : HumanBodyBones.LeftHand);
        var outward = root.transform.right * (left ? 1f : -1f);
        var rest = a.GetBoneTransform(left ? HumanBodyBones.RightLowerLeg : HumanBodyBones.LeftLowerLeg).position +
            root.transform.up * 0.08f + root.transform.forward * 0.025f + outward * 0.105f;
        ArcherLimbIK.Solve(a, left ? HumanBodyBones.RightUpperArm : HumanBodyBones.LeftUpperArm,
            left ? HumanBodyBones.RightLowerArm : HumanBodyBones.LeftLowerArm,
            left ? HumanBodyBones.RightHand : HumanBodyBones.LeftHand,
            Vector3.Lerp(other.position, rest, progress), outward);
    }

    // Blends captured weight transfer with a fitted double kneel and keeps the hands at the wound.
    [MenuItem("Bow Man VR/Injuries/Rebuild Abdomen")]
    public static void RebuildAbdomen()
    {
        var drop = ImportMotion("Kneeling Down");
        var rise = ImportMotion("Standing");
        const float entryLength = 1.25f;
        const float recoveryLength = 1.65f;
        const float standFromKneel = 0.45f;
        const float sourceRiseEnd = 2.12f;
        foreach (var name in new[] { "EnglishArcher", "ErikaArcher" })
        {
            var path = $"Assets/_Game/Prefabs/Characters/{name}.prefab";
            var root = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));
            var mesh = new Mesh();
            try
            {
                var animator = root.GetComponentInChildren<Animator>();
                var bones = animator.GetComponentsInChildren<Transform>().Where(t => t != animator.transform).ToArray();
                var neutral = bones.Select(t => t.localRotation).ToArray();
                var positions = bones.Select(t => t.localPosition).ToArray();
                var modelPosition = animator.transform.localPosition;
                var modelRotation = animator.transform.localRotation;
                var hips = animator.GetBoneTransform(HumanBodyBones.Hips);
                var neutralHip = hips.position;
                var spine = animator.GetBoneTransform(HumanBodyBones.Spine);
                var head = animator.GetBoneTransform(HumanBodyBones.Head);
                var front = root.transform.forward;
                var up = root.transform.up;
                var right = root.transform.right;
                var hitRegions = root.GetComponentsInChildren<BodyHitRegion>();
                var abdomen = hitRegions.Single(hit => hit.Region == BodyRegion.Abdomen).GetComponent<BoxCollider>();
                var hipVolume = hitRegions.Single(hit => hit.Region == BodyRegion.Pelvis).GetComponent<Collider>();
                var bodyVolumes = CoreVolumes(hitRegions);
                var leftElbow = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
                var rightElbow = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
                var leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
                var rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
                var leftVolumes = ForearmVolumes(hitRegions, leftElbow, leftHand);
                var rightVolumes = ForearmVolumes(hitRegions, rightElbow, rightHand);
                var bootVolumes = hitRegions.Where(hit => hit.Region is BodyRegion.LeftFoot or BodyRegion.RightFoot)
                    .Select(hit => hit.GetComponent<Collider>()).ToArray();
                var leadFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
                var neutralLeadFoot = leadFoot.position;

                FitAbdomenKneel(root, bones, positions, neutral);
                FitContacts(1f, false);
                var kneeling = bones.Select(t => t.localRotation).ToArray();
                var kneelPositions = bones.Select(t => t.localPosition).ToArray();

                SampleLowerBody(drop, 0f);
                var dropOffset = Vector3.ProjectOnPlane(neutralHip - hips.position, up);
                SampleLowerBody(rise, sourceRiseEnd);
                var riseOffset = Vector3.ProjectOnPlane(neutralHip - hips.position, up);
                SampleLowerBody(rise, 0f);
                var plantedLead = leadFoot.position + riseOffset;
                plantedLead.y = neutralLeadFoot.y;
                var leadRotation = leadFoot.rotation;
                var weightBearing = new[] { HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot };

                var entry = Record(root, bones, entryLength, time =>
                {
                    if (time <= 0f) { Restore(bones, positions, neutral); return; }
                    if (time >= entryLength) { Restore(bones, kneelPositions, kneeling); return; }
                    var sourceTime = Mathf.Lerp(0.12f, 2.15f, Mathf.InverseLerp(0.18f, entryLength, time));
                    SampleLowerBody(drop, sourceTime);
                    hips.position += dropOffset;
                    var fold = Ease(time, 0.03f, 0.36f);
                    spine.rotation = Quaternion.AngleAxis(23f * fold, right) * spine.rotation;
                    head.rotation = Quaternion.AngleAxis(12f * fold, right) * head.rotation;
                    BlendFrom(positions, neutral, Ease(time, 0f, 0.22f));
                    BlendFrom(kneelPositions, kneeling, 1f - Ease(time, 0.73f, entryLength));
                    FitContacts(time / entryLength, false);
                });
                var recovery = Record(root, bones, recoveryLength, time =>
                {
                    if (time <= 0f) { Restore(bones, kneelPositions, kneeling); return; }
                    if (time >= recoveryLength) { Restore(bones, positions, neutral); return; }
                    var sourceTime = Mathf.Lerp(0f, sourceRiseEnd, Mathf.InverseLerp(standFromKneel, recoveryLength, time));
                    SampleLowerBody(rise, sourceTime);
                    hips.position += riseOffset;
                    var fold = 1f - Ease(time, 0.58f, 1.22f);
                    spine.rotation = Quaternion.AngleAxis(21f * fold, right) * spine.rotation;
                    head.rotation = Quaternion.AngleAxis(11f * fold, right) * head.rotation;
                    BlendFrom(kneelPositions, kneeling, Ease(time, 0f, standFromKneel));
                    BlendFrom(positions, neutral, 1f - Ease(time, 1.35f, recoveryLength));
                    var plant = Ease(time, 0.18f, standFromKneel) * (1f - Ease(sourceTime, 1.2f, 1.55f));
                    if (plant > 0f)
                    {
                        var rotation = leadFoot.rotation;
                        ArcherLimbIK.Solve(animator, weightBearing[0], weightBearing[1], weightBearing[2],
                            Vector3.Lerp(leadFoot.position, plantedLead, plant), front);
                        leadFoot.rotation = Quaternion.Slerp(rotation, leadRotation, plant);
                    }
                    FitContacts(time / recoveryLength, true);
                });

                entry.name = "Abdomen Entry";
                recovery.name = "Abdomen Recovery";
                var folder = $"Assets/_Game/Animations/Injuries/{name}";
                entry = SaveClip(entry, $"{folder}/Abdomen_Entry.anim");
                recovery = SaveClip(recovery, $"{folder}/Abdomen_Recovery.anim");
                var prefab = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    prefab.GetComponent<ArcherReactionAnimation>().SetSequence(BodyRegion.Abdomen, entry, recovery);
                    PrefabUtility.SaveAsPrefabAsset(prefab, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(prefab); }

                // Keeps captured hip and leg motion while restoring the upper-body reference.
                void SampleLowerBody(AnimationClip source, float time)
                {
                    Restore(bones, positions, neutral);
                    Sample(animator, source, time, modelPosition, modelRotation);
                    for (var i = 0; i < bones.Length; i++)
                    {
                        if (bones[i] == hips) continue;
                        bones[i].localPosition = positions[i];
                        if (bones[i] == spine || bones[i].IsChildOf(spine)) bones[i].localRotation = neutral[i];
                    }
                }

                // Transitions from an exact neutral or fitted kneeling pose to the current sampled pose.
                void BlendFrom(Vector3[] startPositions, Quaternion[] startRotations, float weight)
                {
                    for (var i = 0; i < bones.Length; i++)
                    {
                        bones[i].localPosition = Vector3.Lerp(startPositions[i], bones[i].localPosition, weight);
                        bones[i].localRotation = Quaternion.Slerp(startRotations[i], bones[i].localRotation, weight);
                    }
                }

                // Maintains the wound reach and prevents the boots from tucking through the hips.
                void FitContacts(float progress, bool recovering)
                {
                    var point = abdomen.transform.TransformPoint(abdomen.center + Vector3.forward * (abdomen.size.z * 0.5f));
                    var reach = ArcherLimbIK.AbdomenReachWeight(progress, recovering);
                    ArcherLimbIK.ReachAbdomen(animator, point, abdomen.transform.forward, reach, Mathf.Sin(Mathf.PI * reach));
                    ArcherLimbIK.ClearForearm(animator, true, leftVolumes, bodyVolumes);
                    ArcherLimbIK.ClearForearm(animator, false, rightVolumes, bodyVolumes);
                    foreach (var boot in bootVolumes)
                    {
                        if (!Physics.ComputePenetration(boot, boot.transform.position, boot.transform.rotation,
                            hipVolume, hipVolume.transform.position, hipVolume.transform.rotation, out var away, out var depth)) continue;
                        var left = boot.GetComponent<BodyHitRegion>().Region == BodyRegion.LeftFoot;
                        var thigh = left ? HumanBodyBones.LeftUpperLeg : HumanBodyBones.RightUpperLeg;
                        var knee = left ? HumanBodyBones.LeftLowerLeg : HumanBodyBones.RightLowerLeg;
                        var foot = left ? HumanBodyBones.LeftFoot : HumanBodyBones.RightFoot;
                        var ankle = animator.GetBoneTransform(foot);
                        var rotation = ankle.rotation;
                        ArcherLimbIK.Solve(animator, thigh, knee, foot, ankle.position + away * (depth + 0.006f),
                            animator.GetBoneTransform(knee).position - animator.GetBoneTransform(thigh).position);
                        ankle.rotation = rotation;
                    }
                    Ground(root, hips, mesh, 0f);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(mesh);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
        RebuildProneFinisher(BodyRegion.Abdomen);
        AssetDatabase.SaveAssets();
    }

    // Fits each kneepad to the floor and leaves the model in its supported end pose.
    private static void FitAbdomenKneel(GameObject root, Transform[] bones, Vector3[] positions, Quaternion[] rotations)
    {
        var animator = root.GetComponentInChildren<Animator>();
        var kneeHeights = Vector2.one * 0.08f;
        for (var fit = 0; fit < 3; fit++)
        {
            Restore(bones, positions, rotations);
            AbdomenKneel(root, kneeHeights);
            kneeHeights.x -= NearbyFloor(root, animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg).position) - root.transform.position.y;
            kneeHeights.y -= NearbyFloor(root, animator.GetBoneTransform(HumanBodyBones.RightLowerLeg).position) - root.transform.position.y;
        }
        Restore(bones, positions, rotations);
        AbdomenKneel(root, kneeHeights);
    }

    // Folds over the abdominal wound while placing both visible knees on their fitted support surfaces.
    private static void AbdomenKneel(GameObject root, Vector2 kneeHeights)
    {
        var a = root.GetComponentInChildren<Animator>();
        var hips = a.GetBoneTransform(HumanBodyBones.Hips);
        var hipStart = hips.position;
        var thighs = new[] { a.GetBoneTransform(HumanBodyBones.LeftUpperLeg), a.GetBoneTransform(HumanBodyBones.RightUpperLeg) };
        var knees = new[] { a.GetBoneTransform(HumanBodyBones.LeftLowerLeg), a.GetBoneTransform(HumanBodyBones.RightLowerLeg) };
        var feet = new[] { a.GetBoneTransform(HumanBodyBones.LeftFoot), a.GetBoneTransform(HumanBodyBones.RightFoot) };
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
            // The ankles sit outside the pelvis while both knees retain their fitted contact.
            const float ankleHeight = 0.22f;
            var ankleRise = ankleHeight - kneeHeights[i];
            var ankleBack = Mathf.Sqrt(Mathf.Max(0f, lowerLengths[i] * lowerLengths[i] - ankleRise * ankleRise));
            var ankleOffset = Quaternion.AngleAxis(i == 0 ? 24f : -24f, root.transform.up)
                * new Vector3(0f, ankleRise, -ankleBack);
            targets[i] = kneeTargets[i] + ankleOffset;
        }
        hips.position = finalHip;
        hips.rotation = Quaternion.AngleAxis(20f, root.transform.right) * hips.rotation;
        var spine = a.GetBoneTransform(HumanBodyBones.Spine);
        spine.rotation = Quaternion.AngleAxis(25f, root.transform.right) * spine.rotation;
        for (var i = 0; i < 2; i++)
        {
            ArcherLimbIK.Solve(a, i == 0 ? HumanBodyBones.LeftUpperLeg : HumanBodyBones.RightUpperLeg,
                i == 0 ? HumanBodyBones.LeftLowerLeg : HumanBodyBones.RightLowerLeg,
                i == 0 ? HumanBodyBones.LeftFoot : HumanBodyBones.RightFoot,
                targets[i],
                kneeTargets[i] - thighs[i].position);
            feet[i].rotation = Quaternion.AngleAxis(45f, root.transform.right) * rotations[i];
        }
        var volume = root.GetComponentsInChildren<BodyHitRegion>().Single(r => r.Region == BodyRegion.Abdomen).GetComponent<BoxCollider>();
        var point = volume.transform.TransformPoint(volume.center + Vector3.forward * (volume.size.z * 0.5f));
        ArcherLimbIK.ReachAbdomen(a, point, volume.transform.forward, 1f);
    }

    // Retargets the recorded fall and half-kneel rise around the visible supported seat.
    [MenuItem("Bow Man VR/Injuries/Rebuild Pelvis")]
    public static void RebuildPelvis()
    {
        var fall = ImportMotion("Falling Down");
        var rise = ImportMotion("Standing");
        const float recordedDrop = 0.9f;
        const float entryLength = 1.2f;
        const float kneelTransition = 0.65f;
        const float riseSpeed = 1.4f;
        const float recordedRise = 1.68f;
        const float recoveryLength = 2.15f;
        foreach (var name in new[] { "EnglishArcher", "ErikaArcher" })
        {
            var path = $"Assets/_Game/Prefabs/Characters/{name}.prefab";
            var root = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));
            var mesh = new Mesh();
            try
            {
                var animator = root.GetComponentInChildren<Animator>();
                var bones = animator.GetComponentsInChildren<Transform>().Where(t => t != animator.transform).ToArray();
                var positions = bones.Select(t => t.localPosition).ToArray();
                var rotations = bones.Select(t => t.localRotation).ToArray();
                var modelPosition = animator.transform.localPosition;
                var modelRotation = animator.transform.localRotation;
                var hips = animator.GetBoneTransform(HumanBodyBones.Hips);
                var neutralHip = hips.position;
                var leadBoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
                var rearKnee = animator.GetBoneTransform(HumanBodyBones.RightLowerLeg);
                var rearThigh = animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
                var rearBoot = animator.GetBoneTransform(HumanBodyBones.RightFoot);
                var standingBootHeight = leadBoot.position.y;
                var plantedRotation = leadBoot.rotation;
                var armBones = new[]
                {
                    new[] { HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand },
                    new[] { HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand }
                };
                var arms = armBones.Select(chain => chain.Select(animator.GetBoneTransform).ToArray()).ToArray();
                var hitRegions = root.GetComponentsInChildren<BodyHitRegion>();
                var bodyVolumes = CoreVolumes(hitRegions);
                var forearms = arms.Select(chain => ForearmVolumes(hitRegions, chain[1], chain[2])).ToArray();
                var rearSole = hitRegions.Single(hit => hit.Region == BodyRegion.RightFoot).GetComponent<Collider>();
                var hipVolume = hitRegions.Single(hit => hit.Region == BodyRegion.Pelvis).GetComponent<Collider>();

                FitPelvisSeat(root, bones, positions, rotations);
                var seatPositions = bones.Select(t => t.localPosition).ToArray();
                var seatRotations = bones.Select(t => t.localRotation).ToArray();
                var seatHip = hips.position;
                var seatPalms = arms.Select(chain => chain[2].position).ToArray();
                var seatPalmRotations = arms.Select(chain => chain[2].rotation).ToArray();
                SampleSource(fall, 0f);
                var fallStart = hips.position;
                SampleSource(fall, recordedDrop);
                var fallDistance = Vector3.ProjectOnPlane(hips.position - fallStart, root.transform.up);
                SampleSource(rise, recordedRise);
                var riseOffset = Vector3.ProjectOnPlane(neutralHip - hips.position, root.transform.up);
                SampleSource(rise, 0f);
                hips.position += riseOffset;
                Ground(root, hips, mesh, 0f);
                var kneelingPalms = arms.Select(chain => chain[2].position).ToArray();
                var plantedBoot = leadBoot.position;
                plantedBoot.y = standingBootHeight;

                var entry = Record(root, bones, entryLength, time =>
                {
                    if (time <= 0f) { Restore(bones, positions, rotations); return; }
                    if (time >= entryLength) { Restore(bones, seatPositions, seatRotations); return; }
                    var sourceTime = Mathf.Min(time, recordedDrop);
                    SampleSource(fall, sourceTime);
                    var descent = Mathf.InverseLerp(0f, recordedDrop, sourceTime);
                    var sway = Vector3.ProjectOnPlane(hips.position - fallStart, root.transform.up) - fallDistance * descent;
                    var planarHip = Vector3.Lerp(neutralHip, seatHip, descent) + sway * 0.35f;
                    hips.position = planarHip + root.transform.up * Vector3.Dot(hips.position - planarHip, root.transform.up);
                    BlendFrom(positions, rotations, Ease(time, 0f, 0.18f));
                    BlendFrom(seatPositions, seatRotations, 1f - Ease(time, 0.67f, entryLength));
                    var brace = Ease(time, 0.66f, 0.82f) * (1f - Ease(time, 0.92f, 1.14f));
                    MoveArm(0, arms[0][2].position - root.transform.right * (0.085f * brace),
                        arms[0][1].position - arms[0][0].position, arms[0][2].rotation);
                    ArcherLimbIK.ClearForearm(animator, true, forearms[0], bodyVolumes);
                    ArcherLimbIK.ClearForearm(animator, false, forearms[1], bodyVolumes);
                    Ground(root, hips, mesh, 0f);
                    // Allow for rotational interpolation as the hands swing down to brace.
                    hips.position += root.transform.up * (0.008f * Ease(time, 0.68f, 0.87f) * (1f - Ease(time, 0.95f, entryLength)));
                });
                var recovery = Record(root, bones, recoveryLength, time =>
                {
                    if (time <= 0f) { Restore(bones, seatPositions, seatRotations); return; }
                    if (time >= recoveryLength) { Restore(bones, positions, rotations); return; }
                    var sourceTime = Mathf.Clamp((time - kneelTransition) * riseSpeed, 0f, recordedRise);
                    SampleSource(rise, sourceTime);
                    hips.position += riseOffset;
                    BlendFrom(seatPositions, seatRotations, Ease(time, 0f, kneelTransition));
                    BlendFrom(positions, rotations, 1f - Ease(time, 1.7f, recoveryLength));
                    Ground(root, hips, mesh, 0f);
                    var plantWeight = Ease(time, 0.27f, kneelTransition) * (1f - Ease(sourceTime, 1.2f, 1.48f));
                    if (plantWeight > 0f)
                    {
                        var rotation = leadBoot.rotation;
                        ArcherLimbIK.Solve(animator, HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg,
                            HumanBodyBones.LeftFoot, Vector3.Lerp(leadBoot.position, plantedBoot, plantWeight), root.transform.forward);
                        leadBoot.rotation = Quaternion.Slerp(rotation, plantedRotation, plantWeight);
                    }

                    for (var side = 0; side < arms.Length; side++)
                    {
                        var wrist = arms[side][2];
                        var outward = root.transform.right * (side == 0 ? -1f : 1f);
                        var wristTarget = wrist.position;
                        var bend = arms[side][1].position - arms[side][0].position;
                        if (time < kneelTransition)
                        {
                            var release = Ease(time, side == 0 ? 0f : 0.2f, kneelTransition);
                            var arc = Mathf.Sin(Mathf.PI * release);
                            wristTarget = Vector3.Lerp(seatPalms[side], kneelingPalms[side], release)
                                + (outward * (side == 0 ? 0.38f : 0.32f) + root.transform.up * 0.05f) * arc;
                            if (side == 0)
                                wristTarget += (outward * 0.035f + root.transform.forward * 0.025f)
                                    * Ease(time, 0.30f, 0.45f) * (1f - Ease(time, 0.57f, kneelTransition));
                            bend = outward - root.transform.up * 0.45f;
                        }
                        if (side == 1)
                            wristTarget += outward * (0.10f * Ease(time, 0.42f, 0.78f) * (1f - Ease(time, 1.64f, 1.92f)));
                        var rotation = wrist.rotation;
                        if (side == 1)
                            rotation = Quaternion.Slerp(rotation, seatPalmRotations[side], 1f - Ease(time, 0.20f, 0.42f));
                        MoveArm(side, wristTarget, bend, rotation);
                        ArcherLimbIK.ClearForearm(animator, side == 0, forearms[side], bodyVolumes);
                    }
                    if (Physics.ComputePenetration(rearSole, rearSole.transform.position, rearSole.transform.rotation,
                        hipVolume, hipVolume.transform.position, hipVolume.transform.rotation, out var away, out var depth))
                    {
                        var rotation = rearBoot.rotation;
                        ArcherLimbIK.Solve(animator, HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg,
                            HumanBodyBones.RightFoot, rearBoot.position + away * (depth + 0.001f),
                            rearKnee.position - rearThigh.position);
                        rearBoot.rotation = rotation;
                    }
                    Ground(root, hips, mesh, 0f);
                });
                entry.name = "Pelvis Entry";
                recovery.name = "Pelvis Recovery";
                var folder = $"Assets/_Game/Animations/Injuries/{name}";
                entry = SaveClip(entry, $"{folder}/Pelvis_Entry.anim");
                recovery = SaveClip(recovery, $"{folder}/Pelvis_Recovery.anim");
                var prefab = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    prefab.GetComponent<ArcherReactionAnimation>().SetSequence(BodyRegion.Pelvis, entry, recovery);
                    PrefabUtility.SaveAsPrefabAsset(prefab, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(prefab); }

                // Samples the retargeted source without translating individual limb bones.
                void SampleSource(AnimationClip source, float time)
                {
                    Restore(bones, positions, rotations);
                    Sample(animator, source, time, modelPosition, modelRotation);
                    for (var i = 0; i < bones.Length; i++)
                        if (bones[i] != hips) bones[i].localPosition = positions[i];
                }

                // Moves the wrist along its brace path and retains the chosen palm orientation.
                void MoveArm(int side, Vector3 target, Vector3 bend, Quaternion rotation)
                {
                    var wrist = arms[side][2];
                    if (target != wrist.position)
                        ArcherLimbIK.Solve(animator, armBones[side][0], armBones[side][1], armBones[side][2], target, bend);
                    wrist.rotation = rotation;
                }

                // Transitions from a measured pose to the current source sample.
                void BlendFrom(Vector3[] startPositions, Quaternion[] startRotations, float weight)
                {
                    for (var i = 0; i < bones.Length; i++)
                    {
                        bones[i].localPosition = Vector3.Lerp(startPositions[i], bones[i].localPosition, weight);
                        bones[i].localRotation = Quaternion.Slerp(startRotations[i], bones[i].localRotation, weight);
                    }
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(mesh);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
        RebuildGroundedFinisher(BodyRegion.Pelvis);
        AssetDatabase.SaveAssets();
    }

    // Fits a reclined seat to the visible hips, raised knees, and both rear palms.
    private static void FitPelvisSeat(GameObject root, Transform[] bones, Vector3[] positions, Quaternion[] rotations)
    {
        var hips = root.GetComponentInChildren<Animator>().GetBoneTransform(HumanBodyBones.Hips);
        var seatHeight = 0.18f;
        for (var fit = 0; fit < 3; fit++)
        {
            Restore(bones, positions, rotations);
            PelvisSeat(root, seatHeight);
            seatHeight -= NearbyFloor(root, hips.position, true) - root.transform.position.y;
        }
        Restore(bones, positions, rotations);
        PelvisSeat(root, seatHeight);
    }

    // Places the seated hips and feet using each rig's measured leg lengths.
    private static void PelvisSeat(GameObject root, float seatHeight)
    {
        var a = root.GetComponentInChildren<Animator>();
        var hips = a.GetBoneTransform(HumanBodyBones.Hips);
        var feet = new[] { a.GetBoneTransform(HumanBodyBones.LeftFoot), a.GetBoneTransform(HumanBodyBones.RightFoot) };
        var positions = feet.Select(t => t.position).ToArray();
        var rotations = feet.Select(t => t.rotation).ToArray();
        hips.position += root.transform.up * (root.transform.position.y + seatHeight - hips.position.y)
            - root.transform.forward * 0.15f;
        hips.rotation = Quaternion.AngleAxis(-45f, root.transform.right) * hips.rotation;
        for (var i = 0; i < 2; i++)
        {
            var target = positions[i] + root.transform.forward * 0.45f + root.transform.up * 0.012f;
            ArcherLimbIK.Solve(a, i == 0 ? HumanBodyBones.LeftUpperLeg : HumanBodyBones.RightUpperLeg,
                i == 0 ? HumanBodyBones.LeftLowerLeg : HumanBodyBones.RightLowerLeg,
                i == 0 ? HumanBodyBones.LeftFoot : HumanBodyBones.RightFoot, target, root.transform.forward);
            feet[i].rotation = Quaternion.AngleAxis(-8f, root.transform.right) * rotations[i];
        }
        GroundPalm(root, a, true, 1f, -0.08f);
        GroundPalm(root, a, false, 1f, -0.08f);
    }

    // Places a fitted support palm on the floor in front of or behind its shoulder.
    private static void GroundPalm(GameObject root, Animator a, bool left, float progress, float forwardOffset,
        float sideOffset = 0.06f)
    {
        var hand = a.GetBoneTransform(left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
        var shoulder = a.GetBoneTransform(left ? HumanBodyBones.LeftUpperArm : HumanBodyBones.RightUpperArm);
        var support = shoulder.position + root.transform.right * (left ? -sideOffset : sideOffset) + root.transform.forward * forwardOffset;
        support.y = root.transform.position.y + 0.03f;
        ArcherLimbIK.Solve(a, left ? HumanBodyBones.LeftUpperArm : HumanBodyBones.RightUpperArm,
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
            ArcherLimbIK.Solve(animator, left ? HumanBodyBones.LeftUpperArm : HumanBodyBones.RightUpperArm,
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
        finally { UnityEngine.Object.DestroyImmediate(mesh); }
        if (float.IsInfinity(floor)) throw new InvalidOperationException("No visible skin near the support joint.");
        return floor;
    }

    // Eases into the next contact without changing the sampled motion's time.
    private static float Ease(float time, float start, float end) =>
        Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(start, end, time));

    // Retargets the chest fall onto each owned skeleton and preserves its other injury clips.
    [MenuItem("Bow Man VR/Injuries/Rebuild Chest Fall")]
    public static void RebuildChestFall()
    {
        RebuildFall(BodyRegion.Chest, 0.5f, 2.7f, 1f);
        RebuildGroundedFinisher(BodyRegion.Chest);
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
                var leftShoulder = animator.GetBoneTransform(HumanBodyBones.LeftShoulder);
                var rightShoulder = animator.GetBoneTransform(HumanBodyBones.RightShoulder);
                var armBones = bones.Select(bone => bone.IsChildOf(leftShoulder) || bone.IsChildOf(rightShoulder)).ToArray();
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
                        var ready = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(3.72f, 5.4f, time));
                        // Let both arms drop after the ground push, before the source raises its guard.
                        var relaxArms = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(2.95f, 3.8f, time));
                        for (var i = 0; i < bones.Length; i++)
                        {
                            bones[i].localPosition = Vector3.Lerp(fallenPositions[i], bones[i].localPosition, arrival);
                            bones[i].localRotation = Quaternion.Slerp(fallenRotations[i], bones[i].localRotation, arrival);
                            var neutralWeight = armBones[i] ? Mathf.Max(ready, relaxArms) : ready;
                            bones[i].localPosition = Vector3.Lerp(bones[i].localPosition, neutralPositions[i], neutralWeight);
                            bones[i].localRotation = Quaternion.Slerp(bones[i].localRotation, neutralRotations[i], neutralWeight);
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

    // Relaxes the archer from a chest landing or supported seat into the final back-down pose.
    private static void RebuildGroundedFinisher(BodyRegion region)
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
                var entry = player.Entry(region);
                entry.SampleAnimation(root, entry.length);
                var positions = bones.Select(t => t.localPosition).ToArray();
                var rotations = bones.Select(t => t.localRotation).ToArray();
                var start = hips.position;

                var collapse = player.Entry(BodyRegion.Head);
                collapse.SampleAnimation(root, collapse.length);
                hips.position = new Vector3(start.x, hips.position.y, start.z);
                if (region == BodyRegion.Pelvis) hips.position -= root.transform.forward * 0.2f;
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
        AssetDatabase.SaveAssets();
    }

    // Authors a rapid loss of posture and relaxed head separately from the surviving chest sequence.
    [MenuItem("Bow Man VR/Injuries/Rebuild Head Collapse")]
    public static void RebuildHeadCollapse() => RebuildFall(BodyRegion.Head, 0.8f, 1f, 2.5f);

    // Authors a wound reach followed by buckling knees and a fatal collapse, without a recovery clip.
    [MenuItem("Bow Man VR/Injuries/Rebuild Neck Collapse")]
    public static void RebuildNeckCollapse() => RebuildFall(BodyRegion.Neck, 0.8f, 2.4f, 1.3f);

    // Returns the central colliders used to check nonadjacent arm penetration.
    internal static Collider[] CoreVolumes(BodyHitRegion[] regions) =>
        regions.Where(hit => hit.Region is BodyRegion.Head or BodyRegion.Neck or BodyRegion.Chest or
            BodyRegion.Abdomen or BodyRegion.Pelvis).Select(hit => hit.GetComponent<Collider>()).ToArray();

    // Returns the hand and forearm colliders attached to the two distal joints.
    internal static Collider[] ForearmVolumes(BodyHitRegion[] regions, Transform elbow, Transform wrist) =>
        regions.Where(hit => hit.transform.parent == elbow || hit.transform.parent == wrist)
            .Select(hit => hit.GetComponent<Collider>()).ToArray();

    // Bakes the recorded left-shoulder fall and its Humanoid mirror onto both archers.
    [MenuItem("Bow Man VR/Injuries/Rebuild Fatal Shoulders")]
    public static void RebuildFatalShoulders()
    {
        var source = ImportMotion("Shoulder Hit And Fall");
        RebuildFall(BodyRegion.LeftShoulder, 0f, source.length, 1f, source);
        RebuildFall(BodyRegion.RightShoulder, 0f, source.length, 1f, source);
    }

    // Keeps mapped joints in parent-first order for anatomical reflection.
    private static Transform[] HumanoidBones(Animator animator)
    {
        var mapped = Enumerable.Range(0, (int)HumanBodyBones.LastBone)
            .Select(i => animator.GetBoneTransform((HumanBodyBones)i)).ToHashSet();
        return animator.GetComponentsInChildren<Transform>().Where(mapped.Contains).ToArray();
    }

    // Pairs each joint with its opposite side, preserving midline joints.
    private static int[] OppositeBoneIndices(Animator animator, Transform[] bones)
    {
        var ids = Enumerable.Range(0, (int)HumanBodyBones.LastBone).Select(i => (HumanBodyBones)i)
            .Where(id => animator.GetBoneTransform(id) != null).ToDictionary(animator.GetBoneTransform);
        return bones.Select(bone =>
        {
            var name = ids[bone].ToString();
            var opposite = name.StartsWith("Right") ? name.Replace("Right", "Left") : name.Replace("Left", "Right");
            return Array.IndexOf(bones, animator.GetBoneTransform(Enum.Parse<HumanBodyBones>(opposite)));
        }).ToArray();
    }

    // Reflects recorded motion around the character's sagittal plane in each joint's own neutral frame.
    private static void MirrorBody(Transform root, Transform hips, Vector3 hipStart, Transform[] bones,
        Vector3[] positions, Quaternion[] rotations, Quaternion[] worldRotations, int[] mirrored)
    {
        var sampled = bones.Select(t => t.rotation).ToArray();
        var offset = root.InverseTransformVector(hips.position - hipStart);
        offset.x = -offset.x;
        Restore(bones, positions, rotations);
        hips.position = hipStart + root.TransformVector(offset);
        for (var i = 0; i < bones.Length; i++)
        {
            var other = mirrored[i];
            var delta = Quaternion.Inverse(root.rotation) * sampled[other]
                * Quaternion.Inverse(worldRotations[other]) * root.rotation;
            delta = new Quaternion(delta.x, -delta.y, -delta.z, delta.w);
            bones[i].rotation = root.rotation * delta
                * Quaternion.Inverse(root.rotation) * worldRotations[i];
        }
    }

    // Fits the recorded arm paths around the model's chest while the supporting fingers reach the shoulder cap.
    private static void FitShoulderArm(Animator animator, BoxCollider chestVolume, bool left, bool support, float time)
    {
        var chest = chestVolume.transform;
        var forward = chest.forward;
        var up = chest.up;
        var right = chest.right;
        var reach = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.53f, 0.83f, time));
        var upper = left ? HumanBodyBones.LeftUpperArm : HumanBodyBones.RightUpperArm;
        var lower = left ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm;
        var hand = left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand;
        var wrist = animator.GetBoneTransform(hand);
        var elbow = animator.GetBoneTransform(lower);
        var arm = animator.GetBoneTransform(upper);
        var orientation = wrist.rotation;
        var target = wrist.position;
        var lead = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.18f, 0.55f, time));
        var hint = elbow.position - arm.position;
        if (support)
        {
            var finger = animator.GetBoneTransform(left ? HumanBodyBones.LeftIndexDistal : HumanBodyBones.RightIndexDistal);
            var cap = animator.GetBoneTransform(left ? HumanBodyBones.RightUpperArm : HumanBodyBones.LeftUpperArm);
            var contact = cap.position + forward * 0.065f + up * 0.05f + right * (left ? 0.025f : -0.025f);
            var foldedWrist = contact + forward * 0.15f + up * 0.065f + right * (left ? -0.10f : 0.10f);
            target = Vector3.Lerp(target, foldedWrist, reach);
            var frontDepth = chestVolume.size.z * 0.5f + 0.08f - 0.025f * reach +
                0.055f * Mathf.Sin(reach * Mathf.PI);
            var local = chest.InverseTransformPoint(target);
            target += forward * (Mathf.Max(0f, frontDepth - local.z) * lead);
            var turn = Quaternion.FromToRotation(finger.position - wrist.position, contact - target);
            orientation = Quaternion.Slerp(Quaternion.identity, turn, reach) * orientation;
            hint = Vector3.Lerp(hint, forward * 0.27f - up * 0.13f, lead);
        }
        else
        {
            var outward = left ? -1f : 1f;
            var local = chest.InverseTransformPoint(target);
            var sideClearance = chestVolume.size.x * 0.5f + 0.095f;
            target += right * (outward * Mathf.Max(0f, sideClearance - outward * local.x) * lead);
            hint += right * (outward * 0.10f * lead);
        }
        ArcherLimbIK.Solve(animator, upper, lower, hand, target, hint);
        wrist.rotation = orientation;
    }

    // Bakes the region's own fall timing and head posture onto each target skeleton.
    private static void RebuildFall(BodyRegion region, float sourceStart, float duration, float speed, AnimationClip source = null)
    {
        source ??= ImportMotion("Dying Backwards");
        var shoulder = region is BodyRegion.LeftShoulder or BodyRegion.RightShoulder;
        foreach (var name in new[] { "EnglishArcher", "ErikaArcher" })
        {
            var path = $"Assets/_Game/Prefabs/Characters/{name}.prefab";
            var root = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));
            try
            {
                var animator = root.GetComponentInChildren<Animator>();
                var bones = shoulder ? HumanoidBones(animator) :
                    animator.GetComponentsInChildren<Transform>().Where(t => t != animator.transform).ToArray();
                var rightShoulder = region == BodyRegion.RightShoulder;
                var opposite = rightShoulder ? OppositeBoneIndices(animator, bones) : null;
                var worldRotations = rightShoulder ? bones.Select(t => t.rotation).ToArray() : null;
                var regions = root.GetComponentsInChildren<BodyHitRegion>();
                var core = shoulder ? CoreVolumes(regions) : null;
                var leftArm = shoulder ? ForearmVolumes(regions, animator.GetBoneTransform(HumanBodyBones.LeftLowerArm),
                    animator.GetBoneTransform(HumanBodyBones.LeftHand)) : null;
                var rightArm = shoulder ? ForearmVolumes(regions, animator.GetBoneTransform(HumanBodyBones.RightLowerArm),
                    animator.GetBoneTransform(HumanBodyBones.RightHand)) : null;
                if (shoulder)
                {
                    // The extra baking clearance keeps the forearm clear of the face between sampled poses.
                    var head = regions.Single(r => r.Region == BodyRegion.Head).GetComponent<BoxCollider>();
                    head.size += Vector3.one * 0.024f;
                }
                var chestVolume = shoulder ? (BoxCollider)regions.Single(r => r.Region == BodyRegion.Chest).GetComponent<Collider>() : null;
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
                        if (rightShoulder)
                            MirrorBody(root.transform, hips, neutralHips, bones, positions, rotations, worldRotations, opposite);
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
                        if (shoulder)
                        {
                            FitShoulderArm(animator, chestVolume, true, rightShoulder, time);
                            FitShoulderArm(animator, chestVolume, false, !rightShoulder, time);
                            ArcherLimbIK.ClearForearm(animator, true, leftArm, core, 14);
                            ArcherLimbIK.ClearForearm(animator, false, rightArm, core, 14);
                        }
                        var clearance = region == BodyRegion.Head && time > 0f && time < duration ? 0.004f : 0f;
                        Ground(root, hips, mesh, clearance);
                    });
                }
                finally { UnityEngine.Object.DestroyImmediate(mesh); }
                var phase = shoulder ? "Fatal" : "Entry";
                clip.name = region + " " + phase;
                var folder = $"Assets/_Game/Animations/Injuries/{name}";
                clip = SaveClip(clip, $"{folder}/{region}_{phase}.anim");
                Restore(bones, positions, rotations);
                var prefab = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var player = prefab.GetComponent<ArcherReactionAnimation>();
                    if (shoulder) player.SetFatalShoulder(region, clip);
                    else player.SetSequence(region, clip, player.Recovery(region));
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
        RebuildProneFinisher(region);
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

    // Bakes the transition from a supported injury pose into its fatal forward collapse.
    private static void RebuildProneFinisher(BodyRegion region)
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
                var regions = root.GetComponentsInChildren<BodyHitRegion>();
                var core = CoreVolumes(regions);
                var leftArm = ForearmVolumes(regions, animator.GetBoneTransform(HumanBodyBones.LeftLowerArm), animator.GetBoneTransform(HumanBodyBones.LeftHand));
                var rightArm = ForearmVolumes(regions, animator.GetBoneTransform(HumanBodyBones.RightLowerArm), animator.GetBoneTransform(HumanBodyBones.RightHand));
                const float duration = 0.8f;
                var clip = Record(root, bones, duration, time =>
                {
                    var progress = Mathf.SmoothStep(0f, 1f, time / duration);
                    for (var i = 0; i < bones.Length; i++)
                    {
                        bones[i].localPosition = Vector3.Lerp(positions[i], targetPositions[i], progress);
                        bones[i].localRotation = Quaternion.Slerp(rotations[i], targetRotations[i], progress);
                    }
                    if (region is BodyRegion.Abdomen or BodyRegion.LeftLeg or BodyRegion.RightLeg)
                    {
                        ArcherLimbIK.ClearForearm(animator, false, rightArm, core);
                        ArcherLimbIK.ClearForearm(animator, true, leftArm, core);
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
        // Playback samples transform paths from the prefab root, above its Humanoid Animator.
        clip.legacy = true;
        var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (existing == null) { AssetDatabase.CreateAsset(clip, path); return clip; }
        EditorUtility.CopySerialized(clip, existing);
        UnityEngine.Object.DestroyImmediate(clip);
        EditorUtility.SetDirty(existing);
        AssetDatabase.SaveAssets();
        return existing;
    }
}
