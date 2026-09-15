using System;
using System.Collections.Generic;
using UnityEngine;

// 사막 지대 전용 — 밤마다 보드를 훑어 바람 노출 여부를 판정합니다.
public class DesertZoneEffect : ZoneDebuffEffect, IDisposable
{
    private readonly DesertZone desertZone;
    private readonly WindShelterData shelterData;
    private readonly WindwallData windwallData;
    private readonly WindPreview preview;
    private readonly DesertLineEffect lineEffect;
    private readonly GameManager gameManager;

    // 사막 판정에 필요한 고정 정보와 시각효과 담당을 보관합니다.
    public DesertZoneEffect(
        DesertZone desertZone,
        MapBoard desertBoard,
        WindShelterData shelterData,
        WindwallData windwallData,
        PlacedUnitData unitList,
        WindPreview preview,
        DesertLineEffect lineEffect,
        GameManager gameManager)
        : base(desertBoard, unitList, desertZone.Debuffs)
    {
        this.desertZone = desertZone;
        this.shelterData = shelterData;
        this.windwallData = windwallData;
        this.preview = preview;
        this.lineEffect = lineEffect;
        this.gameManager = gameManager;
    }

    // 생성한 낮 안내 화살표 자원을 정리합니다.
    public void Dispose()
    {
        preview.Dispose();
        lineEffect.Hide();
    }

    // 낮이 시작되면 어제 걸린 사막 효과를 지우고 새 바람과 화살표를 준비합니다.
    protected override void RunDay()
    {
        ClearBoard();
        lineEffect.Hide();
        Vector2Int wind = WindPicker.Pick(gameManager.GameSeed, gameManager.DayCount);
        desertZone.SetWindDirection(wind);
        preview.Show(wind);
        ReportWind();
    }

    // 밤이 시작되면 사막 보드의 영웅을 훑어 노출 여부에 따라 디버프를 확정합니다.
    protected override void RunNight()
    {
        preview.Hide();
        BoardHeroes heroes = ReadHeroes();
        ApplyNight(heroes);
    }

    // 같은 밤의 고정 유닛 가림막을 만들고 각 영웅의 노출 결과를 적용합니다.
    private void ApplyNight(BoardHeroes heroes)
    {
        Vector2Int wind = desertZone.WindDirection;
        UnitShelter unitShelter = new(ReadTiles(heroes));
        DesertEffectData effectData = new DesertEffectCalc().BuildData(board, shelterData, unitShelter, windwallData, wind);
        lineEffect.Show(wind, effectData);
        ApplyEach(heroes, wind, unitShelter);
    }

    // 영웅마다 노출 여부를 판정해 사막 효과를 적용합니다.
    private void ApplyEach(BoardHeroes heroes, Vector2Int wind, UnitShelter unitShelter)
    {
        for (int index = 0; index < heroes.Heroes.Count; index++)
        {
            ApplyWind(heroes.Heroes[index], heroes.Areas[index], wind, unitShelter);
        }
    }

    // 한 영웅의 고지·유닛·가림막 보호 결과에 맞춰 사막 효과를 적용합니다.
    private void ApplyWind(Hero hero, PlacementArea area, Vector2Int wind, UnitShelter unitShelter)
    {
        if (!board.TryGetCell(area.Origin, out Tile tile))
        {
            return;
        }

        bool unsheltered = DesertShelterQuery.IsUnsheltered(shelterData, unitShelter, windwallData, tile, wind);
        ApplyUnsheltered(hero, unsheltered);
        LogShelter(hero, area.Origin, wind, unsheltered);
    }

    // 가려지지 않은 영웅에게만 사막 디버프를 적용합니다.
    private void ApplyUnsheltered(Hero hero, bool unsheltered)
    {
        if (!unsheltered)
        {
            return;
        }

        Apply(hero);
    }

    // 영웅의 확정된 밤 노출 상태를 로그로 출력합니다.
    private static void LogShelter(Hero hero, Vector2Int cell, Vector2Int wind, bool unsheltered)
    {
        // Debug.Log($"[Zone] {hero.name} 사막 밤 - 좌표={cell} 바람={wind} 노출={unsheltered}");
    }

    // 현재 바람 방향을 로그로 출력합니다.
    private void ReportWind()
    {
        Vector2Int wind = desertZone.WindDirection;
        string directionText = WindDirectionText.ReadDirection(wind);
        // Debug.Log($"[Zone] 현재 바람={directionText} 벡터={wind}");
    }

    // 영웅 목록에서 실제 서 있는 타일만 모읍니다.
    private List<Tile> ReadTiles(BoardHeroes heroes)
    {
        List<Tile> tiles = new();
        for (int index = 0; index < heroes.Areas.Count; index++)
        {
            KeepTile(heroes.Areas[index].Origin, tiles);
        }

        return tiles;
    }

    // 좌표에 실제 타일이 있을 때만 목록에 담습니다.
    private void KeepTile(Vector2Int origin, List<Tile> tiles)
    {
        if (!board.TryGetCell(origin, out Tile tile))
        {
            return;
        }

        tiles.Add(tile);
    }
}
