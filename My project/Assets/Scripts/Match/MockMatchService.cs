using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 기기 안 가상 상대 (B-V3). 서버에 매칭 API 가 아직 없을 때 (404) · 서버 없는 모드에서 같은 흐름을 보여 준다.
/// 대기열에 들어가면 몇 초 뒤 가상 상대를 제안하고, 수락하면 곧 상대도 수락한다. 인상착의 · 스몰토크도 가짜로 채운다.
/// 시연용 가상 상대 (C-V3) 가 서버에 생기면 그쪽이 대신한다.
/// </summary>
public class MockMatchService : IMatchService
{
    public const float ProposeAfter = 4f, PartnerAcceptsAfter = 2f, PartnerLookAfter = 3f;
    public const int WaitSeconds = 300, DecideSeconds = 30, NoShowSeconds = 600;

    static readonly string[] Nicknames = { "산책하는 다람쥐", "공강러 원숭이", "노을 보는 여우", "학식 탐험가", "느긋한 거북이" };
    static readonly string[] Looks = { "회색 맨투맨, 남색 백팩", "흰 셔츠, 검은 에코백", "베이지 코트, 운동화", "초록 후드티, 안경" };
    // 스몰토크 예시 20개 (A-V3, MeetSpotCatalog). 처음 3개는 서로 다른 주제에서 하나씩
    static string[] Topics => MeetSpotCatalog.SmallTalk;

    readonly Func<MeetSpot> nearestSpot;
    readonly MatchState state = new MatchState();
    float joinedAt, acceptedAt, matchedAt;
    int topic;
    List<string> myTastes = new List<string>();

    readonly Func<float> clock;

    /// <param name="clock">지금 시각 (초). 테스트에서 바꿔 넣는다. 없으면 Time.realtimeSinceStartup</param>
    public MockMatchService(Func<MeetSpot> nearestSpot, Func<float> clock = null)
    {
        this.nearestSpot = nearestSpot;
        this.clock = clock;
    }

    public string Description => "가상 상대";

    float Now => clock != null ? clock() : Time.realtimeSinceStartup;

    public IEnumerator Join(MatchQueueRequest request, Action<MatchState, string> onDone)
    {
        yield return null;
        if (state.Active)
        {
            onDone(Copy(), "이미 매칭 중이에요");
            yield break;
        }
        Reset();
        state.status = MatchState.Waiting;
        state.minutes = request.minutes;
        state.matchId = "mock_" + Guid.NewGuid().ToString("N").Substring(0, 8);
        myTastes = new List<string>(request.tastes ?? new List<string>());
        joinedAt = Now;
        onDone(Tick(), null);
    }

    public IEnumerator Leave(Action<MatchState, string> onDone)
    {
        yield return null;
        Reset();
        onDone(Copy(), null);
    }

    public IEnumerator Status(Action<MatchState, string> onDone)
    {
        yield return null;
        onDone(Tick(), null);
    }

    public IEnumerator Respond(string matchId, bool accept, Action<MatchState, string> onDone)
    {
        yield return null;
        if (state.status != MatchState.Proposed)
        {
            onDone(Tick(), "제안이 이미 끝났어요");
            yield break;
        }
        if (accept)
        {
            state.status = MatchState.Accepted;
            acceptedAt = Now;
        }
        else
        {
            Reset();
            state.status = MatchState.Cancelled;
            state.message = "제안을 거절했어요";
        }
        onDone(Tick(), null);
    }

    public IEnumerator SetLook(string matchId, string look, Action<MatchState, string> onDone)
    {
        yield return null;
        state.myLook = look;
        onDone(Tick(), null);
    }

    public IEnumerator NextTopic(string matchId, Action<string, string> onDone)
    {
        yield return null;
        topic = (topic + 1) % Topics.Length;
        onDone(Topics[topic], null);
    }

    public IEnumerator StartWalk(string matchId, Action<MatchState, string> onDone)
    {
        yield return null;
        if (state.status == MatchState.Matched)
            state.status = MatchState.Walking;
        onDone(Tick(), null);
    }

