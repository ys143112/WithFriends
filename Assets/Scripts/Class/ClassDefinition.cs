using UnityEngine;

public enum JobType { Warrior = 0, Archer = 1, Healer = 2 }

[CreateAssetMenu(menuName = "RPG/Class Definition")]
public class ClassDefinition : ScriptableObject
{
    public JobType id;

    [Header("Display")]
    public string displayName;

    [TextArea(3, 6)]
    public string description;   // ✅ 직업 설명 텍스트

    [Header("Base Stats")]
    public int baseHp;
    public int baseAtk;
    public float moveSpeed;
}
