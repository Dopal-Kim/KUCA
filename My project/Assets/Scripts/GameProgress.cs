using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// 점수와 종류별 획득 수를 기기에 JSON으로 저장한다.
/// 경로: Application.persistentDataPath/progress.json
/// </summary>
[Serializable]
public class GameProgress
{
    [Serializable]
    public class Entry
    {
        public string id;
        public int count;
    }

    public int score;
    public int totalCollected;
    public List<Entry> collected = new List<Entry>();

    static string FilePath => Path.Combine(Application.persistentDataPath, "progress.json");

    public int CountOf(string id)
    {
        Entry e = collected.Find(x => x.id == id);
        return e != null ? e.count : 0;
    }

    public void Add(CollectibleType type)
    {
        score += type.points;
        totalCollected++;
        Entry e = collected.Find(x => x.id == type.id);
        if (e == null)
            collected.Add(e = new Entry { id = type.id });
        e.count++;
    }

    public static GameProgress Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonUtility.FromJson<GameProgress>(File.ReadAllText(FilePath)) ?? new GameProgress();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[GameProgress] 저장 데이터를 읽지 못했습니다: {e.Message}");
        }
        return new GameProgress();
    }

    public void Save()
    {
        try
        {
            File.WriteAllText(FilePath, JsonUtility.ToJson(this));
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[GameProgress] 저장하지 못했습니다: {e.Message}");
        }
    }
}
