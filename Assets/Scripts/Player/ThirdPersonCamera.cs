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

    [Header("Camera Collision (TP + ADS)")]
    public bool enableCameraCollision = true;

    [Tooltip("카메라를 구(반지름)로 보고 충돌 체크. 값이 작으면 땅/벽에 더 잘 박힘.")]
    public float cameraCollisionRadius = 0.18f;

    [Tooltip("충돌면에서 카메라를 얼마나 띄울지(너무 작으면 살짝 관통/깜빡 가능).")]
    public float cameraCollisionPadding = 0.05f;

    [Tooltip("Ground/Wall/Environment만 포함 권장 (Player/Enemy 제외)")]
    public LayerMask cameraCollisionMask = ~0;

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

        // 목표 포즈로 부드럽게 따라가기(원본 desired)
        Vector3 desiredPos = Vector3.Lerp(cam.transform.position, targetHolder.position, followSmooth * Time.deltaTime);
        Quaternion desiredRot = Quaternion.Lerp(cam.transform.rotation, targetHolder.rotation, followSmooth * Time.deltaTime);

        // ✅ TP/ADS 모두 충돌 보정
        if (enableCameraCollision)
            desiredPos = ResolveCameraCollision(desiredPos);

        cam.transform.position = desiredPos;
        cam.transform.rotation = desiredRot;

        // FOV
        float targetFov = IsAds ? adsFov : normalFov;
        cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, targetFov, fovLerpSpeed * Time.deltaTime);
    }

    Vector3 ResolveCameraCollision(Vector3 desiredCamPos)
    {
        Vector3 pivotPos = cameraPivot.position;

        Vector3 dir = desiredCamPos - pivotPos;
        float dist = dir.magnitude;
        if (dist < 0.01f) return desiredCamPos;

        dir /= dist;

        // 여러 개 맞을 수 있으니 "자기 몸" 제외하고 가장 가까운 것 선택
        RaycastHit[] hits = Physics.SphereCastAll(
            pivotPos,
            cameraCollisionRadius,
            dir,
            dist,
            cameraCollisionMask,
            QueryTriggerInteraction.Ignore);

        if (hits == null || hits.Length == 0)
            return desiredCamPos;

        float best = float.MaxValue;
        RaycastHit bestHit = default;
        bool found = false;

        for (int i = 0; i < hits.Length; i++)
        {
            var h = hits[i];
            if (h.collider == null) continue;

            // ✅ 자기 자신(플레이어 루트/자식 콜라이더)이면 무시
            if (h.collider.transform.root == transform) continue;

            if (h.distance < best)
            {
                best = h.distance;
                bestHit = h;
                found = true;
            }
        }

        if (!found) return desiredCamPos;

        return bestHit.point - dir * cameraCollisionPadding;
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