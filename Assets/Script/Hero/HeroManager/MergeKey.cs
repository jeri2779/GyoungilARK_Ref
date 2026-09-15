using System;

public readonly struct MergeKey : IEquatable<MergeKey>
{
    public readonly int UnitId;   // 데이터 테이블상 고유 ID
    public readonly int Tier;     // 별/등급

    public MergeKey(int unitId, int tier) { UnitId = unitId; Tier = tier; }

    public bool Equals(MergeKey other) => UnitId == other.UnitId && Tier == other.Tier;
    public override bool Equals(object o) => o is MergeKey k && Equals(k);
    public override int GetHashCode() => (UnitId * 397) ^ Tier;
}
