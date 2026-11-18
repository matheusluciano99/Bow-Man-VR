namespace BowMan.Menu
{
    // The two ways to play a duel.
    public enum GameMode
    {
        PvE,
        PvP,
    }

    // Scene names, build order and panel copy for the mode menu.
    public static class GameScenes
    {
        public const string MainMenu = "MainMenu";
        public const string LocalDuel = "LocalDuel";
        public const string PvPConnection = "PvPConnectionTest";

        public static readonly string[] BuildOrder = { MainMenu, LocalDuel, PvPConnection };

        // Returns the scene that starts the chosen mode.
        public static string For(GameMode mode) => mode == GameMode.PvP ? PvPConnection : LocalDuel;

        // Maps the desktop keys 1 and 2 to PvE and PvP; any other key chooses nothing.
        public static GameMode? ModeForKey(char key)
        {
            switch (key)
            {
                case '1': return GameMode.PvE;
                case '2': return GameMode.PvP;
                default: return null;
            }
        }

        // Returns the two-line title printed on a mode's panel.
        public static string Title(GameMode mode) => mode == GameMode.PvP ? "PvP\nonline duel" : "PvE\nvs AI archer";
    }
}
