using System;
using NUnit.Framework;
using UnityEngine;

/// <summary>디자인 기준 v3 (KucaTheme): 계절 구분과 글자 대비. Test Runner › EditMode.</summary>
public class KucaThemeTests
{
    [Test]
    public void 계절은_지도와_같은_달로_나뉜다()
    {
        Assert.AreEqual(KucaTheme.Season.Spring, KucaTheme.FromDate(new DateTime(2026, 4, 1)));
        Assert.AreEqual(KucaTheme.Season.Summer, KucaTheme.FromDate(new DateTime(2026, 7, 1)));
        Assert.AreEqual(KucaTheme.Season.Autumn, KucaTheme.FromDate(new DateTime(2026, 10, 19)));
        Assert.AreEqual(KucaTheme.Season.Winter, KucaTheme.FromDate(new DateTime(2026, 1, 15)));
    }

    [Test]
    public void 흰_글자와_강조색_대비는_4_5_이상()
    {
        Assert.GreaterOrEqual(Contrast(KucaTheme.Primary, Color.white), 4.5f);
        Assert.GreaterOrEqual(Contrast(KucaTheme.PrimaryDeep, KucaTheme.PrimarySoft), 4.5f);
        foreach (var s in KucaTheme.Seasons)
        {
            Assert.GreaterOrEqual(Contrast(s.accent, Color.white), 4.5f, s.name);
            Assert.GreaterOrEqual(Contrast(s.accentText, s.accentSoft), 4.5f, s.name);
        }
    }

    static float Contrast(Color a, Color b)
    {
        float la = Lum(a), lb = Lum(b);
        return (Mathf.Max(la, lb) + 0.05f) / (Mathf.Min(la, lb) + 0.05f);
    }

    static float Lum(Color c) => 0.2126f * Lin(c.r) + 0.7152f * Lin(c.g) + 0.0722f * Lin(c.b);
    static float Lin(float v) => v <= 0.03928f ? v / 12.92f : Mathf.Pow((v + 0.055f) / 1.055f, 2.4f);
}
