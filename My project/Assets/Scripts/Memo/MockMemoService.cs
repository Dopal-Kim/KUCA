using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// 서버 없이 쓰는 가짜 메모 서버. 메모는 기기 안(persistentDataPath/mock_memos)에 저장되어 나만 보인다.
/// 규칙(글자 수, 최신순, 삭제 권한)은 docs/api.md 와 같게 흉내 낸다.
/// 처음 실행할 때 Resources/MemoSamples/samples.json 의 예시 메모를 넣어 둔다.
/// </summary>
public class MockMemoService : IMemoService
{
    [Serializable]
    class Store
    {
        public bool seeded;
        public List<MemoData> memos = new List<MemoData>();
        public List<string> owners = new List<string>(); // memos 와 같은 순서의 작성 기기 ID
    }

    static string Dir => Path.Combine(Application.persistentDataPath, "mock_memos");
    static string StorePath => Path.Combine(Dir, "memos.json");

    Store store;

    public string Description => "가짜 서버 (이 기기에만 저장)";

    Store Data
    {
        get
        {
            if (store != null)
                return store;
            Directory.CreateDirectory(Dir);
            store = File.Exists(StorePath) ? JsonUtility.FromJson<Store>(File.ReadAllText(StorePath)) : new Store();
            if (!store.seeded)
            {
                Seed();
                store.seeded = true;
                Save();
            }
            return store;
        }
    }

    void Save() => File.WriteAllText(StorePath, JsonUtility.ToJson(store));

    void Seed()
    {
        var samples = Resources.Load<TextAsset>("MemoSamples/samples");
        if (samples == null)
            return;
        foreach (MemoData m in JsonUtility.FromJson<MemoDataList>(samples.text).items)
        {
            store.memos.Add(m);
            store.owners.Add("sample");
        }
    }

    public IEnumerator GetMemos(string buildingId, Action<List<MemoData>, string> onDone)
    {
        yield return null; // 네트워크처럼 한 프레임 쉰다
        var list = Data.memos.FindAll(m => m.buildingId == buildingId);
        list.Sort((a, b) => string.CompareOrdinal(b.createdAt, a.createdAt));
        onDone(list, null);
    }

    public IEnumerator CreateMemo(string buildingId, NewMemo memo, Action<MemoData, string> onDone)
    {
        yield return null;
        string author = memo.author?.Trim() ?? "";
        string text = memo.text?.Trim() ?? "";
        if (author.Length < 1 || author.Length > 20) { onDone(null, "닉네임은 1~20자로 써 주세요."); yield break; }
        if (text.Length < 1 || text.Length > 500) { onDone(null, "메모는 1~500자로 써 주세요."); yield break; }

        var m = new MemoData
        {
            id = Guid.NewGuid().ToString(),
            buildingId = buildingId,
            author = author,
            text = text,
            createdAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
        };
        if (memo.photoJpg != null && memo.photoJpg.Length > 0)
        {
            File.WriteAllBytes(Path.Combine(Dir, m.id + ".jpg"), memo.photoJpg);
            m.photoUrl = "mock://" + m.id + ".jpg";
        }
        Data.memos.Add(m);
        Data.owners.Add(memo.deviceId);
        Save();
        onDone(m, null);
    }

    public IEnumerator DeleteMemo(string memoId, string deviceId, Action<bool, string> onDone)
    {
        yield return null;
        int i = Data.memos.FindIndex(m => m.id == memoId);
        if (i < 0) { onDone(false, "메모가 없습니다."); yield break; }
        if (Data.owners[i] != deviceId) { onDone(false, "내가 쓴 메모만 지울 수 있어요."); yield break; }

        string photo = Path.Combine(Dir, memoId + ".jpg");
        if (File.Exists(photo))
            File.Delete(photo);
        Data.memos.RemoveAt(i);
        Data.owners.RemoveAt(i);
        Save();
        onDone(true, null);
    }

    public IEnumerator LoadPhoto(MemoData memo, Action<Texture2D, string> onDone)
    {
        yield return null;
        string url = memo.photoUrl ?? "";
        if (url.StartsWith("resource://"))
        {
            var tex = Resources.Load<Texture2D>(url.Substring("resource://".Length));
            onDone(tex, tex == null ? "예시 사진을 찾을 수 없습니다." : null);
        }
        else if (url.StartsWith("mock://"))
        {
            string path = Path.Combine(Dir, url.Substring("mock://".Length));
            if (!File.Exists(path)) { onDone(null, "사진 파일이 없습니다."); yield break; }
            var tex = new Texture2D(2, 2);
            tex.LoadImage(File.ReadAllBytes(path));
            onDone(tex, null);
        }
        else
            onDone(null, "사진 주소를 알 수 없습니다.");
    }

    /// <summary>가짜 서버의 데이터를 모두 지우고 예시 메모부터 다시 시작한다 (테스트용).</summary>
    public static void ResetAll()
    {
        if (Directory.Exists(Dir))
            Directory.Delete(Dir, true);
    }
}
