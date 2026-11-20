using System.Threading.Tasks;
using Unity.Networking.Transport.Relay;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;

namespace BowMan.Networking
{
    // Signs in to Unity Gaming Services and creates or joins a Unity Relay allocation.
    public sealed class RelayConnector
    {
        private const string Protocol = "dtls";
        private const int RemotePlayers = 1;

        private static Task signIn;

        // Shares the process-wide sign-in with a retry made while the previous connection attempt is pending.
        public Task EnsureSignedInAsync(string profile)
        {
            if (signIn == null || signIn.IsCompleted) signIn = SignInAsync(profile);
            return signIn;
        }

        // Initializes services with the process profile and signs in when an account session is needed.
        private static async Task SignInAsync(string profile)
        {
            while (UnityServices.State == ServicesInitializationState.Initializing) await Task.Yield();
            if (UnityServices.State != ServicesInitializationState.Initialized)
                await UnityServices.InitializeAsync(new InitializationOptions().SetProfile(profile));
            if (!AuthenticationService.Instance.IsSignedIn)
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }

        // Returns a one-opponent Relay endpoint and its join code.
        public async Task<(RelayServerData Data, string Code)> HostAsync()
        {
            var allocation = await RelayService.Instance.CreateAllocationAsync(RemotePlayers);
            var code = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
            return (allocation.ToRelayServerData(Protocol), code);
        }

        // Returns the Relay endpoint for the allocation behind the join code.
        public async Task<RelayServerData> JoinAsync(string code)
        {
            var allocation = await RelayService.Instance.JoinAllocationAsync(code);
            return allocation.ToRelayServerData(Protocol);
        }
    }
}
