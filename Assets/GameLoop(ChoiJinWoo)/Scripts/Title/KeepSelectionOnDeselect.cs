using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;

public class KeepSelectionOnDeselect : MonoBehaviour, IDeselectHandler
{
    public void OnDeselect(BaseEventData eventData)
    {
        ReselectNextFrame().Forget();
    }

    private async UniTaskVoid ReselectNextFrame()
    {
        await UniTask.Yield();
        if (EventSystem.current.currentSelectedGameObject == null)
            EventSystem.current.SetSelectedGameObject(gameObject);
    }
}