using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 산책하는 동안 (WalkSession.Active: 코스 · 산책메이트 · 자유 산책) 플레이어가 지나간 길을 기록한다.
/// 0.5초마다 보고 4 m 이상 움직였을 때만 점을 더한다. 끝난 산책의 길은 WalkSession.Finish 가 가져간다.
/// </summary>
public class WalkTrack : MonoBehaviour
{
    const float Interval = 0.5f, MinStep = 4f;
    const float MaxJump = 120f;   // 시작 직후 이보다 멀리 튀면 그 전 점은 버린다 (그 뒤의 튐은 그냥 잇는다)
    const int MaxPoints = 4000;

    static WalkTrack instance;
    static readonly List<Vector3> points = new List<Vector3>();
    static int recording = -1;   // 지금 기록 중인 WalkSession.Id

    CollectController collect;
    float next;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install()
    {
        if (instance != null)
            return;
        var go = new GameObject("WalkTrack");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<WalkTrack>();
    }

    /// <summary>session 산책에서 기록한 길 (복사본). 다른 산책이면 빈 목록</summary>
    public static List<Vector3> Points(int session)
    {
        var copy = new List<Vector3>();
        if (session == recording)
            copy.AddRange(points);
        if (instance != null && session == recording && instance.Player(out Vector3 now))
            Add(copy, now);   // 끝낸 자리까지
        return copy;
    }

    /// <summary>지금 산책의 길 (걷는 중 지도에 그릴 때)</summary>
    public static List<Vector3> Current => WalkSession.Active ? Points(WalkSession.Id) : new List<Vector3>();

    bool Player(out Vector3 pos)
    {
        if (collect == null)
            collect = FindAnyObjectByType<CollectController>();
        Transform p = collect != null && collect.campusMap != null ? collect.campusMap.player : null;
        pos = p != null ? p.position : Vector3.zero;
        return p != null;
    }

    void Update()
    {
        if (!WalkSession.Active || Time.unscaledTime < next)
            return;
        next = Time.unscaledTime + Interval;
        if (recording != WalkSession.Id)
        {
            recording = WalkSession.Id;
            points.Clear();
        }
        if (Player(out Vector3 pos) && points.Count < MaxPoints)
            Add(points, pos);
    }

    static void Add(List<Vector3> list, Vector3 p)
    {
        if (list.Count > 0)
        {
            Vector3 last = list[list.Count - 1];
            float d = new Vector2(p.x - last.x, p.z - last.z).magnitude;
            if (d < MinStep)
                return;
            if (d > MaxJump && list.Count < 3)
                list.Clear();   // 걷기 시작하자마자 멀리 튀었으면 (가상 산책 켜기 · 첫 위치 잡기) 새 자리에서 다시 시작
        }
        list.Add(p);
    }

    /// <summary>점을 최대 max 개로 줄인다 (처음 · 끝은 남김). 기록 저장용</summary>
    public static List<Vector3> Thin(List<Vector3> line, int max)
    {
        if (line == null || line.Count <= max || max < 2)
            return line != null ? new List<Vector3>(line) : new List<Vector3>();
        var r = new List<Vector3>(max);
        float step = (line.Count - 1) / (float)(max - 1);
        for (int i = 0; i < max; i++)
            r.Add(line[Mathf.Min(line.Count - 1, Mathf.RoundToInt(i * step))]);
        return r;
    }
}
