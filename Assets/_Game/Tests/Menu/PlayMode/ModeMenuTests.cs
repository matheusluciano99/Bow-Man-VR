using System.Collections;
using System.Collections.Generic;
using BowMan;
using BowMan.Menu;
using System.Linq;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif
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

#if UNITY_EDITOR
    // Reads every credits and source page with the scene's pointer, then returns to an actionable mode menu.
    [UnityTest]
    public IEnumerator CreditsShowEveryNoticeAndReturnToModeSelection()
    {
        const string path = "Assets/_Game/Scenes/MainMenu.unity";
        yield return EditorSceneManager.LoadSceneAsyncInPlayMode(path, new LoadSceneParameters(LoadSceneMode.Additive));
        var scene = SceneManager.GetSceneByPath(path);
        try
        {
            yield return null;
            var roots = scene.GetRootGameObjects();
            var menu = roots.SelectMany(g => g.GetComponentsInChildren<ModeMenu>()).Single();
            var pointer = roots.SelectMany(g => g.GetComponentsInChildren<ControllerPointer>()).Single();
            var camera = roots.SelectMany(g => g.GetComponentsInChildren<Camera>()).Single();
            var options = roots.SelectMany(g => g.GetComponentsInChildren<ModeOption>(true)).ToArray();
            var pages = roots.Single(g => g.name == "Release credits");
            var body = pages.transform.Find("Credit text").GetComponent<TextMesh>();
            var more = options.Single(o => o.name == "Next credit page");
            string loaded = null;
            menu.Loader = name => loaded = name;

            // Uses the authored collider and the same confirm event as a controller trigger or mouse click.
            void Click(string name)
            {
                var option = options.Single(o => o.name == name);
                Assert.That(option.gameObject.activeInHierarchy, Is.True, name);
                Physics.SyncTransforms();
                pointer.Point(new Ray(camera.transform.position, option.transform.position - camera.transform.position));
                Assert.That(pointer.Hovered, Is.SameAs(option), name);
                pointer.Confirm();
            }

            Click("Credits option");
            Assert.That(menu.Choose(GameMode.PvE), Is.False);
            Assert.That(body.text, Does.Contain("Matheus"));
            int count = 1;
            while (more.gameObject.activeInHierarchy)
            {
                Click("Next credit page");
                Assert.That(++count, Is.LessThan(32));
            }
            Assert.That(count, Is.EqualTo(9));
            Click("Previous credit page");
            Assert.That(body.text, Does.Contain("qubodup"));
            Click("Source notices");
            var read = body.text;
            count = 1;
            while (more.gameObject.activeInHierarchy)
            {
                Click("Next credit page");
                read += body.text;
                Assert.That(++count, Is.LessThan(120));
            }
            var notices = System.IO.File.ReadAllText(System.IO.Path.Combine(Application.streamingAssetsPath, "BowMan-Credits.txt"));
            Assert.That(string.Concat(read.Where(c => !char.IsWhiteSpace(c))),
                Is.EqualTo(string.Concat(notices.Where(c => !char.IsWhiteSpace(c)))));
            Click("Return to menu");
            pointer.Confirm();
            Assert.That(loaded, Is.Null, "A second confirmation cannot select the hidden credits control");
            Click("Credits option");
            Assert.That(body.text, Does.Contain("Matheus"));
            Click("Return to menu");
            Click("PvP option");
            Assert.That(loaded, Is.EqualTo(GameScenes.PvPConnection));
        }
        finally { SceneManager.UnloadSceneAsync(scene); }
        yield return null;
    }

    // Checks that the online lobby starts with the same bow-hand preference as the local duel.
    [UnityTest]
    public IEnumerator OnlineLobbyUsesTheChosenHand()
    {
        var original = PlayerPrefs.GetInt("BowMan.BowLeft", -1);
        const string path = "Assets/_Game/Scenes/PvPConnectionTest.unity";
        Scene lobby = default;
        try
        {
            VRBowInput.PreferredBowLeft = false;
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(path, new LoadSceneParameters(LoadSceneMode.Additive));
            lobby = SceneManager.GetSceneByPath(path);
            yield return null;
            var input = lobby.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<VRBowInput>(true)).Single();
            Assert.That(input.BowInLeftHand, Is.False);
        }
        finally
        {
            if (original < 0) PlayerPrefs.DeleteKey("BowMan.BowLeft");
            else PlayerPrefs.SetInt("BowMan.BowLeft", original);
            PlayerPrefs.Save();
            if (lobby.IsValid() && lobby.isLoaded) SceneManager.UnloadSceneAsync(lobby);
        }
        yield return null;
    }

    // Gives the in-headset lobby pointer its own ray, shown only while the lobby board is shown in a headset.
    [UnityTest]
    public IEnumerator OnlineLobbyPointerShowsItsRayOnlyWithTheBoard()
    {
        const string path = "Assets/_Game/Scenes/TargetRange.unity";
        Scene lobby = default;
        AsyncOperation unload = null;
        try
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(path, new LoadSceneParameters(LoadSceneMode.Additive));
            lobby = SceneManager.GetSceneByPath(path);
            yield return null;
            yield return null;
            var pointer = lobby.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<ControllerPointer>(true)).Single();
            var ray = pointer.GetComponentsInChildren<LineRenderer>(true).Single(line => line.name == "Lobby pointer ray");
            bool board = VRBowInput.HeadsetActive;
            Assert.AreEqual(board, pointer.enabled);
            Assert.AreEqual(board, ray.enabled);
        }
        finally
        {
            if (lobby.IsValid() && lobby.isLoaded) unload = SceneManager.UnloadSceneAsync(lobby);
        }
        // Waits for the unload so the next test cannot find this scene's connection controller.
        if (unload != null) yield return unload;
    }

    // Uses the authored menu panels and follows the selected hand into the playable duel.
    [UnityTest]
    public IEnumerator NewPlayerChoosesHandEntersDuelAndDraws()
    {
        var original = PlayerPrefs.GetInt("BowMan.BowLeft", -1);
        var previousArcher = PlayerPrefs.GetInt("BowMan.Archer", -1);
        var previous = SceneManager.GetActiveScene();
        Scene menuScene = default, duelScene = default;
        try
        {
            ArcherSelection.Preferred = ArcherModel.English;
            VRBowInput.PreferredBowLeft = true;
            const string menuPath = "Assets/_Game/Scenes/MainMenu.unity";
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(menuPath, new LoadSceneParameters(LoadSceneMode.Additive));
            menuScene = SceneManager.GetSceneByPath(menuPath);
            SceneManager.SetActiveScene(menuScene);
            yield return null;
            var roots = menuScene.GetRootGameObjects();
            var menu = roots.SelectMany(g => g.GetComponentsInChildren<ModeMenu>()).Single();
            var pointer = roots.SelectMany(g => g.GetComponentsInChildren<ControllerPointer>()).Single();
            var choices = roots.Single(g => g.name == "Mode choices").transform;
            var hand = choices.Find("Bow hand option").gameObject;
            var camera = roots.SelectMany(g => g.GetComponentsInChildren<Camera>()).Single();
            Physics.SyncTransforms();
            pointer.Point(new Ray(camera.transform.position, hand.transform.position - camera.transform.position));
            Assert.That(pointer.Hovered.gameObject, Is.EqualTo(hand));
            pointer.Confirm();
            Assert.That(hand.GetComponentInChildren<TextMesh>().text, Does.Contain("Draw with left"));
            var character = choices.Find("Archer option");
            pointer.Point(new Ray(camera.transform.position, character.position - camera.transform.position));
            Assert.That(pointer.Hovered.transform, Is.SameAs(character));
            pointer.Confirm();
            Assert.That(character.GetComponentInChildren<TextMesh>().text, Does.Contain("Erika Archer"));
            string chosen = null;
            menu.Loader = name => chosen = name;
            var pve = choices.Find("PvE option").gameObject;
            pointer.Point(new Ray(camera.transform.position, pve.transform.position - camera.transform.position));
            pointer.Confirm();
            Assert.That(chosen, Is.EqualTo(GameScenes.LocalDuel));
            yield return SceneManager.UnloadSceneAsync(menuScene);
            var path = "Assets/_Game/Scenes/" + chosen + ".unity";
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(path, new LoadSceneParameters(LoadSceneMode.Additive));
            duelScene = SceneManager.GetSceneByPath(path);
            SceneManager.SetActiveScene(duelScene);
            yield return null;
            roots = duelScene.GetRootGameObjects();
            var input = roots.SelectMany(g => g.GetComponentsInChildren<VRBowInput>(true)).Single();
            var bow = roots.SelectMany(g => g.GetComponentsInChildren<Bow>(true)).Single();
            var duel = roots.SelectMany(g => g.GetComponentsInChildren<LocalDuel>()).Single();
            Assert.That(duel.LocalArcher.name, Does.StartWith("ErikaArcher"));
            Assert.That(duel.Opponent.name, Does.StartWith("EnglishArcher"));
            var localBreath = duel.LocalArcher.GetComponentInChildren<AudioSource>(true);
            Assert.That(localBreath.clip, Is.SameAs(Resources.Load<AudioClip>("DuelAudio/Erika/Breath")));
            Assert.That(input.BowInLeftHand, Is.False);
            Assert.That(input.ShotHint(true), Does.Contain("Hold Space"));
            bow.SetHeld(true);
            Assert.That(bow.TryBeginDraw(bow.RestPosition), Is.True);
            Assert.That(input.ShotHint(true), Does.Contain("Release Space"));
            bow.PullTo(bow.RestPosition - bow.transform.forward * 0.45f);
            Assert.That(bow.Release(), Is.Not.Null);
            Assert.That(roots.SelectMany(g => g.GetComponentsInChildren<LocalDuel>()).Single().Flow.Phase, Is.EqualTo(DuelPhase.Flying));
        }
        finally
        {
            ArcherSelection.Preferred = previousArcher == (int)ArcherModel.Erika ? ArcherModel.Erika : ArcherModel.English;
            if (previousArcher < 0) PlayerPrefs.DeleteKey("BowMan.Archer");
            if (original < 0) PlayerPrefs.DeleteKey("BowMan.BowLeft");
            else PlayerPrefs.SetInt("BowMan.BowLeft", original);
            PlayerPrefs.Save();
            if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            if (menuScene.IsValid() && menuScene.isLoaded) SceneManager.UnloadSceneAsync(menuScene);
            if (duelScene.IsValid() && duelScene.isLoaded) SceneManager.UnloadSceneAsync(duelScene);
        }
        yield return null;
    }
