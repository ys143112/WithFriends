using UnityEngine;
using Unity.Netcode;

public class PlayerStats : NetworkBehaviour
{
    [Header("Base Stats")]
    public int maxHp = 100;

    public int MaxHp => maxHp;

    public override void OnNetworkSpawn()
    {
        // 필요 시 서버 초기화 로직
    }
}