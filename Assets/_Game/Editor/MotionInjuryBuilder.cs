using System;
using System.Linq;
using BowMan;
using UnityEditor;
using UnityEngine;

public static class MotionInjuryBuilder
{
    private const string MotionFolder = "Assets/ThirdParty/MixamoAnimations/Motions/";

    // Retargets the recorded fall and half-kneel rise around the visible supported seat.
    internal static void RebuildPelvis()
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

                InjuryAnimationBuilder.FitPelvisSeat(root, bones, positions, rotations);
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
                Ground(root, hips, mesh);
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
                    Ground(root, hips, mesh);
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
                    Ground(root, hips, mesh);
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
                    Ground(root, hips, mesh);
                });
                entry.name = "Pelvis Entry";
                recovery.name = "Pelvis Recovery";
                var folder = $"Assets/_Game/Animations/Injuries/{name}";
                entry = InjuryAnimationBuilder.SaveClip(entry, $"{folder}/Pelvis_Entry.anim");
                recovery = InjuryAnimationBuilder.SaveClip(recovery, $"{folder}/Pelvis_Recovery.anim");
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
        RebuildDeathFinishers(BodyRegion.Pelvis);
        AssetDatabase.SaveAssets();
    }

    // Blends captured weight transfer with a fitted double kneel and keeps the hands at the wound.
    internal static void RebuildAbdomen()
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

                InjuryAnimationBuilder.FitAbdomenKneel(root, bones, positions, neutral);
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
                entry = InjuryAnimationBuilder.SaveClip(entry, $"{folder}/Abdomen_Entry.anim");
                recovery = InjuryAnimationBuilder.SaveClip(recovery, $"{folder}/Abdomen_Recovery.anim");
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
                    Ground(root, hips, mesh);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(mesh);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
        RebuildDeathFinishers(BodyRegion.Abdomen);
        AssetDatabase.SaveAssets();
    }

    // Eases into the next contact without changing the sampled motion's time.
    private static float Ease(float time, float start, float end) =>
        Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(start, end, time));

    // Fits the complete downloaded foot performance, with planted support and model-specific transitions.
    internal static void RebuildFoot(BodyRegion region)
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
            finally
            {
                UnityEngine.Object.DestroyImmediate(contactMesh);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
        RebuildDeathFinishers(region);
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

    // Bakes each body region's own transition from its injured pose into a complete fatal collapse.
    [MenuItem("Bow Man VR/Injuries/Rebuild Body Death Finishers")]
    public static void RebuildDeathFinishers() => RebuildDeathFinishers(null);

    // Replaces only the requested region's collapse, or all body collapses for a complete rebuild.
    internal static void RebuildDeathFinishers(BodyRegion? onlyRegion)
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
                var hitRegions = root.GetComponentsInChildren<BodyHitRegion>();
                var bodyVolumes = CoreVolumes(hitRegions);
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
                    var fitBothArms = region is BodyRegion.LeftArm or BodyRegion.RightArm or
                        BodyRegion.LeftHand or BodyRegion.RightHand or BodyRegion.Abdomen or
                        BodyRegion.LeftLeg or BodyRegion.RightLeg;
                    var supportLeft = region is BodyRegion.RightShoulder or BodyRegion.RightArm;
                    Collider[] supportVolumes = null;
                    Collider[] injuredVolumes = null;
                    if (fitBothArms || region is BodyRegion.LeftShoulder or BodyRegion.RightShoulder)
                    {
                        var elbow = animator.GetBoneTransform(supportLeft ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm);
                        var wrist = animator.GetBoneTransform(supportLeft ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
                        supportVolumes = ForearmVolumes(hitRegions, elbow, wrist);
                        if (fitBothArms)
                        {
                            var injuredElbow = animator.GetBoneTransform(supportLeft ? HumanBodyBones.RightLowerArm : HumanBodyBones.LeftLowerArm);
                            var injuredWrist = animator.GetBoneTransform(supportLeft ? HumanBodyBones.RightHand : HumanBodyBones.LeftHand);
                            injuredVolumes = ForearmVolumes(hitRegions, injuredElbow, injuredWrist);
                        }
                    }
                    var clip = Record(root, bones, 0.8f, time =>
                    {
                        var progress = Mathf.SmoothStep(0f, 1f, time / 0.8f);
                        for (var i = 0; i < bones.Length; i++)
                        {
                            bones[i].localPosition = Vector3.Lerp(positions[i], targetPositions[i], progress);
                            bones[i].localRotation = Quaternion.Slerp(rotations[i], targetRotations[i], progress);
                        }
                        if (time <= 0f) return;
                        if (supportVolumes != null)
                            ArcherLimbIK.ClearForearm(animator, supportLeft, supportVolumes, bodyVolumes);
                        if (injuredVolumes != null)
                            ArcherLimbIK.ClearForearm(animator, !supportLeft, injuredVolumes, bodyVolumes);
                        Ground(root, hips, mesh);
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

    // Returns the central colliders used to check nonadjacent arm penetration.
    internal static Collider[] CoreVolumes(BodyHitRegion[] regions) =>
        regions.Where(hit => hit.Region is BodyRegion.Head or BodyRegion.Neck or BodyRegion.Chest or
            BodyRegion.Abdomen or BodyRegion.Pelvis).Select(hit => hit.GetComponent<Collider>()).ToArray();

    // Returns the hand and forearm colliders attached to the two distal joints.
    internal static Collider[] ForearmVolumes(BodyHitRegion[] regions, Transform elbow, Transform wrist) =>
        regions.Where(hit => hit.transform.parent == elbow || hit.transform.parent == wrist)
            .Select(hit => hit.GetComponent<Collider>()).ToArray();

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
                        var clearance = region == BodyRegion.Head && time > 0f && time < duration ? 0.004f : 0f;
                        Ground(root, hips, mesh, clearance);
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

    // Fits the sampled visible skin and equipment to the floor, with a small margin during headshot interpolation.
    private static void Ground(GameObject root, Transform hips, Mesh mesh, float clearance = 0f)
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

    // Bakes complete transform groups at sixty Hz to retain fitted contacts between source frames.
    private static AnimationClip Record(GameObject root, Transform[] bones, float duration, Action<float> pose)
    {
        var clip = new AnimationClip { frameRate = 60f };
        var curves = bones.Select(_ => Enumerable.Range(0, 7).Select(i => new AnimationCurve()).ToArray()).ToArray();
        var frames = Mathf.RoundToInt(duration * 60f);
        for (var frame = 0; frame <= frames; frame++)
        {
            var time = frame / 60f;
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
}
