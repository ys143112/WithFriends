using UnityEngine;
using Unity.Netcode;

public class EnemyStats : NetworkBehaviour
{
    public NetworkVariable<int> Hp =
        new(30, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    EnemySpawner spawner;
    int spawnIndex = -1;

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;
        Hp.Value = 30;
    }

    // =========================
    // 데미지 처리
    // =========================
    public void TakeDamage(int dmg)
    {
        if (!IsServer) return;
        if (dmg <= 0) return;
        if (Hp.Value <= 0) return;

        Hp.Value = Mathf.Max(0, Hp.Value - dmg);

        if (Hp.Value == 0)
            Die();
    }

    public void ServerInitSpawner(EnemySpawner ownerSpawner, int index)
    {
        if (!IsServer) return;
        spawner = ownerSpawner;
        spawnIndex = index;
    }

    void Die()
    {
        if (!IsServer) return;

        if (spawner != null && spawnIndex >= 0)
            spawner.ServerOnEnemyDied(spawnIndex);

        NetworkObject.Despawn();
    }
}