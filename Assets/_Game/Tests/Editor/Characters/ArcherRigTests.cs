using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public class ArcherRigTests
{
    // Opens the project-owned prefab in an isolated preview scene.
    private static GameObject Load(string name) =>
        PrefabUtility.LoadPrefabContents($"Assets/_Game/Prefabs/Characters/{name}.prefab");

    // Reads posed skin vertices in the prefab's root space for geometric checks.
    private static Vector3[] Vertices(GameObject root, SkinnedMeshRenderer skin)
    {
        var mesh = new Mesh();
        try
        {
            skin.BakeMesh(mesh, true);
            return mesh.vertices.Select(v => root.transform.InverseTransformPoint(skin.transform.TransformPoint(v))).ToArray();
        }
        finally { Object.DestroyImmediate(mesh); }
    }

    // Combines every skin section without depending on cached renderer bounds.
    private static Vector3[] Vertices(GameObject root) =>
        root.GetComponentsInChildren<SkinnedMeshRenderer>().SelectMany(skin => Vertices(root, skin)).ToArray();

    // Measures the visible posed body including clothing and rigid equipment.
    private static Bounds BoundsOf(GameObject root)
    {
        var vertices = Vertices(root);
        Assert.That(vertices, Is.Not.Empty);
        var bounds = new Bounds(vertices[0], Vector3.zero);
        foreach (var vertex in vertices) bounds.Encapsulate(vertex);
        return bounds;
    }

    // Samples the shared Humanoid check through the same controller used by the review scene.
    private static void Sample(Animator animator, float seconds)
    {
        animator.Play("Rig Mobility Check", 0, seconds / 12f);
        animator.Update(0f);
    }

    // Checks whether a vertex follows this finger joint or any of its children.
    private static bool Influenced(BoneWeight weight, bool[] family) =>
        weight.weight0 > 0.01f && family[weight.boneIndex0] ||
        weight.weight1 > 0.01f && family[weight.boneIndex1] ||
        weight.weight2 > 0.01f && family[weight.boneIndex2] ||
        weight.weight3 > 0.01f && family[weight.boneIndex3];

    // Verifies complete body/finger mappings and finite normalized skin bindings on both models.
    [TestCase("EnglishArcher")]
    [TestCase("ErikaArcher")]
    public void HumanoidAndSkinAreComplete(string name)
    {
        var root = Load(name);
        try
        {
            var animator = root.GetComponentInChildren<Animator>();
            Assert.That(animator, Is.Not.Null);
            Assert.That(animator.avatar.isValid && animator.avatar.isHuman, Is.True);
            for (var i = 0; i < (int)HumanBodyBones.LastBone; i++)
            {
                var bone = (HumanBodyBones)i;
                if (bone != HumanBodyBones.Jaw)
                    Assert.That(animator.GetBoneTransform(bone), Is.Not.Null, bone.ToString());
            }
            var skins = root.GetComponentsInChildren<SkinnedMeshRenderer>();
            Assert.That(skins, Is.Not.Empty);
            foreach (var skin in skins)
            {
                Assert.That(skin.bones.All(bone => bone != null), Is.True);
                Assert.That(skin.sharedMaterials.All(material => material != null), Is.True);
                var mesh = skin.sharedMesh;
                Assert.That(mesh.bindposes.Length, Is.EqualTo(skin.bones.Length));
                Assert.That(mesh.boneWeights.Length, Is.EqualTo(mesh.vertexCount));
                foreach (var weight in mesh.boneWeights)
                {
                    Assert.That(weight.weight0 + weight.weight1 + weight.weight2 + weight.weight3,
                        Is.EqualTo(1f).Within(0.001f));
                    foreach (var index in new[] { weight.boneIndex0, weight.boneIndex1, weight.boneIndex2, weight.boneIndex3 })
                        Assert.That(index, Is.InRange(0, skin.bones.Length - 1));
                }
            }
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    // Verifies physical scale, floor contact, anatomical sides and the calibrated eye reference.
    [TestCase("EnglishArcher")]
    [TestCase("ErikaArcher")]
    public void NeutralPoseMatchesScaleFloorAndEyes(string name)
    {
        var root = Load(name);
        try
        {
            var animator = root.GetComponentInChildren<Animator>();
            var bounds = BoundsOf(root);
            Assert.That(bounds.size.y, Is.EqualTo(1.75f).Within(0.002f));
            Assert.That(bounds.min.y, Is.EqualTo(0f).Within(0.001f));
            foreach (var part in new[] { "Shoulder", "Hand", "Foot" })
            {
                var left = animator.GetBoneTransform((HumanBodyBones)Enum.Parse(typeof(HumanBodyBones), "Left" + part));
                var right = animator.GetBoneTransform((HumanBodyBones)Enum.Parse(typeof(HumanBodyBones), "Right" + part));
                Assert.That(root.transform.InverseTransformPoint(left.position).x,
                    Is.LessThan(root.transform.InverseTransformPoint(right.position).x), part);
            }
            var head = animator.GetBoneTransform(HumanBodyBones.Head);
            var eyes = head.Find("EyeAnchor");
            Assert.That(eyes, Is.Not.Null);
            var midpoint = (animator.GetBoneTransform(HumanBodyBones.LeftEye).position +
                animator.GetBoneTransform(HumanBodyBones.RightEye).position) * 0.5f;
            Assert.That(Vector3.Distance(eyes.position, midpoint), Is.LessThan(0.001f));
            Assert.That(Vector3.Dot(eyes.forward, root.transform.forward), Is.GreaterThan(0.99f));
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    // Catches mapped but unskinned fingers and weights that pull unrelated body sections.
    [TestCase("EnglishArcher")]
    [TestCase("ErikaArcher")]
    public void FingerBonesDeformTheirOwnHands(string name)
    {
        var root = Load(name);
        try
        {
            var animator = root.GetComponentInChildren<Animator>();
            var skins = root.GetComponentsInChildren<SkinnedMeshRenderer>();
            var points = skins.ToDictionary(skin => skin, skin => Vertices(root, skin));
            foreach (var side in new[] { "Left", "Right" })
            foreach (var digit in new[] { "Thumb", "Index", "Middle", "Ring", "Little" })
            foreach (var segment in new[] { "Proximal", "Intermediate", "Distal" })
            {
                var bone = (HumanBodyBones)Enum.Parse(typeof(HumanBodyBones), side + digit + segment);
                var finger = animator.GetBoneTransform(bone);
                var hand = animator.GetBoneTransform((HumanBodyBones)Enum.Parse(typeof(HumanBodyBones), side + "Hand"));
                var handPosition = root.transform.InverseTransformPoint(hand.position);
                var affected = 0;
                foreach (var skin in skins)
                {
                    var family = skin.bones.Select(t => t.IsChildOf(finger)).ToArray();
                    var weights = skin.sharedMesh.boneWeights;
                    for (var i = 0; i < weights.Length; i++)
                    {
                        if (!Influenced(weights[i], family)) continue;
                        affected++;
                        Assert.That(Vector3.Distance(points[skin][i], handPosition), Is.LessThan(0.30f), bone.ToString());
                    }
                }
                Assert.That(affected, Is.GreaterThan(0), bone.ToString());
            }
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    // Verifies visible retargeted movement and a planted supporting foot through the shared clip.
    [TestCase("EnglishArcher")]
    [TestCase("ErikaArcher")]
    public void SharedClipMovesBodyAndKeepsSupportFootPlanted(string name)
    {
        var root = Load(name);
        try
        {
            var animator = root.GetComponentInChildren<Animator>();
            animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                "Assets/_Game/Animations/RigMobilityCheck.controller");
            Assert.That(animator.runtimeAnimatorController, Is.Not.Null);
            Assert.That(animator.runtimeAnimatorController.animationClips.Single().humanMotion, Is.True);
            animator.enabled = true;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.Rebind();
            animator.Update(0f);
            Sample(animator, 0f);
            var eyes = animator.GetBoneTransform(HumanBodyBones.Head).Find("EyeAnchor");
            var eyeStart = eyes.position;
            var hand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            var handStart = hand.position;
            var leftFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            var rightFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot);
            var leftStart = leftFoot.position;
            var rightStart = rightFoot.position;
            var finger = animator.GetBoneTransform(HumanBodyBones.LeftIndexDistal);
            var fingerStart = finger.localRotation;
            var vertices = Vertices(root);

            Sample(animator, 2f);
            Assert.That(Vector3.Distance(eyes.position, eyeStart), Is.GreaterThan(0.02f));
            Sample(animator, 4f);
            Assert.That(Vector3.Distance(hand.position, handStart), Is.GreaterThan(0.12f));
            Sample(animator, 6f);
            Assert.That(Quaternion.Angle(finger.localRotation, fingerStart), Is.GreaterThan(10f));
            var curled = Vertices(root);
            Assert.That(vertices.Where((v, i) => Vector3.Distance(v, curled[i]) > 0.01f).Count(), Is.GreaterThan(50));

            Sample(animator, 8f);
            Assert.That(leftFoot.position.y - leftStart.y, Is.GreaterThan(0.06f));
            Assert.That(Vector3.Distance(rightFoot.position, rightStart), Is.LessThan(0.002f));
            Assert.That(Mathf.Abs(BoundsOf(root).min.y), Is.LessThan(0.004f));
            Sample(animator, 10f);
            Assert.That(rightFoot.position.y - rightStart.y, Is.GreaterThan(0.06f));
            Assert.That(Vector3.Distance(leftFoot.position, leftStart), Is.LessThan(0.002f));
            Assert.That(Mathf.Abs(BoundsOf(root).min.y), Is.LessThan(0.004f));
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
}
