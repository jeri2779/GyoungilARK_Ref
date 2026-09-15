using System;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using VContainer;

public class DayNightButton : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private RectTransform icon;
    [SerializeField] private TextMeshProUGUI dayText;
    [SerializeField] private Animator slideAnim;
    [SerializeField] private Key nightKey = Key.N;
    private static readonly int UpHash = Animator.StringToHash("DayUp");
    private static readonly int DownHash = Animator.StringToHash("DayDown");
    private GameManager gameManager;
    private EnviromentManager enviromentManager;
    private InputAction nightKeyAction;
    private bool canToggle = true;
    // 씬 로딩 중 게임을 끄면 Start가 끝나기 전에 OnDestroy가 불릴 수 있어, 이때 아래 필드들이
    // 아직 null이라 OnDestroy가 터진다. 이 플래그로 Start 완료 여부를 확인하고 조기 종료한다.
    private bool started;

    // Quaternion.Slerp은 180도 회전에서 어느 쪽으로 돌지가 애매해서(부동소수점에 따라 달라짐),
    // 방향을 확실히 통제하려고 각도를 직접 실수로 누적한다(래핑 없이 계속 더함).
    private float currentZ = 0f;

    [Inject]
    private void Construct(GameManager gameManager, EnviromentManager enviromentManager)
    {
        this.gameManager = gameManager;
        this.enviromentManager = enviromentManager;
    }

    // 버튼 입력과 낮 전환 이벤트를 연결한다.
    private void Start()
    {
        button.onClick.AddListener(OnButton);
        icon.transform.rotation = Quaternion.identity;
        gameManager.ChangeToDay += OnDayStart;
        enviromentManager.OnDay += FinishDay;
        dayText.text = $"Day {gameManager.DayCount}";

        nightKeyAction = new InputAction("NightToggle", binding: Keyboard.current[nightKey].path);
        nightKeyAction.performed += OnNightKeyPerformed;
        nightKeyAction.Enable();

        // Debug.Log(gameManager.CanBuild);

        icon.localRotation = gameManager.CanBuild ? Quaternion.identity : Quaternion.Euler(0f, 0f, 180f);

        started = true;
    }

    private void OnNightKeyPerformed(InputAction.CallbackContext context)
    {
        if (!TutorialInputGate.BlockHotkeys) OnButton();
    }

    // 밤 전환 요청이 가능한지 확인하고 전환 실행부를 호출한다.
    private void OnButton()
    {
        if (!CanToggle())
        {
            return;
        }

        StartNight();
    }

    // 낮에서만 밤 전환 요청을 허용한다.
    private bool CanToggle()
    {
        return canToggle;
    }

    // 버튼을 내리고 밤 전환을 시작한다.
    private void StartNight()
    {
        canToggle = false;
        slideAnim.Play(DownHash, 0, 0f);
        gameManager.OnNight();
        RotateIconBy(180f, null).Forget();
    }

    // 낮 전환 시간 동안 아이콘을 회전시킨다.
    private void OnDayStart()
    {
        RotateIconBy(180f, null).Forget();
    }

    // 낮 전환이 끝난 뒤 버튼을 올리고 입력을 다시 허용한다.
    private void FinishDay()
    {
        slideAnim.Play(UpHash, 0, 0f);
        button.interactable = true;
        canToggle = true;
    }

    private async UniTaskVoid RotateIconBy(float deltaZ, Action onComplete)
    {
        if (icon == null)
        {
            onComplete?.Invoke();
            return;
        }

        float duration = enviromentManager.TransitionDuration;
        float startZ = currentZ;
        float targetZ = currentZ + deltaZ;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            currentZ = Mathf.Lerp(startZ, targetZ, elapsed / duration);
            icon.localRotation = Quaternion.Euler(0f, 0f, currentZ);
            await UniTask.Yield();
        }
        currentZ = targetZ;
        // 한 바퀴(360도) 돌면 값을 0~-360 범위로 접어서 계속 불어나지 않게 한다 - 같은 방향으로 계속
        // 돌되, 시각적으로는 완전히 한 바퀴 돈 자리라 티가 안 난다.
        if (currentZ <= -360f || currentZ >= 360f)
        {
            currentZ %= 360f;
        }
        icon.localRotation = Quaternion.Euler(0f, 0f, currentZ);
        dayText.text = $"Day {gameManager.DayCount}";
        onComplete?.Invoke();
    }

    // 버튼 입력과 낮 전환 이벤트 연결을 해제한다.
    private void OnDestroy()
    {
        if (!started)
        {
            return;
        }

        button.onClick.RemoveAllListeners();
        gameManager.ChangeToDay -= OnDayStart;
        enviromentManager.OnDay -= FinishDay;

        nightKeyAction.performed -= OnNightKeyPerformed;
        nightKeyAction.Disable();
        nightKeyAction.Dispose();
    }

    // 버튼을 내린 밤 모습으로 맞추고, 아이콘도 밤 방향으로 맞춘 뒤 버튼·N키 입력을 잠근다 (로드 복원 전용, 연출 없이 즉시)
    public void RestoreNightLock()
    {
        canToggle = false;
        slideAnim.Play(DownHash, 0, 0f);

        RotateIconBy(180f, null).Forget();
    }

    // 세이브 로드처럼 화면 연출 없이 조용히 일차가 바뀌었을 때 "Day N" 글자만 다시 찍는다 (로드 복원 전용)
    public void RefreshDayText()
    {
        dayText.text = $"Day {gameManager.DayCount}";
    }
}
