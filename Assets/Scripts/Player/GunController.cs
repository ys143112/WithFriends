using UnityEngine;
using Unity.Netcode;

public class GunController : NetworkBehaviour
{
    [Header("Refs")]
    public Camera aimCamera;                 // Owner에서 Camera.main
    public Transform shootOrigin;            // TP/ADS에서 스왑
    public LayerMask hitMask = ~0;           // Enemy/Wall 포함
    public LayerMask enemyMask;              // Enemy 레이어

    [Header("Gun")]
    public int damage = 10;
    public float fireRate = 12f;
    public float range = 200f;
    public float spreadAngle = 1.0f;
    public bool holdToFire = true;

    [Header("ADS (server spread)")]
    [Tooltip("ADS 때 서버 스프레드를 얼마나 줄일지(0.25 = 1/4). ThirdPersonCamera가 이 값을 서버에 전달함.")]
    public float adsSpreadMultiplierServer = 0.25f;

    [Header("Ammo (optional)")]
    public bool useAmmo = false;
    public int magazineSize = 30;
    public float reloadTime = 1.6f;

    [Header("FX (optional)")]
    public bool enableFxRpc = true;
    public float tracerDuration = 0.05f;
    public float impactNormalOffset = 0.02f;
    public ParticleSystem impactFxPrefab;

    [Header("Recoil (local casual)")]
    public float recoilPitchKick = 1.2f;
    public float recoilYawKickRandom = 0.45f;

    int ammoInMag;
    bool reloading;

    float _nextLocalFireTime;
    float _nextServerFireTime;

    // 서버에서만 의미있는 ADS 스프레드 배율(ThirdPersonCamera가 SetAdsServerRpc로 갱신)
    float _spreadMul = 1f;

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

        // ✅ 로컬 반동(캐주얼)
        var tpc = GetComponent<ThirdPersonCamera>();
        if (tpc != null)
        {
            float yawKick = Random.Range(-recoilYawKickRandom, recoilYawKickRandom);
            tpc.AddRecoil(recoilPitchKick, yawKick);
        }

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
    // Server: ADS 상태 전달 (ThirdPersonCamera가 호출)
    [ServerRpc]
    public void SetAdsServerRpc(bool on, float adsSpreadMultiplier)
    {
        _spreadMul = on ? Mathf.Clamp01(adsSpreadMultiplier) : 1f;
    }

    // -------------------------
    // Server: Shoot

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

        // 카메라 origin 검증
        Vector3 playerPos = transform.position;
        if ((clientRayOrigin - playerPos).sqrMagnitude > 25f * 25f)
            clientRayOrigin = playerPos + Vector3.up * 1.4f;

        Vector3 shootPos = GetShootPosServer();
        Vector3 aimPoint = GetAimPointFromClientRay(clientRayOrigin, clientRayDir);

        Vector3 dir = aimPoint - shootPos;
        float dist = dir.magnitude;
        if (dist < 0.01f) dir = transform.forward;
        else dir /= dist;

        // 서버 스프레드(ADS 배율 반영)
        uint seed = MakeShotSeed();
        dir = ApplySpreadDeterministic(dir, spreadAngle * _spreadMul, seed);

        Vector3 fxEnd = shootPos + dir * range;

        if (Physics.Raycast(shootPos, dir, out RaycastHit hit, range, hitMask, QueryTriggerInteraction.Ignore))
        {
            fxEnd = hit.point;

            var enemy = hit.collider.GetComponentInParent<EnemyStats>();
            if (enemy != null && enemy.NetworkObject != null && (((1 << hit.collider.gameObject.layer) & enemyMask.value) != 0))
            {
                enemy.TakeDamage(damage);

                // ✅ 적 로컬좌표로 임팩트 전송(보간 오차로 벽에 박혀 보이는 문제 해결)
                ulong enemyId = enemy.NetworkObjectId;
                Vector3 localPoint = enemy.transform.InverseTransformPoint(hit.point);
                Vector3 localNormal = enemy.transform.InverseTransformDirection(hit.normal);

                if (enableFxRpc)
                    HitImpactOnEnemyClientRpc(enemyId, localPoint, localNormal);
            }
            else
            {
                if (enableFxRpc)
                    HitImpactWorldClientRpc(hit.point, hit.normal);
            }
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
        Debug.DrawLine(from, to, Color.yellow, tracerDuration);
    }

    [ClientRpc]
    void HitImpactWorldClientRpc(Vector3 point, Vector3 normal)
    {
        SpawnImpactFx(point, normal, parent: null);
    }

    [ClientRpc]
    void HitImpactOnEnemyClientRpc(ulong enemyId, Vector3 localPoint, Vector3 localNormal)
    {
        if (impactFxPrefab == null) return;
        if (NetworkManager.Singleton == null) return;

        if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(enemyId, out var netObj))
            return; // 이미 despawn이면 스킵

        Transform t = netObj.transform;
        Vector3 worldPoint = t.TransformPoint(localPoint);
        Vector3 worldNormal = t.TransformDirection(localNormal).normalized;

        // ✅ 적에 붙여서 생성(더 자연스러움)
        SpawnImpactFx(worldPoint, worldNormal, parent: t);
    }

    void SpawnImpactFx(Vector3 point, Vector3 normal, Transform parent)
    {
        if (impactFxPrefab == null) return;

        Vector3 pos = point + normal * impactNormalOffset;
        Quaternion rot = Quaternion.LookRotation(normal);

        var fx = parent != null
            ? Instantiate(impactFxPrefab, pos, rot, parent)
            : Instantiate(impactFxPrefab, pos, rot);

        fx.Play(true);
        Destroy(fx.gameObject, 2f);
    }
}