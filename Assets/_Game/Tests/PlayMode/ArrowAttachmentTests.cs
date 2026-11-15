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

    // Verifies attachment preserves the actual tip and rotation through falling, kneeling and recovery transforms.
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
        var count = 0;
        Vector3 actualImpact = default;
        arrow.Hit += hit => { actualImpact = hit.point; count++; };
        arrow.Launch(Vector3.forward * 100f);
        yield return new WaitForFixedUpdate();
        Assert.That(count, Is.EqualTo(1));
        Assert.That(arrow.transform.parent, Is.EqualTo(bone.transform));
        Assert.That(arrow.transform.position, Is.EqualTo(actualImpact));
        var localPoint = bone.transform.InverseTransformPoint(actualImpact);
        var localRotation = Quaternion.Inverse(bone.transform.rotation) * arrow.transform.rotation;
        foreach (var pose in new[] { 1f, 0.4f, 0.7f, 1f })
        {
            body.transform.rotation = Quaternion.Euler(90f * (1f - pose), 25f, 0f);
            bone.transform.localPosition = Vector3.up * pose;
            bone.transform.localRotation = Quaternion.Euler(40f * pose, 15f, 30f * (1f - pose));
            yield return new WaitForFixedUpdate();
            Assert.That(Vector3.Distance(arrow.transform.position, bone.transform.TransformPoint(localPoint)), Is.LessThan(0.0001f));
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
