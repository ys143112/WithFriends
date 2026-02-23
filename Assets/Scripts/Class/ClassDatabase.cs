using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(menuName = "RPG/Class Database")]
public class ClassDatabase : ScriptableObject
{
    public List<ClassDefinition> classes;

    Dictionary<JobType, ClassDefinition> map;

    public ClassDefinition Get(JobType id)
    {
        if (map == null)
        {
            map = new();
            foreach (var c in classes)
                if (!map.ContainsKey(c.id))
                    map.Add(c.id, c);
        }
        return map[id];
    }
}