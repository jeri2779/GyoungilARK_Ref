using System;
using System.Globalization;

// 저장 시각 문자열을 화면 표시용 짧은 형식으로 계산한다.
public static class SaveTimeCalc
{
    // 저장 시각을 파싱해 짧은 형식으로 바꾼다. 파싱 실패 시 대시를 반환한다.
    public static string FormatSaveTime(string saveTime)
    {
        bool parsed = DateTime.TryParse(
            saveTime,
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind,
            out DateTime savedAt);

        if (!parsed)
        {
            return "-";
        }

        return savedAt.ToString("MM-dd HH:mm", CultureInfo.InvariantCulture);
    }
}
