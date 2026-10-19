using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// KUCA 디자인 기준 v3: 색 · 글자 크기 · 간격. 화면마다 따로 쓰던 색 상수를 여기 하나로 모은다.
/// - 바탕은 캠퍼스 초록(Primary). 개성은 절제하고, 계절마다 토글 · 칩 · 진행 막대의 강조색(Accent)만 조금 바뀐다.
/// - 계절은 날짜로 자동 (지도 KeyArtLook · SpawnConditions 와 같은 3–5 봄 · 6–8 여름 · 9–11 가을 · 12–2 겨울).
///   설정에서 고정할 수 있다 (UI 색만 바뀌고 뽑기 확률은 그대로).
/// - 기준 캔버스 1080×1920 (UIKit.CreateCanvas). 설계안: docs/디자인_v3.md
/// </summary>
public static class KucaTheme
{
    // ---------- 고정 색 ----------
    public static readonly Color Surface = Hex(0xF7F8F4);      // 화면 바탕 (초록 기운이 도는 종이색)
    public static readonly Color Card = Color.white;
    public static readonly Color Line = Hex(0xE2E7DF);         // 구분선 · 테두리
    public static readonly Color Ink = Hex(0x1D2B22);          // 본문 글자
    public static readonly Color InkMuted = Hex(0x5C6B61);     // 보조 글자
    public static readonly Color InkSoft = Hex(0x8A978E);      // 비활성 글자 · 아이콘
    public static readonly Color Primary = Hex(0x2B7A4B);      // 캠퍼스 초록: 주요 버튼 (흰 글자 대비 5.3)
    public static readonly Color PrimaryDeep = Hex(0x1E5E3B);  // 눌림 · 제목
    public static readonly Color PrimarySoft = Hex(0xE4F2E8);  // 보조 버튼 · 선택 배경
    public static readonly Color Crimson = Hex(0xA6192E);      // 경희 크림슨: 배지 · 좋아요 · 경고에만
    public static readonly Color Scrim = new Color(0.06f, 0.10f, 0.08f, 0.55f);   // 팝업 뒤 어둡게

    // ---------- 계절 강조색 ----------
    public enum Season { Spring, Summer, Autumn, Winter }

    public readonly struct SeasonSet
    {
        public readonly string name;
        public readonly Color accent;      // 켜진 토글 · 칩 · 진행 막대 (흰 글자 대비 4.5 이상)
        public readonly Color accentText;  // 옅은 배경 위 글자
        public readonly Color accentSoft;  // 옅은 배경
        public SeasonSet(string name, int accent, int text, int soft)
        {
            this.name = name;
            this.accent = Hex(accent);
            accentText = Hex(text);
            accentSoft = Hex(soft);
        }
    }

    /// <summary>봄 벚꽃 · 여름 숲 그늘 · 가을 단풍 · 겨울 새벽 하늘</summary>
    public static readonly SeasonSet[] Seasons =
    {
        new SeasonSet("봄", 0xC2457A, 0xA3305F, 0xFBE3EC),
        new SeasonSet("여름", 0x1A7E73, 0x166B62, 0xD8F1EE),
        new SeasonSet("가을", 0xB8521C, 0x9A4515, 0xFBE6D6),
        new SeasonSet("겨울", 0x3F6FB0, 0x2F5A94, 0xE1EAF6),
    };

    const string PrefKey = "ui_season";   // 없음 = 자동, 0~3 = 고정
    static bool loaded;
    static Season? overrideSeason;

    /// <summary>계절이 바뀌었을 때 (설정에서 고정하거나 자동으로 되돌렸을 때). 열린 화면이 다시 칠한다</summary>
    public static event Action Changed;

    /// <summary>설정에서 고정한 계절 (null = 날짜로 자동)</summary>
    public static Season? Override
    {
        get
        {
            if (!loaded)
            {
                loaded = true;
                int v = PlayerPrefs.GetInt(PrefKey, -1);
                overrideSeason = v >= 0 && v < Seasons.Length ? (Season)v : (Season?)null;
            }
            return overrideSeason;
        }
        set
        {
            loaded = true;
            overrideSeason = value;
            if (value.HasValue) PlayerPrefs.SetInt(PrefKey, (int)value.Value);
            else PlayerPrefs.DeleteKey(PrefKey);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }
    }

    /// <summary>날짜의 계절 (지도·출현 규칙과 같은 달 구분)</summary>
    public static Season FromDate(DateTime t) => (Season)(int)SpawnConditions.SeasonAt(t);

    public static Season Current => Override ?? FromDate(DateTime.Now);
    public static SeasonSet Set => Seasons[(int)Current];
    public static Color Accent => Set.accent;
    public static Color AccentText => Set.accentText;
    public static Color AccentSoft => Set.accentSoft;

    // ---------- 글자 크기 (기준 캔버스 px) ----------
    public const int Display = 60;   // 큰 숫자 (걸은 거리 · 티켓)
    public const int Title = 48;     // 화면 제목
    public const int Heading = 38;   // 카드 제목 · 큰 버튼
    public const int Body = 32;      // 본문 · 버튼
    public const int Label = 28;     // 칩 · 보조 설명
    public const int Caption = 24;   // 작은 안내 · 배지 숫자

    // ---------- 간격 (8의 배수) ----------
    public const float Space1 = 8f, Space2 = 16f, Space3 = 24f, Space4 = 32f, Space6 = 48f, Space8 = 64f;
    /// <summary>손가락으로 누르는 것의 최소 크기</summary>
    public const float TouchMin = 96f;

    /// <summary>토글 · 칩 하나를 켜짐 / 꺼짐 모양으로 칠한다 (켜짐 = 계절 강조색 + 흰 글자)</summary>
    public static void PaintToggle(Image background, Text label, bool on)
    {
        if (background != null) background.color = on ? Accent : PrimarySoft;
        if (label != null) label.color = on ? Color.white : PrimaryDeep;
    }

    static Color Hex(int rgb) => new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        loaded = false;
        overrideSeason = null;
        Changed = null;
    }
}
