using UnityEngine;
using Unity.Netcode;

/// <summary>
/// 3인칭 히트스캔 총 (정석 단계 2 + FX)
/// - 입력/조준 레이 계산: Owner
/// - 판정/스프레드/연사 제한/데미지: Server
/// - FX(트레이서/임팩트): Server -> All ClientRpc
/// </summary>
public class GunController : NetworkBehaviour
{
    [Header("Refs")]
    public Camera aimCamera;
    public Transform shootOrigin;
    public LayerMask hitMask = ~0;
    public LayerMask enemyMask;

    [Header("Gun")]
    public int damage = 10;
    public float fireRate = 12f;
    public float range = 200f;
    [Tooltip("탄퍼짐(도). 서버에서만 적용됨")]
    public float spreadAngle = 1.0f;
    public bool holdToFire = true;

    [Header("Ammo (optional)")]
    public bool useAmmo = false;
    public int magazineSize = 30;
    public float reloadTime = 1.6f;

    [Header("FX (optional)")]
    public bool enableFxRpc = true;
    public float tracerDuration = 0.05f;
    public float impactNormalOffset = 0.02f;
    public ParticleSystem impactFxPrefab; // 있으면 생성(클라에서)

    int ammoInMag;
    bool reloading;

    float _nextLocalFireTime;
    float _nextServerFireTime;

    void Start()
    {
        if (IsOwner && aimCamera == null)
            aimCamera = Camera.main;

        ammoInMag = Mathf.Clamp(magazineSize, 1, 9999);
    }

    void Update()
    {
        if (!IsOwner) return;

        if (useAmmo && Input.GetKeyDown(KeyCode.R))
            TryReload();

        bool fireInput = holdToFire ? Input.GetMouseButton(0) : Input.GetMouseButtonDown(0);
        if (!fireInput) return;

        TryFire();
    }

    void TryFire()
    {
        if (Time.time < _nextLocalFireTime) return;
        if (reloading) return;

        if (useAmmo && ammoInMag <= 0)
        {
            TryReload();
            return;
        }

        float cooldown = 1f / Mathf.Max(1f, fireRate);
        _nextLocalFireTime = Time.time + cooldown;

        if (useAmmo) ammoInMag--;

        // 로컬 즉시 연출(선택): 반동/총구화염/사운드
        // PlayLocalMuzzleFlash(); PlayLocalRecoil(); PlayLocalSfx();

        // 클라에서 카메라 중앙 레이 전송
        var cam = aimCamera != null ? aimCamera : Camera.main;
        if (cam == null)
        {
            Vector3 fallbackOrigin = transform.position + Vector3.up * 1.4f;
            Vector3 fallbackDir = transform.forward;
            RequestShootServerRpc(fallbackOrigin, fallbackDir.normalized);
            return;
        }

        Ray camRay = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        RequestShootServerRpc(camRay.origin, camRay.direction.normalized);
    }

    void TryReload()
    {
        if (!useAmmo) return;
        if (reloading) return;
        if (ammoInMag >= magazineSize) return;

        StartCoroutine(CoReload());
    }

    System.Collections.IEnumerator CoReload()
    {
        reloading = true;
        yield return new WaitForSeconds(reloadTime);
        ammoInMag = Mathf.Clamp(magazineSize, 1, 9999);
        reloading = false;
    }

    // -------------------------
    // Server

