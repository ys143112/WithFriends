using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class HitMarkerUI : MonoBehaviour
{
    public static HitMarkerUI Instance;

    public Image marker;
    public float showTime = 0.08f;

    void Awake()
    {
        Instance = this;
        marker.enabled = false;
    }

    public void Show()
    {
        StopAllCoroutines();
        StartCoroutine(CoShow());
    }

    IEnumerator CoShow()
    {
        marker.enabled = true;
        yield return new WaitForSeconds(showTime);
        marker.enabled = false;
    }
}