using System;
using BowMan;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class ArcherHitboxBuilder
{
    // Rebuilds project-owned volumes on both prepared character prefabs without editing imported art.
    [MenuItem("Bow Man VR/Characters/Rebuild Body Hitboxes")]
    public static void Rebuild()
    {
        foreach (var name in new[] { "EnglishArcher", "ErikaArcher" })
        {
            var path = $"Assets/_Game/Prefabs/Characters/{name}.prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                foreach (var region in root.GetComponentsInChildren<BodyHitRegion>())
                    Object.DestroyImmediate(region.gameObject);
                var body = root.GetComponent<Rigidbody>();
                if (body == null) body = root.AddComponent<Rigidbody>();
                body.isKinematic = true;
                body.useGravity = false;
                Build(root);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }

    // Fits neutral anatomical divisions to each skeleton; dimensions are provisional review volumes in metres.
    private static void Build(GameObject root)
    {
        var animator = root.GetComponentInChildren<Animator>();
        if (animator == null || !animator.isHuman) throw new InvalidOperationException("A Humanoid rig is required.");
        var axis = root.transform;
        var hips = animator.GetBoneTransform(HumanBodyBones.Hips);
        var spine = animator.GetBoneTransform(HumanBodyBones.Spine);
        var chest = animator.GetBoneTransform(HumanBodyBones.Chest);
        var upper = animator.GetBoneTransform(HumanBodyBones.UpperChest);
        var neck = animator.GetBoneTransform(HumanBodyBones.Neck);
        var head = animator.GetBoneTransform(HumanBodyBones.Head);
        var split = (spine.position.y + chest.position.y) * 0.5f;
        var waist = spine.position.y - 0.045f;
        Box(BodyRegion.Pelvis, hips, axis, waist - 0.18f, waist, 0.27f, 0.19f, 0.025f);
        Box(BodyRegion.Abdomen, spine, axis, waist, split, 0.27f, 0.19f, 0.025f);
        Box(BodyRegion.Chest, upper, axis, split, neck.position.y - 0.025f, 0.29f, 0.21f, 0.035f);
        Box(BodyRegion.Neck, neck, axis, neck.position.y - 0.025f, head.position.y, 0.095f, 0.11f, 0.025f);
        Box(BodyRegion.Head, head, axis, head.position.y, 1.75f, 0.18f, 0.20f, 0.045f);

        foreach (var side in new[] { "Left", "Right" })
        {
            var arm = Bone(animator, side, "UpperArm");
            var elbow = Bone(animator, side, "LowerArm");
            var hand = Bone(animator, side, "Hand");
            var thigh = Bone(animator, side, "UpperLeg");
            var knee = Bone(animator, side, "LowerLeg");
            var foot = Bone(animator, side, "Foot");
            var toes = Bone(animator, side, "Toes");
            var shoulderEnd = Vector3.Lerp(arm.position, elbow.position, 0.18f);
            Segment(Parse(side + "Shoulder"), arm, arm.position, shoulderEnd, 0.061f);
            Segment(Parse(side + "Arm"), arm, shoulderEnd, elbow.position, 0.051f);
            Segment(Parse(side + "Arm"), elbow, elbow.position, hand.position, 0.043f);
            var fingertip = Bone(animator, side, "MiddleDistal").position;
            Segment(Parse(side + "Hand"), hand, hand.position, fingertip + (fingertip - hand.position).normalized * 0.025f, 0.037f);
            Segment(Parse(side + "Leg"), thigh, thigh.position, knee.position, 0.071f);
            var ankle = foot.position + axis.up * 0.045f;
            Segment(Parse(side + "Leg"), knee, knee.position, ankle, 0.054f);
            var footObject = Volume(Parse(side + "Foot"), foot,
                new Vector3(foot.position.x, (foot.position.y + 0.045f) * 0.5f, (foot.position.z + toes.position.z) * 0.5f + 0.015f),
                axis.rotation);
            var collider = footObject.AddComponent<BoxCollider>();
            collider.size = new Vector3(0.105f, foot.position.y + 0.045f, Mathf.Abs(toes.position.z - foot.position.z) + 0.14f);
        }
    }

    // Resolves an anatomical bone using the Humanoid's explicit side and part names.
    private static Transform Bone(Animator animator, string side, string part) =>
        animator.GetBoneTransform((HumanBodyBones)Enum.Parse(typeof(HumanBodyBones), side + part));

    // Resolves the matching region identifier for a side-specific volume.
    private static BodyRegion Parse(string name) => (BodyRegion)Enum.Parse(typeof(BodyRegion), name);

    // Parents a unit-world-scale volume to its animated bone while retaining its fitted neutral placement.
    private static GameObject Volume(BodyRegion region, Transform bone, Vector3 position, Quaternion rotation)
    {
        var obj = new GameObject("Hit_" + region);
        obj.transform.SetPositionAndRotation(position, rotation);
        obj.transform.SetParent(bone, true);
        obj.AddComponent<BodyHitRegion>().Configure(region);
        return obj;
    }

    // Splits the central body into touching boxes without a separate back region or equipment target.
    private static void Box(BodyRegion region, Transform bone, Transform axis, float bottom, float top,
        float width, float depth, float forward)
    {
        var center = new Vector3(bone.position.x, (bottom + top) * 0.5f, bone.position.z) + axis.forward * forward;
        var obj = Volume(region, bone, center, axis.rotation);
        obj.AddComponent<BoxCollider>().size = new Vector3(width, top - bottom, depth);
    }

    // Covers each limb segment and overlaps adjacent joint caps so bending cannot expose a seam.
    private static void Segment(BodyRegion region, Transform bone, Vector3 start, Vector3 end, float radius)
    {
        var length = Vector3.Distance(start, end);
        var obj = Volume(region, bone, (start + end) * 0.5f, Quaternion.FromToRotation(Vector3.up, end - start));
        var collider = obj.AddComponent<CapsuleCollider>();
        collider.radius = radius;
        collider.height = length + collider.radius * 2f;
    }
}
