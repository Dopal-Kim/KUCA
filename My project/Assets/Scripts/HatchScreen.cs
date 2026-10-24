using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 뽑기 결과 연출 (B-V1, 예전 알 부화 화면을 고쳐 씀). 티켓이 흔들리다 번쩍 열리고, 나온 경희몬 · 등급 · 진화 경험치를 보여 준다.
/// 메모판·산책 길잡이 위에서도 보이도록 자기 캔버스(순서 40)를 쓴다.
/// </summary>
public class HatchScreen : MonoBehaviour
{
    static readonly Color Ink = KucaTheme.Ink;
    static readonly Color InkQuiet = KucaTheme.InkMuted;
    static readonly Color TicketPaper = new Color(1f, 0.93f, 0.78f);
    static readonly Color TicketGold = new Color(1f, 0.78f, 0.25f);

    GameObject root;
    RectTransform egg, creature, rays;
    RawImage creatureImage;
    Image flash;
    Text title, nameText, tierText, info;
    Button ok;
    bool skip;   // 흔들리는 중에 화면을 누르면 바로 연다
    System.Action onClose;

    /// <summary>뽑기 결과를 보여 준다. premium: 5장 뽑기 (티켓이 금색)</summary>
    public static void Show(GameProgress.DrawResult r, bool premium, int ticketsLeft, System.Action closed = null)
    {
        HatchScreen s = FindAnyObjectByType<HatchScreen>();
        if (s == null)
            s = new GameObject("HatchScreen").AddComponent<HatchScreen>();
        s.Play(r, premium, ticketsLeft, closed);
    }

    void Awake() => Build();

    void Play(GameProgress.DrawResult r, bool premium, int ticketsLeft, System.Action closed)
    {
        StopAllCoroutines();
        onClose = closed;
        root.SetActive(true);
        Texture2D tex = null;
        try { tex = MonsterThumbnails.Get(r.species); }
        catch (System.Exception e) { Debug.LogWarning("[Hatch] 썸네일 실패: " + e.Message); }
        creatureImage.texture = tex;
        creatureImage.enabled = tex != null;
        egg.GetComponent<Image>().color = premium ? TicketGold : TicketPaper;
        nameText.text = CreatureLibrary.NameOf(r.species);
        tierText.text = $"{GachaRules.Label(r.grade)} 등급";
        tierText.color = GachaRules.ColorOf(r.grade);
        string what = r.isNew ? "새 경희몬!  도감에 등록됐어요"
            : r.evolved ? $"진화 경험치 +{r.growthAdded}\n{KhuMenus.EvolvedMessage(r.species, r.stage)}"
            : $"이미 있는 경희몬  ·  진화 경험치 +{r.growthAdded}";
        info.text = $"{what}\n남은 티켓 {ticketsLeft}장";
        UIInputBlocker.SetModal(this, true);
        StartCoroutine(Animate());
    }

