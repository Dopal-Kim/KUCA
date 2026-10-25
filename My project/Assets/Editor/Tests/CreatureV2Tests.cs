using System.Collections.Generic;
using NUnit.Framework;

/// <summary>A-V1 경희펫 v2 값 테스트 (등급표 · 확률 · 단계 이름 · 뽑기 가중치). Test Runner › EditMode.</summary>
public class CreatureV2Tests
{
    [TearDown]
    public void ResetConditions()
    {
        SpawnConditions.OverrideTime = null;
        SpawnConditions.OverrideSeason = null;
        SpawnConditions.OverrideWeather = null;
    }

    [Test]
    public void 등급표는_24종_모두를_C4_B4_A8_S8_로_나눈다()
    {
        var count = new Dictionary<GachaRules.Grade, int>();
        foreach (string id in CreatureLibrary.AllSpecies)
        {
            Assert.IsTrue(CreatureLibrary.Grades.TryGetValue(id, out GachaRules.Grade g), id);
            count[g] = count.TryGetValue(g, out int n) ? n + 1 : 1;
        }
        Assert.AreEqual(CreatureLibrary.AllSpecies.Count, CreatureLibrary.Grades.Count);
        Assert.AreEqual(4, count[GachaRules.Grade.C]);
        Assert.AreEqual(4, count[GachaRules.Grade.B]);
        Assert.AreEqual(8, count[GachaRules.Grade.A]);
        Assert.AreEqual(8, count[GachaRules.Grade.S]);
    }

    [Test]
    public void 확률표_합은_1_일장은_S_없음_오장은_C_없음()
    {
        float single = 0f, premium = 0f;
        foreach (var (g, c) in CreatureLibrary.SingleOdds) { single += c; Assert.AreNotEqual(GachaRules.Grade.S, g); }
        foreach (var (g, c) in CreatureLibrary.PremiumOdds) { premium += c; Assert.AreNotEqual(GachaRules.Grade.C, g); }
        Assert.AreEqual(1f, single, 1e-4f);
        Assert.AreEqual(1f, premium, 1e-4f);
        Assert.Less(CreatureLibrary.StageThresholds[0], CreatureLibrary.StageThresholds[1]);
        Assert.AreEqual(CreatureLibrary.StageScales.Length, CreatureLibrary.StageRims.Length);
    }

    [Test]
    public void 단계_이름은_아기_도감이름_빛나는()
    {
        Assert.AreEqual("아기 올빼미", CreatureLibrary.StageName("library_owl", 0));
        Assert.AreEqual("중앙도서관 올빼미", CreatureLibrary.StageName("library_owl", 1));
        Assert.AreEqual("빛나는 중앙도서관 올빼미", CreatureLibrary.StageName("library_owl", 2));
        Assert.AreEqual("아기 비둘기", CreatureLibrary.StageName("pigeon", 0));
    }

    [Test]
    public void 뽑기는_지금_어울리는_종을_더_자주_고른다()
    {
        // 가을 맑은 밤: 올빼미(밤 ×3) 가중치 √3, 까치(밤 ×0.2) 는 하한 0.5 → 올빼미 약 78%
        SpawnConditions.OverrideTime = SpawnConditions.TimeSlot.Night;
        SpawnConditions.OverrideSeason = SpawnConditions.Season.Autumn;
        SpawnConditions.OverrideWeather = SpawnConditions.Weather.Clear;
        var pool = new List<string> { "library_owl", "gate_magpie" };
        Assert.AreEqual("library_owl", SpawnConditions.PickForGacha(pool, 0f));
        Assert.AreEqual("library_owl", SpawnConditions.PickForGacha(pool, 0.7f));
        Assert.AreEqual("gate_magpie", SpawnConditions.PickForGacha(pool, 0.85f));
        Assert.AreEqual("gate_magpie", SpawnConditions.PickForGacha(pool, 1f));
        Assert.AreEqual(1f, SpawnConditions.GachaWeight("no_such_species"), 1e-4f);
        Assert.IsNull(SpawnConditions.PickForGacha(new List<string>(), 0.5f));
    }
}
