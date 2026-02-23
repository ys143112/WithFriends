using UnityEngine;
using Unity.Netcode;

public class WarriorSlashProjectile : NetworkBehaviour
{
    public float speed = 18f;
    public float lifeTime = 0.6f;
    public float radius = 0.8f;

    private Vector3 dir;
    private int damage;

    public void Init(Vector3 direction, int dmg)
    {
        dir = direction.normalized;
        damage = dmg;
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
            Invoke(nameof(ServerDespawn), lifeTime);
    }

    void Update()
    {
        if (!IsServer) return;

        transform.position += dir * speed * Time.deltaTime;
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
