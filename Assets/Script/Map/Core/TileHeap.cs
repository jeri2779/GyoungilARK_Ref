using System.Collections.Generic;

// 점수가 가장 작은 타일을 즉시 꺼내는 최소 힙. Pathfinder가 "다음에 살필 칸"을 고르는 용도로만 쓴다.
// Push/Pop 이름은 자료구조 업계 표준 동사를 그대로 따랐다(스택·큐와 같은 관례).
public sealed class TileHeap
{
    private readonly List<Tile> tiles = new();
    private readonly List<int> scores = new();

    public int Count => tiles.Count;

    // 이 타일을 이 점수로 쌓는다. 같은 타일이 더 나은 점수로 다시 들어와도 된다 —
    // 오래된 항목은 나중에 Pathfinder가 closed로 걸러낸다(decrease-key 대신 지연 삭제).
    public void Push(Tile tile, int score)
    {
        tiles.Add(tile);
        scores.Add(score);
        SiftUp(tiles.Count - 1);
    }

    // 가장 작은 점수의 타일을 꺼낸다. 비어 있을 땐 호출부가 Count로 미리 지킨다.
    public Tile Pop()
    {
        Tile top = tiles[0];
        int last = tiles.Count - 1;

        tiles[0] = tiles[last];
        scores[0] = scores[last];
        tiles.RemoveAt(last);
        scores.RemoveAt(last);

        if (tiles.Count > 0)
        {
            SiftDown(0);
        }

        return top;
    }

    private void SiftUp(int index)
    {
        while (index > 0)
        {
            int parent = (index - 1) / 2;
            if (scores[parent] <= scores[index])
            {
                return;
            }

            Swap(parent, index);
            index = parent;
        }
    }

    private void SiftDown(int index)
    {
        int count = tiles.Count;

        while (true)
        {
            int left = index * 2 + 1;
            int right = index * 2 + 2;
            int smallest = index;

            if (left < count && scores[left] < scores[smallest])
            {
                smallest = left;
            }

            if (right < count && scores[right] < scores[smallest])
            {
                smallest = right;
            }

            if (smallest == index)
            {
                return;
            }

            Swap(smallest, index);
            index = smallest;
        }
    }

    private void Swap(int a, int b)
    {
        (tiles[a], tiles[b]) = (tiles[b], tiles[a]);
        (scores[a], scores[b]) = (scores[b], scores[a]);
    }
}
