public interface IZoneEffect
{
    // 낮이 시작되면 실행할 지대 효과 반응입니다.
    void OnDayChanged();

    // 밤이 시작되면 실행할 지대 효과 반응입니다.
    void OnNightChanged();
}
