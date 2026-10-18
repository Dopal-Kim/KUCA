using NUnit.Framework;

/// <summary>B-V1 규칙 테스트 (Window › General › Test Runner › EditMode). 규칙마다 하나씩.</summary>
public class GachaRulesTests
{
    static GameProgress Fresh()
    {
        var p = new GameProgress { dataVersion = 2 };
        return p;
    }

    [Test]
    public void 일장_뽑기는_C_A_만_오장_뽑기는_B_S_만()
    {
        for (float r = 0f; r < 1f; r += 0.01f)
        {
            Assert.AreNotEqual(GachaRules.Grade.S, GachaRules.RollGrade(false, r));
            Assert.AreNotEqual(GachaRules.Grade.C, GachaRules.RollGrade(true, r));
            string single = GachaRules.RollSpecies(false, r, r);
            string premium = GachaRules.RollSpecies(true, r, 1f - r);
            Assert.AreNotEqual(GachaRules.Grade.S, GachaRules.GradeOf(single));
            Assert.AreNotEqual(GachaRules.Grade.C, GachaRules.GradeOf(premium));
        }
        Assert.AreEqual(GachaRules.Grade.C, GachaRules.RollGrade(false, 0f));
        Assert.AreEqual(GachaRules.Grade.S, GachaRules.RollGrade(true, 0.999f));
    }

    [Test]
    public void 확률은_합이_1이고_모든_등급에_종이_있다()
    {
        foreach (bool premium in new[] { false, true })
        {
            float sum = 0f;
            foreach (var (_, chance) in GachaRules.OddsOf(premium)) sum += chance;
            Assert.AreEqual(1f, sum, 1e-4f);
        }
        foreach (GachaRules.Grade g in System.Enum.GetValues(typeof(GachaRules.Grade)))
            Assert.Greater(GachaRules.SpeciesOf(g).Count, 0, g.ToString());
        Assert.AreEqual(CreatureLibrary.AllSpecies.Count,
            GachaRules.SpeciesOf(GachaRules.Grade.C).Count + GachaRules.SpeciesOf(GachaRules.Grade.B).Count
            + GachaRules.SpeciesOf(GachaRules.Grade.A).Count + GachaRules.SpeciesOf(GachaRules.Grade.S).Count);
    }

    [Test]
    public void 일km_마다_티켓_한장_하루_상한()
    {
        GameProgress p = Fresh();
        GameProgress.WalkResult r = p.AddWalk(2500f);
        Assert.AreEqual(2, r.tickets);
        Assert.AreEqual(2, p.tickets);
        Assert.AreEqual(500f, p.ticketMeters, 0.01f);

        r = p.AddWalk(10000f); // 상한 5장까지만
        Assert.AreEqual(GachaRules.TicketsPerDay - 2, r.tickets);
        Assert.AreEqual(GachaRules.TicketsPerDay, p.tickets);
        Assert.IsTrue(p.TicketCapped);
        Assert.AreEqual(GachaRules.TicketMeters, p.ticketMeters, 0.01f); // 다 차서 내일을 기다림
    }

    [Test]
    public void 걸으면_일km_마다_경험치_10()
    {
        GameProgress p = Fresh();
        Assert.AreEqual(25, p.AddWalk(2550f).xp);
        Assert.AreEqual(25, p.score);
    }

    [Test]
    public void 중복이면_그_종의_진화_경험치가_오른다()
    {
        GameProgress p = Fresh();
        p.tickets = 2;
        Assert.IsTrue(p.Draw(false, 0f, 0f, out GameProgress.DrawResult first));
        Assert.IsTrue(first.isNew);
        Assert.AreEqual(0, first.growthAdded);
        Assert.IsTrue(p.Draw(false, 0f, 0f, out GameProgress.DrawResult again)); // 같은 난수 → 같은 종
        Assert.AreEqual(first.species, again.species);
        Assert.IsFalse(again.isNew);
        Assert.AreEqual(GachaRules.DuplicateGrowth(again.grade), again.growthAdded);
        Assert.AreEqual(again.growthAdded, p.GrowthOf(again.species));
        Assert.AreEqual(0, p.tickets);
        Assert.IsFalse(p.Draw(false, 0f, 0f, out _)); // 티켓이 없으면 못 뽑음
    }

    [Test]
    public void 오장_뽑기는_티켓_다섯장()
    {
        GameProgress p = Fresh();
        p.tickets = 4;
        Assert.IsFalse(p.Draw(true, 0.5f, 0.5f, out _));
        p.tickets = 5;
        Assert.IsTrue(p.Draw(true, 0.5f, 0.5f, out GameProgress.DrawResult r));
        Assert.AreEqual(0, p.tickets);
        Assert.AreNotEqual(GachaRules.Grade.C, r.grade);
    }

    [Test]
    public void 진화_경험치로_단계가_정해진다()
    {
        Assert.AreEqual(0, GachaRules.StageOf(0));
        Assert.AreEqual(0, GachaRules.StageOf(GachaRules.StageThresholds[0] - 1));
        Assert.AreEqual(1, GachaRules.StageOf(GachaRules.StageThresholds[0]));
        Assert.AreEqual(2, GachaRules.StageOf(GachaRules.StageThresholds[1]));
        Assert.AreEqual(GachaRules.MaxStage, GachaRules.StageOf(99999));
        Assert.AreEqual(GachaRules.StageThresholds[0], GachaRules.NextThreshold(0));
        Assert.AreEqual(0, GachaRules.NextThreshold(99999));
    }

    [Test]
    public void 파트너와_함께_걸으면_진화한다()
    {
        GameProgress p = Fresh();
        string starter = GachaRules.SpeciesOf(GachaRules.Grade.C)[0];
        p.SetPartner(p.AddCreature(starter).uid);
        float toStage1 = GachaRules.StageThresholds[0] * GachaRules.GrowthMetersPerPoint;
        GameProgress.WalkResult r = p.AddWalk(toStage1 - 50f);
        Assert.IsFalse(r.evolved);
        r = p.AddWalk(50f);
        Assert.IsTrue(r.evolved);
        Assert.AreEqual(1, r.stage);
        Assert.AreEqual(1, p.StageOf(starter));
    }

    [Test]
    public void 첫_파트너는_C_등급_세마리()
    {
        var starters = GachaRules.Starters(1234);
        Assert.AreEqual(3, starters.Count);
        foreach (string s in starters)
            Assert.AreEqual(GachaRules.Grade.C, GachaRules.GradeOf(s));
        CollectionAssert.AreEqual(starters, GachaRules.Starters(1234)); // 같은 기기면 늘 같은 3마리
    }

    [Test]
    public void 옛_저장은_레벨을_초기화하고_경희몬은_남긴다()
    {
        var p = new GameProgress { dataVersion = 0, score = 1234, rewardedLevel = 7 };
        GameProgress.Caught c = p.AddCreature(GachaRules.SpeciesOf(GachaRules.Grade.B)[0]);
        p.SetPartner(c.uid);
        p.dailyQuests.Add(new GameProgress.QuestState { id = "d_catch5" });
        p.MigrateToV2();
        Assert.AreEqual(0, p.score);
        Assert.AreEqual(0, p.rewardedLevel);
        Assert.AreEqual(2, p.dataVersion);
        Assert.AreEqual(0, p.dailyQuests.Count);
        Assert.AreEqual(1, p.caught.Count);
        Assert.AreEqual(c.uid, p.partnerUid);

        p.score = 50;
        p.MigrateToV2(); // 두 번째는 아무것도 안 함
        Assert.AreEqual(50, p.score);
    }
}
