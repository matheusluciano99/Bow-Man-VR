using System.Threading.Tasks;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;

namespace BowMan.Networking
{
    // Signs in to Unity Gaming Services and points the transport at a Unity Relay allocation.
    public sealed class RelayConnector
    {
        private const string Protocol = "dtls";
        private const int RemotePlayers = 1;

        // Initializes services once with the given auth profile, then signs in anonymously if needed.
        public async Task EnsureSignedInAsync(string profile)
        {
            while (UnityServices.State == ServicesInitializationState.Initializing) await Task.Yield();
            if (UnityServices.State != ServicesInitializationState.Initialized)
                await UnityServices.InitializeAsync(new InitializationOptions().SetProfile(profile));
            if (!AuthenticationService.Instance.IsSignedIn)
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }

        // Creates a one-opponent Relay allocation, configures the host transport and returns its join code.
        public async Task<string> HostAsync(UnityTransport transport)
        {
            var allocation = await RelayService.Instance.CreateAllocationAsync(RemotePlayers);
            var code = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
            transport.SetRelayServerData(allocation.ToRelayServerData(Protocol));
            return code;
        }

        // Joins the Relay allocation behind the code and configures the client transport.
        public async Task JoinAsync(UnityTransport transport, string code)
        {
            var allocation = await RelayService.Instance.JoinAllocationAsync(code);
            transport.SetRelayServerData(allocation.ToRelayServerData(Protocol));
        }
    }
}
