using System;
using System.IO;
using UnityEngine;

/// <summary>
/// 사용자가 고른 캐릭터 외형. 부위마다 선택지 id를 저장한다.
/// 경로: Application.persistentDataPath/character.json
/// </summary>
[Serializable]
public class CharacterAppearance
{
    public int gender;
    public string head;
    public string body;
    public string legs;
    public string feet;
    public string accessory;
    [Tooltip("저장 형식 버전. CurrentVersion 보다 낮으면 기본 캐릭터(경희 모험가)로 바꾼다")]
    public int version;

    /// <summary>2 = 기본 캐릭터가 경희 모험가로 바뀐 뒤</summary>
    public const int CurrentVersion = 2;
    /// <summary>처음 시작할 때 입는 옷 (Characters/{Men,Women}/KHU_Adventurer)</summary>
    public const string DefaultOutfit = "KHU_Adventurer";

    static string FilePath => Path.Combine(Application.persistentDataPath, "character.json");

    public static bool Exists => File.Exists(FilePath);

    public string Get(CharacterSlot slot)
    {
        switch (slot)
        {
            case CharacterSlot.Head: return head;
            case CharacterSlot.Body: return body;
            case CharacterSlot.Legs: return legs;
            case CharacterSlot.Feet: return feet;
            default: return accessory;
        }
    }

    public void Set(CharacterSlot slot, string id)
    {
        switch (slot)
        {
            case CharacterSlot.Head: head = id; break;
            case CharacterSlot.Body: body = id; break;
            case CharacterSlot.Legs: legs = id; break;
            case CharacterSlot.Feet: feet = id; break;
            default: accessory = id; break;
        }
    }

    public CharacterAppearance Clone() => (CharacterAppearance)MemberwiseClone();

    public static CharacterAppearance Load()
    {
        try
        {
            if (Exists)
                return JsonUtility.FromJson<CharacterAppearance>(File.ReadAllText(FilePath));
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[CharacterAppearance] 저장된 캐릭터를 읽지 못했습니다: {e.Message}");
        }
        return null;
    }

    public void Save()
    {
        version = CurrentVersion;
        try
        {
            File.WriteAllText(FilePath, JsonUtility.ToJson(this));
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[CharacterAppearance] 저장하지 못했습니다: {e.Message}");
        }
    }
}
