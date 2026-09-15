using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 건설 선택 팝업의 옵션 버튼 하나 - 해당 BuildableFacility의 아이콘/이름을 그대로 보여준다.
public class BuildOptionView : MonoBehaviour
{
    [SerializeField] private Image icon;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private Button button;

    private int index;
    public int Index => index;

    public void SetIndex(int value)
    {
        index = value;
    }

    public void SetOption(Sprite optionIcon, string label)
    {
        if (icon != null) icon.sprite = optionIcon;
        if (nameText != null) nameText.text = label;
    }

    public void SetInteractable(bool interactable)
    {
        if (button != null) button.interactable = interactable;
    }

    public void BindClick(Action<int> onClick)
    {
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => onClick(index));
    }
}
