using UnityEngine;

public class Modifier
{
    private ModifierType type;
    private float value;
    private object source;
    private StatLayer layer = StatLayer.Buff;
    private float duration = 1f;
    public ModifierType Type => type;
    public float Value => value;
    public object Source => source;
    public StatLayer Layer => layer;
    public float Duration => duration;

    public Modifier(ModifierType type, float value, object source)
    {
        this.type = type;
        this.value = value;
        this.source = source;
    }
    public Modifier(ModifierType type, float value, float duration, StatLayer layer, object source)
    {
        this.type = type;
        this.value = value;
        this.duration = duration;
        this.layer = layer;
        this.source = source;
    }
}
