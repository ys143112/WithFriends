using UnityEngine;
using Unity.Netcode;
using System.Collections;

public class CombatInteractor : NetworkBehaviour
{
    PlayerStats stats;
    PlayerClassState classState;

    [Header("Animation")]
    public Animator animator;

    static readonly int AnimAttack = Animator.StringToHash("Attack");
    static readonly int AnimShoot = Animator.StringToHash("Shoot");
    static readonly int AnimHeal = Animator.StringToHash("Heal");
    static readonly int AnimCharging = Animator.StringToHash("Charging");
    static readonly int AnimCharge01 = Animator.StringToHash("Charge01");

    [Header("Aim")]
    public Camera aimCamera;
    public LayerMask aimMask = ~0;
    public float aimMaxDistance = 200f;

    [Header("Shoot Origin")]
    public Transform shootOrigin;

    [Header("Archer - Charge Shot")]
    public NetworkObject arrowPrefab;
    public float minArrowSpeed = 18f;
    public float maxArrowSpeed = 35f;
    public float minArrowLife = 2.0f;
    public float maxArrowLife = 4.0f;
    public float chargeMaxTime = 1.2f;

    [Header("Warrior - Slash Projectile")]
    public NetworkObject slashPrefab;
    public float slashSpeed = 40f;
    public float slashLifeTime = 0.6f;

    [Header("Healer - Basic Attack Projectile")]
    public NetworkObject healerBoltPrefab;
    public float healerBoltSpeed = 28f;
    public float healerBoltLifeTime = 3.0f;

    [Header("Charge Visual")]
    public Transform chargeVisual;
    public Vector3 chargeLocalOffset = new Vector3(0f, 0f, -0.12f);
    public float chargeVisualLerp = 12f;

    [Header("UI")]
    public ChargeBarUI chargeBarUI;
    public CrosshairUI crosshairUI;

    float nextActionTime;
    bool isCharging;
    float chargeStartTime;
    Vector3 chargeVisualBaseLocalPos;

    void Awake()
    {
        stats = GetComponent<PlayerStats>();
        classState = GetComponent<PlayerClassState>();

        if (animator == null)
            animator = GetComponentInChildren<Animator>();
    }

    void Start()
    {
        if (!IsOwner) return;

        if (aimCamera == null)
            aimCamera = Camera.main;

        if (chargeVisual != null)
            chargeVisualBaseLocalPos = chargeVisual.localPosition;
    }

    void Update()
    {
        if (!IsOwner) return;

        var job = classState != null ? classState.CurrentJob : JobType.Warrior;

        // 좌클릭
        if (job == JobType.Archer)
        {
            if (Input.GetMouseButtonDown(0)) BeginCharge();
            if (Input.GetMouseButtonUp(0)) ReleaseCharge();

            UpdateChargeVisual();
        }
        else
        {
            if (Input.GetMouseButtonDown(0))
                TryPrimaryInstant();
        }

        // 우클릭
        if (Input.GetMouseButtonDown(1))
            TrySecondary();
    }

    // -------------------------
    // Client-side input

    void BeginCharge()
    {
        if (Time.time < nextActionTime) return;

        isCharging = true;
        chargeStartTime = Time.time;

        if (animator)
            animator.SetBool(AnimCharging, true);

        if (chargeBarUI != null)
            chargeBarUI.SetVisible(true);
    }

    void ReleaseCharge()
    {
        if (!isCharging) return;
        isCharging = false;

        if (animator)
        {
            animator.SetBool(AnimCharging, false);
            animator.SetTrigger(AnimShoot);
            animator.SetFloat(AnimCharge01, 0f);
        }

        if (chargeBarUI != null)
        {
            chargeBarUI.SetCharge01(0f);
            chargeBarUI.SetVisible(false);
        }

        if (Time.time < nextActionTime) return;

        float charge01 = Mathf.Clamp01((Time.time - chargeStartTime) / chargeMaxTime);
        nextActionTime = Time.time + (stats != null ? stats.AttackCooldown : 0.5f);

        Vector3 aimPoint = GetAimPoint();
        RequestPrimaryRpc(aimPoint, charge01);
    }

    void TryPrimaryInstant()
    {
        if (Time.time < nextActionTime) return;
        nextActionTime = Time.time + (stats != null ? stats.AttackCooldown : 0.5f);

        if (animator)
            animator.SetTrigger(AnimAttack);

        Vector3 aimPoint = GetAimPoint();
        RequestPrimaryRpc(aimPoint, 0f);
    }

    void TrySecondary()
    {
        if (Time.time < nextActionTime) return;
        nextActionTime = Time.time + 0.8f;

        var job = classState != null ? classState.CurrentJob : JobType.Warrior;
        if (job == JobType.Healer && animator)
            animator.SetTrigger(AnimHeal);

        RequestSecondaryRpc();
    }

