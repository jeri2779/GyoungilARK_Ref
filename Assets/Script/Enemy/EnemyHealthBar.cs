using UnityEngine;
using UnityEngine.UI;

public class EnemyHealthBar
{
    private Slider _slider;
    private Transform _tf;
    private Camera _cam;
    private float _smoothSpeed = 10f;
    private bool _shown;      // 마지막으로 반영한 표시 상태 — 바뀐 프레임에만 SetActive 호출
    private bool _billboard;  // 이 바가 카메라를 향해 돌아야 하는지. Setup에서 캔버스 모드로 1회 판정

    public bool IsSetup => _slider != null;

    public void Setup(Slider slider, float smoothSpeed)
    {
        _slider = slider;
        if (_slider == null) return;

        _tf = _slider.transform;
        _smoothSpeed = Mathf.Max(0.01f, smoothSpeed);
        _slider.transition = Selectable.Transition.None;
        _slider.interactable = false;
        foreach (Graphic g in _slider.GetComponentsInChildren<Graphic>(true))
            g.raycastTarget = false;


        Canvas canvas = _slider.GetComponentInParent<Canvas>(true);
        _billboard = canvas != null && canvas.rootCanvas.renderMode == RenderMode.WorldSpace;

        Reset();
    }
    public void ResetTo(float hp, float maxHp)
    {
        if (_slider == null) return;

        _slider.maxValue = Mathf.Max(1f, maxHp);
        _slider.value = Mathf.Clamp(hp, 0f, _slider.maxValue);
    }

    private const float FullEpsilon = 0.01f;
    private const float DrainedRatio = 0.005f;

    public void Tick(float hp, float maxHp, bool isDead, bool forceHidden,EnemyClass enemyClass)
    {
        if (_slider == null) return;

        _slider.maxValue = Mathf.Max(1f, maxHp); // 버프로 최대 체력이 바뀌어도 따라가게
        float target = Mathf.Clamp(hp, 0f, _slider.maxValue);

        bool damaged = target < _slider.maxValue - FullEpsilon;
        bool drained = _slider.value <= _slider.maxValue * DrainedRatio;

        bool visible = !forceHidden && (isDead ? !drained : damaged);
        if(enemyClass == EnemyClass.Boss)
        visible = true;
        if (_shown != visible)
        {
            _shown = visible;
            _slider.gameObject.SetActive(visible);
        }
        if (!visible) return;
        // 프레임률에 독립적인 지수 감쇠 보간. Lerp(a,b,dt*speed)는 fps에 따라 속도가 달라진다.
        _slider.value = Mathf.Lerp(_slider.value, target, 1f - Mathf.Exp(-_smoothSpeed * Time.deltaTime));

        if (!_billboard) return;                 // 화면 고정 바는 회전 대상이 아니다
        if (_cam == null) _cam = Camera.main;    // 매 프레임 Camera.main을 부르지 않도록 캐시
        if (_cam != null) _tf.rotation = _cam.transform.rotation;
    }

    public void Reset()
    {
        if (_slider == null) return;

        _shown = false;
        _slider.gameObject.SetActive(false);
    }
}
