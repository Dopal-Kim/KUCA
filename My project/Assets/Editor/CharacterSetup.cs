using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Assets/Characters/{Men,Women} 의 Quaternius 모듈형 캐릭터 FBX로
/// 임포트 설정, 성별별 애니메이터(Idle/Walk/Run 블렌드), 부위 데이터베이스를 만든다.
/// 메뉴: KUCA/Build Character Database
/// </summary>
public static class CharacterSetup
{
    const string Root = "Assets/Characters";
    const string DatabasePath = Root + "/CharacterPartsDatabase.asset";

    static readonly (string folder, string displayName)[] Genders = { ("Men", "남자"), ("Women", "여자") };

    static readonly Dictionary<string, string> OutfitNames = new Dictionary<string, string>
    {
        { "Adventurer", "모험가" }, { "Beach", "해변" }, { "Casual", "캐주얼" }, { "Casual_2", "캐주얼 2" },
        { "Casual_Hoodie", "후드티" }, { "Farmer", "농부" }, { "Formal", "포멀" }, { "Horse", "말 머리" },
        { "King", "왕" }, { "Medieval", "중세" }, { "Punk", "펑크" }, { "SciFi", "SF" }, { "Soldier", "군인" },
        { "SpaceSuit", "우주복" }, { "Suit", "정장" }, { "Swat", "특수부대" }, { "Witch", "마녀" }, { "Worker", "작업자" },
    };

    static readonly string[] LoopClips = { "Idle", "Walk", "Run" };

    [MenuItem("KUCA/Build Character Database")]
    public static void Build()
    {
        var db = AssetDatabase.LoadAssetAtPath<CharacterPartsDatabase>(DatabasePath);
        if (db == null)
        {
            db = ScriptableObject.CreateInstance<CharacterPartsDatabase>();
            AssetDatabase.CreateAsset(db, DatabasePath);
        }
        db.genders.Clear();

        foreach (var (folder, displayName) in Genders)
        {
            string dir = $"{Root}/{folder}";
            if (!AssetDatabase.IsValidFolder(dir))
                continue;

            ConfigureAnimations($"{dir}/Animations.fbx");
            var set = new CharacterPartsDatabase.GenderSet
            {
                id = folder.ToLowerInvariant(),
                displayName = displayName,
                animator = BuildController(dir, folder),
            };
            set.accessories.Add(new CharacterPartsDatabase.PartOption { id = "none", displayName = "없음" });

            foreach (string outfitDir in AssetDatabase.GetSubFolders(dir).OrderBy(p => p))
            {
                string outfit = Path.GetFileName(outfitDir);
                string name = OutfitNames.TryGetValue(outfit, out string n) ? n : outfit;
                foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { outfitDir }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    ConfigurePart(path);
                    string file = Path.GetFileNameWithoutExtension(path);
                    var option = new CharacterPartsDatabase.PartOption
                    {
                        id = outfit,
                        displayName = name,
                        prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path),
                    };
                    if (file.EndsWith("_Head")) set.heads.Add(option);
                    else if (file.EndsWith("_Body")) set.bodies.Add(option);
                    else if (file.EndsWith("_Legs")) set.legs.Add(option);
                    else if (file.EndsWith("_Feet")) set.feet.Add(option);
                    else if (file.EndsWith("_Backpack")) { option.displayName = name + " 가방"; set.accessories.Add(option); }
                }
            }
            db.genders.Add(set);
            Debug.Log($"[CharacterSetup] {displayName}: 머리 {set.heads.Count}, 상의 {set.bodies.Count}, 하의 {set.legs.Count}, 신발 {set.feet.Count}, 가방 {set.accessories.Count - 1}");
        }

        EditorUtility.SetDirty(db);
        AssetDatabase.SaveAssets();
    }

    /// <summary>
    /// 파트 FBX는 메시와 뼈대만 쓰므로 애니메이션은 가져오지 않는다.
    /// Blender에서 내보낸 축 변환(루트 회전 -90°, 스케일 100)은 임포트 때 구워서 루트를 1배로 만든다.
    /// </summary>
    static void ConfigurePart(string path)
    {
        var importer = (ModelImporter)AssetImporter.GetAtPath(path);
        if (importer.animationType == ModelImporterAnimationType.Generic && !importer.importAnimation && importer.bakeAxisConversion)
            return;
        importer.animationType = ModelImporterAnimationType.Generic;
        importer.importAnimation = false;
        importer.bakeAxisConversion = true;
        importer.SaveAndReimport();
    }

    /// <summary>Idle / Walk / Run 클립은 반복 재생되도록 설정한다.</summary>
    static void ConfigureAnimations(string path)
    {
        var importer = (ModelImporter)AssetImporter.GetAtPath(path);
        if (importer == null)
            return;
        importer.animationType = ModelImporterAnimationType.Generic;
        importer.bakeAxisConversion = true;
        ModelImporterClipAnimation[] clips = importer.clipAnimations.Length > 0 ? importer.clipAnimations : importer.defaultClipAnimations;
        foreach (var clip in clips)
            clip.loopTime = LoopClips.Contains(ShortName(clip.name));
        importer.clipAnimations = clips;
        importer.SaveAndReimport();
    }

    /// <summary>Speed(m/s) 하나로 Idle → Walk → Run 을 섞는 애니메이터를 만든다.</summary>
    static RuntimeAnimatorController BuildController(string dir, string folder)
    {
        var clips = AssetDatabase.LoadAllAssetsAtPath($"{dir}/Animations.fbx").OfType<AnimationClip>()
            .Where(c => !c.name.StartsWith("__preview__"))
            .ToDictionary(c => ShortName(c.name), c => c);

        string path = $"{dir}/{folder}.controller";
        AssetDatabase.DeleteAsset(path);
        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(path);
        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);

        BlendTree tree;
        AnimatorState state = controller.CreateBlendTreeInController("Locomotion", out tree);
        tree.blendType = BlendTreeType.Simple1D;
        tree.blendParameter = "Speed";
        tree.useAutomaticThresholds = false;
        if (clips.TryGetValue("Idle", out var idle)) tree.AddChild(idle, 0f);
        if (clips.TryGetValue("Walk", out var walk)) tree.AddChild(walk, 1.4f);
        if (clips.TryGetValue("Run", out var run)) tree.AddChild(run, 4f);
        controller.layers[0].stateMachine.defaultState = state;
        return controller;
    }

    /// <summary>"CharacterArmature|Walk" → "Walk"</summary>
    static string ShortName(string clipName)
    {
        int i = clipName.LastIndexOf('|');
        return i >= 0 ? clipName.Substring(i + 1) : clipName;
    }
}
