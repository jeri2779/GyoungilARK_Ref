using System;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HeroInfoUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI heroInfoText;
    [SerializeField] private Image heroIcon;
    [SerializeField] private Button button;

    public void Set(Placeable slot, Action<Placeable> onclick)
    {
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => onclick?.Invoke(slot));
        var temp = slot.prefab.GetComponent<Hero>();
        //heroIcon.sprite = temp.Icon;
        var sb = new StringBuilder();
        //sb.Append($"영웅 이름\n영웅 설명\n공격력: {temp.PreviewAttackPower} 방어력: {temp.PreviewDefence} 체력: {temp.StatData.maxHp}\n사거리: {temp.Range} 인구 수: {temp.CitizenAmount}\n소모 자원\n");
        //foreach(var item in temp.Cost)
        //{
        //    sb.Append($"{item.Type} : {-item.Amount} ");
        //}
        //if(temp.Cost.Length == 0)
        //{
        //    sb.Append("소모 자원 없음");
        //}
        heroInfoText.text = sb.ToString();
    }
}
