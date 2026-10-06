using System.Collections.Generic;
using UnityEngine;

/// <summary>캐릭터 부위 (머리, 상의, 하의, 신발, 액세서리)</summary>
public enum CharacterSlot { Head, Body, Legs, Feet, Accessory }

/// <summary>
/// 성별별로 고를 수 있는 부위 목록과 애니메이터를 모아 둔 데이터.
/// 에디터 메뉴 KUCA/Build Character Database 로 Assets/Characters 의 FBX에서 자동 생성한다.
/// </summary>
[CreateAssetMenu(menuName = "KUCA/Character Parts Database")]
public class CharacterPartsDatabase : ScriptableObject
{
    [System.Serializable]
    public class PartOption
    {
        public string id;
        public string displayName;
        [Tooltip("비어 있으면 '없음' 선택지")]
        public GameObject prefab;
    }

    [System.Serializable]
    public class GenderSet
    {
        public string id;
        public string displayName;
        public RuntimeAnimatorController animator;
        public List<PartOption> heads = new List<PartOption>();
        public List<PartOption> bodies = new List<PartOption>();
        public List<PartOption> legs = new List<PartOption>();
        public List<PartOption> feet = new List<PartOption>();
        public List<PartOption> accessories = new List<PartOption>();

        public List<PartOption> Options(CharacterSlot slot)
        {
            switch (slot)
            {
                case CharacterSlot.Head: return heads;
                case CharacterSlot.Body: return bodies;
                case CharacterSlot.Legs: return legs;
                case CharacterSlot.Feet: return feet;
                default: return accessories;
            }
        }
    }

    public List<GenderSet> genders = new List<GenderSet>();
}
