using System.Collections;
using System.Collections.Generic;
using BowMan.Menu;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public class ModeMenuTests
{
    private static readonly Vector3 Origin = new Vector3(2000f, 2000f, 2000f);
    private readonly List<GameObject> objects = new List<GameObject>();

    // Creates an object away from any scene content and tracks it for cleanup.
    private GameObject Create(string name)
    {
        var obj = new GameObject(name);
        obj.transform.position = Origin;
        objects.Add(obj);
        return obj;
    }

    // Places a mode panel with a solid board at the given forward distance from the test origin.
    private ModeOption Panel(GameMode mode, float z)
    {
        var root = Create(mode + " panel");
        var board = GameObject.CreatePrimitive(PrimitiveType.Cube);
        board.transform.SetParent(root.transform, false);
        board.transform.localScale = new Vector3(1f, 1f, 0.05f);
        root.transform.position = Origin + Vector3.forward * z;
        var option = root.AddComponent<ModeOption>();
        option.Configure(mode, board.GetComponent<Renderer>());
        Physics.SyncTransforms();
        return option;
    }

    // Destroys test objects and waits a frame for Unity to finish.
    [UnityTearDown]
    public IEnumerator CleanUp()
    {
        foreach (var obj in objects) if (obj != null) Object.Destroy(obj);
        objects.Clear();
        yield return null;
    }

    // Highlights the panel the ray hits and clears it when the ray moves away.
    [UnityTest]
    public IEnumerator PointerHighlightsOnlyTheHoveredPanel()
    {
        var option = Panel(GameMode.PvP, 3f);
        var pointer = Create("Pointer").AddComponent<ControllerPointer>();
        yield return null;

        pointer.Point(new Ray(Origin, Vector3.forward));
        Assert.AreSame(option, pointer.Hovered);
        Assert.IsTrue(option.IsHighlighted);

        pointer.Point(new Ray(Origin, Vector3.back));
        Assert.IsNull(pointer.Hovered);
        Assert.IsFalse(option.IsHighlighted);
    }

    // Loads the confirmed panel's scene exactly once even if the trigger is pulled again.
    [UnityTest]
    public IEnumerator ConfirmingAPanelLoadsItsSceneOnce()
    {
        Panel(GameMode.PvE, 3f);
        var pointer = Create("Pointer").AddComponent<ControllerPointer>();
        var menu = Create("Menu").AddComponent<ModeMenu>();
        var loads = new List<string>();
        menu.Loader = loads.Add;
        menu.Connect(pointer);
        yield return null;

        pointer.Point(new Ray(Origin, Vector3.forward));
        pointer.Confirm();
        pointer.Confirm();
        Assert.IsFalse(menu.Choose(GameMode.PvP));
        CollectionAssert.AreEqual(new[] { "LocalDuel" }, loads);
    }

    // Ignores the trigger when the ray is not on a panel.
    [UnityTest]
    public IEnumerator ConfirmWithoutHoverChoosesNothing()
    {
        var pointer = Create("Pointer").AddComponent<ControllerPointer>();
        var menu = Create("Menu").AddComponent<ModeMenu>();
        var loads = new List<string>();
        menu.Loader = loads.Add;
        menu.Connect(pointer);
        yield return null;

        pointer.Point(new Ray(Origin, Vector3.forward));
        pointer.Confirm();
        CollectionAssert.IsEmpty(loads);
    }

    // Returns to the menu once, and not at all when the menu is missing from the build list.
    [UnityTest]
    public IEnumerator ReturnLoadsTheMenuOnceWhenAvailable()
    {
        var missing = Create("Missing menu").AddComponent<ReturnToMenu>();
        var blocked = new List<string>();
        missing.Loader = blocked.Add;
        missing.CanLoad = _ => false;
        var back = Create("Back").AddComponent<ReturnToMenu>();
        var loads = new List<string>();
        back.Loader = loads.Add;
        back.CanLoad = _ => true;
        yield return null;

        LogAssert.Expect(LogType.Warning, "MainMenu is not in the build list. Run Bow Man VR > Build Mode Menu And Game Scenes.");
        Assert.IsFalse(missing.Return());
        Assert.IsTrue(back.Return());
        Assert.IsFalse(back.Return());
        CollectionAssert.IsEmpty(blocked);
        CollectionAssert.AreEqual(new[] { "MainMenu" }, loads);
    }
}
