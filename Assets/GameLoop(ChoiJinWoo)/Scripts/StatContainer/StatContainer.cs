using System.Collections.Generic;

public class StatContainer
{
    private readonly Dictionary<StatType, Stat> stats = new();

    public void AddStat(StatType type, float baseValue = 0f)
    {
        stats[type] = new Stat(baseValue);
    }

    public float GetValue(StatType type)
    {
        return stats[type].Value;
    }

    public void SetBaseValue(StatType type, float value)
    {
        stats[type].ResetBase(value);
    }

    public float GetBaseValue(StatType type)
    {
        return stats[type].BaseValue;
    }

    public void AddModifier(StatType type, Modifier modifier)
    {
        stats[type].AddModifier(modifier);
    }

    public void RemoveModifier(object source)
    {
        foreach (var stat in stats.Values)
            stat.RemoveModifier(source);
    }

    public void RemoveModifier(StatType type, Modifier modifier)
    {
        stats[type].RemoveModifier(modifier);
    }

    public float this[StatType type]
    {
        get => stats[type].Value;
        set
        {
            stats[type] = new Stat(value);
        }
    }
}