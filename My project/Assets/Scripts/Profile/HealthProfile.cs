using UnityEngine;

/// <summary>
/// 몸무게 (선택). 취향 체크리스트 마지막 단계에서 받는다.
/// 건강 정보라서 이 기기(PlayerPrefs)에만 저장하고, 산책 추천 · 산책 완료 요청에 그때그때 실어 보낸다 (서버는 저장하지 않음).
/// 값이 없으면 서버가 칼로리를 50~70kg 범위로 보여 준다. 취향 다시 고르기에서 비우면 지워진다.
/// </summary>
public static class HealthProfile
{
    const string Key = "health_weight_kg";
    public const int MinKg = 30, MaxKg = 200;

    /// <summary>저장된 몸무게 (kg). 없으면 null</summary>
    public static int? WeightKg
    {
        get
        {
            int v = PlayerPrefs.GetInt(Key, 0);
            return v >= MinKg && v <= MaxKg ? v : (int?)null;
        }
        set
        {
            if (value is int kg && kg >= MinKg && kg <= MaxKg) PlayerPrefs.SetInt(Key, kg);
            else PlayerPrefs.DeleteKey(Key);
            PlayerPrefs.Save();
        }
    }

    /// <summary>서버에 보낼 값 (모르면 0 — 서버가 "모름" 으로 본다. JsonUtility 는 필드를 뺄 수 없어서)</summary>
    public static float ForRequest => WeightKg ?? 0;
}
