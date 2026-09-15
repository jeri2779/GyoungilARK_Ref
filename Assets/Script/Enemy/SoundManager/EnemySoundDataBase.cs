using System.Collections.Generic;
using UnityEngine;
[CreateAssetMenu(fileName = "EnemySoundDatabase", menuName = "Data/EnemySoundDatabase")]
public class EnemySoundDataBase : ScriptableObject
{
    public enum SoundType
    {
        Sfx,
        Bgm,
        System,
    }

    [System.Serializable]
    public class Entry
    {
        public string key;
        public AudioClip clip;
        [Range(0f, 1f)] public float volume = 1f;
        public SoundType type = SoundType.Sfx;
        public bool loop = false;
        [Tooltip("0이면 클립을 끝까지 재생한다. 값을 넣으면 그 초가 지날 때 잘라낸다 " +
                 "(긴 클립을 연출 길이에 맞춰 쓸 때). Sfx에만 적용 — PlayBgm은 이 값을 보지 않는다.")]
        public float soundTime = 0;
    }

    public List<Entry> entries;

    private Dictionary<string, Entry> _lookup;

    public Entry Get(string key)
    {
        if (_lookup == null)
        {
            _lookup = new Dictionary<string, Entry>();
            foreach (var e in entries)
                if (e != null && e.clip != null && !string.IsNullOrEmpty(e.key))
                    _lookup[e.key] = e;
        }
        return _lookup.TryGetValue(key, out var entry) ? entry : null;
    }
}
