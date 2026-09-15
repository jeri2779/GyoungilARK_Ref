using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SpecificGuide : MonoBehaviour
{
    [SerializeField] private Button guideButton;
    [SerializeField] private Sprite guideImage;
    [SerializeField, TextArea] private string guideInfo;

    public Button GuideButton => guideButton;
    public Sprite GuideImage => guideImage;
    public string GuideInfo => guideInfo;
}
