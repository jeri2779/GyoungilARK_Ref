using System;
using UnityEngine;
using VContainer;

// 플레이어 스킬 전용 글로벌 마나 풀. Hero의 StatContainer(StatType.SP/SPR)와는 무관한 단일 자원이다.
// 밤에만 회복되고(GameManager.CanBuild==false), 낮에는 그대로 유지된다(스킬 자체가 밤 전용이라 소모도 없음).
public class PlayerManaManager : MonoBehaviour
{
    [SerializeField] private float maxMana = 100f;
    [SerializeField] private float manaRegenPerSecond = 5f;

    private float currentMana;
    private GameManager gameManager;

    public float CurrentMana => currentMana;
    public float MaxMana => maxMana;
    public event Action ManaChanged;

    [Inject]
    private void Construct(GameManager gameManager)
    {
        this.gameManager = gameManager;
    }
    private void OnEnable()
    {
        if (gameManager != null)
        {
            gameManager.ChangeToDay += RefillMana;
        }
    }

    private void OnDisable()
    {
        if (gameManager != null)
        {
            gameManager.ChangeToDay -= RefillMana;
        }

    }
    private void Awake()
    {
        currentMana = maxMana;
    }

    private void Update()
    {
        if (gameManager.CanBuild) return; // 낮에는 회복하지 않는다(CanBuild==true가 낮)
        if (currentMana >= maxMana) return;

        currentMana = Mathf.Min(maxMana, currentMana + manaRegenPerSecond * Time.deltaTime);
        ManaChanged?.Invoke();
    }

    public bool TryConsume(float amount)
    {
        if (currentMana < amount) return false;

        currentMana -= amount;
        ManaChanged?.Invoke();
        return true;
    }

    public void RefillMana()
    {
        currentMana = maxMana;
        ManaChanged?.Invoke();
    }
}
