using System;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class BaseUpgradeButton : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private Image icon;
    [SerializeField] private Image buttonImage;
    [SerializeField] private Sprite lockedIcon;
    [SerializeField] private Sprite unlockedIcon;
    [SerializeField, Range(0f, 1f)] private float lockedAlpha = 0.3f;

    public BaseUpgradeData Data { get; private set; }
    public event Action<BaseUpgradeData> Clicked;

    public void Set(BaseUpgradeData data, bool unlocked, bool canUnlock)
    {
        Data = data;
        icon.sprite = data.icon;
        icon.color = data.iconColor;
        buttonImage.sprite = unlocked ? unlockedIcon : lockedIcon;
        var color = buttonImage.color;
        color.a = lockedAlpha;
        icon.color = canUnlock ? buttonImage.color : color;
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => Clicked?.Invoke(data));
    }
}