#endif

    // Confirms the range panel on the saved main menu using the same ray as the headset.
#if UNITY_EDITOR
    [UnityTest]
    public IEnumerator TargetPanelOpensTheOnlineCompetition()
    {
        const string path = "Assets/_Game/Scenes/MainMenu.unity";
        yield return EditorSceneManager.LoadSceneAsyncInPlayMode(path, new LoadSceneParameters(LoadSceneMode.Additive));
        var scene = SceneManager.GetSceneByPath(path);
        try
        {
            yield return null;
            var roots = scene.GetRootGameObjects();
            var menu = roots.SelectMany(g => g.GetComponentsInChildren<ModeMenu>()).Single();
            var panel = roots.SelectMany(g => g.GetComponentsInChildren<ModeOption>()).Single(o => o.Mode == GameMode.Target);
            var pointer = roots.SelectMany(g => g.GetComponentsInChildren<ControllerPointer>()).Single();
            var camera = roots.SelectMany(g => g.GetComponentsInChildren<Camera>()).Single();
            string route = null;
            menu.Loader = name => route = name;
            Physics.SyncTransforms();
            pointer.Point(new Ray(camera.transform.position, panel.transform.position - camera.transform.position));
            pointer.Confirm();
            Assert.AreEqual(GameScenes.TargetRange, route);
            Assert.IsTrue(Application.CanStreamedLevelBeLoaded(route));
        }
        finally { if (scene.IsValid() && scene.isLoaded) SceneManager.UnloadSceneAsync(scene); }
    }
#endif

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

    // Draws a runtime-assigned ray to the hovered panel and hides it as soon as the pointer is disabled.
    [UnityTest]
    public IEnumerator RuntimeRayEndsOnThePanelAndHidesWithItsPointer()
    {
        Panel(GameMode.PvP, 3f);
        var pointer = Create("Pointer").AddComponent<ControllerPointer>();
        var ray = Create("Ray").AddComponent<LineRenderer>();
        pointer.UseLine(ray);
        Assert.IsFalse(ray.enabled);
        yield return null;

        pointer.Point(new Ray(Origin, Vector3.forward), true);
        Assert.IsTrue(ray.enabled);
        Assert.That(Vector3.Distance(Origin + Vector3.forward * 2.975f, ray.GetPosition(1)), Is.LessThan(0.001f));

        pointer.enabled = false;
        Assert.IsFalse(ray.enabled);
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
