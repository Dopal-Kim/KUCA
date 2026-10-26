using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 함께 걷기 화면의 움직임 (10/25 피드백: "매칭이 실시간으로 이뤄지는 생동감이 없다").
// - MatchRipple: 기다리는 동안 파트너 그림 뒤로 원 파동이 계속 퍼진다 (찾는 중)
// - Stardust: 상대를 찾은 순간 위에서 별가루가 쏟아진다 (매칭 완료)
// - PopIn: 카드가 살짝 커졌다 제자리로 (새 제안 · 새 코스 · 새 주제)
// 색은 KucaTheme 만 쓰고 (알파만 바꿈), 시간은 Time.unscaledTime (일시정지 · 시간 배율과 무관).

/// <summary>퍼지는 원 파동. holder 가운데에 고리 3개가 번갈아 커지며 옅어진다</summary>
public class MatchRipple : MonoBehaviour
{
    const int Rings = 3;
    const float Period = 2.4f;               // 고리 하나가 퍼져서 사라지는 시간 (초)
    const float FromScale = 0.45f, ToScale = 1.35f;
    const float StartAlpha = 0.6f;

    Image[] rings;
    Image core;
    Color tint;

    /// <summary>holder 안에 size 크기의 원 파동을 깐다 (holder 의 다른 그림보다 뒤에)</summary>
    public static MatchRipple Add(RectTransform holder, float size, Color tint)
    {
        var ripple = holder.gameObject.AddComponent<MatchRipple>();
        ripple.tint = tint;
        ripple.Build(holder, size);
        return ripple;
    }

    void Build(RectTransform holder, float size)
    {
        rings = new Image[Rings];
        for (int i = 0; i < Rings; i++)
        {
            Image ring = UIKit.Image(holder, "Ring" + i, tint);
            ring.sprite = HudIcons.GlowRing;
            Center(ring.rectTransform, size);
            ring.rectTransform.SetAsFirstSibling();
            rings[i] = ring;
        }
        // 가운데 옅은 원이 숨 쉬듯 커졌다 작아진다
        core = UIKit.Image(holder, "Core", KucaTheme.AccentSoft);
        core.sprite = HudIcons.SoftDisk;
        Center(core.rectTransform, size * 0.62f);
        core.rectTransform.SetSiblingIndex(Rings);
    }

    static void Center(RectTransform rt, float size)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(size, size);
    }

    void Update()
    {
        float now = Time.unscaledTime;
        for (int i = 0; i < Rings; i++)
        {
            float t = Mathf.Repeat(now / Period + i / (float)Rings, 1f);
            float eased = 1f - (1f - t) * (1f - t);   // 처음엔 빨리, 끝에선 천천히
            rings[i].rectTransform.localScale = Vector3.one * Mathf.Lerp(FromScale, ToScale, eased);
            Color c = tint;
            c.a = StartAlpha * (1f - t);
            rings[i].color = c;
        }
        core.rectTransform.localScale = Vector3.one * (1f + 0.06f * Mathf.Sin(now * 2.6f));
    }
}

/// <summary>별가루: area 위쪽에서 별 · 반짝이가 흩날리며 떨어지고, 다 떨어지면 스스로 사라진다</summary>
public class Stardust : MonoBehaviour
{
    const int Count = 42;
    const float Life = 2.2f, MaxDelay = 0.7f;

    class Bit
    {
        public RectTransform rt;
        public Image image;
        public Vector2 from, velocity;
        public float delay, life, spin, phase, size;
        public Color color;
    }

    readonly List<Bit> bits = new List<Bit>();
    float started;

    /// <summary>area 를 덮는 별가루를 한 번 뿌린다 (눌림은 막지 않는다)</summary>
    public static void Burst(RectTransform area)
    {
        RectTransform layer = UIKit.Rect(area, "Stardust");
        layer.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
        UIKit.Stretch(layer);
        layer.SetAsLastSibling();
        layer.gameObject.AddComponent<Stardust>().Spawn(area.rect);
    }

