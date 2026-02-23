using UnityEngine;

public class ClassDatabaseHolder : MonoBehaviour
{
    public static ClassDatabaseHolder Instance { get; private set; }

    public ClassDatabase Database; // 인스펙터에 연결

    void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }
}
