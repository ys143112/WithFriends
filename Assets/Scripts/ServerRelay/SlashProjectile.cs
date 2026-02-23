using UnityEngine;
using Unity.Netcode;

public class SlashProjectile : NetworkBehaviour
{
    public float speed = 30f;
    public float lifeTime = 0.8f;

    Vector3 targetPos;
    int damage;

    public void InitToTarget(Vector3 targetWorldPos, int dmg, float newSpeed, float newLifeTime)
    {
        targetPos = targetWorldPos;
        damage = dmg;
        speed = newSpeed;
        lifeTime = newLifeTime;
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
            Invoke(nameof(ServerDespawn), lifeTime);
    }

    void Update()
    {
        if (!IsServer) return;

        transform.position = Vector3.MoveTowards(transform.position, targetPos, speed * Time.deltaTime);

        Vector3 to = targetPos - transform.position;
        if (to.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.LookRotation(to);
    }

    void OnTriggerEnter(Collider other)
    {
        if (!IsServer) return;

        var enemy = other.GetComponentInParent<EnemyStats>();
        if (!enemy) return;

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
