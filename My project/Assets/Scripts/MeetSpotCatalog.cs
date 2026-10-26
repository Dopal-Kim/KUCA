using System.Collections.Generic;

/// <summary>
/// A-V3 산책 매칭 콘텐츠: 만남 스팟 10곳과 그 앞에 서 있는 경희펫, 스몰토크 예시 20개.
/// - 만남 스팟: 경희스팟 중 찾기 쉽고 공개된 곳 (기숙사·천문대처럼 들어가기 어렵거나 먼 곳은 뺐다). 경희펫은 그 장소의 랜드마크 동물.
/// - 서버(C)는 같은 값을 server/KucaMemoServer/Data/spots.json 의 meetSpecies 와 Data/smalltalk.json 에서 읽는다 → 바꿀 때 두 곳을 같이.
/// </summary>
public static class MeetSpotCatalog
{
    /// <summary>만남 스팟 건물 id → 그 앞에 서 있는 경희펫 (CreatureLibrary id)</summary>
    public static readonly Dictionary<string, string> SpeciesByBuilding = new Dictionary<string, string>
    {
        { "way-473963422", "gate_magpie" },          // 정문
        { "way-474085534", "library_owl" },          // 중앙도서관
        { "way-474085535", "pigeon" },               // 학생회관 (학식 비둘기)
        { "way-455725718", "space_rabbit" },         // 공학관
        { "way-474531805", "amphitheater_frog" },    // 노천극장
        { "way-585696506", "clock_rooster" },        // 선승관
        { "way-585696507", "language_parrot" },      // 외국어대학관
        { "way-455726113", "lion_cub" },             // 체육대학관
        { "way-474085537", "multimedia_meerkat" },   // 멀티미디어교육관
        { "way-474085536", "art_chameleon" },        // 예술디자인대학
    };

    public static bool IsMeetSpot(string buildingId) => buildingId != null && SpeciesByBuilding.ContainsKey(buildingId);

    /// <summary>그 만남 스팟의 경희펫 (만남 스팟이 아니면 null)</summary>
    public static string SpeciesAt(string buildingId) =>
        buildingId != null && SpeciesByBuilding.TryGetValue(buildingId, out string s) ? s : null;

    /// <summary>스몰토크 예시 20개 (공강 · 학식 · 전공 · 캠퍼스 장소 · 계절 각 4개). AI 가 실패하면 이 안에서 꺼낸다</summary>
    public static readonly string[] SmallTalk =
    {
        // 공강
        "공강 시간에 주로 어디서 시간 보내요?",
        "오늘 공강은 얼마나 길어요? 끝나고 수업 있어요?",
        "공강에 해 본 것 중에 제일 알찼던 건 뭐예요?",
        "캠퍼스에서 잠깐 쉬기 좋은 곳 알아요?",
        // 학식
        "학식 최애 메뉴 있어요?",
        "학생회관 말고 자주 가는 식당 있어요?",
        "캠퍼스 근처에 혼밥하기 좋은 곳 추천해 줄래요?",
        "오늘 점심 뭐 먹었어요? 맛있었어요?",
        // 전공
        "무슨 과예요? 과 건물은 어디예요?",
        "이번 학기 제일 재밌는 수업은 뭐예요?",
        "우리 과 사람만 아는 꿀팁 하나 알려 줄래요?",
        "과제할 때 주로 가는 자리 있어요?",
        // 캠퍼스 장소
        "국제캠에서 제일 좋아하는 장소는 어디예요?",
        "아직 한 번도 안 가 본 건물 있어요?",
        "노천극장이나 천문대 가 본 적 있어요?",
        "캠퍼스에서 사진 찍기 제일 좋은 곳은 어디 같아요?",
        // 계절
        "국제캠 사계절 중에 언제가 제일 좋아요?",
        "벚꽃 필 때 캠퍼스 어디가 제일 예뻐요?",
        "요즘 날씨에 걷기 좋은 길 알아요?",
        "이번 방학엔 뭐 할 계획이에요?",
    };
}
