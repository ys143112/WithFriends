using UnityEngine;
using UnityEngine.UI;

public class CrosshairUI : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private RectTransform rect;
    [SerializeField] private Image image;

    [Header("Size")]
    public float baseSize = 16f;        // 기본 크기
    public float chargeSpread = 14f;    // 차지 100% 때 추가 크기(퍼짐)
    public float lerpSpeed = 12f;

    [Header("Color")]
    public Color normalColor = Color.white;
    public Color fullColor = Color.red;
    public float fullThreshold = 0.99f;

    [Header("Full Pulse")]
    public float pulseSpeed = 8f;
    public float pulseMinAlpha = 0.65f;

    float targetSize;
    float charge01;
    bool visible = true;

    void Awake()
    {
        if (rect == null) rect = GetComponent<RectTransform>();
        if (image == null) image = GetComponent<Image>();

        targetSize = baseSize;
        ApplyVisual(0f);
    }

    void Update()
    {
        if (!visible || rect == null) return;

        // 크기 부드럽게 보간
        float cur = rect.sizeDelta.x;
        float next = Mathf.Lerp(cur, targetSize, Time.unscaledDeltaTime * lerpSpeed);
        rect.sizeDelta = new Vector2(next, next);

        // 완충 펄스(알파)
        if (image != null && charge01 >= fullThreshold && pulseSpeed > 0f)
        {
            float t = (Mathf.Sin(Time.unscaledTime * pulseSpeed) + 1f) * 0.5f;
            float a = Mathf.Lerp(pulseMinAlpha, 1f, t);

            Color c = image.color;
            c.a = a;
            image.color = c;
        }
    }

    /// <summary>
    /// 0~1 차지값에 따라 크로스헤어 퍼짐/색상 갱신
    /// </summary>
    public void SetCharge01(float t01)
    {
        charge01 = Mathf.Clamp01(t01);
        ApplyVisual(charge01);
    }

    public void SetVisible(bool v)
    {
        visible = v;
        if (image) image.enabled = v;
    }

    void ApplyVisual(float t01)
    {
        // 크기 목표
        targetSize = baseSize + chargeSpread * t01;

        // 색
        if (image != null)
        {
            bool full = t01 >= fullThreshold;
            Color c = full ? fullColor : normalColor;
            c.a = 1f; // 기본 알파
            image.color = c;
        }
    }
}
