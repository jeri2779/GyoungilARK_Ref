using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
public class HeroTabButton : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI label;
    [SerializeField] private Button button;
    [SerializeField] private Image selectedHighlight;

    public void Set(string text, Action onClick)
    {
        label.text = text;
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => onClick());
    }

    public void SetLabel(string text) => label.text = text;

    public void SetSelected(bool selected) => selectedHighlight.gameObject.SetActive(selected);
}
