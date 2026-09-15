using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 모든 Button에 자동으로 붙는 호버/클릭 사운드 - ButtonSfxAutoAttacher가 씬의 모든 Button에 동적으로 부착한다.
public class ButtonSfx : MonoBehaviour, IPointerEnterHandler
{
    private Button button;
    [SerializeField] private string buttonHoverSFX = "UI_Hover";
    [SerializeField] private string buttonClickSFX = "UI_Click";

    private void Awake()
    {
        button = GetComponent<Button>();
        button.onClick.AddListener(PlayClick);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (button.interactable) EnemySoundManager.Play(buttonHoverSFX);
    }

    private void PlayClick() => EnemySoundManager.Play(buttonClickSFX);
}
