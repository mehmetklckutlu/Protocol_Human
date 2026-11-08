using System.Collections;
using UnityEngine;

public static class AnimUtil
{
    public const string Attack = "Attack";
    public const string MeleeAttack = "MeleeAttack";
    public const string RangedAttack = "RangedAttack";
    public const string Die = "Die";
    public const string Explode = "Explode";
    public const string Search = "Search";
    public const string Deplete = "Deplete";

    public static Animator Find(Component c)
    {
        if (c == null) return null;
        Animator a = c.GetComponent<Animator>();
        if (a == null) a = c.GetComponentInChildren<Animator>();
        return a;
    }

    public static bool HasParam(Animator a, string name, AnimatorControllerParameterType type)
    {
        if (a == null || a.runtimeAnimatorController == null || !a.isActiveAndEnabled) return false;
        foreach (AnimatorControllerParameter p in a.parameters)
            if (p.type == type && p.name == name) return true;
        return false;
    }

    public static bool Trigger(Animator a, string name)
    {
        if (!HasParam(a, name, AnimatorControllerParameterType.Trigger)) return false;
        a.SetTrigger(name);
        return true;
    }

    public static bool SetBool(Animator a, string name, bool value)
    {
        if (!HasParam(a, name, AnimatorControllerParameterType.Bool)) return false;
        a.SetBool(name, value);
        return true;
    }

    public static float LongestClipLength(Animator a)
    {
        if (a == null || a.runtimeAnimatorController == null) return 0f;
        float max = 0f;
        foreach (AnimationClip clip in a.runtimeAnimatorController.animationClips)
            if (clip != null && clip.length > max) max = clip.length;
        return max;
    }

    public static IEnumerator WaitForState(Animator a, string stateName, float maxTime)
    {
        if (a == null) yield break;

        float t = 0f;
        while (t < maxTime && a != null && !a.GetCurrentAnimatorStateInfo(0).IsName(stateName))
        {
            t += Time.deltaTime;
            yield return null;
        }

        while (t < maxTime && a != null)
        {
            AnimatorStateInfo info = a.GetCurrentAnimatorStateInfo(0);
            if (!info.IsName(stateName) || info.normalizedTime >= 1f) yield break;
            t += Time.deltaTime;
            yield return null;
        }
    }
}
