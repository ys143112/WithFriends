using UnityEngine;
using Unity.Netcode;

public class HpDebugUI : NetworkBehaviour
{
    HealthNetwork hp;

    void Awake() => hp = GetComponent<HealthNetwork>();

    void OnGUI()
    {
        if (!IsOwner || hp == null) return;
        GUI.Label(new Rect(10, 250, 220, 30), $"HP: {hp.CurrentHp.Value}/{hp.MaxHpNet.Value}");
    }
}
