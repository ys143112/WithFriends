using UnityEngine;

public class RelayLauncherUI : MonoBehaviour
{
    public RelayManager relay;
    string inputCode = "";

    void OnGUI()
    {
        if (!relay)
        {
            GUI.Label(new Rect(10, 10, 400, 30), "RelayManager를 연결해줘");
            return;
        }

        GUI.Label(new Rect(10, 10, 250, 25), "Relay Multiplayer");

        if (GUI.Button(new Rect(10, 40, 220, 40), "Start Host (Relay)"))
        {
            _ = relay.StartHostWithRelay(2);
        }

        // ✅ Host가 만든 Join Code 화면 표시
        GUI.Label(new Rect(10, 90, 320, 25), $"Join Code: {relay.LastJoinCode}");

        GUI.Label(new Rect(10, 120, 220, 25), "Enter Join Code:");
        inputCode = GUI.TextField(new Rect(10, 150, 220, 30), inputCode);

        if (GUI.Button(new Rect(10, 190, 220, 40), "Join Client (Relay)"))
        {
            _ = relay.JoinClientWithRelay(inputCode);
        }
    }
}
