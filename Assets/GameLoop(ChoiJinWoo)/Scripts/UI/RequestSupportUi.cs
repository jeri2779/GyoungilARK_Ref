using System;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using VContainer;

public class RequestSupportUi : MonoBehaviour
{
    [SerializeField] private List<SupportRegion> supportList;
    [SerializeField] private GameObject firstChoicePanel;
    [SerializeField] private GameObject secondChoicePanel;
    private List<SupportRegion> supportListCopy = new();
    private SupportRegion firstChoice;
    private SupportRegion secondChoice;

    public event Action<byte> OnUnlock;

    [SerializeField] private Button firstButton;
    [SerializeField] private Button secondButton;
    [SerializeField] private TextMeshProUGUI firstLabel;
    [SerializeField] private TextMeshProUGUI secondLabel;
    [SerializeField] private TextMeshProUGUI firstChoiceText;
    [SerializeField] private TextMeshProUGUI secondChoiceText;

    private ExpandEvent expand;

    [Inject]
    private void Construct(ExpandEvent expand)
    {
        this.expand = expand;
    }

    private void Awake()
    {
        foreach(var support in supportList)
        {
            supportListCopy.Add(support);
        }
    }

    private void OnEnable()
    {
        expand.ShowChoices();

        List<SupportRegion> eligible = supportListCopy.FindAll(s => expand.GetModuleId(s.ModuleID, out _));

        if (eligible.Count >= 2)
        {
            int firstIndex = UnityEngine.Random.Range(0, eligible.Count);
            firstChoice = eligible[firstIndex];
            firstChoiceText.text = $"영웅 해금 : {firstChoice.UnlockHero}";

            int secondIndex;
            do
            {
                secondIndex = UnityEngine.Random.Range(0, eligible.Count);
            } while (secondIndex == firstIndex);
            secondChoice = eligible[secondIndex];
            secondChoiceText.text = $"영웅 해금 : {secondChoice.UnlockHero}";
        }
        else if (eligible.Count == 1)
        {
            firstChoice = eligible[0];
            firstChoiceText.text = $"영웅 해금 : {firstChoice.UnlockHero}";
            secondChoicePanel.SetActive(false);
        }

        if (expand.Choices.Count == 0)
        {
            gameObject.SetActive(false);
            return;
        }

        FillButton(firstButton, firstLabel, firstChoice.ModuleID);
        FillButton(secondButton, secondLabel, secondChoice.ModuleID);
    }

    public void FirstButton() => Choose(firstChoice);

    public void SecondButton() => Choose(secondChoice);

    private void Choose(SupportRegion choice)
    {
        OnUnlock?.Invoke((byte)choice.UnlockHero);
        SelectChoice(choice.ModuleID);
        supportListCopy.Remove(choice);
        gameObject.SetActive(false);
    }

    private void FillButton(Button button, TextMeshProUGUI label, int index)
    {
        var has = expand.GetModuleId(index, out int id);
        button.gameObject.SetActive(has);
        if (has)
        {
            label.text = $"지역 {id}";
        }
    }

    private void SelectChoice(int index)
    {

        if (expand.GetModuleId(index, out _))
        {
            expand.SelectById(index);
        }

        gameObject.SetActive(false);
    }
}
