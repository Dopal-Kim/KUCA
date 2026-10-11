using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 퀘스트 (B-5). 매일 자정에 오늘의 퀘스트 3개, 매주 월요일에 이번 주 퀘스트 1개를 새로 뽑는다.
/// 뽑기는 날짜로 정해서 모두 같은 퀘스트를 받는다 (같이 하는 재미). 목록은 앱에 고정 (서버 불필요).
/// 게임 곳곳에서 Report(종류, 양, 대상) 로 알리면 맞는 퀘스트가 차고, 다 차면 메뉴 › 퀘스트에서 보상을 받는다.
/// 보상: 오늘의 퀘스트는 XP, 이번 주 퀘스트는 XP + 알(바로 부화, 걷기 알 하루 상한과 별개).
/// </summary>
public static class Quests
{
    public class Def
    {
        public string id, kind, title;
        public int target, xp;
        public bool egg;
    }

    // kind: like_other(다른 사람 사진 좋아요) · visit_new(처음 가는 스팟) · visit_spot(스팟 들르기)
    //       course_done(산책 코스 완주) · catch(잡기) · catch_blue(파랑 이상 잡기) · walk(걷기 m)
    public static readonly Def[] Daily =
    {
        new Def { id = "d_like3", kind = "like_other", title = "다른 사람 사진에 좋아요 3번", target = 3, xp = 60 },
        new Def { id = "d_newspot", kind = "visit_new", title = "처음 가는 경희스팟 1곳 방문", target = 1, xp = 100 },
        new Def { id = "d_course", kind = "course_done", title = "산책 추천 코스 완주하기", target = 1, xp = 120 },
        new Def { id = "d_catch5", kind = "catch", title = "경희몬 5마리 잡기", target = 5, xp = 80 },
        new Def { id = "d_blue", kind = "catch_blue", title = "파랑 이상 경희몬 1마리 잡기", target = 1, xp = 90 },
        new Def { id = "d_walk1k", kind = "walk", title = "1km 걷기", target = 1000, xp = 70 },
    };

    public static readonly Def[] Weekly =
    {
        new Def { id = "w_spots5", kind = "visit_spot", title = "경희스팟 5곳 들르기", target = 5, xp = 200, egg = true },
        new Def { id = "w_walk5k", kind = "walk", title = "5km 걷기", target = 5000, xp = 200, egg = true },
        new Def { id = "w_like15", kind = "like_other", title = "다른 사람 사진에 좋아요 15번", target = 15, xp = 200, egg = true },
        new Def { id = "w_catch25", kind = "catch", title = "경희몬 25마리 잡기", target = 25, xp = 200, egg = true },
    };

    public const int DailyCount = 3;

    /// <summary>보상을 받을 수 있는 퀘스트가 생기거나 받았을 때</summary>
    public static event Action Changed;

    public static Def DefOf(string id) => Array.Find(Daily, d => d.id == id) ?? Array.Find(Weekly, d => d.id == id);

    static string Today => DateTime.Now.ToString("yyyyMMdd");

    /// <summary>이번 주 월요일 (yyyyMMdd)</summary>
    static string ThisWeek
    {
        get
        {
            DateTime d = DateTime.Now.Date;
            return d.AddDays(-(((int)d.DayOfWeek + 6) % 7)).ToString("yyyyMMdd");
        }
    }

    /// <summary>다음 자정까지 남은 시간</summary>
    public static TimeSpan UntilMidnight => DateTime.Now.Date.AddDays(1) - DateTime.Now;

    /// <summary>날이나 주가 바뀌었으면 새 퀘스트를 뽑는다.</summary>
    public static void Ensure(GameProgress p, int spotCount = 30)
    {
        if (p == null)
            return;
        if (p.questDay != Today)
        {
            p.questDay = Today;
            p.dailyQuests.Clear();
            var pool = new List<Def>(Daily);
            if (p.visitedSpots.Count >= spotCount)
                pool.RemoveAll(d => d.kind == "visit_new"); // 다 가 봤으면 할 수 없는 퀘스트는 뺀다
            var rng = new System.Random(int.Parse(p.questDay));
            for (int i = 0; i < DailyCount && pool.Count > 0; i++)
            {
                int k = rng.Next(pool.Count);
                p.dailyQuests.Add(new GameProgress.QuestState { id = pool[k].id });
                pool.RemoveAt(k);
            }
        }
        if (p.questWeek != ThisWeek)
        {
            p.questWeek = ThisWeek;
            p.weeklyQuests.Clear();
            var rng = new System.Random(int.Parse(p.questWeek) * 7 + 3);
            p.weeklyQuests.Add(new GameProgress.QuestState { id = Weekly[rng.Next(Weekly.Length)].id });
        }
    }

    public static IEnumerable<GameProgress.QuestState> All(GameProgress p)
    {
        foreach (var q in p.dailyQuests) yield return q;
        foreach (var q in p.weeklyQuests) yield return q;
    }

    public static bool IsDone(GameProgress.QuestState q) => DefOf(q.id) is Def d && q.progress >= d.target;

    /// <summary>다 했지만 아직 보상을 안 받은 퀘스트가 있는지</summary>
    public static bool HasReward(GameProgress p)
    {
        if (p == null) return false;
        foreach (var q in All(p))
            if (IsDone(q) && !q.claimed) return true;
        return false;
    }

    /// <summary>
    /// 퀘스트에 알린다. kind 가 맞는 퀘스트를 amount 만큼 채운다.
    /// target 을 주면 같은 대상은 한 번만 센다 (같은 사진 좋아요를 눌렀다 뗐다 하거나 같은 스팟을 계속 들러도 1번).
    /// catch 의 target 은 등급(sprout/crystal/star) — 파랑 이상이면 catch_blue 도 찬다.
    /// </summary>
    public static void Report(string kind, int amount = 1, string target = null)
    {
        var collect = UnityEngine.Object.FindAnyObjectByType<CollectController>();
        GameProgress p = collect != null ? collect.Progress : null;
        if (p == null || amount <= 0)
            return;
        Ensure(p);
        bool changed = false;
        foreach (var q in All(p))
        {
            Def d = DefOf(q.id);
            if (d == null || q.claimed || q.progress >= d.target)
                continue;
            bool match = d.kind == kind || (d.kind == "catch_blue" && kind == "catch" && (target == "crystal" || target == "star"));
            if (!match)
                continue;
            bool dedupe = kind == "like_other" || kind == "visit_spot";
            if (dedupe && target != null)
            {
                if (q.seen.Contains(target)) continue;
                q.seen.Add(target);
            }
            q.progress = Mathf.Min(d.target, q.progress + amount);
            changed = true;
            if (q.progress >= d.target && collect.hud != null)
                collect.hud.Toast($"퀘스트 완료! {d.title}  ·  메뉴 › 퀘스트에서 보상 받기");
        }
        if (!changed)
            return;
        p.Save();
        Changed?.Invoke();
    }

    /// <summary>보상을 받는다. 알 보상이면 true (부르는 쪽에서 부화시킨다)</summary>
    public static bool Claim(GameProgress p, GameProgress.QuestState q, out int xp)
    {
        xp = 0;
        Def d = DefOf(q.id);
        if (d == null || q.claimed || q.progress < d.target)
            return false;
        q.claimed = true;
        xp = d.xp;
        p.score += d.xp;
        p.Save();
        Changed?.Invoke();
        return d.egg;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => Changed = null;
}
