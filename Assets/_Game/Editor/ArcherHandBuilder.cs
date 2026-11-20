using System.Collections.Generic;
using System.IO;
using System.Linq;
using BowMan;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

public static class ArcherHandBuilder
{
    private const string Output = "Assets/_Game/Resources/ArcherHands";
    private const string ModelPath = "Assets/_Game/Prefabs/Characters/ErikaArcher.prefab";

    // Extracts just the two hands and short cuffs from the prepared Humanoid and preserves their materials.
    [MenuItem("Bow Man VR/Characters/Bake first-person hands")]
    public static void Bake()
    {
        Directory.CreateDirectory(Output);
        AssetDatabase.Refresh();
        foreach (var left in new[] { true, false })
        {
            var actor = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath));
            try { BakeHand(actor, left); }
            finally { Object.DestroyImmediate(actor); }
        }
        AssetDatabase.SaveAssets();
    }

    // Bakes three gestures onto the same surface vertices in controller space, including the grip offset.
    private static void BakeHand(GameObject actor, bool left)
    {
        var side = left ? "Left" : "Right";
        var rig = actor.GetComponentInChildren<Animator>();
        using var handler = new HumanPoseHandler(rig.avatar, rig.transform);
        var wrist = rig.GetBoneTransform(left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
        var fingers = new[] {
            left ? HumanBodyBones.LeftThumbProximal : HumanBodyBones.RightThumbProximal,
            left ? HumanBodyBones.LeftIndexProximal : HumanBodyBones.RightIndexProximal,
            left ? HumanBodyBones.LeftMiddleProximal : HumanBodyBones.RightMiddleProximal,
            left ? HumanBodyBones.LeftLittleProximal : HumanBodyBones.RightLittleProximal
        }.Select(rig.GetBoneTransform).ToArray();
        var neutral = new HumanPose();
        handler.GetHumanPose(ref neutral);
        var vertices = new List<Vector3>();
        var normals = new List<Vector3>();
        var uv = new List<Vector2>();
        var triangles = new List<int[]>();
        var materials = new List<Material>();
        var source = new List<(SkinnedMeshRenderer skin, int[] selected)>();
        foreach (var skin in actor.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            var mesh = skin.sharedMesh;
            var bones = skin.bones;
            var weights = mesh.boneWeights;
            var map = new Dictionary<int,int>();
            var selected = new List<int>();
            var meshUV = mesh.uv;
            for (var sub = 0; sub < mesh.subMeshCount; sub++)
            {
                var indices = mesh.GetTriangles(sub);
                var kept = new List<int>();
                for (var i = 0; i < indices.Length; i += 3)
                {
                    if (!PartOfHand(indices[i]) || !PartOfHand(indices[i+1]) || !PartOfHand(indices[i+2])) continue;
                    for (var j = 0; j < 3; j++)
                    {
                        var index = indices[i+j];
                        if (!map.TryGetValue(index, out var local))
                        {
                            local = map.Count;
                            map.Add(index,local);
                            selected.Add(index);
                            uv.Add(meshUV[index]);
                        }
                        kept.Add(vertices.Count+local);
                    }
                }
                if (kept.Count == 0) continue;
                triangles.Add(kept.ToArray());
                materials.Add(skin.sharedMaterials[sub]);
            }
            if (selected.Count == 0) continue;
            source.Add((skin,selected.ToArray()));
            vertices.AddRange(new Vector3[selected.Count]);
            normals.AddRange(new Vector3[selected.Count]);

            // Keeps the hand-weighted surface and the nearby forearm cuff, including its original UV seam.
            bool PartOfHand(int index)
            {
                if (index >= weights.Length) return false;
                var bone = bones[weights[index].boneIndex0];
                return bone != null && (bone == wrist || bone.IsChildOf(wrist) || bone == wrist.parent);
            }
        }
        var result = new Mesh { name = side + " Archer Hand" };
        for (var gesture = 0; gesture < 3; gesture++)
        {
            var pose = neutral;
            pose.muscles = (float[])neutral.muscles.Clone();
            for (var i=0; i<pose.muscles.Length; i++)
            {
                var name = HumanTrait.MuscleName[i];
                if (!name.StartsWith(side+" ") || !name.Contains("Stretched")) continue;
                var thumb = name.Contains("Thumb");
                var index = name.Contains("Index");
                pose.muscles[i] = gesture switch {
                    1 => thumb ? -.28f : -.74f,
                    2 => thumb || index ? -.4f : -.56f,
                    _ => thumb ? .02f : .22f
                };
            }
            handler.SetHumanPose(ref pose);
            var thumbTip = FingerTip(fingers[0]);
            var indexTip = FingerTip(fingers[1]);
            if (gesture == 2) ClosePinch(fingers[0], thumbTip, indexTip.position);
            if (gesture == 1) ClosePinch(fingers[0], thumbTip, fingers[1].GetChild(0).position);
            var forward = fingers[2].position - wrist.position;
            var up = fingers[1].position - fingers[3].position;
            var toController = Quaternion.Euler(90,0,0) * Quaternion.Inverse(Quaternion.LookRotation(forward, up));
            if (gesture == 1)
                toController = Quaternion.AngleAxis(left ? 32f : -32f, Vector3.forward) * toController;
            var anchor = gesture switch {
                1 => Vector3.Lerp(fingers[2].position, fingers[2].GetChild(0).position, .55f),
                2 => (thumbTip.position+indexTip.position)*.5f,
                _ => Vector3.Lerp(wrist.position, fingers[2].position, .7f)
            };
            var forearm = (wrist.parent.position - wrist.position).normalized;
            var indexInHand = 0;
            var delta = new Vector3[vertices.Count];
            var normalDelta = new Vector3[vertices.Count];
            foreach (var part in source)
            {
                var baked = new Mesh();
                part.skin.BakeMesh(baked, true);
                var v = baked.vertices;
                var n = baked.normals;
                foreach (var original in part.selected)
                {
                    var point = part.skin.transform.TransformPoint(v[original]);
                    var surfaceNormal = part.skin.transform.TransformDirection(n[original]).normalized;
                    var behind = Vector3.Dot(point - wrist.position, forearm);
                    if (behind > .052f)
                    {
                        // Roll the original forearm surface into a closed cuff at the back of the wrist.
                        var end = Mathf.Clamp01((behind - .052f) / .015f);
                        var radial = point - wrist.position - forearm * behind;
                        point = wrist.position + forearm * (.052f + .006f * end) + radial * (1f - end);
                        surfaceNormal = Vector3.Slerp(surfaceNormal, forearm, end);
                    }
                    var position = toController * (point - anchor);
                    var normal = toController * surfaceNormal;
                    if (gesture == 0)
                    {
                        vertices[indexInHand] = position;
                        normals[indexInHand] = normal;
                    }
                    else
                    {
                        delta[indexInHand] = position - vertices[indexInHand];
                        normalDelta[indexInHand] = normal - normals[indexInHand];
                    }
                    indexInHand++;
                }
                Object.DestroyImmediate(baked);
            }
            if (gesture == 0)
            {
                result.SetVertices(vertices); result.SetNormals(normals); result.SetUVs(0,uv);
                result.subMeshCount = triangles.Count;
                for (var sub=0; sub<triangles.Count; sub++) result.SetTriangles(triangles[sub],sub);
                result.RecalculateTangents();
            }
            else
            {
                if (gesture == 1)
                {
                    // Lower the hand along the wrapped grip until the nocked shaft clears the skin.
                    var bow = AssetDatabase.LoadAssetAtPath<Bow>("Assets/_Game/Prefabs/Bow.prefab");
                    var ceiling = bow.transform.InverseTransformPoint(bow.RestPosition).y - .009f;
                    var top = vertices.Select((v,i) => v+delta[i]).Where(v => Mathf.Abs(v.x)<.026f).Max(v=>v.z);
                    var lower = Vector3.back * Mathf.Max(0f, top-ceiling);
                    for (var i=0; i<delta.Length; i++) delta[i] += lower;
                }
                result.AddBlendShapeFrame(gesture == 1 ? "Grip" : "Pinch",100,delta,normalDelta,null);
            }
        }
        var path = $"{Output}/{side}.asset";
        var stored = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (stored == null) { AssetDatabase.CreateAsset(result,path); stored=result; }
        else { EditorUtility.CopySerialized(result,stored); Object.DestroyImmediate(result); }
        var hand = new GameObject(side+" archer hand");
        try
        {
            var skin = hand.AddComponent<SkinnedMeshRenderer>();
            skin.sharedMesh = stored;
            skin.sharedMaterials = materials.ToArray();
            skin.localBounds = new Bounds(Vector3.zero, Vector3.one*.45f);
            skin.shadowCastingMode = ShadowCastingMode.Off;
            PrefabUtility.SaveAsPrefabAsset(hand,$"{Output}/{side}.prefab");
        }
        finally { Object.DestroyImmediate(hand); }
    }

    // Finds the tip marker at the end of a modeled finger chain.
    private static Transform FingerTip(Transform finger)
    {
        while (finger.childCount > 0) finger = finger.GetChild(0);
        return finger;
    }

    // Opposes the thumb to the index fingertip while retaining the measured phalanx lengths.
    private static void ClosePinch(Transform baseJoint, Transform tip, Vector3 target)
    {
        for (var pass=0; pass<7 && Vector3.Distance(tip.position,target)>.002f; pass++)
            for (var joint=tip.parent; joint!=baseJoint.parent; joint=joint.parent)
                joint.rotation = Quaternion.FromToRotation(tip.position-joint.position,target-joint.position)*joint.rotation;
    }
}
