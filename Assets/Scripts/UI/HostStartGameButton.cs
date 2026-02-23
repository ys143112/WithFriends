using UnityEngine;
using Unity.Netcode;
using UnityEngine.SceneManagement;

public class HostStartGameButton : MonoBehaviour
{
    public string gameSceneName = "GameScene";

    public void OnClickStart()
    {
        if (!NetworkManager.Singleton)
            return;

        if (!NetworkManager.Singleton.IsHost)
        {
            Debug.Log("Start는 Host만 가능");
            return;
        }

        NetworkManager.Singleton.SceneManager.LoadScene(gameSceneName, LoadSceneMode.Single);
    }
}