    void UpdateChargeVisual()
    {
        float t = isCharging
            ? Mathf.Clamp01((Time.time - chargeStartTime) / chargeMaxTime)
            : 0f;

        if (animator)
            animator.SetFloat(AnimCharge01, t);

        if (chargeBarUI != null)
            chargeBarUI.SetCharge01(t);

        if (crosshairUI != null)
            crosshairUI.SetCharge01(t);

        if (chargeVisual != null)
        {
            Vector3 targetLocal = chargeVisualBaseLocalPos + chargeLocalOffset * t;
            chargeVisual.localPosition =
                Vector3.Lerp(chargeVisual.localPosition, targetLocal, Time.deltaTime * chargeVisualLerp);
        }
    }

    // -------------------------
    // Aim helpers

    Vector3 GetAimPoint()
    {
        var cam = aimCamera != null ? aimCamera : Camera.main;
        if (cam == null)
            return transform.position + transform.forward * 20f;

        Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f));
        if (Physics.Raycast(ray, out RaycastHit hit, aimMaxDistance, aimMask))
            return hit.point;

        return ray.GetPoint(aimMaxDistance);
    }

    Vector3 GetOriginPos()
    {
        if (shootOrigin != null) return shootOrigin.position;
        return transform.position + transform.forward * 1.0f + Vector3.up * 1.2f;
    }

    // -------------------------
    // RPCs

    [Rpc(SendTo.Server)]
    void RequestPrimaryRpc(Vector3 aimPoint, float charge01)
    {
        var job = classState != null ? classState.CurrentJob : JobType.Warrior;

        switch (job)
        {
            case JobType.Warrior:
                ServerWarriorSlash(aimPoint);
                break;

            case JobType.Archer:
                ServerShootArrow(aimPoint, charge01);
                break;

            case JobType.Healer:
                ServerHealerBasicAttack(aimPoint);
                break;
        }
    }

    [Rpc(SendTo.Server)]
    void RequestSecondaryRpc()
    {
        var job = classState != null ? classState.CurrentJob : JobType.Warrior;
        if (job == JobType.Healer)
            ServerHealNearest(12);
    }

    // -------------------------
    // Server-side actions

    void ServerWarriorSlash(Vector3 aimPoint)
    {
        if (slashPrefab == null) return;

        int dmg = Mathf.Max(1, stats != null ? stats.Atk : 1);

        Vector3 origin = GetOriginPos();
        Vector3 to = aimPoint - origin;
        if (to.sqrMagnitude < 0.01f) to = transform.forward;

        var obj = Instantiate(slashPrefab, origin, Quaternion.LookRotation(to));
        obj.Spawn(true);

        var slash = obj.GetComponent<SlashProjectile>();
        if (slash != null)
            slash.InitToTarget(aimPoint, dmg, slashSpeed, slashLifeTime);
    }

    void ServerShootArrow(Vector3 aimPoint, float charge01)
    {
        if (arrowPrefab == null) return;

        int baseDmg = Mathf.Max(1, stats != null ? stats.Atk : 1);
        float speed = Mathf.Lerp(minArrowSpeed, maxArrowSpeed, charge01);
        float life = Mathf.Lerp(minArrowLife, maxArrowLife, charge01);
        int dmg = Mathf.RoundToInt(baseDmg * Mathf.Lerp(1.0f, 1.8f, charge01));

        Vector3 origin = GetOriginPos();
        Vector3 to = aimPoint - origin;
        if (to.sqrMagnitude < 0.01f) to = transform.forward;

        var obj = Instantiate(arrowPrefab, origin, Quaternion.LookRotation(to));
        obj.Spawn(true);

        var arrow = obj.GetComponent<ArrowProjectile>();
        if (arrow != null)
            arrow.InitToTarget(aimPoint, dmg, speed, life, OwnerClientId);
    }

    void ServerHealerBasicAttack(Vector3 aimPoint)
    {
        if (healerBoltPrefab == null) return;

        int dmg = Mathf.Max(1, stats != null ? stats.Atk : 1);

        Vector3 origin = GetOriginPos();
        Vector3 to = aimPoint - origin;
        if (to.sqrMagnitude < 0.01f) to = transform.forward;

        var obj = Instantiate(healerBoltPrefab, origin, Quaternion.LookRotation(to));
        obj.Spawn(true);

        var proj = obj.GetComponent<ArrowProjectile>();
        if (proj != null)
            proj.InitToTarget(aimPoint, dmg, healerBoltSpeed, healerBoltLifeTime, OwnerClientId);
    }

    void ServerHealNearest(int amount)
    {
        var target = FindNearestPlayerInRange(4f);
        if (target != null)
            target.ServerHeal(amount);
    }

    HealthNetwork FindNearestPlayerInRange(float range)
    {
        var myPos = transform.position;
        HealthNetwork best = null;
        float bestDist = float.MaxValue;

        foreach (var hn in FindObjectsByType<HealthNetwork>(FindObjectsSortMode.None))
        {
            float d = Vector3.Distance(myPos, hn.transform.position);
            if (d <= range && d < bestDist)
            {
                bestDist = d;
                best = hn;
            }
        }
        return best;
    }
}
