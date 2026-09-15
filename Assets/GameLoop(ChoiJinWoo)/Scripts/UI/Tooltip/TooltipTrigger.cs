using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

// 버튼 등 UI 오브젝트에 붙이면 마우스 호버 시 TooltipUi에 message를 띄운다.
// hoverDelay만큼 머물러야 뜨게 해서, 스쳐 지나가는 호버에는 반응하지 않는다.
public class TooltipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField, TextArea] private string message;
    [SerializeField] private bool isJust = false;
    private float hoverDelay = 0.2f;

    private CancellationTokenSource cts;

    public void OnPointerEnter(PointerEventData eventData)
    {
        cts?.Cancel();
        cts = new CancellationTokenSource();
        DelayedShow(cts.Token).Forget();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        cts?.Cancel();
        TooltipUi.Instance.Hide();
    }

    private void OnDisable()
    {
        // 호버 도중 버튼/패널이 꺼지면(패널 토글 등) PointerExit이 안 불릴 수 있어 여기서도 닫아준다.
        cts?.Cancel();
        if (TooltipUi.Instance != null) TooltipUi.Instance.Hide();
    }

    public void SetMessaege(string message)
    {
        this.message = message;
    }

    private async UniTaskVoid DelayedShow(CancellationToken token)
    {
        try
        {
            await UniTask.Delay(TimeSpan.FromSeconds(hoverDelay), ignoreTimeScale: true, cancellationToken: token);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        // 딜레이가 끝난 시점의 실제 마우스 위치를 다시 읽는다 - eventData.position은 진입 순간 좌표라
        // 그새 마우스가 움직였으면 어긋난다.
        if (isJust) TooltipUi.Instance.JustShow(message, Mouse.current.position.ReadValue());
        else TooltipUi.Instance.Show(message, Mouse.current.position.ReadValue());
    }
}
