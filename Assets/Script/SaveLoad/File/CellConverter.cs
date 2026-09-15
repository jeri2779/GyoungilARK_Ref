using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

// Vector2Int를 x·y 두 값만 저장하도록 바꾸는 변환기 (기본 직렬화는 magnitude 등 계산값까지 같이 저장됨)
public class CellConverter : JsonConverter
{
    // 이 변환기가 다룰 타입인지 판단한다
    public override bool CanConvert(Type targetType)
    {
        return targetType == typeof(Vector2Int);
    }

    // Vector2Int를 x, y만 담은 JSON으로 적는다
    public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
    {
        Vector2Int cell = (Vector2Int)value;
        writer.WriteStartObject();
        writer.WritePropertyName("x");
        writer.WriteValue(cell.x);
        writer.WritePropertyName("y");
        writer.WriteValue(cell.y);
        writer.WriteEndObject();
    }

    // JSON의 x, y 값으로 Vector2Int를 되살린다. 좌표가 없거나 숫자가 아니면 JsonSerializationException으로 통일해서 던진다
    public override object ReadJson(JsonReader reader, Type targetType, object existingValue, JsonSerializer serializer)
    {
        JObject cellObject = JObject.Load(reader);
        JToken xToken = cellObject["x"];
        JToken yToken = cellObject["y"];
        if (IsMissingCoordinate(xToken, yToken))
        {
            throw new JsonSerializationException("Vector2Int 좌표에 x 또는 y가 없습니다.");
        }
        return ParseCell(xToken, yToken);
    }

    // x, y 토큰이 둘 다 있는지 본다
    private bool IsMissingCoordinate(JToken xToken, JToken yToken)
    {
        return xToken == null || yToken == null;
    }

    // x, y 토큰을 정수로 바꾼다. 숫자가 아니면 JsonSerializationException으로 통일해서 던진다
    private Vector2Int ParseCell(JToken xToken, JToken yToken)
    {
        try
        {
            int cellX = (int)xToken;
            int cellY = (int)yToken;
            return new Vector2Int(cellX, cellY);
        }
        catch (SystemException castException)
        {
            throw new JsonSerializationException("Vector2Int 좌표 값이 숫자가 아닙니다.", castException);
        }
    }
}
