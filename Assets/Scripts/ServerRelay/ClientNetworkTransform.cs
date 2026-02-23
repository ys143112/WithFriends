using Unity.Netcode.Components;

public class ClientNetworkTransform : NetworkTransform
{
    // 서버 권한이 아니라 Owner 권한으로 동기화하도록
    protected override bool OnIsServerAuthoritative()
    {
        return false;
    }
}
