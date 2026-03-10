using UnityEngine;
using System.Collections;

[RequireComponent(typeof(LineRenderer))]
public class BulletTracer : MonoBehaviour
{
    LineRenderer lr;

    public float duration = 0.05f;

    void Awake()
    {
        lr = GetComponent<LineRenderer>();
    }

    public void Play(Vector3 from, Vector3 to)
    {
        lr.positionCount = 2;
        lr.SetPosition(0, from);
        lr.SetPosition(1, to);

        StartCoroutine(CoHide());
    }

    IEnumerator CoHide()
    {
        yield return new WaitForSeconds(duration);
        Destroy(gameObject);
    }
}