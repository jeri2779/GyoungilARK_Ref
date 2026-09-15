using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using VContainer;

// 지역 안개가 걷힌 뒤 그 지역 기믹을 팝업과 외곽선으로 최초 1회만 안내한다.
public class GimmickRevealController : MonoBehaviour
{
    private static readonly List<Tile> EmptyTiles = new();

    [SerializeField] private MapRegistry registry;
    [SerializeField] private GimmickPopup gimmickPopup;
    [SerializeField] private Material edgeMaterial;
    [SerializeField] private float edgeWidth = 0.06f;
    [SerializeField] private float edgeLift = 0.025f;
    [SerializeField] private float highlightSeconds = 3f;

    private GimmickTileData tileData;
    private CampfireEdgeView edgeView;
    private int edgeVersion;
    private readonly List<ModuleLogic> modules = new();
    private readonly HashSet<int> revealingModules = new();

    // 기믹 기록장을 컨테이너에서 받아 둔다(씬 오브젝트가 아니라 컨테이너가 소유한다).
    [Inject]
    private void Construct(GimmickTileData tileData)
    {
        this.tileData = tileData;
    }

    // 외곽선 그리기 도구를 준비한다.
    private void Awake()
    {
        edgeView = new CampfireEdgeView(transform, edgeMaterial, edgeWidth, edgeLift, "GimmickRevealEdge");
    }

    private void OnEnable()
    {
        FogController.RevealDone += OnRegionRevealed;
    }

    private void OnDisable()
    {
        FogController.RevealDone -= OnRegionRevealed;
    }

    // 지역 목록을 굳혀두고 상태 변경 알림을 받아 둔다.
    private void Start()
    {
        CollectModules();
        BindModules();
    }

    private void OnDestroy()
    {
        UnbindModules();
        edgeView.Dispose();
    }

    // 등록된 지역들을 번호로 훑을 수 있게 목록으로 옮겨 담는다.
    private void CollectModules()
    {
        // registry.AllModules는 사전이라 인덱스 순회가 불가능해 여기서만 foreach를 쓴다.
        foreach (ModuleLogic module in registry.AllModules.Values)
        {
            modules.Add(module);
        }
    }

    // 지역마다 해금 알림과 상태 변경 알림을 받아 둔다.
    private void BindModules()
    {
        for (int index = 0; index < modules.Count; index++)
        {
            modules[index].OnUnlocked += OnModuleUnlocked;
            modules[index].OnStateChanged += OnModuleState;
        }
    }

    // 받아 뒀던 알림들을 끊는다.
    private void UnbindModules()
    {
        for (int index = 0; index < modules.Count; index++)
        {
            modules[index].OnUnlocked -= OnModuleUnlocked;
            modules[index].OnStateChanged -= OnModuleState;
        }
    }

    // 게임 중 새로 해금된 지역은 안개 연출이 맡으므로 즉시 새김 대상에서 뺀다.
    private void OnModuleUnlocked(ModuleLogic module)
    {
        revealingModules.Add(module.ModuleId);
    }

    // 상태가 바뀌면(세이브 복원 포함) 이미 열려 있는 기믹 지역을 안내 목록에 올린다.
    private void OnModuleState(ModuleState state)
    {
        for (int index = 0; index < modules.Count; index++)
        {
            MarkWhenAlreadyOpen(modules[index]);
        }
    }

    // 연출 없이 열려 있고 기믹 칸이 있는 지역만 조용히 봤음으로 새긴다.
    private void MarkWhenAlreadyOpen(ModuleLogic module)
    {
        if (revealingModules.Contains(module.ModuleId)) return;
        if (!module.IsUnlocked) return;
        if (tileData.WasShown(module.ModuleId)) return;
        if (GimmickTileCalc.CollectTiles(module.GetComponent<MapBoard>()).Count == 0) return;

        tileData.MarkShown(module.ModuleId);
    }

    // 안개가 걷힌 지역에 기믹 칸이 있으면 안내를 연출한다.
    private void OnRegionRevealed(int moduleId)
    {
        if (tileData.WasShown(moduleId))
        {
            return;
        }
        if (!registry.TryGetModuleLogic(moduleId, out ModuleLogic module))
        {
            return;
        }

        List<Tile> tiles = GimmickTileCalc.CollectTiles(module.GetComponent<MapBoard>());
        if (tiles.Count == 0)
        {
            return;
        }

        tileData.StoreTiles(moduleId, tiles);
        tileData.MarkShown(moduleId);
        PlayReveal(tiles);
    }

    // 팝업을 띄우고 대상 칸에 외곽선을 켠다.
    private void PlayReveal(List<Tile> tiles)
    {
        Tile sample = tiles[0];
        gimmickPopup.Show(GimmickKeyCalc.MakeNameKey(sample), GimmickKeyCalc.MakeDescKey(sample));
        ShowEdge(tiles);
        HideEdgeAsync().Forget();
    }

    // 넘겨받은 칸들로 외곽선을 새로 그린다.
    private void ShowEdge(List<Tile> tiles)
    {
        edgeVersion++;
        edgeView.Show(tiles, edgeVersion);
    }

    // 정해둔 시간이 지나면 외곽선을 지운다.
    private async UniTaskVoid HideEdgeAsync()
    {
        await UniTask.Delay(TimeSpan.FromSeconds(highlightSeconds),
            cancellationToken: this.GetCancellationTokenOnDestroy());
        ShowEdge(EmptyTiles);
    }
}
