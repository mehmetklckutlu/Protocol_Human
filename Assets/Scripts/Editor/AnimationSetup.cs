using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class AnimationSetup
{
    public const float AttackFrame = 0.18f;
    public const float DieFrame = 0.1f;
    public const float ExplodeFrame = 0.07f;
    public const float ScrapFrame = 0.25f;

    private const string EnemyDeathSheet = "UzakçıDroneÖlme";

    [MenuItem("Protocol Human/Animasyon Sistemini Kur")]
    public static void SetupAll()
    {
        AnimatorController player = FindController("Player");
        AnimatorController enemy = FindController("Enemy");
        AnimatorController barrel = FindController("Varil");
        AnimatorController scrap = FindController("hurda");

        if (player != null) RebuildUnit(player, false);
        if (enemy != null) RebuildUnit(enemy, true);
        if (barrel != null) RebuildBarrel(barrel);
        if (scrap != null) RebuildScrap(scrap);
        AssetDatabase.SaveAssets();

        int prefabCount = SetupPrefabs(player, enemy, barrel, scrap);
        int sceneCount = SetupOpenScenes(player, enemy, barrel, scrap);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"<color=lime>Animasyon sistemi kuruldu.</color> Prefab: {prefabCount}, sahnedeki obje: {sceneCount}");
    }

    private static AnimatorController FindController(string name)
    {
        foreach (string guid in AssetDatabase.FindAssets(name + " t:AnimatorController", new[] { "Assets" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (System.IO.Path.GetFileNameWithoutExtension(path) == name)
                return AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
        }
        Debug.LogWarning("Animator Controller bulunamadı: " + name);
        return null;
    }

    private static void RebuildUnit(AnimatorController c, bool isEnemy)
    {
        AnimatorStateMachine sm = c.layers[0].stateMachine;

        AnimationClip idle = ClipOf(FindState(sm, "Idle")) ?? ClipOf(sm.defaultState);
        AnimationClip attack = ClipOf(FindState(sm, "Attack")) ?? FindClipByName(sm, "sald");
        AnimationClip die = ClipOf(FindState(sm, "Die")) ?? FindClipByName(sm, "lme");

        if (isEnemy)
        {
            string folder = System.IO.Path.GetDirectoryName(AssetDatabase.GetAssetPath(c));
            AnimationClip ownAttack = FindClipInFolder(folder, "sald");
            if (ownAttack != null) attack = ownAttack;
            AnimationClip ownDie = FindClipInFolder(folder, "lme");
            if (ownDie != null) die = ownDie;
            if (die != null && IsEmpty(die)) FillFromSheet(die, EnemyDeathSheet, attack ?? idle);
        }

        ClearMachine(c, sm);
        c.AddParameter(AnimUtil.Attack, AnimatorControllerParameterType.Trigger);
        c.AddParameter(AnimUtil.Die, AnimatorControllerParameterType.Trigger);

        AnimatorState sIdle = sm.AddState("Idle", new Vector3(300, 100, 0));
        AnimatorState sAttack = sm.AddState("Attack", new Vector3(560, 20, 0));
        AnimatorState sDie = sm.AddState("Die", new Vector3(560, 200, 0));
        sIdle.motion = idle;
        sAttack.motion = attack;
        sDie.motion = die;
        sm.defaultState = sIdle;

        AnimatorStateTransition t = sIdle.AddTransition(sAttack);
        Instant(t, false);
        t.AddCondition(AnimatorConditionMode.If, 0, AnimUtil.Attack);

        t = sAttack.AddTransition(sAttack);
        Instant(t, false);
        t.AddCondition(AnimatorConditionMode.If, 0, AnimUtil.Attack);

        t = sAttack.AddTransition(sIdle);
        Instant(t, true);

        t = sm.AddAnyStateTransition(sDie);
        Instant(t, false);
        t.canTransitionToSelf = false;
        t.AddCondition(AnimatorConditionMode.If, 0, AnimUtil.Die);

        Retime(attack, AttackFrame, false);
        Retime(die, DieFrame, false);

        EditorUtility.SetDirty(c);
    }

    private static void RebuildBarrel(AnimatorController c)
    {
        AnimatorStateMachine sm = c.layers[0].stateMachine;
        AnimationClip explode = ClipOf(FindState(sm, "Explode")) ?? FindClipByName(sm, "");

        ClearMachine(c, sm);
        c.AddParameter(AnimUtil.Explode, AnimatorControllerParameterType.Trigger);

        AnimatorState sIdle = sm.AddState("Idle", new Vector3(300, 100, 0));
        AnimatorState sExplode = sm.AddState("Explode", new Vector3(560, 100, 0));
        sExplode.motion = explode;
        sm.defaultState = sIdle;

        AnimatorStateTransition t = sm.AddAnyStateTransition(sExplode);
        Instant(t, false);
        t.canTransitionToSelf = false;
        t.AddCondition(AnimatorConditionMode.If, 0, AnimUtil.Explode);

        Retime(explode, ExplodeFrame, false, true);
        EditorUtility.SetDirty(c);
    }

    private static void RebuildScrap(AnimatorController c)
    {
        AnimatorStateMachine sm = c.layers[0].stateMachine;
        AnimationClip vanish = ClipOf(FindState(sm, "Search")) ?? ClipOf(FindState(sm, "Depleted")) ?? FindClipByName(sm, "");

        ClearMachine(c, sm);
        c.AddParameter(AnimUtil.Search, AnimatorControllerParameterType.Bool);

        AnimatorState sIdle = sm.AddState("Idle", new Vector3(300, 100, 0));
        AnimatorState sSearch = sm.AddState("Search", new Vector3(560, 100, 0));
        sSearch.motion = vanish;
        sm.defaultState = sIdle;

        AnimatorStateTransition t = sIdle.AddTransition(sSearch);
        Instant(t, false);
        t.AddCondition(AnimatorConditionMode.If, 0, AnimUtil.Search);

        Retime(vanish, ScrapFrame, false);
        EditorUtility.SetDirty(c);
    }

    private static void ClearMachine(AnimatorController c, AnimatorStateMachine sm)
    {
        foreach (AnimatorStateTransition t in sm.anyStateTransitions.ToArray()) sm.RemoveAnyStateTransition(t);
        foreach (AnimatorTransition t in sm.entryTransitions.ToArray()) sm.RemoveEntryTransition(t);
        foreach (ChildAnimatorState cs in sm.states.ToArray()) sm.RemoveState(cs.state);
        foreach (ChildAnimatorStateMachine csm in sm.stateMachines.ToArray()) sm.RemoveStateMachine(csm.stateMachine);
        foreach (AnimatorControllerParameter p in c.parameters.ToArray()) c.RemoveParameter(p);
    }

    private static AnimationClip ClipOf(AnimatorState s)
    {
        return s != null ? s.motion as AnimationClip : null;
    }

    private static AnimationClip FindClipByName(AnimatorStateMachine sm, string part)
    {
        foreach (ChildAnimatorState cs in sm.states)
        {
            AnimationClip clip = ClipOf(cs.state);
            if (clip == null) continue;
            if (clip.name.ToLowerInvariant().Contains(part) || cs.state.name.ToLowerInvariant().Contains(part)) return clip;
        }
        return null;
    }

    private static AnimationClip FindClipInFolder(string folder, string part)
    {
        foreach (string guid in AssetDatabase.FindAssets("t:AnimationClip", new[] { folder.Replace('\\', '/') }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (System.IO.Path.GetFileNameWithoutExtension(path).ToLowerInvariant().Contains(part))
                return AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        }
        return null;
    }

    private static EditorCurveBinding? SpriteBinding(AnimationClip clip)
    {
        if (clip == null) return null;
        foreach (EditorCurveBinding b in AnimationUtility.GetObjectReferenceCurveBindings(clip))
            if (b.type == typeof(SpriteRenderer) && b.propertyName == "m_Sprite") return b;
        return null;
    }

    private static bool IsEmpty(AnimationClip clip)
    {
        EditorCurveBinding? b = SpriteBinding(clip);
        if (b == null) return true;
        ObjectReferenceKeyframe[] keys = AnimationUtility.GetObjectReferenceCurve(clip, b.Value);
        return keys == null || keys.Length == 0;
    }

    private static void FillFromSheet(AnimationClip clip, string sheetName, AnimationClip bindingSource)
    {
        Sprite[] sprites = null;
        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (System.IO.Path.GetFileNameWithoutExtension(path) != sheetName) continue;
            sprites = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>()
                .OrderBy(s => SpriteIndex(s.name)).ToArray();
            break;
        }
        if (sprites == null || sprites.Length == 0)
        {
            Debug.LogWarning("Ölüm animasyonu için sprite bulunamadı: " + sheetName);
            return;
        }

        EditorCurveBinding binding = SpriteBinding(bindingSource) ?? EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite");
        ObjectReferenceKeyframe[] keys = new ObjectReferenceKeyframe[sprites.Length];
        for (int i = 0; i < sprites.Length; i++)
            keys[i] = new ObjectReferenceKeyframe { time = i * DieFrame, value = sprites[i] };
        AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);
        EditorUtility.SetDirty(clip);
    }

    private static int SpriteIndex(string name)
    {
        int i = name.LastIndexOf('_');
        return i >= 0 && int.TryParse(name.Substring(i + 1), out int n) ? n : 0;
    }

    private static void Retime(AnimationClip clip, float frameTime, bool loop, bool dropReturnToFirst = false)
    {
        if (clip == null) return;
        EditorCurveBinding? b = SpriteBinding(clip);
        if (b != null)
        {
            ObjectReferenceKeyframe[] keys = AnimationUtility.GetObjectReferenceCurve(clip, b.Value);
            List<Object> frames = new List<Object>();
            foreach (ObjectReferenceKeyframe k in keys.OrderBy(k => k.time))
                if (frames.Count == 0 || frames[frames.Count - 1] != k.value) frames.Add(k.value);
            if (dropReturnToFirst && frames.Count > 2 && frames[frames.Count - 1] == frames[0])
                frames.RemoveAt(frames.Count - 1);

            if (frames.Count > 0)
            {
                ObjectReferenceKeyframe[] nk = new ObjectReferenceKeyframe[frames.Count + 1];
                for (int i = 0; i < frames.Count; i++)
                    nk[i] = new ObjectReferenceKeyframe { time = i * frameTime, value = frames[i] };
                nk[frames.Count] = new ObjectReferenceKeyframe { time = frames.Count * frameTime, value = frames[frames.Count - 1] };
                AnimationUtility.SetObjectReferenceCurve(clip, b.Value, nk);
            }
        }

        AnimationClipSettings s = AnimationUtility.GetAnimationClipSettings(clip);
        s.loopTime = loop;
        AnimationUtility.SetAnimationClipSettings(clip, s);
        EditorUtility.SetDirty(clip);
    }

    private static int SetupPrefabs(AnimatorController player, AnimatorController enemy, AnimatorController barrel, AnimatorController scrap)
    {
        int count = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset == null) continue;
            if (asset.GetComponentInChildren<Unit>(true) == null &&
                asset.GetComponentInChildren<ExplosiveBarrel>(true) == null &&
                asset.GetComponentInChildren<ScrapPile>(true) == null) continue;

            GameObject root = PrefabUtility.LoadPrefabContents(path);
            if (ApplyTo(root, player, enemy, barrel, scrap))
            {
                PrefabUtility.SaveAsPrefabAsset(root, path);
                count++;
            }
            PrefabUtility.UnloadPrefabContents(root);
        }
        return count;
    }

    private static int SetupOpenScenes(AnimatorController player, AnimatorController enemy, AnimatorController barrel, AnimatorController scrap)
    {
        int count = 0;
        HashSet<GameObject> done = new HashSet<GameObject>();
        List<Component> targets = new List<Component>();
        targets.AddRange(Object.FindObjectsByType<Unit>(FindObjectsInactive.Include, FindObjectsSortMode.None));
        targets.AddRange(Object.FindObjectsByType<ExplosiveBarrel>(FindObjectsInactive.Include, FindObjectsSortMode.None));
        targets.AddRange(Object.FindObjectsByType<ScrapPile>(FindObjectsInactive.Include, FindObjectsSortMode.None));

        foreach (Component comp in targets)
        {
            if (comp == null || !done.Add(comp.gameObject)) continue;
            if (ApplyTo(comp.gameObject, player, enemy, barrel, scrap))
            {
                EditorSceneManager.MarkSceneDirty(comp.gameObject.scene);
                count++;
            }
        }
        return count;
    }

    private static bool ApplyTo(GameObject go, AnimatorController player, AnimatorController enemy, AnimatorController barrel, AnimatorController scrap)
    {
        AnimatorController ctrl = null;
        Unit unit = go.GetComponentInChildren<Unit>(true);
        if (unit != null)
        {
            DroneUnit drone = unit.GetComponent<DroneUnit>();
            bool ally = drone != null ? drone.isAlly : unit.isPlayerOwned;
            ctrl = ally ? player : enemy;
            go = unit.gameObject;
        }
        else if (go.GetComponentInChildren<ExplosiveBarrel>(true) != null)
        {
            go = go.GetComponentInChildren<ExplosiveBarrel>(true).gameObject;
            ctrl = barrel;
        }
        else if (go.GetComponentInChildren<ScrapPile>(true) != null)
        {
            go = go.GetComponentInChildren<ScrapPile>(true).gameObject;
            ctrl = scrap;
        }
        if (ctrl == null) return false;

        Animator anim = go.GetComponentInChildren<Animator>(true);
        bool changed = false;
        if (anim == null)
        {
            SpriteRenderer sr = go.GetComponent<SpriteRenderer>();
            if (sr == null) sr = go.GetComponentInChildren<SpriteRenderer>(true);
            GameObject host = sr != null ? sr.gameObject : go;
            anim = Undo.AddComponent<Animator>(host);
            changed = true;
        }
        if (anim.runtimeAnimatorController == null)
        {
            Undo.RecordObject(anim, "Animator Controller");
            anim.runtimeAnimatorController = ctrl;
            changed = true;
        }
        if (changed) EditorUtility.SetDirty(anim);
        return changed;
    }

    private static AnimatorState FindState(AnimatorStateMachine sm, string name)
    {
        foreach (ChildAnimatorState cs in sm.states)
            if (cs.state != null && cs.state.name == name) return cs.state;
        return null;
    }

    private static void Instant(AnimatorStateTransition t, bool exitTime)
    {
        t.hasExitTime = exitTime;
        t.exitTime = 1f;
        t.hasFixedDuration = true;
        t.duration = 0f;
        t.offset = 0f;
    }
}
