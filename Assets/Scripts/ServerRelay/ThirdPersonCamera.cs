using UnityEngine;
using Unity.Netcode;

public class ThirdPersonCamera : NetworkBehaviour
{
    [Header("Refs")]
    public Transform cameraPivot;   // CameraPivot
    public Transform cameraHolder;  // CameraHolder

    [Header("Look")]
    public float sensitivity = 2.2f;
    public float minPitch = -35f;
    public float maxPitch = 60f;
    public float followSmooth = 18f;

    float yaw;
    float pitch;
    Camera cam;

    public override void OnNetworkSpawn()
    {
        if (!IsOwner)
        {
            enabled = false;
            return;
        }

        cam = Camera.main;

        // 마우스 락 (원하면 옵션으로 빼도 됨)
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // 시작 각도
        yaw = transform.eulerAngles.y;
        pitch = 10f;
    }

    void LateUpdate()
    {
        if (!cam || !cameraPivot || !cameraHolder) return;

        // 마우스 입력
        yaw += Input.GetAxis("Mouse X") * sensitivity;
        pitch -= Input.GetAxis("Mouse Y") * sensitivity;
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

        // 플레이어는 yaw만 회전 (muck 느낌)
        transform.rotation = Quaternion.Euler(0f, yaw, 0f);

        // 카메라 피벗은 pitch까지 반영
        cameraPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);

        // 카메라 따라가기
        Vector3 targetPos = cameraHolder.position;
        cam.transform.position = Vector3.Lerp(cam.transform.position, targetPos, followSmooth * Time.deltaTime);
        cam.transform.rotation = Quaternion.Lerp(cam.transform.rotation, cameraHolder.rotation, followSmooth * Time.deltaTime);
    }
}
