#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ContentSizeFitter가 지금 계산해둔 크기를 RectTransform.sizeDelta로 그대로 못박고
/// ContentSizeFitter 컴포넌트만 제거한다.
///
/// 중첩된 ContentSizeFitter(바깥 컬럼이 안쪽 행들의 크기에 의존하는 구조 등)는 런타임에
/// 한 프레임 안에 다 수렴하지 않아서, 처음 패널을 켤 때 크기가 자리를 잡는 게 눈에 보이는
/// 문제가 생긴다. 애초에 내용이 실행 중에 안 바뀌는 패널이라면, 지금 값을 고정값으로 박아버리고
/// 매번 다시 계산 안 하게 하는 쪽이 근본적으로 더 빠르고 확실하다.
///
/// 사용법: 대상 패널이 실제로 열려서(Edit 모드에서도 ContentSizeFitter는 ExecuteAlways라
/// 이미 정확한 크기로 계산돼 있음) 화면에 정상적으로 보이는 상태에서, 하이어라키에서 프리팹
/// 루트(또는 원하는 오브젝트)를 선택한 뒤 이 메뉴를 실행한다. 자식까지 전부 훑어서 처리한다.
/// 앵커가 늘어나 있는(anchorMin != anchorMax) 대상은 sizeDelta 의미가 달라 안전하게 처리할
/// 수 없으므로 건너뛰고 경고만 남긴다.
/// </summary>
public static class ContentSizeFitterFreezer
{
    [MenuItem("Tools/Layout/선택 항목의 ContentSizeFitter 고정 후 제거 (자식 포함)")]
    public static void FreezeAndRemove()
    {
        GameObject[] targets = Selection.gameObjects;
        if (targets == null || targets.Length == 0)
        {
            Debug.LogWarning("[ContentSizeFitterFreezer] 선택된 오브젝트가 없습니다. 하이어라키에서 대상을 선택하세요.");
            return;
        }

        int frozen = 0, skipped = 0;
        var log = new System.Text.StringBuilder("[ContentSizeFitterFreezer] 처리 결과\n");

        foreach (GameObject root in targets)
        {
            foreach (ContentSizeFitter fitter in root.GetComponentsInChildren<ContentSizeFitter>(true))
            {
                var rect = fitter.GetComponent<RectTransform>();

                if (rect.anchorMin != rect.anchorMax)
                {
                    skipped++;
                    log.AppendLine($"  [건너뜀 - 앵커 늘어남] {Path(rect)}");
                    continue;
                }

                Undo.RecordObject(rect, "Freeze ContentSizeFitter size");
                rect.sizeDelta = rect.rect.size; // 지금 실제로 계산되어 있는 크기를 그대로 고정값으로

                Undo.DestroyObjectImmediate(fitter);
                frozen++;
                log.AppendLine($"  [고정 후 제거] {Path(rect)}");
            }
        }

        log.AppendLine($"\n고정 {frozen}개 / 건너뜀(앵커 늘어남) {skipped}개");
        log.AppendLine("프리팹/씬을 저장하는 걸 잊지 마세요 (Ctrl+S).");
        Debug.Log(log.ToString());
    }

    private static string Path(Transform t)
    {
        var sb = new System.Text.StringBuilder(t.name);
        for (Transform p = t.parent; p != null; p = p.parent) sb.Insert(0, p.name + "/");
        return sb.ToString();
    }
}
#endif
