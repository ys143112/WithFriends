using UnityEngine;
using Unity.Netcode;

public class ThirdPersonCamera : NetworkBehaviour
{
    [Header("Refs")]
    public Transform cameraPivot;       // pitch 적용 피벗
    public Transform cameraHolderTP;    // 3인칭 목표 포즈
    public Transform cameraHolderADS;   // ADS(완전 1인칭) 목표 포즈

    [Header("Shoot Origins (for GunController)")]
    public Transform shootOriginTP;
    public Transform shootOriginADS;

    [Header("Hide visuals in ADS (Owner only)")]
    public GameObject[] hideWhenAds; // Visuals(3인칭 몸) 루트

    [Header("Look")]
    public float sensitivity = 2.2f;
    public float minPitch = -35f;
    public float maxPitch = 60f;
    public float followSmooth = 18f;

    [Header("ADS (Full FP while RMB)")]
    public KeyCode adsKey = KeyCode.Mouse1;
    public float normalFov = 75f;
    public float adsFov = 55f;
    public float fovLerpSpeed = 14f;

    [Header("ADS Extras")]
    public float adsSensitivityMultiplier = 0.7f;
    public float adsMoveSpeedMultiplier = 0.7f;

    [Header("Camera Collision (ADS only)")]
    public bool enableCameraCollisionInAds = true;
    public float cameraCollisionRadius = 0.15f;
    public float cameraCollisionMaskDistancePadding = 0.02f;
    public LayerMask cameraCollisionMask = ~0; // Wall/Ground만 남기기 권장

    [Header("Recoil (Casual, Local Only)")]
    public float recoilReturnSpeed = 20f;
    float recoilYaw;
    float recoilPitch;
    float recoilYawVel;
    float recoilPitchVel;

    public bool IsAds { get; private set; }

    float yaw;
    float pitch;
    Camera cam;

    bool lastAds;
    float baseSensitivity;

    GunController gun;
    PlayerMove move;

    public override void OnNetworkSpawn()
    {
        if (!IsOwner)
        {
            enabled = false;
            return;
        }

        cam = Camera.main;
        gun = GetComponentInChildren<GunController>(true);
        move = GetComponent<PlayerMove>();

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        yaw = transform.eulerAngles.y;
        pitch = 10f;

        baseSensitivity = sensitivity;

        ApplyAdsState(false, force: true);
        lastAds = false;
    }

    void LateUpdate()
    {
        if (!cam || !cameraPivot || !cameraHolderTP) return;

        IsAds = Input.GetKey(adsKey);
        if (IsAds != lastAds)
        {
            ApplyAdsState(IsAds);
            lastAds = IsAds;
        }

        // 마우스 입력
        yaw += Input.GetAxis("Mouse X") * sensitivity;
        pitch -= Input.GetAxis("Mouse Y") * sensitivity;
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

        // ✅ 반동 복원(부드럽게 0으로)
        recoilYaw = Mathf.SmoothDamp(recoilYaw, 0f, ref recoilYawVel, 1f / Mathf.Max(1f, recoilReturnSpeed));
        recoilPitch = Mathf.SmoothDamp(recoilPitch, 0f, ref recoilPitchVel, 1f / Mathf.Max(1f, recoilReturnSpeed));

        float finalYaw = yaw + recoilYaw;
        float finalPitch = pitch + recoilPitch;

        // 플레이어 yaw 회전
        transform.rotation = Quaternion.Euler(0f, finalYaw, 0f);

        // 피벗에 pitch 적용
        cameraPivot.localRotation = Quaternion.Euler(finalPitch, 0f, 0f);

        // 목표 holder 선택
        Transform targetHolder = (IsAds && cameraHolderADS != null) ? cameraHolderADS : cameraHolderTP;

        // 카메라 따라가기
        Vector3 desiredPos = Vector3.Lerp(cam.transform.position, targetHolder.position, followSmooth * Time.deltaTime);
        Quaternion desiredRot = Quaternion.Lerp(cam.transform.rotation, targetHolder.rotation, followSmooth * Time.deltaTime);

        cam.transform.position = desiredPos;
        cam.transform.rotation = desiredRot;

        // ADS에서만 카메라 충돌 보정(벽에 박히는 것 완화)
        if (IsAds && enableCameraCollisionInAds)
            ApplyCameraCollision();

        // FOV
        float targetFov = IsAds ? adsFov : normalFov;
        cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, targetFov, fovLerpSpeed * Time.deltaTime);
    }

    void ApplyCameraCollision()
    {
        Vector3 pivotPos = cameraPivot.position;
        Vector3 desiredPos = cam.transform.position;
        Vector3 dir = desiredPos - pivotPos;
        float dist = dir.magnitude;
        if (dist < 0.01f) return;

        dir /= dist;

        if (Physics.SphereCast(pivotPos, cameraCollisionRadius, dir,
            out RaycastHit hit, dist, cameraCollisionMask, QueryTriggerInteraction.Ignore))
        {
            cam.transform.position = hit.point - dir * cameraCollisionMaskDistancePadding;
        }
    }

    void ApplyAdsState(bool on, bool force = false)
    {
        SetVisualsHidden(on);

        // 감도/이속 (Owner 로컬만)
        sensitivity = on ? baseSensitivity * adsSensitivityMultiplier : baseSensitivity;
        if (move != null) move.SetExternalSpeedMultiplier(on ? adsMoveSpeedMultiplier : 1f);

        // 총 연동
        if (gun != null)
        {
            gun.aimCamera = cam;

            if (shootOriginTP != null && shootOriginADS != null)
                gun.shootOrigin = on ? shootOriginADS : shootOriginTP;

            // 서버 스프레드도 ADS 반영
            gun.SetAdsServerRpc(on, gun.adsSpreadMultiplierServer);
        }
    }

    void SetVisualsHidden(bool hide)
    {
        if (hideWhenAds == null) return;
        for (int i = 0; i < hideWhenAds.Length; i++)
        {
            if (hideWhenAds[i] != null)
                hideWhenAds[i].SetActive(!hide);
        }
    }

    // ✅ GunController에서 호출(로컬 반동)
    public void AddRecoil(float pitchKick, float yawKick)
    {
        recoilPitch -= Mathf.Abs(pitchKick); // 위로 튕김
        recoilYaw += yawKick;
    }
}