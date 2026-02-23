using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public class EnemySpawner : NetworkBehaviour
{
    [Header("Prefab (NetworkObject 포함 프리팹)")]
    [SerializeField] private NetworkObject enemyPrefab;

    [Header("Spawn Settings")]
    public int initialCount = 3;
    public float respawnDelay = 3f;

    // 스폰 위치 저장(죽으면 여기로 다시 스폰)
    private readonly List<Vector3> spawnPoints = new();

    // ✅ 서버에서 1회만 초기 스폰하도록 가드
    private bool didInitialSpawn;

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;

        // ✅ 혹시라도 OnNetworkSpawn가 재호출되거나
        // 씬 리로드/재스폰 등으로 중복 실행되는 상황 방지
        if (didInitialSpawn) return;
        didInitialSpawn = true;

        if (enemyPrefab == null)
        {
            Debug.LogError("[EnemySpawner] enemyPrefab이 비어있음. NetworkObject 프리팹을 할당하세요.");
            return;
        }

        spawnPoints.Clear();

        for (int i = 0; i < initialCount; i++)
        {
            Vector3 pos = new Vector3(i * 3, 0, 6);
            spawnPoints.Add(pos);
            SpawnEnemyAtIndex(i);
        }
    }

    private void SpawnEnemyAtIndex(int index)
    {
        if (!IsServer) return;
        if (index < 0 || index >= spawnPoints.Count) return;

        Vector3 pos = spawnPoints[index];

        NetworkObject enemyNetObj = Instantiate(enemyPrefab, pos, Quaternion.identity);

        // ✅ 서버가 소유(기본값)로 Spawn
        enemyNetObj.Spawn();

        // EnemyStats에 스포너 정보 주입
        var stats = enemyNetObj.GetComponent<EnemyStats>();
        if (stats != null)
            stats.ServerInitSpawner(this, index);
    }

    // EnemyStats가 서버에서 호출하는 콜백
    public void ServerOnEnemyDied(int spawnIndex)
    {
        if (!IsServer) return;
        StartCoroutine(CoRespawn(spawnIndex));
    }

    private IEnumerator CoRespawn(int spawnIndex)
    {
        yield return new WaitForSeconds(respawnDelay);
        SpawnEnemyAtIndex(spawnIndex);
    }
}
