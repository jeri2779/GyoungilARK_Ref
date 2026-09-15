using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class GameSpeedUI : MonoBehaviour
{
    [SerializeField] private Key timeIncreaseKey = Key.Tab;
    [SerializeField] private Key timeStopKey = Key.Space;
    [SerializeField] private Image halfImage;
    [SerializeField] private Image zeroImage;
    [SerializeField] private Image oneImage;
    [SerializeField] private Image twoImage;
    [SerializeField] private Image threeImage;
    private Speed beforeTimeSpeed = Speed.Normal;
    private InputAction timeIncreaseAction;
    private InputAction timeStopAction;

    private Dictionary<Speed, Image> images;

    // 이 오브젝트는 낮 동안 비활성 상태로 있다가 밤에 처음 켜지는데, 그러면 Awake()가 그 활성화
    // 순간에야 처음 실행된다. 그 전에(비활성 상태에서) 외부(튜토리얼 등)가 OnButtonClick으로
    // 이미 속도를 걸어뒀다면, Awake()의 기본값 초기화가 그걸 덮어쓰지 않게 이 플래그로 막는다.
    private bool initialized;

    private Speed gameSpeed;
    private Speed GameSpeed
    {
        get
        {
            return gameSpeed;
        }

        set
        {
            initialized = true;
            switch (value)
            {
                case Speed.Zero:
                    Time.timeScale = 0f;
                    gameSpeed = Speed.Zero;
                    ChangeImage(value);
                    break;
                case Speed.Half:
                    Time.timeScale = 0.5f;
                    gameSpeed = Speed.Half;
                    ChangeImage(value);
                    break;
                case Speed.Normal:
                    if(Time.timeScale != 1f)
                        Time.timeScale = 1f;
                    gameSpeed = Speed.Normal;
                    ChangeImage(value);
                    break;
                case Speed.Double:
                    Time.timeScale = 2f;
                    gameSpeed = Speed.Double;
                    ChangeImage(value);
                    break;
                case Speed.Triple:
                    Time.timeScale = 3f;
                    gameSpeed = Speed.Triple;
                    ChangeImage(value);
                    break;
                default:
                    Debug.LogWarning($"{value}는 없는 값입니다.");
                    gameSpeed = Speed.Error;
                    break;
            }
        }
    }

    private void Awake()
    {
        images = new()
        {
            { Speed.Zero, zeroImage },
            { Speed.Half, halfImage },
            { Speed.Normal, oneImage },
            { Speed.Double, twoImage },
            { Speed.Triple, threeImage },
        };

        // 이미 외부에서 속도를 걸어둔 적이 있으면(예: 튜토리얼이 비활성 상태일 때 미리 Zero를 걸어둠)
        // 그 값을 덮어쓰지 않는다.
        if (!initialized) GameSpeed = Speed.Normal;

        timeIncreaseAction = new InputAction("GameSpeedIncrease", binding: Keyboard.current[timeIncreaseKey].path);
        timeIncreaseAction.performed += OnTimeIncreasePerformed;

        timeStopAction = new InputAction("GameSpeedStop", binding: Keyboard.current[timeStopKey].path);
        timeStopAction.performed += OnTimeStopPerformed;
    }

    // 밤마다 UiManager.ToggleGameSpeedUi(true)로 이 UI가 다시 켜질 때 호출된다. Normal로 강제
    // 초기화하지 않고 지난 밤에 고른 속도를 그대로 이어간다 - ResultState.Exit()이 밤 사이
    // Time.timeScale을 1로 되돌려놓으므로, 여기서 다시 적용해 표시와 실제 배속을 맞춰준다.
    private void OnEnable()
    {
        GameSpeed = gameSpeed;
        timeIncreaseAction.Enable();
        timeStopAction.Enable();
    }

    private void OnDisable()
    {
        timeIncreaseAction.Disable();
        timeStopAction.Disable();
    }

    private void OnDestroy()
    {
        timeIncreaseAction.performed -= OnTimeIncreasePerformed;
        timeIncreaseAction.Dispose();

        timeStopAction.performed -= OnTimeStopPerformed;
        timeStopAction.Dispose();
    }

    // gameSpeedGroup(CanvasGroup)의 blocksRaycasts는 EventSystem 레이캐스트(버튼 클릭)에만
    // 관여하고 InputAction으로 받는 키보드 입력은 막지 못한다 - 다른 단축키 처리부(UiManager,
    // BuildModePanel, DayNightButton 등)와 동일하게 BlockHotkeys를 직접 체크해야 한다.
    private void OnTimeIncreasePerformed(InputAction.CallbackContext context)
    {
        if (SpawnerManager.Instance.isDirecting || TutorialInputGate.BlockHotkeys) return;

        if (Time.timeScale < 0.5f)
        {
            GameSpeed = Speed.Half;
        }
        else if (Time.timeScale < 1f)
        {
            GameSpeed = Speed.Normal;
        }
        else if (Time.timeScale < 2f)
        {
            GameSpeed = Speed.Double;
        }
        else if (Time.timeScale < 3f)
        {
            GameSpeed = Speed.Triple;
        }
    }

    private void OnTimeStopPerformed(InputAction.CallbackContext context)
    {
        if (SpawnerManager.Instance.isDirecting || TutorialInputGate.BlockHotkeys) return;

        if (Time.timeScale > 0f)
        {
            beforeTimeSpeed = GameSpeed;
            GameSpeed = Speed.Zero;
        }
        else
        {
            GameSpeed = beforeTimeSpeed;
        }
    }

    public void OnButtonClick(int value)
    {
        GameSpeed = (Speed)value;
    }

    private void ChangeImage(Speed value)
    {
        // 이 오브젝트가 씬 시작부터 비활성 상태면 Awake()가 아직 안 돌아 images가 비어있을 수 있다
        // (예: 밤이 되기 전에 다른 컴포넌트가 OnButtonClick을 미리 호출하는 경우) - 그때를 대비해
        // 여기서도 한 번 더 채워둔다.
        images ??= new()
        {
            { Speed.Zero, zeroImage },
            { Speed.Half, halfImage },
            { Speed.Normal, oneImage },
            { Speed.Double, twoImage },
            { Speed.Triple, threeImage },
        };

        foreach (var image in images)
        {
            image.Value.color = image.Key == value
                ? new Color32(255, 255, 255, 255)
                : new Color32(130, 130, 130, 255);
        }
    }
}
