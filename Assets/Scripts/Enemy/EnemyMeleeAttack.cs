using UnityEngine;
using Unity.Netcode;
using System.Collections;

[RequireComponent(typeof(EnemyStats))]
public class EnemyMeleeAttack : NetworkBehaviour
{
    [Header("Melee")]
    public int damage = 8;
    public float attackRange = 1.8f;
    public float attackCooldown = 1.2f;
    public float attackWindup = 0.15f;
    public float attackAngle = 120f;

    float nextAttackTime;

    void Update()
    {
        if (!IsServer) return;

        Transform target = FindNearestPlayer();
        if (!target) return;

        float dist = Vector3.Distance(transform.position, target.position);
        if (dist > attackRange) return;

        if (Time.time < nextAttackTime) return;

        // 전방 각도 체크
        Vector3 to = (target.position - transform.position);
        to.y = 0f;

        if (to.sqrMagnitude > 0.001f)
        {
            float angle = Vector3.Angle(transform.forward, to.normalized);
            if (angle > attackAngle * 0.5f) return;
        }

        nextAttackTime = Time.time + attackCooldown;

        StartCoroutine(CoDoHit(target));
    }

    IEnumerator CoDoHit(Transform target)
    {
        yield return new WaitForSeconds(attackWindup);

        if (!IsServer || !target) yield break;

        float dist = Vector3.Distance(transform.position, target.position);
        if (dist > attackRange + 0.3f) yield break;

        var hp = target.GetComponent<HealthNetwork>();
        if (hp != null)
        {
            Vector3 hitPos = target.position + Vector3.up * 1.0f;

            // 🔥 방금 수정한 HealthNetwork와 정확히 맞음
            hp.ServerTakeDamage(damage, hitPos);
        }
    }

    Transform FindNearestPlayer()
    {
        Transform best = null;
        float bestDist = float.MaxValue;

        foreach (var p in FindObjectsByType<PlayerStats>(FindObjectsSortMode.None))
        {
            float d = Vector3.Distance(transform.position, p.transform.position);
            if (d < bestDist)
            {
                bestDist = d;
                best = p.transform;
            }
        }

        return best;
    }
}