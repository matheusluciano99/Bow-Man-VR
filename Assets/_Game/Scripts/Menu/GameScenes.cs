namespace BowMan.Menu
{
    // Modes available from the main menu.
    public enum GameMode
    {
        PvE,
        PvP,
        Target,
    }

    // Scene names, build order and panel copy for the mode menu.
    public static class GameScenes
    {
        public const string MainMenu = "MainMenu";
        public const string LocalDuel = "LocalDuel";
        public const string PvPConnection = "PvPConnectionTest";

        public const string TargetRange = "TargetRange";

        public static readonly string[] BuildOrder = { MainMenu, LocalDuel, PvPConnection, TargetRange };

        // Returns the scene that starts the chosen mode.
        public static string For(GameMode mode) => mode switch
        {
            GameMode.Target => TargetRange,
            GameMode.PvP => PvPConnection,
            _ => LocalDuel
        };

        // Maps the three numbered desktop shortcuts to their modes.
        public static GameMode? ModeForKey(char key)
        {
            switch (key)
            {
                case '1': return GameMode.PvE;
                case '2': return GameMode.PvP;
                case '3': return GameMode.Target;
                default: return null;
            }
        }

        // Returns the title printed on a mode's panel.
        public static string Title(GameMode mode) => mode switch
        {
            GameMode.Target => "Tiro ao alvo   •   Local 1v1",
            GameMode.PvP => "PvP\nonline duel",
            _ => "PvE\nvs AI archer"
        };
    }
}