    IEnumerator Animate()
    {
        title.text = "티켓을 여는 중…";
        egg.gameObject.SetActive(true);
        creature.gameObject.SetActive(false);
        rays.gameObject.SetActive(false);
        foreach (Text t in new[] { nameText, tierText, info }) t.enabled = false;
        ok.gameObject.SetActive(false);
        skip = false;
        flash.color = new Color(1f, 1f, 1f, 0f);

        // 1) 점점 세게 흔들린다
        for (float t = 0f; t < 1.6f && !skip; t += Time.unscaledDeltaTime)
        {
            float k = t / 1.6f;
            egg.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 22f) * 16f * k);
            egg.localScale = Vector3.one * (1f + Mathf.Abs(Mathf.Sin(t * 11f)) * 0.05f * k);
            yield return null;
        }
        // 2) 번쩍
        egg.gameObject.SetActive(false);
        creature.gameObject.SetActive(true);
        rays.gameObject.SetActive(true);
        title.text = "뽑았어요!";
        foreach (Text t in new[] { nameText, tierText, info }) t.enabled = true;
        ok.gameObject.SetActive(true);
        for (float t = 0f; t < 0.45f; t += Time.unscaledDeltaTime)
        {
            float k = t / 0.45f;
            flash.color = new Color(1f, 1f, 1f, 1f - k);
            // 작게 → 조금 크게 → 제자리 (톡 튀어나옴)
            float s = k < 0.7f ? Mathf.Lerp(0.3f, 1.12f, k / 0.7f) : Mathf.Lerp(1.12f, 1f, (k - 0.7f) / 0.3f);
            creature.localScale = Vector3.one * s;
            yield return null;
        }
        flash.color = new Color(1f, 1f, 1f, 0f);
        creature.localScale = Vector3.one;
        // 3) 빛줄기가 천천히 돈다
        while (true)
        {
            rays.localRotation = Quaternion.Euler(0f, 0f, Time.unscaledTime * 20f);
            yield return null;
        }
    }

    void Close()
    {
        StopAllCoroutines();
        root.SetActive(false);
        UIInputBlocker.SetModal(this, false);
        System.Action done = onClose;
        onClose = null;
        done?.Invoke();
    }

    void OnDisable() => UIInputBlocker.SetModal(this, false);

    void Build()
    {
        Canvas canvas = UIKit.CreateCanvas(transform, "HatchCanvas", 40);
        root = UIKit.Image(canvas.transform, "Hatch", KucaTheme.Scrim, raycast: true).gameObject;
        BackButton.Attach(root, Close);   // 뒤로 가기로도 닫힌다
        Button tapAnywhere = root.AddComponent<Button>();
        tapAnywhere.transition = Selectable.Transition.None;
        tapAnywhere.onClick.AddListener(() => skip = true);
        UIKit.Stretch((RectTransform)root.transform);

        Image card = UIKit.Image(root.transform, "Card", Color.white, raycast: true);
        card.sprite = HudIcons.Rounded;
        card.type = Image.Type.Sliced;
        card.pixelsPerUnitMultiplier = 0.5f;
        RectTransform cr = card.rectTransform;
        cr.anchorMin = cr.anchorMax = cr.pivot = new Vector2(0.5f, 0.5f);
        cr.sizeDelta = new Vector2(820f, 1060f);

        title = UIKit.Text(cr, "", 44, TextAnchor.MiddleCenter, Ink);
        title.fontStyle = FontStyle.Bold;
        Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -70f), new Vector2(760f, 70f));

        rays = UIKit.Image(cr, "Rays", new Color(1f, 0.8f, 0.3f, 0.8f)).rectTransform;
        rays.GetComponent<Image>().sprite = HudIcons.Rays;
        Center(rays, -370f, 640f);

        egg = UIKit.Image(cr, "Ticket", TicketPaper).rectTransform; // 흔들리다 열리는 티켓
        egg.GetComponent<Image>().sprite = HudIcons.Ticket;
        egg.gameObject.AddComponent<Outline>().effectColor = new Color(0.85f, 0.6f, 0.3f);
        egg.GetComponent<Outline>().effectDistance = new Vector2(4f, -4f);
        Center(egg, -370f, 340f);

        var go = new GameObject("Creature", typeof(RectTransform), typeof(RawImage));
        go.transform.SetParent(cr, false);
        creatureImage = go.GetComponent<RawImage>();
        creatureImage.raycastTarget = false;
        creature = creatureImage.rectTransform;
        Center(creature, -370f, 440f);

        nameText = UIKit.Text(cr, "", 54, TextAnchor.MiddleCenter, Ink);
        nameText.fontStyle = FontStyle.Bold;
        Place(nameText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -610f), new Vector2(760f, 76f));
        tierText = UIKit.Text(cr, "", 34, TextAnchor.MiddleCenter, Ink);
        tierText.fontStyle = FontStyle.Bold;
        Place(tierText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -692f), new Vector2(760f, 50f));
        info = UIKit.Text(cr, "", 30, TextAnchor.MiddleCenter, InkQuiet);
        Place(info.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -754f), new Vector2(760f, 96f));

        ok = UIKit.Button(cr, "확인", Close, KucaTheme.Primary, KucaTheme.Body);
        Image oi = ok.GetComponent<Image>();
        oi.sprite = HudIcons.Pill;
        oi.type = Image.Type.Sliced;
        oi.pixelsPerUnitMultiplier = 120f / 110f;
        ok.GetComponentInChildren<Text>().fontStyle = FontStyle.Bold;
        Place((RectTransform)ok.transform, new Vector2(0.5f, 0f), new Vector2(0f, 50f), new Vector2(720f, 110f));

        flash = UIKit.Image(root.transform, "Flash", new Color(1f, 1f, 1f, 0f));
        UIKit.Stretch(flash.rectTransform);
        root.SetActive(false);
    }

    /// <summary>카드 위쪽 가운데 기준, 중심이 위에서 -y 인 정사각형 (돌릴 때 제자리에서 돈다)</summary>
    static void Center(RectTransform rt, float y, float size)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0f, y);
        rt.sizeDelta = new Vector2(size, size);
    }

    static void Place(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }
}
