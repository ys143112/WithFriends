using UnityEngine;

public class PlayerStats : MonoBehaviour
{
    public int MaxHp;
    public int Atk;
    public float MoveSpeed;

    // 전투용 추가
    public float AttackRange = 1.8f;   // Warrior 기본
    public float AttackCooldown = 0.4f;

    public void SetFromClass(ClassDefinition def)
    {
        MaxHp = def.baseHp;
        Atk = def.baseAtk;
        MoveSpeed = def.moveSpeed;
    }
}
