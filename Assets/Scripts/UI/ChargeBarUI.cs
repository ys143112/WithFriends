using UnityEngine;
using UnityEngine.UI;

public class ChargeBarUI : MonoBehaviour
{
    [SerializeField] private Image barImage;

    [Header("Full Charge Visual")]
    [SerializeField] private float fullThreshold = 0.99f;
    [SerializeField] private Color fullColor = Color.red;     // 🔴 완충 색
    [SerializeField] private float pulseSpeed = 6f;           // 반짝 속도
    [SerializeField] private float pulseMinAlpha = 0.6f;

    Color baseColor;
    bool visible;
    bool isFull;

    void Awake()
    {
        if (barImage == null)
            barImage = GetComponent<Image>();

        if (barImage != null)
            baseColor = barImage.color;

        SetCharge01(0f);
        SetVisible(false);
    }

    void Update()
    {
        if (!visible || barImage == null) return;

        // 완충 상태면 빨간색 유지 + 알파 펄스
        if (isFull && pulseSpeed > 0f)
        {
            float t = (Mathf.Sin(Time.unscaledTime * pulseSpeed) + 1f) * 0.5f;
            float a = Mathf.Lerp(pulseMinAlpha, 1f, t);

            Color c = barImage.color;
            c.a = a;
            barImage.color = c;
        }
    }

    public void SetCharge01(float t01)
    {
        if (barImage == null) return;

        t01 = Mathf.Clamp01(t01);
        barImage.fillAmount = t01;

        bool nowFull = t01 >= fullThreshold;
        if (nowFull != isFull)
        {
            isFull = nowFull;
            ApplyColorState();
        }

        if (!isFull)
        {
            // 차지 중에는 기본 색 유지
            Color c = baseColor;
            barImage.color = c;
        }
    }

    public void SetVisible(bool v)
    {
        visible = v;
        if (barImage == null) return;

        barImage.enabled = v;

        if (!v)
        {
            // 리셋
            isFull = false;
            barImage.fillAmount = 0f;
            barImage.color = baseColor;
        }
        else
        {
            ApplyColorState();
        }
    }

    void ApplyColorState()
    {
        if (barImage == null) return;

        if (isFull)
        {
            Color c = fullColor;
            c.a = baseColor.a;   // 알파는 기본값 유지
            barImage.color = c;
        }
        else
        {
            barImage.color = baseColor;
        }
    }
}
