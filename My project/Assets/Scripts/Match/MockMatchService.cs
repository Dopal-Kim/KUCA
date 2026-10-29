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
    // A 의 예시는 4개씩 공강 · 학식 · 전공 · 캠퍼스 장소 · 계절 순서 (Data/smalltalk.json 과 같음)
    static readonly string[] TopicNames = { "공강", "학식", "전공", "캠퍼스 장소", "계절" };

    /// <summary>가상 상대가 보여 줄 코스 후보 (만남 스팟에서 출발). "다른 코스" 를 누르면 다음 묶음</summary>
    static readonly CourseStop[][] Courses =
    {
        new[] { Stop("중앙도서관", "열람실 창가에서 잠깐 쉬어 가요", 4, 300), Stop("노천극장", "계단에 앉아 이야기하기 좋아요", 6, 450), Stop("외국어대학관", "여러 나라 말이 들리는 복도를 지나요", 5, 380), Stop("학생회관", "따뜻한 음료로 마무리해요", 5, 360) },
        new[] { Stop("체육대학관", "운동장 옆길을 따라 걸어요", 5, 400), Stop("예술디자인대학", "걸려 있는 작품을 구경해요", 6, 470), Stop("멀티미디어교육관", "언덕길에서 캠퍼스를 내려다봐요", 6, 450) },
        new[] { Stop("정문", "캠퍼스 입구의 큰 길을 걸어요", 7, 520), Stop("선승관", "비가 와도 실내로 이어져요", 5, 380), Stop("공학관", "건물 사이 긴 길을 이어 걸어요", 6, 430) },
    };
    public const int CourseChanges = 3;

    static CourseStop Stop(string name, string reason, int minutes, int meters) =>
        new CourseStop { buildingId = "mock-" + name, name = name, reason = reason, walkMinutes = minutes, distanceM = meters };

    readonly Func<MeetSpot> nearestSpot;
    readonly MatchState state = new MatchState();
    float joinedAt, acceptedAt, matchedAt, courseAt;
    int topic, courseIndex;
    string destId = "", destName = "";
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

    public IEnumerator NextTopic(string matchId, Action<TopicCard, string> onDone)
    {
        yield return null;
        topic = (topic + 1) % Topics.Length;
        onDone(Card(topic), null);
    }

    static TopicCard Card(int index) =>
        new TopicCard { text = Topics[index], topic = TopicNames[Mathf.Clamp(index / 4, 0, TopicNames.Length - 1)], emoji = "" };

    public IEnumerator AgreeCourse(string matchId, Action<MatchState, string> onDone)
    {
        yield return null;
        if (!state.HasCourse)
        {
            onDone(Tick(), "코스를 준비하고 있어요. 잠시 뒤에 다시 눌러 주세요.");
            yield break;
        }
        state.course.myAgree = true;
        state.course.confirmed = state.course.partnerAgree;
        onDone(Tick(), null);
    }

    public IEnumerator NextCourse(string matchId, Action<MatchState, string> onDone)
    {
        yield return null;
        if (!state.HasCourse || state.course.confirmed)
        {
            onDone(Tick(), "두 사람이 이미 정한 코스예요.");
            yield break;
        }
        if (state.course.changesLeft <= 0)
        {
            onDone(Tick(), $"다른 코스는 {CourseChanges}번까지 볼 수 있어요. 지금 코스 중에서 골라 주세요.");
            yield break;
        }
        int left = state.course.changesLeft - 1;
        courseIndex = (courseIndex + 1) % Courses.Length;
        state.course = MakeCourse(left);
        courseAt = Now;
        onDone(Tick(), null);
    }

    public IEnumerator SetDestination(string matchId, string buildingId, string name, Action<MatchState, string> onDone)
    {
        yield return null;
        if (!state.HasCourse || state.course.confirmed)
        {
            onDone(Tick(), "두 사람이 이미 정한 코스예요.");
            yield break;
        }
        destId = buildingId ?? "";
        destName = destId.Length > 0 ? name ?? "" : "";
        state.course = MakeCourse(state.course.changesLeft);   // 가상 상대는 바로 다시 동의한다
        courseAt = Now;
        onDone(Tick(), null);
    }

    MatchCourse MakeCourse(int changesLeft)
    {
        var stops = new List<CourseStop>(Courses[courseIndex]);
        stops.RemoveAll(s => s.name == destName);
        if (destId.Length > 0)
            stops.Add(new CourseStop { buildingId = destId, name = destName, reason = "두 사람이 고른 도착지예요", walkMinutes = 5, distanceM = 380 });
        int total = 0, meters = 0;
        foreach (CourseStop s in stops) { total += s.walkMinutes; meters += s.distanceM; }
        return new MatchCourse
        {
            recommendationId = "mock_rec_" + courseIndex,
            title = $"함께 걷는 {state.minutes}분 코스",
            source = "fallback",
            stops = stops,
            totalMinutes = total,
            totalDistanceM = meters,
            changesLeft = changesLeft,
            destinationId = destId,
        };
    }

    public IEnumerator StartWalk(string matchId, Action<MatchState, string> onDone)
    {
        yield return null;
        if (state.status == MatchState.Matched)
        {
            state.status = MatchState.Walking;
            if (state.HasCourse) state.course.confirmed = true;   // 만나서 출발하면 지금 코스로 정해진다 (서버와 같게)
        }
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
                state.nowSearching = 1 + Mathf.FloorToInt(now - joinedAt) % 3;   // 실시간 느낌만 내는 가짜 숫자
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
                        whyMatched = "둘 다 가끔 이야기하며 걷는 걸 좋아해서 연결했어요",
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
                    int[] picks = { rng2.Next(4), 4 + rng2.Next(4), 12 + rng2.Next(4) };
                    state.smalltalk = new List<string>();
                    state.cards = new List<TopicCard>();
                    foreach (int i in picks) { state.smalltalk.Add(Topics[i]); state.cards.Add(Card(i)); }
                    courseIndex = 0;
                    destId = destName = "";
                    state.course = MakeCourse(CourseChanges);
                    matchedAt = courseAt = now;
                }
                break;
            case MatchState.Matched:
                state.noShowSecondsLeft = Mathf.Max(0, NoShowSeconds - Mathf.FloorToInt(now - matchedAt));
                if (string.IsNullOrEmpty(state.partnerLook) && now - matchedAt >= PartnerLookAfter)
                    state.partnerLook = Looks[Mathf.Abs(state.matchId.GetHashCode()) % Looks.Length];
                // 가상 상대는 코스를 받고 조금 뒤 동의한다 (내가 먼저 눌렀으면 그때 확정)
                if (state.HasCourse && !state.course.partnerAgree && now - courseAt >= PartnerLookAfter)
                {
                    state.course.partnerAgree = true;
                    state.course.confirmed = state.course.myAgree;
                }
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
        state.cards = new List<TopicCard>();
        state.course = null;
        state.nowSearching = -1;
        state.myLook = state.partnerLook = null; // 인상착의는 끝나면 지운다
        state.message = "";
        state.waitSecondsLeft = state.decideSecondsLeft = state.noShowSecondsLeft = 0;
        topic = 15;
    }

    MatchState Copy()
    {
        MatchState c = JsonUtility.FromJson<MatchState>(JsonUtility.ToJson(state));
        if (!state.HasCourse) c.course = null;   // JsonUtility 는 없는 코스도 빈 객체로 만든다
        return c;
    }
}
