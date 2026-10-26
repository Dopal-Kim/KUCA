using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// v2 (A-V3): 만남 스팟 10곳의 지붕 위, 경희스팟 기둥 옆에 그 장소의 경희펫이 서 있다 (MeetSpotCatalog).
/// 야생 출현이 꺼진 지도에서 "여기서 만나요" 표지가 된다. 탭·잡기는 없다 (충돌체 없음).
/// CollectController 가 붙인다.
/// </summary>
public class MeetSpotFigures : MonoBehaviour
{
    /// <summary>
    /// 지도에 만남 스팟 경희펫을 세울지. 10/24: 지도에는 경희펫을 두지 않고 뽑기로만 얻게 해서 꺼 둔다 (B 요청).
    /// 매칭 화면은 여전히 스팟 이름과 그 경희펫 그림을 보여 준다. 다시 세우려면 true.
    /// </summary>
    public static bool ShowOnMap = false;

    const float Scale = 1.15f;       // 지도 경희펫과 같은 크기 (받침 포함 약 10 m)
    const float SideOffset = 16f;    // 기둥에서 옆으로 (배지와 겹치지 않게)
    const int RimStage = 1;          // 1단계 진화 테두리 빛 → 다른 지도 장식과 구별

    readonly List<(Transform figure, float phase)> figures = new List<(Transform, float)>();
    Camera cam;

    IEnumerator Start()
    {
        // 경희스팟은 KyungHeeSpots.Start 에서 만들어진다 → 모든 Start 가 끝난 다음 프레임에 찾는다
        yield return null;
        if (!ShowOnMap)
            yield break;
        var spots = FindAnyObjectByType<KyungHeeSpots>();
        if (spots == null)
            yield break;
        foreach (KyungHeeSpot spot in spots.Spots)
        {
            string species = spot != null && spot.Building != null ? MeetSpotCatalog.SpeciesAt(spot.Building.buildingId) : null;
            if (species == null || !CreatureLibrary.TryGetMesh(species, out Mesh mesh))
                continue;
            var go = new GameObject("MeetSpot_" + species, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(spot.transform, false);
            // 지붕이 좁으면 가장자리 밖으로 나가지 않게
            go.transform.localPosition = new Vector3(Mathf.Min(SideOffset, spot.BuildingBounds.extents.x * 0.7f), 0f, 0f);
            go.transform.localScale = Vector3.one * Scale;
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            go.GetComponent<MeshRenderer>().sharedMaterial = CreatureLibrary.Material;
            if (CreatureLibrary.TryGetGlass(species, out Mesh glass) && CreatureLibrary.GlassMaterial != null)
            {
                var g = new GameObject("Glass", typeof(MeshFilter), typeof(MeshRenderer));
                g.transform.SetParent(go.transform, false);
                g.GetComponent<MeshFilter>().sharedMesh = glass;
                var gr = g.GetComponent<MeshRenderer>();
                gr.sharedMaterial = CreatureLibrary.GlassMaterial;
                gr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            PartnerBuddy.ApplyStage(go.transform, RimStage);
            figures.Add((go.transform, Random.value * Mathf.PI * 2f));
        }
    }

    void Update()
    {
        if (figures.Count == 0)
            return;
        if (cam == null) cam = Camera.main;   // 매 프레임 찾으면 비싸다
        float camYaw = cam != null ? cam.transform.eulerAngles.y + 180f : 0f;
        foreach ((Transform f, float phase) in figures)
        {
            if (f == null)
                continue;
            // 카메라를 보며 천천히 몸을 흔든다 (뛰지 않음 → 지도 위 장식)
            f.rotation = Quaternion.Euler(0f, camYaw + 10f * Mathf.Sin(Time.time * 0.8f + phase), 0f);
        }
    }
}
