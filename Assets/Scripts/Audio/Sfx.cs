using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Oyunun ses efektleri. Sesler Assets/Resources/Audio/SFX klasöründen adıyla yüklenir;
// bir sesi değiştirmek için aynı adla yeni bir .wav koyman yeterli.
// Kullanım: Sfx.Play("explosion");  Sfx.Voice();  var s = Sfx.StartLoop("scrap_search"); ... Sfx.StopLoop(s);
// Ses seviyeleri Ayarlar menüsünden (Esc) değiştirilir ve kaydedilir.
public class Sfx : MonoBehaviour
{
    private const string Folder = "Audio/SFX/";
    private const int PoolSize = 16;
    private const int VoiceCount = 6;

    private static Sfx instance;
    private static readonly Dictionary<string, AudioClip> cache = new Dictionary<string, AudioClip>();
    private static readonly HashSet<string> missing = new HashSet<string>();
    private static readonly Dictionary<string, float> lastPlayed = new Dictionary<string, float>();

    private AudioSource[] pool;
    private int next;

    // ------------------------------------------------------------------ ses seviyeleri

    /// <summary>Tüm oyunun ses seviyesi (AudioListener).</summary>
    public static float MasterVolume
    {
        get => GetPref("MasterVolume", 1f);
        set { SetPref("MasterVolume", value); AudioListener.volume = Mathf.Clamp01(value); }
    }

    /// <summary>Ses efektleri (silah, patlama, kartlar...).</summary>
    public static float Volume
    {
        get => GetPref("SfxVolume", 0.8f);
        set => SetPref("SfxVolume", value);
    }

    /// <summary>Tutorial'daki robot konuşma sesi.</summary>
    public static float VoiceVolume
    {
        get => GetPref("VoiceVolume", 0.8f);
        set => SetPref("VoiceVolume", value);
    }

    /// <summary>Uçan drone'ların motor uğultusu.</summary>
    public static float EngineVolume
    {
        get => GetPref("EngineVolume", 0.7f);
        set => SetPref("EngineVolume", value);
    }

    private static readonly Dictionary<string, float> prefCache = new Dictionary<string, float>();

    private static float GetPref(string key, float def)
    {
        if (!prefCache.TryGetValue(key, out float v))
        {
            v = PlayerPrefs.GetFloat(key, def);
            prefCache[key] = v;
        }
        return v;
    }

    private static void SetPref(string key, float v)
    {
        v = Mathf.Clamp01(v);
        prefCache[key] = v;
        PlayerPrefs.SetFloat(key, v);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ApplySavedMaster()
    {
        AudioListener.volume = MasterVolume;
    }

    // ------------------------------------------------------------------ çalma

    /// <param name="name">Resources/Audio/SFX içindeki dosya adı (uzantısız).</param>
    /// <param name="vol">Bu sesin seviyesi (0-1).</param>
    /// <param name="pitchJitter">Her çalışta perdenin rastgele ne kadar oynayacağı; tekrar eden sesler mekanik duymasın.</param>
    /// <param name="minInterval">Aynı ses bu süreden sık çalınmaz (üst üste binip patlamasın).</param>
    public static void Play(string name, float vol = 1f, float pitchJitter = 0.04f, float minInterval = 0.05f, float pitch = 1f)
    {
        PlayScaled(name, vol * Volume, pitchJitter, minInterval, pitch);
    }

    private static void PlayScaled(string name, float finalVol, float pitchJitter, float minInterval, float pitch)
    {
        if (!Application.isPlaying || string.IsNullOrEmpty(name) || finalVol <= 0.001f) return;

        float now = Time.unscaledTime;
        if (lastPlayed.TryGetValue(name, out float last) && now - last < minInterval) return;

        AudioClip clip = GetClip(name);
        if (clip == null) return;
        lastPlayed[name] = now;

        Sfx s = Get();
        AudioSource src = s.pool[s.next];
        s.next = (s.next + 1) % s.pool.Length;
        src.pitch = pitch * (1f + Random.Range(-pitchJitter, pitchJitter));
        src.PlayOneShot(clip, Mathf.Clamp01(finalVol));
    }

    /// <summary>Robot konuşma sesi: yazı ekrana gelirken harf harf çalınan kısa hece.</summary>
    // Hece hızı: yazı ne kadar hızlı akarsa aksın, saniyede ~6 hece duyulur (konuşma ritmi).
    private static float nextVoiceTime;
    private static int lastVoice = -1;

    public static void Voice(float pitch = 1f)
    {
        float now = Time.unscaledTime;
        if (now < nextVoiceTime) return;
        nextVoiceTime = now + Random.Range(0.15f, 0.18f);

        int i = Random.Range(0, VoiceCount - 1);
        if (i >= lastVoice) i++;          // aynı heceyi üst üste çalma
        if (i >= VoiceCount) i = 0;
        lastVoice = i;
        PlayScaled("voice_" + i, 0.55f * VoiceVolume, 0.03f, 0f, pitch);
    }

    /// <summary>Eski adım sesi kaldırıldı; drone'lar artık DroneHoverSound ile motor uğultusu çıkarıyor.</summary>
    public static void Step() { }

    /// <summary>Döngüde çalan bir ses başlatır (ör. hurda arama). Durdurmak için StopLoop.</summary>
    public static AudioSource StartLoop(string name, float vol = 1f, float fadeIn = 0.15f)
    {
        if (!Application.isPlaying) return null;
        AudioClip clip = GetClip(name);
        if (clip == null) return null;

        Sfx s = Get();
        AudioSource src = s.gameObject.AddComponent<AudioSource>();
        src.clip = clip;
        src.loop = true;
        src.playOnAwake = false;
        src.spatialBlend = 0f;
        src.volume = 0f;
        src.time = Random.Range(0f, clip.length * 0.5f);
        src.Play();
        s.StartCoroutine(s.Fade(src, Mathf.Clamp01(vol * Volume), fadeIn, false));
        return src;
    }

    public static void StopLoop(AudioSource src, float fadeOut = 0.25f)
    {
        if (src == null || instance == null) return;
        instance.StartCoroutine(instance.Fade(src, 0f, fadeOut, true));
    }

    private IEnumerator Fade(AudioSource src, float target, float time, bool destroyAtEnd)
    {
        float start = src != null ? src.volume : 0f;
        float t = 0f;
        while (src != null && t < time)
        {
            t += Time.unscaledDeltaTime;
            src.volume = Mathf.Lerp(start, target, t / Mathf.Max(0.001f, time));
            yield return null;
        }
        if (src == null) yield break;
        src.volume = target;
        if (destroyAtEnd)
        {
            src.Stop();
            Destroy(src);
        }
    }

    public static AudioClip GetClip(string name)
    {
        if (cache.TryGetValue(name, out AudioClip c) && c != null) return c;
        if (missing.Contains(name)) return null;

        c = Resources.Load<AudioClip>(Folder + name);
        if (c == null)
        {
            missing.Add(name);
            Debug.LogWarning("Sfx: '" + Folder + name + "' bulunamadı.");
            return null;
        }
        cache[name] = c;
        return c;
    }

    private static Sfx Get()
    {
        if (instance != null) return instance;
        GameObject go = new GameObject("[Sfx]");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<Sfx>();
        instance.pool = new AudioSource[PoolSize];
        for (int i = 0; i < PoolSize; i++)
        {
            AudioSource a = go.AddComponent<AudioSource>();
            a.playOnAwake = false;
            a.spatialBlend = 0f;
            instance.pool[i] = a;
        }
        return instance;
    }
}
