using UnityEngine;

public class PlayerJob : MonoBehaviour
{
    private PlayerStats stats;

    private void Awake()
    {
        stats = GetComponent<PlayerStats>();
    }

    public void Apply(ClassDefinition def)
    {
        if (def == null)
        {
            Debug.LogError("[PlayerJob] Apply 실패: ClassDefinition(def)이 null");
            return;
        }

        // NGO 타이밍 대비: Awake 전에 호출되거나 stats 캐싱이 실패했을 때 재시도
        if (stats == null)
            stats = GetComponent<PlayerStats>();

        if (stats == null)
        {
            Debug.LogError("[PlayerJob] Apply 실패: PlayerStats가 Player에 없음 (프리팹에 붙였는지 확인)");
            return;
        }

        stats.SetFromClass(def);

        var mover = GetComponent<PlayerMove>();
        if (mover != null)
            mover.SetSpeed(def.moveSpeed);


        Debug.Log($"[PlayerJob] Apply {def.displayName} / HP:{stats.MaxHp} ATK:{stats.Atk} SPD:{stats.MoveSpeed}");
    }
}
