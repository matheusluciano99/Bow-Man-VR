using BowMan.Menu;
using NUnit.Framework;

public class GameScenesTests
{
    // Opens the AI duel for PvE and the online connection scene for PvP.
    [Test]
    public void EachModeOpensItsScene()
    {
        Assert.AreEqual("LocalDuel", GameScenes.For(GameMode.PvE));
        Assert.AreEqual("PvPConnectionTest", GameScenes.For(GameMode.PvP));
        Assert.AreEqual("TargetRange", GameScenes.For(GameMode.Target));
        Assert.AreEqual("MainMenu", GameScenes.MainMenu);
    }

    // Lists the menu first so a built player starts on the mode choice.
    [Test]
    public void BuildOrderStartsWithTheMenu()
    {
        CollectionAssert.AreEqual(new[] { "MainMenu", "LocalDuel", "PvPConnectionTest", "TargetRange" }, GameScenes.BuildOrder);
    }

    // Maps the three desktop shortcuts and ignores unrelated keys.
    [Test]
    public void DigitKeysChooseModes()
    {
        Assert.AreEqual(GameMode.PvE, GameScenes.ModeForKey('1'));
        Assert.AreEqual(GameMode.PvP, GameScenes.ModeForKey('2'));
        Assert.AreEqual(GameMode.Target, GameScenes.ModeForKey('3'));
        Assert.IsNull(GameScenes.ModeForKey('a'));
    }

    // Names each panel in the copy shown in the headset.
    [Test]
    public void PanelTitlesDescribeModes()
    {
        Assert.AreEqual("PvE\nvs AI archer", GameScenes.Title(GameMode.PvE));
        Assert.AreEqual("PvP\nonline duel", GameScenes.Title(GameMode.PvP));
        Assert.That(GameScenes.Title(GameMode.Target), Does.Contain("Online 1v1"));
    }
}
