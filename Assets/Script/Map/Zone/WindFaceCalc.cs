using System.Collections.Generic;
using UnityEngine;

// 바람 방향마다 화살표가 설 외곽 면 타일을 뽑는 계산 전담.
public static class WindFaceCalc
{
    // 네 바람 방향에 대응하는 외곽 면 타일 목록을 만든다.
    public static Dictionary<Vector2Int, List<Tile>> BuildFaces(MapBoard board)
    {
        RectInt play = board.PlayRect;
        int west = play.xMin - 1;
        int east = play.xMax;
        int south = play.yMin - 1;
        int north = play.yMax;

        Dictionary<Vector2Int, List<Tile>> faces = new();
        faces[GridCalculator.Right] = ReadColumn(board, west, south, north);
        faces[GridCalculator.Left] = ReadColumn(board, east, south, north);
        faces[GridCalculator.Up] = ReadRow(board, south, west, east);
        faces[GridCalculator.Down] = ReadRow(board, north, west, east);
        return faces;
    }

    // 한 열의 Special 타일을 아래에서 위 순서로 모은다.
    private static List<Tile> ReadColumn(MapBoard board, int column, int minRow, int maxRow)
    {
        List<Tile> face = new();
        for (int row = minRow; row <= maxRow; row++)
        {
            KeepSpecial(board, face, new Vector2Int(column, row));
        }

        return face;
    }

    // 한 행의 Special 타일을 왼쪽에서 오른쪽 순서로 모은다.
    private static List<Tile> ReadRow(MapBoard board, int row, int minColumn, int maxColumn)
    {
        List<Tile> face = new();
        for (int column = minColumn; column <= maxColumn; column++)
        {
            KeepSpecial(board, face, new Vector2Int(column, row));
        }

        return face;
    }

    // 실제로 존재하는 Special 타일만 현재 면에 추가한다.
    private static void KeepSpecial(MapBoard board, List<Tile> face, Vector2Int cell)
    {
        if (board.TryGetCell(cell, out Tile tile) && tile.IsSpecial)
        {
            face.Add(tile);
        }
    }
}
