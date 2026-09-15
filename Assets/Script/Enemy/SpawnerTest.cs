using TMPro;
using UnityEngine;
using VContainer;
public class SpawnerTest : MonoBehaviour
{
    public TMP_Dropdown _dropdown;
    public TMP_Text text;
    public WaveSpawner waveSpawner;
    private int Region = 1;

    public void Inputregion(int region)
    {
        Region = region+1;
        text.text = $"{Region}지역";
    }
    public void SpawnWave(int currentStage)
    {
        waveSpawner.SpawnWave(Region,currentStage);
    }
}
