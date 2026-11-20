namespace BowMan.Networking
{
    // Stages of starting, running and ending a PvP connection.
    public enum ConnectionPhase
    {
        Offline,
        SigningIn,
        CreatingMatch,
        WaitingForOpponent,
        Joining,
        Connected,
        Failed,
        Disconnected,
    }

    // Snapshot of the local connection shown on the desktop panel and in the headset.
    public readonly struct ConnectionStatus
    {
        public readonly ConnectionPhase Phase;
        public readonly ArcherRole Role;
        public readonly string JoinCode;
        public readonly string Error;

        // Captures one connection state with its role, join code and failure reason.
        public ConnectionStatus(ConnectionPhase phase, ArcherRole role, string joinCode, string error)
        {
            Phase = phase;
            Role = role;
            JoinCode = joinCode ?? string.Empty;
            Error = error ?? string.Empty;
        }

        public const string LostConnection = "Lost connection to the other player";
        private const string NetcodeDebugPrefix = "[Disconnect Event]";

        public static ConnectionStatus Offline => new ConnectionStatus(ConnectionPhase.Offline, ArcherRole.None, "", "");

        // Keeps a reason the server sent; replaces empty reasons and Netcode's internal debug text.
        public static string ReadableDisconnectReason(string raw) =>
            string.IsNullOrEmpty(raw) || raw.StartsWith(NetcodeDebugPrefix, System.StringComparison.Ordinal) ? LostConnection : raw;

        // True when no session is starting or running, so Host and Join may be used.
        public bool CanStart =>
            Phase == ConnectionPhase.Offline || Phase == ConnectionPhase.Failed || Phase == ConnectionPhase.Disconnected;

        // Returns the player-facing sentence for this state.
        public string Describe()
        {
            switch (Phase)
            {
                case ConnectionPhase.SigningIn: return "Signing in…";
                case ConnectionPhase.CreatingMatch: return "Creating match…";
                case ConnectionPhase.WaitingForOpponent: return $"Archer 1 — share code {JoinCode}, waiting for opponent";
                case ConnectionPhase.Joining: return $"Joining {JoinCode}…";
                case ConnectionPhase.Connected:
                    var side = Role == ArcherRole.Archer2 ? 2 : 1;
                    return JoinCode.Length > 0 ? $"Connected as Archer {side} — code {JoinCode}" : $"Connected as Archer {side}";
                case ConnectionPhase.Failed: return $"Connection failed: {Error}";
                case ConnectionPhase.Disconnected: return Error.Length > 0 ? $"Disconnected: {Error}" : "Disconnected";
                default: return "Offline — Host or enter a code to Join";
            }
        }
    }
}
