using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// 끝난 산책 기록 (기기 안 persistentDataPath/walk_history.json). 최근 것부터 MaxEntries 개까지.
/// 산책 기록 화면 (WalkHistoryScreen) 과 완료 화면의 돌아보기가 쓴다.
/// </summary>
public static class WalkHistory
{
    public const int MaxEntries = 50, MaxPoints = 300;

    [Serializable]
    public class Entry
    {
        public string id;
        public string kind;
        public string title;
        public long endedAtUnix;
        public int meters, minutes, stopsDone, stopsTotal;
        public bool completed;
        public List<string> visitedBuildingIds = new List<string>();
        /// <summary>걸은 길 x, z 를 번갈아 (m, 소수 첫째 자리)</summary>
        public List<float> xz = new List<float>();

        public DateTime EndedAt => DateTimeOffset.FromUnixTimeSeconds(endedAtUnix).LocalDateTime;

        public List<Vector3> Track
        {
            get
            {
                var r = new List<Vector3>(xz.Count / 2);
                for (int i = 0; i + 1 < xz.Count; i += 2)
                    r.Add(new Vector3(xz[i], 0f, xz[i + 1]));
                return r;
            }
        }
    }

    [Serializable]
    class Store
    {
        public List<Entry> entries = new List<Entry>();
    }

    static Store cache;

    static string FilePath => Path.Combine(Application.persistentDataPath, "walk_history.json");

    /// <summary>최근 것부터</summary>
    public static List<Entry> All => new List<Entry>(Load().entries);

    /// <summary>끝난 산책을 기록하고 그 기록을 돌려준다 (완료 판정이 늦게 오면 id 로 SetCompleted)</summary>
    public static Entry Add(WalkCompleteRequest walk, DateTime endedAt)
    {
        var e = FromWalk(walk, endedAt);
        Store s = Load();
        s.entries.Insert(0, e);
        if (s.entries.Count > MaxEntries)
            s.entries.RemoveRange(MaxEntries, s.entries.Count - MaxEntries);
        Save();
        return e;
    }

    /// <summary>완료 판정이 늦게 오면 그때 고친다</summary>
    public static void SetCompleted(string id, bool completed)
    {
        Store s = Load();
        Entry e = s.entries.Find(x => x.id == id);
        if (e == null || e.completed == completed)
            return;
        e.completed = completed;
        Save();
    }

    public static Entry FromWalk(WalkCompleteRequest walk, DateTime endedAt)
    {
        var e = new Entry
        {
            id = Guid.NewGuid().ToString("N").Substring(0, 12),
            kind = walk.kind,
            title = string.IsNullOrEmpty(walk.title) ? (walk.kind == WalkSession.Mate ? "산책메이트와 함께" : "산책") : walk.title,
            endedAtUnix = new DateTimeOffset(endedAt).ToUnixTimeSeconds(),
            meters = walk.meters,
            minutes = walk.minutes,
            stopsDone = walk.stopsDone,
            stopsTotal = walk.stopsTotal,
            visitedBuildingIds = new List<string>(walk.visitedBuildingIds ?? new List<string>()),
        };
        foreach (Vector3 p in WalkTrack.Thin(walk.track, MaxPoints))
        {
            e.xz.Add(Mathf.Round(p.x * 10f) / 10f);
            e.xz.Add(Mathf.Round(p.z * 10f) / 10f);
        }
        return e;
    }

    /// <summary>회원 탈퇴: 기록 파일과 읽어 둔 기록을 지운다</summary>
    public static void Forget()
    {
        cache = null;
        if (File.Exists(FilePath))
            File.Delete(FilePath);
    }

    static Store Load()
    {
        if (cache != null)
            return cache;
        cache = new Store();
        try
        {
            if (File.Exists(FilePath))
                cache = JsonUtility.FromJson<Store>(File.ReadAllText(FilePath)) ?? new Store();
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[WalkHistory] 기록을 읽지 못했어요: " + ex.Message);
        }
        return cache;
    }

    static void Save()
    {
        try
        {
            File.WriteAllText(FilePath, JsonUtility.ToJson(cache));
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[WalkHistory] 기록을 저장하지 못했어요: " + ex.Message);
        }
    }
}
