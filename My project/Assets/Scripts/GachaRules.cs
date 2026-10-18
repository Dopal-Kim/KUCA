using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// v2 경희몬 규칙 (B-V1): 걸어서 티켓 → 뽑기 → 중복·함께 걷기로 진화.
/// 화면이 없는 계산만 모아 EditMode 테스트로 확인한다 (Assets/Editor/Tests/GachaRulesTests.cs).
///
/// ⚠ 등급표 · 확률 · 진화 기준은 **임시 값**이다. 정식 값은 A 가 CreatureLibrary 에 넣기로 했다
/// (docs/guides/v2_역할분담.md "A → B 등급 표·확률표·진화 거리 상수"). 넣으면 아래 "임시" 표시가 붙은 곳만 바꾼다.
/// </summary>
public static class GachaRules
{
    public enum Grade { C, B, A, S }

    // ---------- 등급 (임시) ----------

    /// <summary>임시: 랜드마크 16종 중 S. 나머지 랜드마크는 A, 파랑 4종은 B, 초록 4종은 C</summary>
    static readonly HashSet<string> TempS = new HashSet<string>
    {
        "lion_cub", "library_owl", "plaza_duck", "gate_magpie", "clock_rooster", "space_rabbit", "stadium_jindo", "amphitheater_frog",
    };

    public static Grade GradeOf(string species)
    {
        string tier = CreatureLibrary.TierOf(species);
        if (tier == "sprout") return Grade.C;
        if (tier == "crystal") return Grade.B;
        return TempS.Contains(species) ? Grade.S : Grade.A;
    }

    static Dictionary<Grade, List<string>> byGrade;

    public static IReadOnlyList<string> SpeciesOf(Grade grade)
    {
        if (byGrade == null)
        {
            byGrade = new Dictionary<Grade, List<string>>();
            foreach (Grade g in System.Enum.GetValues(typeof(Grade)))
                byGrade[g] = new List<string>();
            foreach (string id in CreatureLibrary.AllSpecies)
                byGrade[GradeOf(id)].Add(id);
        }
        return byGrade[grade];
    }

    public static string Label(Grade g) => g.ToString();

    public static Color ColorOf(Grade g) => g switch
    {
        Grade.C => new Color(0.55f, 0.60f, 0.62f),
        Grade.B => new Color(0.24f, 0.55f, 0.95f),
        Grade.A => new Color(0.62f, 0.36f, 0.90f),
        _ => new Color(0.98f, 0.72f, 0.12f),
    };

    // ---------- 티켓 ----------

    /// <summary>이만큼 걸으면 티켓 1장</summary>
    public const float TicketMeters = 1000f;
    /// <summary>하루에 걸어서 받을 수 있는 티켓 수 (V0 결정 Q3, 임시 5)</summary>
    public const int TicketsPerDay = 5;
    public const int SingleCost = 1, PremiumCost = 5;

    // ---------- 뽑기 확률 (임시) ----------

    /// <summary>1장 뽑기: C~A</summary>
    public static readonly (Grade grade, float chance)[] SingleOdds = { (Grade.C, 0.70f), (Grade.B, 0.25f), (Grade.A, 0.05f) };
    /// <summary>5장 뽑기: B~S</summary>
    public static readonly (Grade grade, float chance)[] PremiumOdds = { (Grade.B, 0.70f), (Grade.A, 0.25f), (Grade.S, 0.05f) };

    public static (Grade grade, float chance)[] OddsOf(bool premium) => premium ? PremiumOdds : SingleOdds;

    /// <summary>r: 0~1 균등 난수</summary>
    public static Grade RollGrade(bool premium, float r)
    {
        var odds = OddsOf(premium);
        float acc = 0f;
        foreach (var (grade, chance) in odds)
        {
            acc += chance;
            if (r < acc)
                return grade;
        }
        return odds[odds.Length - 1].grade;
    }

    /// <summary>
    /// 등급을 정하고 그 등급 안에서 한 종을 고른다.
    /// 지금은 등급 안에서 균등. A 의 SpawnConditions "지금 어울리는 종 가중치" 함수가 나오면 여기서 가중치로 고른다.
    /// </summary>
    public static string RollSpecies(bool premium, float rGrade, float rSpecies)
    {
        IReadOnlyList<string> pool = SpeciesOf(RollGrade(premium, rGrade));
        if (pool.Count == 0)
            return null;
        return pool[Mathf.Clamp(Mathf.FloorToInt(rSpecies * pool.Count), 0, pool.Count - 1)];
    }

    // ---------- 진화 (임시) ----------

    /// <summary>파트너로 함께 이만큼 걸을 때마다 진화 경험치 +1</summary>
    public const float GrowthMetersPerPoint = 100f;
    /// <summary>진화 경험치가 이 값 이상이면 단계 1, 2 (단계는 0~2)</summary>
    public static readonly int[] StageThresholds = { 100, 300 };
    public const int MaxStage = 2;

    /// <summary>같은 종이 또 나왔을 때 그 종의 진화 경험치</summary>
    public static int DuplicateGrowth(Grade g) => g switch { Grade.C => 10, Grade.B => 20, Grade.A => 30, _ => 40 };

    public static int StageOf(int growth)
    {
        int stage = 0;
        foreach (int t in StageThresholds)
            if (growth >= t)
                stage++;
        return stage;
    }

    /// <summary>다음 단계까지 필요한 진화 경험치 (최종 단계면 0)</summary>
    public static int NextThreshold(int growth)
    {
        foreach (int t in StageThresholds)
            if (growth < t)
                return t;
        return 0;
    }

    // ---------- 경험치 · 레벨 ----------

    /// <summary>걸은 거리 1km 마다 경험치 (v2 경험치 표 제안값, C-V4 이후 서버 값으로)</summary>
    public const int XpPerKm = 10;

    // ---------- 첫 파트너 ----------

    /// <summary>첫 실행에 고를 C 등급 3마리 (key 로 늘 같은 3마리)</summary>
    public static List<string> Starters(int key)
    {
        var pool = new List<string>(SpeciesOf(Grade.C));
        var rng = new System.Random(key);
        while (pool.Count > 3)
            pool.RemoveAt(rng.Next(pool.Count));
        return pool;
    }
}
