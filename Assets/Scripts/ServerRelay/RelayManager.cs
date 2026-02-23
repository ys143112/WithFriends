using System;
using System.Threading.Tasks;
using UnityEngine;

using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;

using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;

public class RelayManager : MonoBehaviour
{
    bool _ready;
    public string LastJoinCode { get; private set; }


    async void Awake()
    {
        await EnsureServicesReady();
    }

    public async Task EnsureServicesReady()
    {
        if (_ready) return;

        try
        {
            await UnityServices.InitializeAsync();

            // ✅ Relay는 인증 필수 (가장 쉬운 익명 로그인)
            if (!AuthenticationService.Instance.IsSignedIn)
                await AuthenticationService.Instance.SignInAnonymouslyAsync();

            _ready = true;
            Debug.Log("[Relay] Services ready + signed in");
        }
        catch (Exception e)
        {
            Debug.LogError("[Relay] EnsureServicesReady failed: " + e);
        }
    }

    public async Task StartHostWithRelay(int maxPlayers = 2)
    {
        await EnsureServicesReady();
        if (!_ready) return;

        try
        {
            Allocation alloc = await RelayService.Instance.CreateAllocationAsync(maxPlayers);
            string joinCode = await RelayService.Instance.GetJoinCodeAsync(alloc.AllocationId);

            var utp = NetworkManager.Singleton.GetComponent<UnityTransport>();
            RelayServerData relayServerData = AllocationUtils.ToRelayServerData(alloc, "dtls");
            utp.SetRelayServerData(relayServerData);
            LastJoinCode = joinCode;
            Debug.Log($"[Relay] Join Code: {joinCode}");



            Debug.Log($"[Relay] Join Code: {joinCode}");
            NetworkManager.Singleton.StartHost();
        }
        catch (Exception e)
        {
            Debug.LogError("[Relay] StartHostWithRelay failed: " + e);
        }
    }

    public async Task JoinClientWithRelay(string joinCode)
    {
        await EnsureServicesReady();
        if (!_ready) return;

        try
        {
            JoinAllocation joinAlloc = await RelayService.Instance.JoinAllocationAsync(joinCode);

            var utp = NetworkManager.Singleton.GetComponent<UnityTransport>();
            RelayServerData relayServerData = AllocationUtils.ToRelayServerData(joinAlloc, "dtls");
            utp.SetRelayServerData(relayServerData);



            NetworkManager.Singleton.StartClient();
        }
        catch (Exception e)
        {
            Debug.LogError("[Relay] JoinClientWithRelay failed: " + e);
        }
    }
}
