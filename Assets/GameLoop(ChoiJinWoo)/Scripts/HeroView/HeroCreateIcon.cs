using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

// 생성 버튼 하나(근접용 인스턴스 1개, 원거리용 인스턴스 1개). 아이콘/라벨은 인스펙터에서 미리 세팅.
public class HeroCreateIcon : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private List<ResourceCost> resourceCost;
    [SerializeField] private List<ResourceIcon> resourceIcons;
    [SerializeField] private string createName;
    [SerializeField] private Image activeFrame;
    public List<ResourceCost> ResourceCost => resourceCost;
    public List<ResourceIcon> ResourceIcons => resourceIcons;
    public string CreateName => createName;
    private UnityAction currentListener;
    public void Set(bool interactable, Action onClick)
    {
        button.interactable = interactable;
        if (currentListener != null) button.onClick.RemoveListener(currentListener);
        currentListener = () => onClick?.Invoke();
        button.onClick.AddListener(currentListener);
    }

    public void SetInteractable(bool interactable)
    {
        button.interactable = interactable;
    }

    public void SetSelected(bool selected)
    {
        activeFrame.enabled = selected;
    }
}