    public IEnumerator End(string matchId, Action<MatchState, string> onDone)
    {
        yield return null;
        int minutes = state.minutes;
        string nick = state.partner?.nickname;
        Reset();
        state.status = MatchState.Ended;
        state.minutes = minutes;
        state.partner = new MatchPartner { nickname = nick };
        onDone(Copy(), null);
    }

    public IEnumerator Cancel(string matchId, string reason, Action<MatchState, string> onDone)
    {
        yield return null;
        Reset();
        state.status = MatchState.Cancelled;
        state.message = "매칭을 취소했어요";
        onDone(Copy(), null);
    }

    public IEnumerator Report(string matchId, string reason, string detail, bool block, Action<MatchState, string> onDone)
    {
        yield return null;
        Debug.Log($"[Match] 가상 상대 신고: {reason} block={block} {detail}");
        Reset();
        state.status = MatchState.Cancelled;
        state.message = block ? "신고하고 차단했어요. 다시 매칭되지 않아요" : "신고했어요. 운영진이 확인할게요";
        onDone(Copy(), null);
    }

    /// <summary>시간이 지나면서 바뀌는 가상 상대의 행동</summary>
    MatchState Tick()
    {
        float now = Now;
        switch (state.status)
        {
            case MatchState.Waiting:
                state.waitSecondsLeft = Mathf.Max(0, WaitSeconds - Mathf.FloorToInt(now - joinedAt));
                if (now - joinedAt >= ProposeAfter)
                {
                    state.status = MatchState.Proposed;
                    var rng = new System.Random(state.matchId.GetHashCode());
                    var common = new List<string>(myTastes);
                    if (common.Count == 0) common.Add("quiet");
                    if (common.Count > 2) common.RemoveRange(2, common.Count - 2);
                    state.partner = new MatchPartner
                    {
                        nickname = Nicknames[rng.Next(Nicknames.Length)],
                        commonTastes = common,
                        talkStyle = "some",
                    };
                    joinedAt = now; // 여기부터 결정 시간
                }
                break;
            case MatchState.Proposed:
                state.decideSecondsLeft = Mathf.Max(0, DecideSeconds - Mathf.FloorToInt(now - joinedAt));
                if (state.decideSecondsLeft == 0)
                {
                    Reset();
                    state.status = MatchState.Expired;
                    state.message = "제안 시간이 지났어요";
                }
                break;
            case MatchState.Accepted:
                if (now - acceptedAt >= PartnerAcceptsAfter)
                {
                    state.status = MatchState.Matched;
                    state.partnerAccepted = true;
                    state.spot = nearestSpot?.Invoke() ?? new MeetSpot { name = "중앙도서관 앞" };
                    var rng2 = new System.Random(state.matchId.GetHashCode());
                    state.smalltalk = new List<string> { Topics[rng2.Next(4)], Topics[4 + rng2.Next(4)], Topics[12 + rng2.Next(4)] };
                    matchedAt = now;
                }
                break;
            case MatchState.Matched:
                state.noShowSecondsLeft = Mathf.Max(0, NoShowSeconds - Mathf.FloorToInt(now - matchedAt));
                if (string.IsNullOrEmpty(state.partnerLook) && now - matchedAt >= PartnerLookAfter)
                    state.partnerLook = Looks[Mathf.Abs(state.matchId.GetHashCode()) % Looks.Length];
                if (state.noShowSecondsLeft == 0)
                {
                    Reset();
                    state.status = MatchState.Cancelled;
                    state.message = "10분 안에 만나지 못해 자동으로 취소됐어요";
                }
                break;
        }
        return Copy();
    }

    void Reset()
    {
        state.status = MatchState.Idle;
        state.matchId = null;
        state.partner = null;
        state.partnerAccepted = false;
        state.spot = null;
        state.smalltalk = new List<string>();
        state.myLook = state.partnerLook = null; // 인상착의는 끝나면 지운다
        state.message = "";
        state.waitSecondsLeft = state.decideSecondsLeft = state.noShowSecondsLeft = 0;
        topic = 15;
    }

    MatchState Copy() => JsonUtility.FromJson<MatchState>(JsonUtility.ToJson(state));
}
