using System;
using System.Collections;
using System.Collections.Generic;

// 실시간 산책 매칭 (B-V3). docs/api_v2_매칭_초안.md 의 MatchState 와 같은 모양 (JsonUtility 로 그대로 읽는다).

[Serializable]
public class MatchQueueRequest
{
    /// <summary>30 · 60</summary>
    public int minutes = 30;
    /// <summary>true 면 동성만 (내 성별은 서버 계정에 있음)</summary>
    public bool sameGender;
    /// <summary>지금 있는 건물 ID (GPS 좌표는 보내지 않는다)</summary>
    public string buildingId;
    public List<string> tastes = new List<string>();
}

[Serializable]
public class MatchPartner
{
    public string nickname;
    public List<string> commonTastes = new List<string>();
    /// <summary>chatty · some · quiet</summary>
    public string talkStyle;
}

[Serializable]
public class MeetSpot
{
    public string buildingId;
    public string name;
    public double lat, lon;
    /// <summary>그 스팟에 서 있는 경희몬 (CreatureLibrary id)</summary>
    public string species;
}

[Serializable]
public class MatchState
{
    public const string Idle = "idle", Waiting = "waiting", Proposed = "proposed", Accepted = "accepted",
        Matched = "matched", Walking = "walking", Ended = "ended", Cancelled = "cancelled", Expired = "expired";

    public string status = Idle;
    public string matchId;
    public int waitSecondsLeft;
    public int decideSecondsLeft;
    public MatchPartner partner;
    public bool partnerAccepted;
    public MeetSpot spot;
    public List<string> smalltalk = new List<string>();
    public string myLook;
    public string partnerLook;
    public int noShowSecondsLeft;
    public int minutes;
    public string message;

    /// <summary>대기 · 제안 · 만남 · 산책 중 (화면을 닫아도 계속 지켜볼 상태)</summary>
    public bool Active => status == Waiting || status == Proposed || status == Accepted || status == Matched || status == Walking;
}

/// <summary>
/// 매칭 서버. HTTP 구현(HttpMatchService)과 기기 안 가상 상대(MockMatchService)가 같은 모양을 따른다 (Memo 의 Mock/Http 와 같은 구조).
/// 모든 호출은 코루틴이고, 끝나면 onDone(상태, 에러 메시지). 에러가 없으면 null.
/// </summary>
public interface IMatchService
{
    /// <summary>화면에 작게 보여 줄 이름 (예: "가상 상대")</summary>
    string Description { get; }
    IEnumerator Join(MatchQueueRequest request, Action<MatchState, string> onDone);
    IEnumerator Leave(Action<MatchState, string> onDone);
    IEnumerator Status(Action<MatchState, string> onDone);
    IEnumerator Respond(string matchId, bool accept, Action<MatchState, string> onDone);
    IEnumerator SetLook(string matchId, string look, Action<MatchState, string> onDone);
    /// <summary>다음 스몰토크 주제. onDone(주제, 에러)</summary>
    IEnumerator NextTopic(string matchId, Action<string, string> onDone);
    IEnumerator StartWalk(string matchId, Action<MatchState, string> onDone);
    /// <summary>산책 끝내기 → ended (완료 · 리뷰 · 보상은 B-V4 / C-V4)</summary>
    IEnumerator End(string matchId, Action<MatchState, string> onDone);
    IEnumerator Cancel(string matchId, string reason, Action<MatchState, string> onDone);
    /// <summary>reason: rude · noshow · personal · other</summary>
    IEnumerator Report(string matchId, string reason, string detail, bool block, Action<MatchState, string> onDone);
}
