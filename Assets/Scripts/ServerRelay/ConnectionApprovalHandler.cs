using Unity.Netcode;
using UnityEngine;

public class ConnectionApprovalHandler : MonoBehaviour
{
    void Awake()
    {
        NetworkManager.Singleton.ConnectionApprovalCallback += ApprovalCheck;
    }

    void OnDestroy()
    {
        if (NetworkManager.Singleton != null)
            NetworkManager.Singleton.ConnectionApprovalCallback -= ApprovalCheck;
    }

    void ApprovalCheck(
        NetworkManager.ConnectionApprovalRequest request,
        NetworkManager.ConnectionApprovalResponse response)
    {
        // ✅ 연결은 허용
        response.Approved = true;

        // ❌ 하지만 Player는 자동 생성하지 않음
        response.CreatePlayerObject = false;

        // 여기선 아직 씬 이동 안 함
        response.Pending = false;
    }
}
