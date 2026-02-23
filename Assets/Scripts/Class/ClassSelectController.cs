using UnityEngine;
using Unity.Netcode;

public class ClassSelectController : MonoBehaviour
{
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1)) Set(JobType.Warrior);
        if (Input.GetKeyDown(KeyCode.Alpha2)) Set(JobType.Archer);
        if (Input.GetKeyDown(KeyCode.Alpha3)) Set(JobType.Healer);
    }

    void Set(JobType job)
    {
        var p = NetworkManager.Singleton.LocalClient.PlayerObject;
        p.GetComponent<PlayerClassState>()
         .RequestSetJobRpc((int)job);
    }
}
