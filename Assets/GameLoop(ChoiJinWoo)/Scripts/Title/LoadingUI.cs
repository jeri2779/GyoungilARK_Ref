using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;

public class LoadingUI : MonoBehaviour
{
    [SerializeField] private List<GameObject> loadingHeros;
    [SerializeField] private List<Transform> spawnPositions;
    private readonly int[] animatorTriggers = new int[] { Animator.StringToHash("Dance1"), Animator.StringToHash("Dance2"), Animator.StringToHash("Dance3"), Animator.StringToHash("Dance4"), Animator.StringToHash("Dance5") };
    [SerializeField] private TextMeshProUGUI loadingText;
    private CancellationTokenSource dotsCts;

    private void OnEnable()
    {
        var animation = animatorTriggers[UnityEngine.Random.Range(0, animatorTriggers.Length)];

        for (int i = 0; i < spawnPositions.Count; i++)
        {
            var go = Instantiate(loadingHeros[UnityEngine.Random.Range(0, loadingHeros.Count)], spawnPositions[i]);
            go.GetComponent<Animator>().SetTrigger(animation);
        }

        dotsCts = new CancellationTokenSource();
        AnimateDots(dotsCts.Token).Forget();
    }

    private void OnDisable()
    {
        dotsCts?.Cancel();
        dotsCts?.Dispose();
    }

    private async UniTask AnimateDots(CancellationToken token)
    {
        await UniTask.Yield();

        const string baseText = "Loading";
        int dotCount = 0;

        while (!token.IsCancellationRequested)
        {
            dotCount = (dotCount % 3) + 1; // 1 -> 2 -> 3 -> 1 ...
            loadingText.text = baseText + new string('.', dotCount);
            await UniTask.Delay(TimeSpan.FromSeconds(0.5f), cancellationToken: token);
        }
    }
}