    void Spawn(Rect area)
    {
        started = Time.unscaledTime;
        Color[] palette = { KucaTheme.Gold, KucaTheme.Accent, KucaTheme.Gold, KucaTheme.PrimarySoft };
        float w = Mathf.Max(area.width, 600f), h = Mathf.Max(area.height, 900f);
        for (int i = 0; i < Count; i++)
        {
            var bit = new Bit
            {
                size = Random.Range(26f, 66f),
                color = palette[i % palette.Length],
                from = new Vector2(Random.Range(-0.48f, 0.48f) * w, h * 0.5f + Random.Range(0f, 160f)),
                velocity = new Vector2(Random.Range(-90f, 90f), -Random.Range(h * 0.32f, h * 0.55f)),
                delay = Random.Range(0f, MaxDelay),
                life = Life * Random.Range(0.8f, 1.15f),
                spin = Random.Range(-220f, 220f),
                phase = Random.Range(0f, 6.28f),
            };
            bit.image = UIKit.Image(transform, "Bit", bit.color);
            bit.image.sprite = i % 3 == 0 ? HudIcons.SoftDisk : HudIcons.Star;   // 별 2 : 반짝이 1
            bit.rt = bit.image.rectTransform;
            bit.rt.anchorMin = bit.rt.anchorMax = bit.rt.pivot = new Vector2(0.5f, 0.5f);
            bit.rt.sizeDelta = new Vector2(bit.size, bit.size);
            bit.image.enabled = false;
            bits.Add(bit);
        }
    }

    void Update()
    {
        float t = Time.unscaledTime - started;
        bool alive = false;
        foreach (Bit b in bits)
        {
            float lt = t - b.delay;
            if (lt < 0f) { alive = true; continue; }
            float k = lt / b.life;
            if (k >= 1f) { b.image.enabled = false; continue; }
            alive = true;
            b.image.enabled = true;
            // 아래로 떨어지면서 좌우로 살랑, 끝으로 갈수록 느려진다
            Vector2 drift = new Vector2(Mathf.Sin(lt * 3f + b.phase) * 28f, 0f);
            b.rt.anchoredPosition = b.from + b.velocity * (lt * (1f - 0.35f * k)) + drift;
            b.rt.localRotation = Quaternion.Euler(0f, 0f, b.spin * lt);
            b.rt.localScale = Vector3.one * (0.85f + 0.25f * Mathf.Sin(lt * 11f + b.phase));   // 반짝반짝
            Color c = b.color;
            c.a = k < 0.12f ? k / 0.12f : 1f - (k - 0.12f) / 0.88f;
            b.image.color = c;
        }
        if (!alive)
            Destroy(gameObject);
    }
}

/// <summary>살짝 커졌다 제자리로 (0.35초). 내용이 바뀐 카드에 Play()</summary>
public class PopIn : MonoBehaviour
{
    const float Duration = 0.35f;
    float started = -1f;

    public static void Play(Component target)
    {
        if (target == null) return;
        if (!target.TryGetComponent(out PopIn pop))   // Unity 객체는 ?? 로 비교하지 않는다
            pop = target.gameObject.AddComponent<PopIn>();
        pop.started = Time.unscaledTime;
        pop.transform.localScale = Vector3.one * 0.92f;
    }

    void Update()
    {
        if (started < 0f) return;
        float k = Mathf.Clamp01((Time.unscaledTime - started) / Duration);
        // 0.92 → 1.04 → 1 (살짝 튀는 느낌)
        float s = k < 0.6f ? Mathf.Lerp(0.92f, 1.04f, k / 0.6f) : Mathf.Lerp(1.04f, 1f, (k - 0.6f) / 0.4f);
        transform.localScale = Vector3.one * s;
        if (k >= 1f) started = -1f;
    }
}
