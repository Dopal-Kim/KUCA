using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// 점수(경험치), 가진 경희몬(한 마리씩), 파트너, 티켓 · 진화, 도구, 활동 기록을 기기에 JSON으로 저장한다.
/// v2 (B-V1): 알 대신 티켓 → 뽑기, 종마다 진화 경험치. 규칙 값은 GachaRules.
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

    /// <summary>잡은 경희몬 한 마리</summary>
    [Serializable]
    public class Caught
    {
        public string uid;
        public string typeId;
        public int cp;
        public long caughtAt; // DateTime.Ticks (현지 시각)
        /// <summary>잡을 때 찍은 사진 파일 이름 (스크랩북 폴더 안, 없으면 빈 문자열)</summary>
        public string photo = "";
        /// <summary>동물 캐릭터 id (CreatureLibrary), 일반 경희몬이면 빈 문자열</summary>
        public string speciesId = "";
        /// <summary>쓰고 있는 액세서리 id (CreatureAccessories, 부위마다 하나)</summary>
        public List<string> accessories = new List<string>();

        public DateTime CaughtAt => new DateTime(caughtAt);
    }

    [Serializable]
    public class PartnerRecord
    {
        public string uid;
        public long since;
    }

    public int score;
    public int totalCollected;
    /// <summary>종류별 획득 수 (도감)</summary>
    public List<Entry> collected = new List<Entry>();
    /// <summary>동물별 획득 수 (도감)</summary>
    public List<Entry> species = new List<Entry>();
    /// <summary>지금 가지고 있는 경희몬</summary>
    public List<Caught> caught = new List<Caught>();
    public string partnerUid = "";
    public List<PartnerRecord> partnerHistory = new List<PartnerRecord>();
    /// <summary>가지고 있는 도구 (id, 개수)</summary>
    public List<Entry> items = new List<Entry>();
    /// <summary>레벨 보상을 이 레벨까지 받았다</summary>
    public int rewardedLevel;
    public float walkedMeters;
    public List<string> visitedSpots = new List<string>();

    // ---------- v2: 티켓 · 진화 (B-V1, 규칙은 GachaRules) ----------

    /// <summary>저장 형식 버전. 2 = v2 (알 삭제, 레벨 초기화)</summary>
    public int dataVersion;
    public int tickets;
    /// <summary>다음 티켓까지 걸은 거리 (m). 오늘 상한이면 TicketMeters 에서 멈춰 있다가 내일 1장이 된다</summary>
    public float ticketMeters;
    /// <summary>ticketsToday 를 센 날 (yyyyMMdd)</summary>
    public string ticketDay = "";
    public int ticketsToday;
    /// <summary>파트너로 함께 걸은 거리 중 아직 진화 경험치로 바꾸지 않은 나머지 (m)</summary>
    public float partnerMeters;
    /// <summary>아직 경험치로 바꾸지 않은 걸은 거리 (m)</summary>
    public float xpMeters;

    [Serializable]
    public class Growth
    {
        public string id;   // 동물 id
        public int points;  // 진화 경험치
    }
    /// <summary>종별 진화 경험치</summary>
    public List<Growth> growth = new List<Growth>();

    static string Today => DateTime.Now.ToString("yyyyMMdd");

    public int TicketsToday => ticketDay == Today ? ticketsToday : 0;
    public bool TicketCapped => TicketsToday >= GachaRules.TicketsPerDay;

    public int GrowthOf(string species)
    {
        Growth g = growth.Find(x => x.id == species);
        return g != null ? g.points : 0;
    }

    public int StageOf(string species) => GachaRules.StageOf(GrowthOf(species));

    /// <summary>그 종에 진화 경험치를 더한다. 단계가 올랐으면 true</summary>
    public bool AddGrowth(string species, int points, out int stage)
    {
        stage = StageOf(species);
        if (string.IsNullOrEmpty(species) || points <= 0)
            return false;
        Growth g = growth.Find(x => x.id == species);
        if (g == null)
            growth.Add(g = new Growth { id = species });
        int before = stage;
        g.points += points;
        stage = GachaRules.StageOf(g.points);
        return stage > before;
    }

    public struct WalkResult
    {
        public int tickets;     // 새로 받은 티켓
        public int xp;          // 새로 받은 경험치
        public bool evolved;    // 파트너가 진화했는지
        public int fromStage;   // 진화 전 단계
        public int stage;       // 파트너의 지금 단계
    }

    /// <summary>
    /// 걸은 거리를 더한다: 1km 마다 티켓 (하루 상한), 경험치 (1km 10), 파트너 진화 경험치 (100m 1).
    /// 상한에 걸린 날의 거리는 TicketMeters 에서 멈추고, 날이 바뀐 뒤 처음 걸을 때 1장이 된다.
    /// </summary>
    public WalkResult AddWalk(float meters)
    {
        var r = new WalkResult();
        meters = Mathf.Max(0f, meters);
        walkedMeters += meters;

        if (ticketDay != Today)
        {
            ticketDay = Today;
            ticketsToday = 0;
        }
        ticketMeters += meters;
        while (ticketMeters >= GachaRules.TicketMeters && ticketsToday < GachaRules.TicketsPerDay)
        {
            ticketMeters -= GachaRules.TicketMeters;
            ticketsToday++;
            tickets++;
            r.tickets++;
        }
        if (ticketsToday >= GachaRules.TicketsPerDay)
            ticketMeters = Mathf.Min(ticketMeters, GachaRules.TicketMeters);

        xpMeters += meters;
        float perXp = 1000f / GachaRules.XpPerKm;
        while (xpMeters >= perXp)
        {
            xpMeters -= perXp;
            score++;
            r.xp++;
        }

        Caught partner = Partner;
        if (partner != null && !string.IsNullOrEmpty(partner.speciesId))
        {
            partnerMeters += meters;
            int points = Mathf.FloorToInt(partnerMeters / GachaRules.GrowthMetersPerPoint);
            partnerMeters -= points * GachaRules.GrowthMetersPerPoint;
            r.fromStage = StageOf(partner.speciesId);
            r.evolved = AddGrowth(partner.speciesId, points, out r.stage);
        }
        return r;
    }

    public struct DrawResult
    {
        public string species;
        public GachaRules.Grade grade;
        public bool isNew;       // 도감에 처음 들어옴
        public int growthAdded;  // 중복이면 그 종의 진화 경험치
        public bool evolved;
        public int fromStage;
        public int stage;
        public Caught caught;
    }

    /// <summary>티켓을 써서 뽑는다 (1장: C~A, 5장: B~S). 티켓이 모자라면 false</summary>
    public bool Draw(bool premium, float rGrade, float rSpecies, out DrawResult result)
    {
        result = default;
        int cost = premium ? GachaRules.PremiumCost : GachaRules.SingleCost;
        string species = GachaRules.RollSpecies(premium, rGrade, rSpecies);
        if (tickets < cost || species == null)
            return false;
        tickets -= cost;
        result.species = species;
        result.grade = GachaRules.GradeOf(species);
        result.isNew = SpeciesCountOf(species) == 0;
        if (!result.isNew)
        {
            result.growthAdded = GachaRules.DuplicateGrowth(result.grade);
            result.fromStage = StageOf(species);
            result.evolved = AddGrowth(species, result.growthAdded, out result.stage);
        }
        else
            result.stage = StageOf(species);
        result.caught = AddCreature(species);
        return true;
    }

    /// <summary>뽑기 · 첫 파트너로 얻은 경희몬 (경험치 없이 도감과 가방에만)</summary>
    public Caught AddCreature(string species)
    {
        string tier = CreatureLibrary.TierOf(species) ?? "sprout";
        totalCollected++;
        Entry e = collected.Find(x => x.id == tier);
        if (e == null)
            collected.Add(e = new Entry { id = tier });
        e.count++;
        var c = new Caught
        {
            uid = Guid.NewGuid().ToString("N"),
            typeId = tier,
            cp = RollCp(new CollectibleType { id = tier, points = PointsGuess(tier) }, UnityEngine.Random.value, UnityEngine.Random.value),
            caughtAt = DateTime.Now.Ticks,
            speciesId = species,
        };
        caught.Add(c);
        AddSpecies(species);
        return c;
    }

    // ---------- 퀘스트 (B-5): 매일 3개 + 주간 1개. 정의는 Quests.cs ----------
    [Serializable]
    public class QuestState
    {
        public string id;
        public int progress;
        public bool claimed;
        /// <summary>한 번만 세야 하는 대상 (좋아요 누른 메모 id, 들른 스팟 id)</summary>
        public List<string> seen = new List<string>();
    }
    /// <summary>오늘의 퀘스트를 뽑은 날 (yyyyMMdd)</summary>
    public string questDay = "";
    /// <summary>이번 주 퀘스트를 뽑은 주 (그 주 월요일 yyyyMMdd)</summary>
    public string questWeek = "";
    public List<QuestState> dailyQuests = new List<QuestState>();
    public List<QuestState> weeklyQuests = new List<QuestState>();

    static string FilePath => Path.Combine(Application.persistentDataPath, "progress.json");

    public int CountOf(string id)
    {
        Entry e = collected.Find(x => x.id == id);
        return e != null ? e.count : 0;
    }

    /// <summary>잡을 때 찍은 사진을 모아 두는 폴더</summary>
    public static string ScrapbookDir => Path.Combine(Application.persistentDataPath, "scrapbook");

    public Caught Add(CollectibleType type, string photo = "")
    {
        score += type.points;
        totalCollected++;
        Entry e = collected.Find(x => x.id == type.id);
        if (e == null)
            collected.Add(e = new Entry { id = type.id });
        e.count++;

        var c = new Caught
        {
            uid = Guid.NewGuid().ToString("N"),
            typeId = type.id,
            cp = RollCp(type, UnityEngine.Random.value, UnityEngine.Random.value),
            caughtAt = DateTime.Now.Ticks,
            photo = photo ?? "",
        };
        caught.Add(c);
        return c;
    }

    public void AddSpecies(string id)
    {
        if (species == null)
            species = new List<Entry>();
        Entry e = species.Find(x => x.id == id);
        if (e == null)
            species.Add(e = new Entry { id = id });
        e.count++;
    }

    public int SpeciesCountOf(string id)
    {
        Entry e = species?.Find(x => x.id == id);
        return e != null ? e.count : 0;
    }

    /// <summary>점수가 높은 종류일수록 CP 가 높다 (10점 → 약 90~210).</summary>
    public static int RollCp(CollectibleType type, float a, float b) =>
        Mathf.Max(10, Mathf.RoundToInt(type.points * (9f + a * 8f) + b * 40f));

    public Caught Find(string uid) => string.IsNullOrEmpty(uid) ? null : caught.Find(c => c.uid == uid);

    public Caught Partner => Find(partnerUid);

    public void SetPartner(string uid)
    {
        if (partnerUid == uid)
            return;
        partnerUid = uid;
        partnerHistory.Add(new PartnerRecord { uid = uid, since = DateTime.Now.Ticks });
    }

    public int ItemCount(string id)
    {
        Entry e = items.Find(x => x.id == id);
        return e != null ? e.count : 0;
    }

    public int TotalItems
    {
        get
        {
            int n = 0;
            foreach (var e in items)
                n += e.count;
            return n;
        }
    }

    public void AddItem(string id, int count)
    {
        Entry e = items.Find(x => x.id == id);
        if (e == null)
            items.Add(e = new Entry { id = id });
        e.count += count;
    }

    public void VisitSpot(string id, out bool isNew)
    {
        isNew = !string.IsNullOrEmpty(id) && !visitedSpots.Contains(id);
        if (isNew)
            visitedSpots.Add(id);
    }

    /// <summary>
    /// 예전 저장 파일 맞추기: 종류별 개수만 있으면 모자란 만큼 경희몬을 채워 넣고 (CP 는 순서로 정해지는 값이라 매번 같다),
    /// 동물 기록이 없는 경희몬에는 동물을 정해 준다.
    /// </summary>
    void Migrate()
    {
        caught ??= new List<Caught>();
        foreach (Caught c in caught)
            c.accessories ??= new List<string>();
        species ??= new List<Entry>();
        partnerHistory ??= new List<PartnerRecord>();
        items ??= new List<Entry>();
        visitedSpots ??= new List<string>();
        ticketDay ??= "";
        growth ??= new List<Growth>();
        questDay ??= "";
        questWeek ??= "";
        dailyQuests ??= new List<QuestState>();
        weeklyQuests ??= new List<QuestState>();
        foreach (QuestState q in dailyQuests) q.seen ??= new List<string>();
        foreach (QuestState q in weeklyQuests) q.seen ??= new List<string>();
        foreach (Entry e in collected)
        {
            int have = caught.FindAll(c => c.typeId == e.id).Count;
            for (int i = have; i < e.count; i++)
            {
                var rng = new System.Random(StableHash(e.id) * 31 + i);
                var type = new CollectibleType { id = e.id, points = PointsGuess(e.id) };
                caught.Add(new Caught
                {
                    uid = $"old_{e.id}_{i}",
                    typeId = e.id,
                    cp = RollCp(type, (float)rng.NextDouble(), (float)rng.NextDouble()),
                    caughtAt = DateTime.Now.AddMinutes(-(e.count - i) * 7).Ticks,
                });
            }
        }

        // 동물 피규어가 들어오기 전에 잡은 경희몬은 어떤 동물인지 기록이 없다.
        // 등급에 맞는 동물을 uid 로 하나 정해(늘 같은 결과) 도감에도 올린다.
        foreach (Caught c in caught)
        {
            if (!string.IsNullOrEmpty(c.speciesId))
                continue;
            c.speciesId = CreatureLibrary.StableSpecies(c.typeId, c.uid) ?? "";
            if (c.speciesId.Length > 0)
                AddSpecies(c.speciesId);
        }
    }

    /// <summary>
    /// v2 로 한 번 옮기기 (결정 D1 · D9): 알은 버리고 (예전 필드는 읽지 않음), 레벨 · 경험치는 0 부터,
    /// 퀘스트는 숨기므로 비운다. 가진 경희몬 · 도감 · 파트너는 그대로.
    /// </summary>
    public void MigrateToV2()
    {
        if (dataVersion >= 2)
            return;
        score = 0;
        rewardedLevel = 0;
        dailyQuests.Clear();
        weeklyQuests.Clear();
        questDay = questWeek = "";
        dataVersion = 2;
    }

    static int StableHash(string s)
    {
        int h = 17;
        foreach (char ch in s)
            h = h * 31 + ch;
        return h;
    }

    internal static int PointsGuess(string id) => id == "star" ? 100 : id == "crystal" ? 30 : 10;

    public static GameProgress Load()
    {
        GameProgress p = null;
        try
        {
            if (File.Exists(FilePath))
                p = JsonUtility.FromJson<GameProgress>(File.ReadAllText(FilePath));
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[GameProgress] 저장 데이터를 읽지 못했습니다: {e.Message}");
        }
        bool fresh = p == null;
        p ??= new GameProgress();
        p.Migrate();
        if (fresh)
            p.dataVersion = 2; // 새로 깐 앱은 옮길 것이 없다
        p.MigrateToV2();
        return p;
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
