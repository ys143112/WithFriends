using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using UnityEngine.SceneManagement;

public class RunManager : MonoBehaviour
{
    public string gameSceneName = "GameScene";
    public NetworkObject playerPrefab;

    void Awake()
    {
        DontDestroyOnLoad(gameObject);
        StartCoroutine(WaitAndHook());
    }

    IEnumerator WaitAndHook()
    {
        while (NetworkManager.Singleton == null || NetworkManager.Singleton.SceneManager == null)
            yield return null;

        NetworkManager.Singleton.SceneManager.OnLoadEventCompleted += OnLoadEventCompleted;
        Debug.Log("[RunManager] hooked OnLoadEventCompleted");
    }

    void OnDestroy()
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.SceneManager != null)
            NetworkManager.Singleton.SceneManager.OnLoadEventCompleted -= OnLoadEventCompleted;
    }

    public void HostStartGame()
    {
        var nm = NetworkManager.Singleton;
        if (nm == null || !nm.IsServer) return;

        Debug.Log("[RunManager] HostStartGame -> Network LoadScene");
        nm.SceneManager.LoadScene(gameSceneName, LoadSceneMode.Single); // ✅ 꼭 이걸로
    }

    void OnLoadEventCompleted(string sceneName, LoadSceneMode mode,
        List<ulong> clientsCompleted, List<ulong> clientsTimedOut)
    {
        Debug.Log($"[RunManager] LoadCompleted scene={sceneName} server={NetworkManager.Singleton.IsServer}");

        if (!NetworkManager.Singleton.IsServer) return;
        if (sceneName != gameSceneName) return;

        foreach (var clientId in NetworkManager.Singleton.ConnectedClientsIds)
        {
            var client = NetworkManager.Singleton.ConnectedClients[clientId];
            if (client.PlayerObject != null) continue;

            var player = Instantiate(playerPrefab, Vector3.zero, Quaternion.identity);
            player.SpawnAsPlayerObject(clientId, true);
            Debug.Log($"[RunManager] Spawned player for {clientId}");
        }
    }
}
