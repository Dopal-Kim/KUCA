using UnityEngine;

/// <summary>
/// 3D 건물 하나의 ID와 이름. ID는 메모 서버와 같은 "way-455725718" 형식(OSM 종류-번호)이다.
/// </summary>
public class CampusBuildingInfo : MonoBehaviour
{
    [Tooltip("OSM 종류-번호 (예: way-474085534). docs/api.md 의 건물 ID")]
    public string buildingId;
    [Tooltip("OSM 건물 이름. 없으면 비어 있음")]
    public string buildingName;

    /// <summary>화면에 보여 줄 이름. 이름이 없는 건물은 "이름 없는 건물"</summary>
    public string DisplayName => string.IsNullOrEmpty(buildingName) ? "이름 없는 건물" : buildingName;
}
