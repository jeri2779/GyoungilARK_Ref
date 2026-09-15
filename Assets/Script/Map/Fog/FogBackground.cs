using UnityEngine;

// 모듈 프리팹 자식으로 둔 장식용 배경(예: 배경 Plane)에 붙이는 표식.
// FogController가 그 모듈의 안개 사각형을 계산할 때 이 오브젝트의 Renderer.bounds까지 자동으로 합쳐서,
// 별도 배선 없이 그 모듈이 해금될 때 배경도 같이 열리게 한다.
[RequireComponent(typeof(Renderer))]
public class FogBackground : MonoBehaviour
{
}
