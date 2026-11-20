using System.Text.RegularExpressions;
using BowMan.Networking;
using NUnit.Framework;

public class ConnectionRulesTests
{
    // Rejects invalid release samples before the host creates any remote projectile.
    [Test]
    public void RemoteReleaseMustBeFiniteReachableAndDrawn()
    {
        var origin = UnityEngine.Vector3.zero;
        var forward = UnityEngine.Vector3.forward;
        Assert.IsTrue(PvPDuel.ValidRelease(origin, origin + UnityEngine.Vector3.up, forward, 0.6f));
        Assert.IsFalse(PvPDuel.ValidRelease(origin, origin, forward, float.NaN));
        Assert.IsFalse(PvPDuel.ValidRelease(origin, origin, forward, 1.01f));
        Assert.IsFalse(PvPDuel.ValidRelease(origin, origin, forward, 0f));
        Assert.IsFalse(PvPDuel.ValidRelease(origin, origin, forward * 2f, 1f));
        Assert.IsFalse(PvPDuel.ValidRelease(origin, forward * 10f, forward, 1f));
        Assert.IsFalse(PvPDuel.ValidRelease(origin, new UnityEngine.Vector3(float.NaN, 0f, 0f), forward, 1f));
    }

    // Gives the host the first seat and the joining client the second.
    [Test]
    public void FirstClientIsArcher1SecondIsArcher2()
    {
        var seats = new ArcherSeatAllocator();
        Assert.IsTrue(seats.TryAssign(0, out var first));
        Assert.IsTrue(seats.TryAssign(1, out var second));
        Assert.AreEqual(ArcherRole.Archer1, first);
        Assert.AreEqual(ArcherRole.Archer2, second);
    }

    // Refuses a third client once both archer seats are taken.
    [Test]
    public void ThirdClientIsRefused()
    {
        var seats = new ArcherSeatAllocator();
        seats.TryAssign(0, out _);
        seats.TryAssign(1, out _);
        Assert.IsFalse(seats.TryAssign(2, out var role));
        Assert.AreEqual(ArcherRole.None, role);
        Assert.AreEqual(2, seats.Occupied);
    }

    // Keeps the same seat when one client is assigned twice.
    [Test]
    public void ReassigningSameClientKeepsSeat()
    {
        var seats = new ArcherSeatAllocator();
        seats.TryAssign(0, out _);
        Assert.IsTrue(seats.TryAssign(1, out var once));
        Assert.IsTrue(seats.TryAssign(1, out var twice));
        Assert.AreEqual(ArcherRole.Archer2, once);
        Assert.AreEqual(ArcherRole.Archer2, twice);
        Assert.AreEqual(2, seats.Occupied);
    }

    // Lets a returning client with a new ID take the freed second seat.
    [Test]
    public void ReleasedSeatGoesToNextClient()
    {
        var seats = new ArcherSeatAllocator();
        seats.TryAssign(0, out _);
        seats.TryAssign(1, out _);
        Assert.IsTrue(seats.Release(1));
        Assert.IsTrue(seats.TryAssign(5, out var role));
        Assert.AreEqual(ArcherRole.Archer2, role);
        Assert.AreEqual(ArcherRole.Archer2, seats.RoleOf(5));
        Assert.AreEqual(ArcherRole.None, seats.RoleOf(1));
    }

    // Reports that releasing a client without a seat changed nothing.
    [Test]
    public void ReleaseUnknownClientReturnsFalse()
    {
        Assert.IsFalse(new ArcherSeatAllocator().Release(42));
    }

    // Accepts codes typed in lowercase, with spaces or a trailing newline.
    [Test]
    public void NormalizeStripsSpacesAndUppercases()
    {
        Assert.AreEqual("K7QX2P", JoinCode.Normalize(" k7q x2p\n"));
        Assert.AreEqual("", JoinCode.Normalize(null));
    }

    // Rejects empty, too-short and punctuated codes before any network call.
    [Test]
    public void IsValidRejectsEmptyShortAndSymbols()
    {
        Assert.IsFalse(JoinCode.IsValid(""));
        Assert.IsFalse(JoinCode.IsValid("ABC"));
        Assert.IsFalse(JoinCode.IsValid("K7Q-X2"));
        Assert.IsTrue(JoinCode.IsValid("K7QX2P"));
    }

    // Gives each process its own legal authentication profile name.
    [Test]
    public void ProfilesDifferPerProcessAndStayLegal()
    {
        Assert.AreNotEqual(AuthProfile.ForProcess(100), AuthProfile.ForProcess(200));
        StringAssert.IsMatch("^[A-Za-z0-9_-]{1,30}$", AuthProfile.ForProcess(int.MaxValue, "editor/clone #2"));
        StringAssert.IsMatch("^[A-Za-z0-9_-]{1,30}$", AuthProfile.ForProcess(7, new string('x', 60)));
    }
}
