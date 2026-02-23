using UnityEngine;
using Unity.Netcode;

public class HealthNetwork : NetworkBehaviour
{
    public NetworkVariable<int> CurrentHp =
        new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public NetworkVariable<int> MaxHpNet =
        new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    PlayerStats stats;

    void Awake()
    {
        stats = GetComponent<PlayerStats>();
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            if (stats == null)
                stats = GetComponent<PlayerStats>();

            MaxHpNet.Value = stats != null ? stats.MaxHp : 10;
            CurrentHp.Value = MaxHpNet.Value;
        }
    }

    // =====================================================
    // 기존 데미지 함수 (다른 코드와 충돌 방지 위해 유지)
    // =====================================================
    public void ServerTakeDamage(int dmg)
    {
        if (!IsServer) return;
        if (dmg <= 0) return;
        if (CurrentHp.Value <= 0) return;

        ApplyDamage(dmg);

        // 기본 피격 피드백
        SendHitFeedbackToOwner(dmg, transform.position + Vector3.up * 1.0f);
    }

    // =====================================================
    // 🔥 몬스터 전용 데미지 (히트 위치 포함)
    // =====================================================
    public void ServerTakeDamage(int dmg, Vector3 hitWorldPos)
    {
        if (!IsServer) return;
        if (dmg <= 0) return;
        if (CurrentHp.Value <= 0) return;

        ApplyDamage(dmg);

        SendHitFeedbackToOwner(dmg, hitWorldPos);
    }

    void ApplyDamage(int dmg)
    {
        CurrentHp.Value = Mathf.Max(0, CurrentHp.Value - dmg);

        if (CurrentHp.Value == 0)
        {
            // TODO: 사망 처리 (다운/리스폰 등)
        }
    }

    // =====================================================
    // 🔥 맞은 사람(Owner)에게만 피드백 전송
    // =====================================================

    void SendHitFeedbackToOwner(int dmg, Vector3 hitWorldPos)
    {
        float intensity01 = Mathf.Clamp01(dmg / 20f);

        var sendParams = new ClientRpcParams
        {
            Send = new ClientRpcSendParams
            {
                TargetClientIds = new[] { OwnerClientId }
            }
        };

        TookHitClientRpc(intensity01, hitWorldPos, sendParams);
    }

    [ClientRpc]
    void TookHitClientRpc(float intensity01, Vector3 hitWorldPos, ClientRpcParams rpcParams = default)
    {
        // 🔥 여기서 화면 흔들림 / 히트스톱 / 사운드 실행

        if (HitFeedbackHub.Instance != null)
        {
            HitFeedbackHub.Instance.PlayGotHit(intensity01, hitWorldPos);
        }
    }

    // =====================================================
    // 회복
    // =====================================================

    public void ServerHeal(int amount)
    {
        if (!IsServer) return;
        if (amount <= 0) return;
        if (CurrentHp.Value <= 0) return;

        CurrentHp.Value =
            Mathf.Min(MaxHpNet.Value, CurrentHp.Value + amount);
    }
}
