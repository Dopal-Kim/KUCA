using System;
using System.Collections.Generic;

/// <summary>메모 하나. docs/api.md 의 Memo JSON과 같은 모양 (JsonUtility로 그대로 읽는다).</summary>
[Serializable]
public class MemoData
{
    public string id;
    public string buildingId;
    public string author;
    public string text;
    /// <summary>"/photos/abc.jpg"(서버) 또는 "mock://abc.jpg"(가짜 서버). 사진이 없으면 비어 있음</summary>
    public string photoUrl;
    /// <summary>ISO 8601 UTC 문자열</summary>
    public string createdAt;

    public bool HasPhoto => !string.IsNullOrEmpty(photoUrl);

    public DateTime CreatedAtLocal =>
        DateTime.TryParse(createdAt, null, System.Globalization.DateTimeStyles.RoundtripKind, out DateTime t) ? t.ToLocalTime() : DateTime.MinValue;
}

/// <summary>JsonUtility는 최상위 배열을 못 읽으므로 감싸서 쓴다.</summary>
[Serializable]
public class MemoDataList
{
    public List<MemoData> items = new List<MemoData>();
}

/// <summary>메모 작성 요청 (docs/api.md 의 POST 필드)</summary>
public class NewMemo
{
    public string author;
    public string text;
    public string deviceId;
    /// <summary>JPEG 바이트. 사진 없이 올리면 null</summary>
    public byte[] photoJpg;
}
