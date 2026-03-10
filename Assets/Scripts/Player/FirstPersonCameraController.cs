using UnityEngine;
using Unity.Netcode;

public class FirstPersonCameraController : NetworkBehaviour
{
    [Header("Refs")]
    public Transform cameraPivot;
    public Transform cameraHolderFP;

    [Header("Shoot Origin")]
    public Transform shootOriginFP;

    [Header("Hide owner visuals")]
    public GameObject[] hideForOwner; // Visuals 루트

    [Header("Look")]
    public float sensitivity = 2.2f;
    public float minPitch = -80f;
    public float maxPitch = 80f;
    public float followSmooth = 20f;

    [Header("ADS Zoom")]
    public KeyCode adsKey = KeyCode.Mouse1;
    public float normalFov = 75f;
    public float adsFov = 50f;
    public float fovLerpSpeed = 14f;
    public float adsSensitivityMultiplier = 0.7f;
    public float adsMoveSpeedMultiplier = 0.8f;

    [Header("Camera Collision")]
    public bool enableCameraCollision = false;
    public float cameraCollisionRadius = 0.12f;
    public float cameraCollisionPadding = 0.03f;
    public LayerMask cameraCollisionMask = ~0;

    [Header("Recoil")]
    public float recoilReturnSpeed = 20f;

    public bool IsAds { get; private set; }

    float yaw;
    float pitch;
    float baseSensitivity;

    float recoilYaw;
    float recoilPitch;
    float recoilYawVel;
    float recoilPitchVel;

    Camera cam;
    GunController gun;
    PlayerMove move;
    bool lastAds;

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

        baseSensitivity = sensitivity;
        yaw = transform.eulerAngles.y;
        pitch = 0f;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        HideOwnerVisuals();

        ApplyAdsState(false, true);
        lastAds = false;
    }

    void LateUpdate()
    {
        if (!cam || !cameraPivot || !cameraHolderFP) return;

        IsAds = Input.GetKey(adsKey);
        if (IsAds != lastAds)
        {
            ApplyAdsState(IsAds);
            lastAds = IsAds;
        }

        yaw += Input.GetAxis("Mouse X") * sensitivity;
        pitch -= Input.GetAxis("Mouse Y") * sensitivity;
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

        recoilYaw = Mathf.SmoothDamp(recoilYaw, 0f, ref recoilYawVel, 1f / Mathf.Max(1f, recoilReturnSpeed));
        recoilPitch = Mathf.SmoothDamp(recoilPitch, 0f, ref recoilPitchVel, 1f / Mathf.Max(1f, recoilReturnSpeed));

        float finalYaw = yaw + recoilYaw;
        float finalPitch = pitch + recoilPitch;

        transform.rotation = Quaternion.Euler(0f, finalYaw, 0f);
        cameraPivot.localRotation = Quaternion.Euler(finalPitch, 0f, 0f);

        Vector3 desiredPos = Vector3.Lerp(cam.transform.position, cameraHolderFP.position, followSmooth * Time.deltaTime);
        Quaternion desiredRot = Quaternion.Lerp(cam.transform.rotation, cameraHolderFP.rotation, followSmooth * Time.deltaTime);

        if (enableCameraCollision)
            desiredPos = ResolveCameraCollision(desiredPos);

        cam.transform.position = desiredPos;
        cam.transform.rotation = desiredRot;

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

        RaycastHit[] hits = Physics.SphereCastAll(
            pivotPos,
            cameraCollisionRadius,
            dir,
            dist,
            cameraCollisionMask,
            QueryTriggerInteraction.Ignore);

        float best = float.MaxValue;
        RaycastHit bestHit = default;
        bool found = false;

        for (int i = 0; i < hits.Length; i++)
        {
            var h = hits[i];
            if (h.collider == null) continue;
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
        sensitivity = on ? baseSensitivity * adsSensitivityMultiplier : baseSensitivity;

        if (move != null)
            move.SetExternalSpeedMultiplier(on ? adsMoveSpeedMultiplier : 1f);

        if (gun != null)
        {
            gun.aimCamera = cam;

            if (shootOriginFP != null)
                gun.shootOrigin = shootOriginFP;

            gun.SetAdsServerRpc(on, gun.adsSpreadMultiplierServer);
        }
    }

    void HideOwnerVisuals()
    {
        if (hideForOwner == null) return;

        for (int i = 0; i < hideForOwner.Length; i++)
        {
            if (hideForOwner[i] != null)
                hideForOwner[i].SetActive(false);
        }
    }

    public void AddRecoil(float pitchKick, float yawKick)
    {
        recoilPitch -= Mathf.Abs(pitchKick);
        recoilYaw += yawKick;
    }
}