using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 씬 인스턴스의 타일이 프리팹과 다른 값(오버라이드)을 갖는지 읽는 에디터 전용 도구.
///
/// 프리팹인 줄 알고 씬 인스턴스를 칠하면 그 값은 오버라이드로만 남아,
/// 같은 프리팹을 쓰는 다른 씬엔 영영 반영되지 않는다 — 그 흔적을 눈에 보이게 한다.
/// State.* 중 하나라도 프리팹과 다르면 그 칸을 오버라이드로 본다.
/// 프리팹 스테이지(원본 편집)에서는 비교할 프리팹이 없어 뜻이 없으므로 창이 씬 모드에서만 부른다.
/// </summary>
public static class TileOverride
{
    /// <summary>이 타일의 State가 프리팹과 다른가. 프리팹 원본이거나 프리팹에 속하지 않으면 false.</summary>
    public static bool IsOverridden(Tile tile)
    {
        var reader = new SerializedObject(tile);
        SerializedProperty state = reader.FindProperty("State");
        if (state == null)
        {
            return false;
        }

        SerializedProperty end = state.GetEndProperty();
        SerializedProperty walk = state.Copy();
        walk.NextVisible(true);

        while (!SerializedProperty.EqualContents(walk, end))
        {
            if (walk.prefabOverride)
            {
                return true;
            }

            if (!walk.NextVisible(false))
            {
                break;
            }
        }

        return false;
    }

    /// <summary>격자 칸들 중 프리팹과 다른(오버라이드된) 칸의 좌표 모음.</summary>
    public static HashSet<Vector2Int> Collect(Dictionary<Vector2Int, Tile> cells)
    {
        var marked = new HashSet<Vector2Int>();
        foreach (KeyValuePair<Vector2Int, Tile> entry in cells)
        {
            if (IsOverridden(entry.Value))
            {
                marked.Add(entry.Key);
            }
        }

        return marked;
    }
}