    [ServerRpc]
    void RequestShootServerRpc(Vector3 clientRayOrigin, Vector3 clientRayDir)
    {
        if (!IsServer) return;

        // 서버 연사 제한
        float serverCooldown = 1f / Mathf.Max(1f, fireRate);
        if (Time.time < _nextServerFireTime) return;
        _nextServerFireTime = Time.time + serverCooldown;

        if (clientRayDir.sqrMagnitude < 0.5f) return;
        clientRayDir.Normalize();

        // 카메라 origin 검증(너무 멀면 보정)
        Vector3 playerPos = transform.position;
        if ((clientRayOrigin - playerPos).sqrMagnitude > 25f * 25f)
            clientRayOrigin = playerPos + Vector3.up * 1.4f;

        Vector3 shootPos = GetShootPosServer();

        // 클라 레이 기준 조준점 추정
        Vector3 aimPoint = GetAimPointFromClientRay(clientRayOrigin, clientRayDir);

        // shootPos -> aimPoint 방향
        Vector3 dir = aimPoint - shootPos;
        float dist = dir.magnitude;
        if (dist < 0.01f) dir = transform.forward;
        else dir /= dist;

        // 서버 스프레드(결정적 시드)
        uint seed = MakeShotSeed();
        dir = ApplySpreadDeterministic(dir, spreadAngle, seed);

        Vector3 fxEnd = shootPos + dir * range;

        // 판정
        if (Physics.Raycast(shootPos, dir, out RaycastHit hit, range, hitMask, QueryTriggerInteraction.Ignore))
        {
            fxEnd = hit.point;

            // Enemy만 데미지
            if (((1 << hit.collider.gameObject.layer) & enemyMask.value) != 0)
            {
                var enemy = hit.collider.GetComponentInParent<EnemyStats>();
                if (enemy != null)
                    enemy.TakeDamage(damage);
            }

            if (enableFxRpc)
                HitImpactClientRpc(hit.point, hit.normal);
        }

        if (enableFxRpc)
            ShotFxClientRpc(shootPos, fxEnd);
    }

    Vector3 GetShootPosServer()
    {
        if (shootOrigin != null) return shootOrigin.position;
        return transform.position + transform.forward * 0.8f + Vector3.up * 1.2f;
    }

    Vector3 GetAimPointFromClientRay(Vector3 rayOrigin, Vector3 rayDir)
    {
        if (Physics.Raycast(rayOrigin, rayDir, out RaycastHit hit, range, hitMask, QueryTriggerInteraction.Ignore))
            return hit.point;

        return rayOrigin + rayDir * range;
    }

    uint MakeShotSeed()
    {
        unchecked
        {
            uint a = (uint)Time.frameCount;
            uint b = (uint)OwnerClientId * 73856093u;
            uint c = (uint)NetworkObjectId * 19349663u;
            return a ^ b ^ c;
        }
    }

    Vector3 ApplySpreadDeterministic(Vector3 dir, float angleDeg, uint seed)
    {
        if (angleDeg <= 0.0001f) return dir;

        float half = angleDeg * 0.5f;

        seed ^= seed << 13;
        seed ^= seed >> 17;
        seed ^= seed << 5;
        float r1 = (seed & 0xFFFF) / 65535f;

        seed ^= seed << 13;
        seed ^= seed >> 17;
        seed ^= seed << 5;
        float r2 = (seed & 0xFFFF) / 65535f;

        float yaw = Mathf.Lerp(-half, half, r1);
        float pitch = Mathf.Lerp(-half, half, r2);

        Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
        return (rot * dir).normalized;
    }

    // -------------------------
    // FX RPCs

    [ClientRpc]
    void ShotFxClientRpc(Vector3 from, Vector3 to)
    {
        // 최소 구현: 디버그 라인
        Debug.DrawLine(from, to, Color.yellow, tracerDuration);

        // 진짜 트레이서를 원하면:
        // - LineRenderer 풀링
        // - 또는 TrailRenderer 풀링
        // 여기선 컴파일만 되는 형태로 남겨둠
    }

    [ClientRpc]
    void HitImpactClientRpc(Vector3 point, Vector3 normal)
    {
        if (impactFxPrefab == null) return;

        Vector3 pos = point + normal * impactNormalOffset;
        Quaternion rot = Quaternion.LookRotation(normal);

        // 네트워크 스폰할 필요 없음(순수 시각효과)
        var fx = Instantiate(impactFxPrefab, pos, rot);
        fx.Play();

        // 파티클 끝나면 제거
        Destroy(fx.gameObject, 2f);
    }
}