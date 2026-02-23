using UnityEngine;
using Unity.Netcode;
using UnityEngine.SceneManagement;

public class PlayerClassState : NetworkBehaviour
{
    public NetworkVariable<int> JobId =
        new((int)JobType.Warrior,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

    public JobType CurrentJob => (JobType)JobId.Value;

    [SerializeField] private PlayerJob playerJob;

    private void Awake()
    {
        if (playerJob == null)
            playerJob = GetComponent<PlayerJob>();
    }

    public override void OnNetworkSpawn()
    {
        JobId.OnValueChanged += OnJobChanged;

        if (IsClient && NetworkManager != null && NetworkManager.SceneManager != null)
            NetworkManager.SceneManager.OnLoadEventCompleted += OnSceneLoadCompleted;

        ApplyNow();

        // ✅ 로컬 플레이어만, 스폰된 직후 서버로 선택값 전송
        if (IsOwner && IsClient)
        {
            int id = (int)SelectedJobCache.Selected;
            Debug.Log($"[Client] Sending selected job to server: {id}");
            RequestSetJobRpc(id);
        }
    }


    public override void OnNetworkDespawn()
    {
        JobId.OnValueChanged -= OnJobChanged;

        if (NetworkManager != null && NetworkManager.SceneManager != null)
            NetworkManager.SceneManager.OnLoadEventCompleted -= OnSceneLoadCompleted;
    }

    void OnJobChanged(int prev, int cur)
    {
        Debug.Log($"[PlayerClassState] JobId changed {prev} -> {cur} (IsServer={IsServer})");
        ApplyNow();
    }

    void OnSceneLoadCompleted(string sceneName, LoadSceneMode mode,
        System.Collections.Generic.List<ulong> clientsCompleted,
        System.Collections.Generic.List<ulong> clientsTimedOut)
    {
        ApplyNow();
    }

    public void ApplyNow()
    {
        var holder = ClassDatabaseHolder.Instance;
        if (holder == null || holder.Database == null)
        {
            Debug.LogWarning("[PlayerClassState] ClassDatabaseHolder/Database를 아직 못 찾음");
            return;
        }

        var def = holder.Database.Get(CurrentJob);

        if (playerJob == null)
            playerJob = GetComponent<PlayerJob>();

        if (playerJob == null)
        {
            Debug.LogWarning("[PlayerClassState] PlayerJob 컴포넌트를 못 찾음 (Player 프리팹에 붙어있나 확인)");
            return;
        }

        playerJob.Apply(def);
    }

    // ✅ 최신 NGO 권장 방식
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void RequestSetJobRpc(int jobId, RpcParams rpcParams = default)
    {
        Debug.Log($"[Server] RequestSetJobRpc received jobId={jobId} sender={rpcParams.Receive.SenderClientId}");

        // (선택) 보안 체크
        // if (rpcParams.Receive.SenderClientId != OwnerClientId) return;

        JobId.Value = jobId;
    }
}
