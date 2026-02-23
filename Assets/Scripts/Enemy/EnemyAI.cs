using UnityEngine;
using Unity.Netcode;

[RequireComponent(typeof(CharacterController))]
public class EnemyAI : NetworkBehaviour
{
    [Header("AI")]
    public float moveSpeed = 3f;
    public float chaseRange = 10f;

    [Header("Physics")]
    public float gravity = -20f;

    private CharacterController cc;
    private float yVel;

    private void Awake()
    {
        cc = GetComponent<CharacterController>();
    }

    public override void OnNetworkSpawn()
    {
        // ✅ 서버만 CharacterController를 켜서 물리/이동을 "단일 권한"으로 만든다
        if (cc != null)
            cc.enabled = IsServer;

        // 서버가 아니면 yVel 같은 내부 상태는 굳이 유지할 필요 없음
        if (!IsServer)
            yVel = 0f;
    }

    private void Update()
    {
        if (!IsServer) return;
        if (cc == null || !cc.enabled) return;

        Transform target = FindNearestPlayer();
        if (!target) return;

        Vector3 to = target.position - transform.position;
        float distSqr = to.sqrMagnitude;
        if (distSqr > chaseRange * chaseRange) return;

        // 회전은 수평만
        Vector3 flat = new Vector3(to.x, 0f, to.z);
        if (flat.sqrMagnitude < 0.0001f) return;

        transform.rotation = Quaternion.LookRotation(flat);

        // 수평 이동
        Vector3 move = flat.normalized * moveSpeed;

        // 중력(지면 붙이기)
        if (cc.isGrounded && yVel < 0f) yVel = -2f;
        yVel += gravity * Time.deltaTime;
        move.y = yVel;

        cc.Move(move * Time.deltaTime);
    }

    private Transform FindNearestPlayer()
    {
        Transform best = null;
        float bestDistSqr = float.MaxValue;

        // ✅ 서버에서만 호출되는 코드라 FindObjectsByType 써도 동작 OK
        foreach (var p in FindObjectsByType<PlayerStats>(FindObjectsSortMode.None))
        {
            Vector3 diff = p.transform.position - transform.position;
            float dSqr = diff.sqrMagnitude;
            if (dSqr < bestDistSqr)
            {
                bestDistSqr = dSqr;
                best = p.transform;
            }
        }
        return best;
    }
}
