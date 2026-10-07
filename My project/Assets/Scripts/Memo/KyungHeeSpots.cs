using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 지정한 캠퍼스 건물마다 경희스팟을 세우고, Player가 가까이 있는 스팟을 표시한다.
/// 스팟 목록은 인스펙터의 Building Ids 에서 고칠 수 있다 (docs/api.md 의 건물 ID).
/// </summary>
public class KyungHeeSpots : MonoBehaviour
{
    public CampusMap campusMap;
    public Transform buildingsRoot;
    public Material badgeMaterial;
    public Material poleMaterial;
    [Tooltip("배지 뒤 후광 (KUCA/SpotGlow). 비우면 후광 없음. Apply Key Art 가 채운다")]
    public Material glowMaterial;

    [Tooltip("이 거리(m) 안에 들어오면 스팟이 돌고 메모를 남길 수 있다 (건물 외곽 기준)")]
    public float interactRadius = 40f;

    [Tooltip("경희스팟을 세울 건물 ID")]
    public List<string> buildingIds = new List<string>
    {
        "way-473963422",   // 경희대학교 국제캠퍼스 정문
        "way-474085534",   // 중앙도서관
        "way-474085535",   // 학생회관
        "way-455725718",   // 공학관
        "way-455728922",   // 공학실험동
        "way-474085539",   // 국제대학
        "way-474085542",   // 국제학관
        "way-474085538",   // 글로벌관
        "way-474531805",   // 노천극장
        "way-474085537",   // 멀티미디어교육관
        "way-474521121",   // 생명과학대학
        "way-585696506",   // 선승관
        "way-585696420",   // 실습농장동
        "way-778946313",   // 실험연구동
        "way-474085536",   // 예술디자인대학
        "way-474521123",   // 예술디자인대학 도예관
        "way-585696507",   // 외국어대학관
        "relation-8269760", // 우정원
        "way-474521122",   // 원예생물공학온실
        "way-455728923",   // 원자로센터
        "way-474085540",   // 전자정보대학
        "relation-6739958", // 제2기숙사 남자동
        "relation-6739964", // 제2기숙사(여자동)
        "way-474521125",   // 천문대
        "way-455726113",   // 체육대학관
    };

    public IReadOnlyList<KyungHeeSpot> Spots => spots;

    readonly List<KyungHeeSpot> spots = new List<KyungHeeSpot>();

    void Start()
    {
        if (buildingsRoot == null)
            return;
        var wanted = new HashSet<string>(buildingIds);
        var done = new HashSet<string>();
        foreach (CampusBuildingInfo info in buildingsRoot.GetComponentsInChildren<CampusBuildingInfo>())
        {
            // 여러 조각으로 된 건물(relation)은 첫 조각에만 세운다
            if (!wanted.Contains(info.buildingId) || !done.Add(info.buildingId))
                continue;
            var r = info.GetComponent<Renderer>();
            if (r == null)
                continue;
            spots.Add(KyungHeeSpot.Create(info, r.bounds, badgeMaterial, poleMaterial, transform, glowMaterial));
        }
    }

    void Update()
    {
        if (campusMap == null || campusMap.player == null)
            return;
        Vector3 p = campusMap.player.position;
        foreach (KyungHeeSpot s in spots)
            s.SetInRange(s.DistanceFrom(p) <= interactRadius);
    }
}
