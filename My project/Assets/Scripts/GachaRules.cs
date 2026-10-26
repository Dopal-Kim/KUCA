using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// v2 경희펫 규칙 (B-V1): 걸어서 티켓 → 뽑기 → 중복·함께 걷기로 진화.
/// 화면이 없는 계산만 모아 EditMode 테스트로 확인한다 (Assets/Editor/Tests/GachaRulesTests.cs).
///
/// 등급표 · 확률 · 진화 기준은 A 가 정한 값 (CreatureLibrary, A-V1) 을 그대로 읽는다. 값을 바꿀 때는 CreatureLibrary 에서.
/// 등급 안에서 어떤 종이 나올지는 지금 시간 · 계절 · 날씨에 어울리는 종일수록 잘 나온다 (SpawnConditions.PickForGacha).
/// </summary>
public static class GachaRules
{
    public enum Grade { C, B, A, S }

    // ---------- 등급 (A: CreatureLibrary.Grades) ----------

    public static Grade GradeOf(string species) =>
        species != null && CreatureLibrary.Grades.TryGetValue(species, out Grade g) ? g : Grade.C;

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

    /// <summary>화면에 보이는 희귀도 말 (펫을 C~S "등급" 으로 부르면 어색했음 — 사용자 결정 10/26). 규칙 · 저장은 Grade 그대로</summary>
    public static string Label(Grade g) => g switch
    {
        Grade.C => "흔함",
        Grade.B => "조금 드묾",
        Grade.A => "드묾",
        _ => "아주 드묾",
    };

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
    /// <summary>하루에 걸어서 받을 수 있는 티켓 수 (V0 결정 Q3: 5장 = 5장 뽑기 한 번)</summary>
    public const int TicketsPerDay = 5;
    public const int SingleCost = 1, PremiumCost = 5;

    // ---------- 뽑기 확률 (A: CreatureLibrary) ----------

    /// <summary>1장 뽑기: C~A</summary>
    public static (Grade grade, float chance)[] SingleOdds => CreatureLibrary.SingleOdds;
    /// <summary>5장 뽑기: B~S</summary>
    public static (Grade grade, float chance)[] PremiumOdds => CreatureLibrary.PremiumOdds;

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

    /// <summary>등급을 정하고, 그 등급 안에서 지금 어울리는 종을 가중치로 고른다 (A: SpawnConditions.PickForGacha)</summary>
    public static string RollSpecies(bool premium, float rGrade, float rSpecies)
    {
        var pool = new List<string>(SpeciesOf(RollGrade(premium, rGrade)));
        return pool.Count == 0 ? null : SpawnConditions.PickForGacha(pool, rSpecies);
    }

    // ---------- 진화 (A: CreatureLibrary) ----------

    /// <summary>파트너로 함께 이만큼 걸을 때마다 진화 경험치 +1</summary>
    public const float GrowthMetersPerPoint = CreatureLibrary.GrowthMetersPerPoint;
    /// <summary>진화 경험치가 이 값 이상이면 단계 1, 2 (단계는 0~2)</summary>
    public static int[] StageThresholds => CreatureLibrary.StageThresholds;
    public static int MaxStage => CreatureLibrary.StageScales.Length - 1;

    /// <summary>같은 종이 또 나왔을 때 그 종의 진화 경험치</summary>
    public static int DuplicateGrowth(Grade g) => CreatureLibrary.DuplicateGrowth(g);

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
