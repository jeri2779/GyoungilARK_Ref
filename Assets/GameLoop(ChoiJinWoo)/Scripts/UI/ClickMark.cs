using UnityEngine;

// 클릭 애니메이션의 종료 명령을 처리한다.
public class ClickMark : MonoBehaviour
{
    // 재생이 끝난 클릭 마크를 제거한다.
    public void Finish()
    {
        Destroy(gameObject);
    }
}
