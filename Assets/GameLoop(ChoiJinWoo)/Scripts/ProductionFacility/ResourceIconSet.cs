using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class ResourceIconEntry
{
    public ProductionType type;
    public Sprite icon;
}

// 자원 타입 -> 아이콘 매핑. 어떤 자원이 나올지 미리 알 수 없는 곳(건물 패널의 생산/업그레이드 비용 등)에서
// ProductionType만 보고 런타임에 아이콘을 찾아 쓴다.
[CreateAssetMenu(fileName = "ResourceIconSet", menuName = "Scriptable Objects/ResourceIconSet")]
public class ResourceIconSet : ScriptableObject
{
    [SerializeField] private List<ResourceIconEntry> icons;

    public Sprite GetIcon(ProductionType type)
    {
        foreach (var entry in icons)
        {
            if (entry.type == type) return entry.icon;
        }
        return null;
    }
}
