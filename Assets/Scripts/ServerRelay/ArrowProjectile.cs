using UnityEngine;
using Unity.Netcode;

public class ArrowProjectile : NetworkBehaviour
{
    [Header("Move")]
    public float speed = 25f;
    public float lifeTime = 3.0f;

    Vector3 targetPos;
    int damage;

    // ✅ 누가 쐈는지 (히트 피드백용)
    ulong ownerId;

    /// <summary>
    /// 서버에서 스폰 직후 호출
    /// </summary>
    public void InitToTarget(
        Vector3 targetWorldPos,
        int dmg,
        float newSpeed,
        float newLifeTime,
        ulong ownerClientId)
    {
        targetPos = targetWorldPos;
        damage = dmg;
        speed = newSpeed;
        lifeTime = newLifeTime;
        ownerId = ownerClientId;
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
            Invoke(nameof(ServerDespawn), lifeTime);
    }

    void Update()
    {
        if (!IsServer) return;

        // 이동
        transform.position = Vector3.MoveTowards(
            transform.position,
            targetPos,
            speed * Time.deltaTime);

        // 방향 회전
        Vector3 to = targetPos - transform.position;
        if (to.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.LookRotation(to);

        // 목표 지점 도착 시 제거(옵션)
        if ((targetPos - transform.position).sqrMagnitude < 0.01f)
            ServerDespawn();
    }

    void OnTriggerEnter(Collider other)
    {
        if (!IsServer) return;

        var enemy = other.GetComponentInParent<EnemyStats>();
        if (!enemy) return;

        // 데미지
        enemy.TakeDamage(damage);

        ServerDespawn();
    }

    void ServerDespawn()
    {
        if (!IsServer) return;

        if (NetworkObject != null && NetworkObject.IsSpawned)
            NetworkObject.Despawn();
    }
}
