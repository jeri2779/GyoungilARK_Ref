using UnityEngine;

[RequireComponent(typeof(TrailRenderer))]
public class MeleeWeaponTrail : MonoBehaviour
{
    [SerializeField] private HeroAnimEvents animEvents;
    [SerializeField] private string startEvent = "Attack";
    [SerializeField] private string endEvent = "Recovery";

    private TrailRenderer trail;

    private void Awake()
    {
        trail = GetComponent<TrailRenderer>();
        trail.emitting = false;
        if (animEvents == null) animEvents = GetComponentInParent<HeroAnimEvents>();
    }

    private void OnEnable()
    {
        animEvents.Subscribe(startEvent, StartTrail);
        animEvents.Subscribe(endEvent, StopTrail);
    }

    private void OnDisable()
    {
        animEvents.Unsubscribe(startEvent, StartTrail);
        animEvents.Unsubscribe(endEvent, StopTrail);
    }

    private void StartTrail()
    {
        trail.Clear(); // 꺼져있던 동안 쌓인 옛 위치와 이어지는 잔상 방지
        trail.emitting = true;
    }

    private void StopTrail()
    {
        trail.emitting = false;
    }
}
