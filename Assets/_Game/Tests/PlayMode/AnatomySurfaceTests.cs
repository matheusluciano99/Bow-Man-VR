using System.Collections.Generic;
using BowMan;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

public class AnatomySurfaceTests
{
    private readonly List<Object> objects = new List<Object>();

    // Removes only meshes and primitives created by the geometric test fixtures.
    [TearDown]
    public void CleanUp()
    {
        foreach (var obj in objects) Object.DestroyImmediate(obj);
        objects.Clear();
    }

    // Uses Unity's closed cube, including its duplicated face vertices and shared-edge crossings.
    private AnatomySurface Cube()
    {
        var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        objects.Add(cube);
        return new AnatomySurface(cube.GetComponent<MeshFilter>().sharedMesh);
    }

    // Accepts real entry/inside contacts but rejects paths that miss the tissue or end before reaching it.
    [Test]
    public void FinitePathsRespectEntryAndContainment()
    {
        var cube = Cube();
        Assert.That(cube.TryHit(Vector3.back, Vector3.forward, out var point), Is.True);
        Assert.That(Vector3.Distance(point, Vector3.back * 0.5f), Is.LessThan(0.0001f));
        Assert.That(cube.TryHit(Vector3.zero, Vector3.up * 0.1f, out point), Is.True);
        Assert.That(point, Is.EqualTo(Vector3.zero));
        Assert.That(cube.TryHit(Vector3.zero, Vector3.forward, out point), Is.True);
        Assert.That(point, Is.EqualTo(Vector3.zero), "A path starting inside tissue contacts it before its exit");
        Assert.That(cube.TryHit(Vector3.back, Vector3.back * 0.6f, out _), Is.False);
        Assert.That(cube.TryHit(Vector3.right + Vector3.back, Vector3.right + Vector3.forward, out _), Is.False);
        Assert.That(cube.TryHit(Vector3.zero, Vector3.zero, out _), Is.False);
        Assert.That(cube.TryHit(Vector3.zero, new Vector3(float.NaN, 0f, 0f), out _), Is.False);
        Assert.That(cube.TryHit(Vector3.zero, new Vector3(float.PositiveInfinity, 0f, 0f), out _), Is.False);
    }

    // Creates a closed ring with an actual empty centre rather than a bounding-box approximation.
    private AnatomySurface Ring()
    {
        const int segments = 24;
        const int sides = 12;
        var vertices = new Vector3[segments * sides];
        var indices = new List<int>();
        for (var i = 0; i < segments; i++)
        for (var j = 0; j < sides; j++)
        {
            var around = i * Mathf.PI * 2f / segments;
            var tube = j * Mathf.PI * 2f / sides;
            var radius = 0.4f + Mathf.Cos(tube) * 0.1f;
            vertices[i * sides + j] = new Vector3(Mathf.Cos(around) * radius, Mathf.Sin(tube) * 0.1f,
                Mathf.Sin(around) * radius);
            var a = i * sides + j;
            var b = ((i + 1) % segments) * sides + j;
            var c = ((i + 1) % segments) * sides + (j + 1) % sides;
            var d = i * sides + (j + 1) % sides;
            indices.AddRange(new[] { a, b, c, a, c, d });
        }
        var mesh = new Mesh { vertices = vertices, triangles = indices.ToArray() };
        mesh.RecalculateBounds();
        objects.Add(mesh);
        return new AnatomySurface(mesh);
    }

    // Rejects the empty centre even inside the mesh bounds, while detecting a segment fully inside its wall.
    [Test]
    public void EmptyHolesAreNotReportedAsOrganTissue()
    {
        var ring = Ring();
        Assert.That(ring.TryHit(Vector3.up, Vector3.down, out _), Is.False);
        Assert.That(ring.TryHit(Vector3.zero, Vector3.up * 0.05f, out _), Is.False);
        Assert.That(ring.TryHit(Vector3.right * 0.4f, Vector3.right * 0.4f + Vector3.up * 0.02f, out var point), Is.True);
        Assert.That(point, Is.EqualTo(Vector3.right * 0.4f));
    }
}
