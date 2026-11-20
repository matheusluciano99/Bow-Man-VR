using BowMan.Networking;
using NUnit.Framework;

public class ConnectionStatusTests
{
    private const string Code = "K7QX2P";

    // Builds a status with the shared test code and error.
    private static string Describe(ConnectionPhase phase, ArcherRole role = ArcherRole.None, string code = Code, string error = "timeout") =>
        new ConnectionStatus(phase, role, code, error).Describe();

    // Shows how to start when nothing is connected.
    [Test]
    public void OfflineExplainsHostAndJoin() =>
        Assert.AreEqual("Offline — Host or enter a code to Join", Describe(ConnectionPhase.Offline));

    // Shows progress while authenticating.
    [Test]
    public void SigningInShowsProgress() => Assert.AreEqual("Signing in…", Describe(ConnectionPhase.SigningIn));

    // Shows progress while the Relay allocation is created.
    [Test]
    public void CreatingMatchShowsProgress() => Assert.AreEqual("Creating match…", Describe(ConnectionPhase.CreatingMatch));

    // Gives the host the code to share while waiting.
    [Test]
    public void WaitingShowsCodeToShare() =>
        Assert.AreEqual("Archer 1 — share code K7QX2P, waiting for opponent", Describe(ConnectionPhase.WaitingForOpponent, ArcherRole.Archer1));

    // Repeats the code being joined.
    [Test]
    public void JoiningShowsCode() => Assert.AreEqual("Joining K7QX2P…", Describe(ConnectionPhase.Joining));

    // Names the local role and the session code once connected.
    [Test]
    public void ConnectedShowsRoleAndCode()
    {
        Assert.AreEqual("Connected as Archer 1 — code K7QX2P", Describe(ConnectionPhase.Connected, ArcherRole.Archer1));
        Assert.AreEqual("Connected as Archer 2", Describe(ConnectionPhase.Connected, ArcherRole.Archer2, code: ""));
    }

    // Shows the failure reason.
    [Test]
    public void FailedShowsError() => Assert.AreEqual("Connection failed: timeout", Describe(ConnectionPhase.Failed));

    // Shows the disconnect reason only when there is one.
    [Test]
    public void DisconnectedShowsOptionalReason()
    {
        Assert.AreEqual("Disconnected: Match is full", Describe(ConnectionPhase.Disconnected, error: "Match is full"));
        Assert.AreEqual("Disconnected", Describe(ConnectionPhase.Disconnected, error: ""));
        Assert.AreEqual("Disconnected", Describe(ConnectionPhase.Disconnected, error: null));
    }

    // Keeps a reason the server sent, but replaces Netcode's internal debug text with a plain sentence.
    [Test]
    public void ReadableReasonHidesNetcodeDebugText()
    {
        Assert.AreEqual("Match is full", ConnectionStatus.ReadableDisconnectReason("Match is full"));
        Assert.AreEqual(ConnectionStatus.LostConnection, ConnectionStatus.ReadableDisconnectReason(
            "[Disconnect Event][Client-1][TransportClientId-0][TransportShutdown] NetworkConnectionManager was shutdown."));
        Assert.AreEqual(ConnectionStatus.LostConnection, ConnectionStatus.ReadableDisconnectReason(""));
        Assert.AreEqual(ConnectionStatus.LostConnection, ConnectionStatus.ReadableDisconnectReason(null));
        Assert.AreEqual("Lost connection to the other player", ConnectionStatus.LostConnection);
    }

    // Allows Host/Join only while no session is starting or running.
    [Test]
    public void CanStartOnlyWhenIdle()
    {
        foreach (ConnectionPhase phase in System.Enum.GetValues(typeof(ConnectionPhase)))
        {
            var idle = phase == ConnectionPhase.Offline || phase == ConnectionPhase.Failed || phase == ConnectionPhase.Disconnected;
            Assert.AreEqual(idle, new ConnectionStatus(phase, ArcherRole.None, "", "").CanStart, phase.ToString());
        }
    }
}
