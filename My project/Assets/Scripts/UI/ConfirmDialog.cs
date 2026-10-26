using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 되돌릴 수 없는 동작 앞의 확인 창 (산책 끝내기 · 로그아웃 등). 화면 가운데 카드에 제목 · 설명 · 버튼 두 개.
/// ConfirmDialog.Show("산책을 끝낼까요?", "지금까지 820m", "끝내기", () => ...);
/// </summary>
public class ConfirmDialog : MonoBehaviour
{
    static readonly Color Ink = new Color(0.11f, 0.11f, 0.12f);
    static readonly Color InkQuiet = new Color(0.11f, 0.11f, 0.12f, 0.6f);
    static readonly Color Soft = new Color(0.94f, 0.95f, 0.96f);
    static readonly Color Danger = new Color(0.86f, 0.24f, 0.26f);

    static ConfirmDialog instance;
    GameObject root;
    Text title, message;
    Button confirm, cancel;
    System.Action onConfirm;

    /// <param name="danger">확인 버튼을 빨간색으로 (지우기 · 로그아웃)</param>
    public static void Show(string title, string message, string confirmLabel, System.Action onConfirm,
                            string cancelLabel = "취소", bool danger = false)
    {
        if (instance == null)
            instance = new GameObject("ConfirmDialog").AddComponent<ConfirmDialog>();
        instance.Open(title, message, confirmLabel, cancelLabel, danger, onConfirm);
    }

    void Awake() => Build();

    void Open(string t, string m, string ok, string no, bool danger, System.Action action)
    {
        onConfirm = action;
        title.text = t;
        message.text = m ?? "";
        message.gameObject.SetActive(!string.IsNullOrEmpty(m));
        UIKit.SetLabel(confirm, ok);
        UIKit.SetLabel(cancel, no);
        confirm.GetComponent<Image>().color = danger ? Danger : Ink;
        root.SetActive(true);
        UIInputBlocker.SetModal(this, true);
    }

    void Close(bool ok)
    {
        root.SetActive(false);
        UIInputBlocker.SetModal(this, false);
        System.Action a = onConfirm;
        onConfirm = null;
        if (ok) a?.Invoke();
    }

    void OnDisable() => UIInputBlocker.SetModal(this, false);

    void Build()
    {
        Canvas canvas = UIKit.CreateCanvas(transform, "ConfirmCanvas", 70);
        root = UIKit.Image(canvas.transform, "Dim", new Color(0f, 0f, 0f, 0.45f), raycast: true).gameObject;
        UIKit.Stretch((RectTransform)root.transform);
        root.AddComponent<Button>().onClick.AddListener(() => Close(false));   // 바깥을 누르면 취소

        Image card = UIKit.Image(root.transform, "Card", Color.white, raycast: true);
        card.sprite = HudIcons.Rounded;
        card.type = Image.Type.Sliced;
        card.pixelsPerUnitMultiplier = 0.5f;
        card.gameObject.AddComponent<Button>().transition = Selectable.Transition.None;   // 카드 안을 눌러도 닫히지 않게
        RectTransform cr = card.rectTransform;
        cr.anchorMin = cr.anchorMax = cr.pivot = new Vector2(0.5f, 0.5f);
        cr.sizeDelta = new Vector2(860f, 0f);
        var v = UIKit.Vertical(card, 26f, new RectOffset(60, 60, 64, 56));
        v.childAlignment = TextAnchor.UpperCenter;
        card.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        title = UIKit.Text(card.transform, "", 44, TextAnchor.MiddleCenter, Ink);
        title.fontStyle = FontStyle.Bold;
        message = UIKit.Text(card.transform, "", 32, TextAnchor.MiddleCenter, InkQuiet);
        message.horizontalOverflow = HorizontalWrapMode.Wrap;

        RectTransform row = UIKit.Rect(card.transform, "Buttons");
        UIKit.Size(row, 116f);
        var h = UIKit.Horizontal(row, 24f);
        h.childForceExpandWidth = true;
        h.childControlWidth = true;
        cancel = Pill(row, "취소", Soft, Ink, () => Close(false));
        confirm = Pill(row, "확인", Ink, Color.white, () => Close(true));
        root.SetActive(false);
    }

    static Button Pill(Transform parent, string label, Color face, Color ink, UnityEngine.Events.UnityAction onClick)
    {
        Button b = UIKit.Button(parent, label, onClick, face, 36);
        Image img = b.GetComponent<Image>();
        img.sprite = HudIcons.Pill;
        img.type = Image.Type.Sliced;
        img.pixelsPerUnitMultiplier = 120f / 116f;
        Text t = b.GetComponentInChildren<Text>();
        t.color = ink;
        t.fontStyle = FontStyle.Bold;
        b.gameObject.AddComponent<PressScale>();
        return b;
    }
}
