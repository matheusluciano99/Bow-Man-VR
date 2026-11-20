using System.Collections;
using BowMan;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public class ArrowAttachmentTests
{
    private GameObject body;
    private GameObject arrowObject;

    // Removes the target hierarchy and any independently surviving arrow after each test.
    [UnityTearDown]
    public IEnumerator CleanUp()
    {
        if (body != null) Object.Destroy(body);
        if (arrowObject != null) Object.Destroy(arrowObject);
        yield return null;
    }

#if UNITY_EDITOR
    // Runs the shared art through resting nock, draw, flight and attachment using the playable bow prefab.
    [UnityTest]
    public IEnumerator PlayableArrowKeepsTheNockOnTheStringAndTheTipAtImpact()
    {
        body = new GameObject("Arrow art test");
        body.transform.position = new Vector3(500f, 500f, 500f);
        var bow = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/_Game/Prefabs/Bow.prefab"), body.transform).GetComponent<Bow>();
        bow.SetHeld(true);
        Assert.That(bow.TryBeginDraw(bow.RestPosition), Is.True);
        var arrow = bow.GetComponentInChildren<ArrowFlight>();
        arrowObject = arrow.gameObject;
        var nock = arrow.transform.Find("Nock").GetComponent<MeshFilter>();
        var tip = arrow.transform.Find("Tip").GetComponent<MeshFilter>();
        var tipZ = tip.sharedMesh.bounds.max.z;
        var tail = Vector3.forward * nock.sharedMesh.bounds.min.z;
        Assert.That(tipZ, Is.EqualTo(0f).Within(0.001f));
        Assert.That(Vector3.Distance(nock.transform.TransformPoint(tail), bow.RestPosition), Is.LessThan(0.001f));
        var drawn = bow.RestPosition - bow.transform.forward * bow.MaximumDrawDistance;
        bow.PullTo(drawn);
        bow.RefreshVisuals();
        Assert.That(Vector3.Distance(nock.transform.TransformPoint(tail), drawn), Is.LessThan(0.001f));
        var target = new GameObject("Struck volume");
        target.transform.SetParent(body.transform);
        target.transform.position = arrow.transform.position + bow.transform.forward;
        target.AddComponent<BoxCollider>().size = Vector3.one * 0.6f;
        target.AddComponent<BodyHitRegion>().Configure(BodyRegion.Chest);
        BodyImpact hit = default;
        arrow.BodyHit += impact => hit = impact;
        Assert.That(bow.Release(), Is.SameAs(arrow));
        for (var i = 0; i < 12 && arrow.transform.parent == null; i++) yield return new WaitForFixedUpdate();
        Assert.That(arrow.transform.parent, Is.EqualTo(target.transform));
        Assert.That(Vector3.Distance(tip.transform.position, hit.PathEnd), Is.LessThan(0.001f));
        Assert.That(arrow.GetComponentsInChildren<MeshRenderer>().Length, Is.EqualTo(3));
    }
#endif

    // Preserves the entry anchor, penetrated geometry and rotation through fall, kneel and recovery transforms.
    [UnityTest]
    public IEnumerator BodyArrowFollowsTheExactLocalImpactThroughPoseChanges()
    {
        body = new GameObject("Archer");
        body.transform.position = new Vector3(1000f, 1000f, 1000f);
        var bone = new GameObject("Animated arm");
        bone.transform.SetParent(body.transform, false);
        bone.transform.localPosition = Vector3.up;
        bone.AddComponent<BoxCollider>().size = new Vector3(0.3f, 0.5f, 0.2f);
        bone.AddComponent<BodyHitRegion>().Configure(BodyRegion.LeftArm);
        arrowObject = new GameObject("Arrow");
        arrowObject.transform.position = bone.transform.position + new Vector3(0.07f, 0.08f, -1f);
        var arrow = arrowObject.AddComponent<ArrowFlight>();
        var tip = new GameObject("Visible tip").transform;
        tip.SetParent(arrowObject.transform, false);
        BodyImpact anatomy = default;
        arrow.BodyHit += hit => anatomy = hit;
        var count = 0;
        Vector3 actualImpact = default;
        arrow.Hit += hit => { actualImpact = hit.point; count++; };
        arrow.Launch(Vector3.forward * 100f);
        yield return new WaitForFixedUpdate();
        Assert.That(count, Is.EqualTo(1));
        Assert.That(arrow.transform.parent, Is.EqualTo(bone.transform));
        Assert.That(arrow.transform.position, Is.EqualTo(actualImpact));
        Assert.That(anatomy.PenetrationDepth, Is.GreaterThan(0f));
        Assert.That(Vector3.Distance(tip.position, anatomy.PathEnd), Is.LessThan(0.0001f));
        var localPoint = bone.transform.InverseTransformPoint(actualImpact);
        var localTip = bone.transform.InverseTransformPoint(tip.position);
        var localRotation = Quaternion.Inverse(bone.transform.rotation) * arrow.transform.rotation;
        foreach (var pose in new[] { 1f, 0.4f, 0.7f, 1f })
        {
            body.transform.rotation = Quaternion.Euler(90f * (1f - pose), 25f, 0f);
            bone.transform.localPosition = Vector3.up * pose;
            bone.transform.localRotation = Quaternion.Euler(40f * pose, 15f, 30f * (1f - pose));
            yield return new WaitForFixedUpdate();
            Assert.That(Vector3.Distance(arrow.transform.position, bone.transform.TransformPoint(localPoint)), Is.LessThan(0.0001f));
            Assert.That(Vector3.Distance(tip.position, bone.transform.TransformPoint(localTip)), Is.LessThan(0.0001f));
            Assert.That(Quaternion.Angle(arrow.transform.rotation, bone.transform.rotation * localRotation), Is.LessThan(0.01f));
            Assert.That(count, Is.EqualTo(1));
        }
        yield return new WaitForSeconds(8.1f);
        Assert.That(arrow != null, Is.True, "Attached arrows must survive flight and impact cleanup timers");
        Object.Destroy(body);
        yield return null;
        Assert.That(arrow == null, Is.True, "The target hierarchy owns attached-arrow cleanup");
    }
}
