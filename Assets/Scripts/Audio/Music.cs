using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// Oyunun arka plan müzikleri. Kendiliğinden oluşur, sahneler arasında yaşar.
//  - Ana menüde menü müziği döngüde çalar.
//  - Oyun sahnelerinde (Tutorial, levellar...) oyun içi çalma listesi çalar:
//    ilk parça her zaman FirstInGame, sonra diğerleri rastgele (aynısı art arda gelmez).
//    Parça bitmeden TrackCrossfade saniye önce bir sonraki yumuşakça girer.
//  - Sahne değiştirmek listeyi baştan başlatmaz; sadece menüye dönüp tekrar girince baştan başlar.
// Tüm müzik dosyaları aynı ses yüksekliğine (-18 LUFS) ayarlandı; tek "Müzik" ayarı hepsini eşit etkiler.
// Yeni müzik: dosyayı Assets/Resources/Audio/Music içine koy, adını InGame listesine ve DisplayNames'e ekle.
public class Music : MonoBehaviour
{
    private const string Folder = "Audio/Music/";
    private const string MenuScene = "AnaMenü";
    private const string MenuTrack = "menu_theme";
    private const string FirstInGame = "ingame_kayip_ses";

    private static readonly string[] InGame =
    {
        "ingame_kayip_ses",
        "ingame_kesif",
        "ingame_pas_ve_yagmur",
        "ingame_bellek_arsivi",
        "ingame_hurda_vadisi",
        "ingame_son_kafe",
    };

    // Müzik çalmayacak sahneler (gerekirse ekle)
    private static readonly HashSet<string> SilentScenes = new HashSet<string>();

    private static readonly Dictionary<string, string> DisplayNames = new Dictionary<string, string>
    {
        { "menu_theme", "Ana Menü - Son Operatör" },
        { "ingame_kayip_ses", "Kayıp Ses" },
        { "ingame_kesif", "Keşif" },
        { "ingame_pas_ve_yagmur", "Pas ve Yağmur" },
        { "ingame_bellek_arsivi", "Bellek Arşivi" },
        { "ingame_hurda_vadisi", "Hurda Vadisi" },
        { "ingame_son_kafe", "Son Kafe" },
    };

    private const float SceneFade = 2f;        // menü <-> oyun geçişi
    private const float TrackCrossfade = 5f;   // oyun içi parçalar arası geçiş
    private const float TrackVolume = 1f;      // dosyalar zaten dengeli; taban seviye

    private static Music instance;
    private AudioSource[] sources;
    private int active;
    private string current;
    private bool playlistMode;
    private bool advancing;
    private Coroutine fadeRoutine;
    private static float volumeCache = -1f;

    /// <summary>Müzik ses seviyesi (Ayarlar > Müzik). Tüm müzikleri eşit etkiler.</summary>
    public static float Volume
    {
        get
        {
            if (volumeCache < 0f) volumeCache = PlayerPrefs.GetFloat("MusicVolume", 0.7f);
            return volumeCache;
        }
        set
        {
            volumeCache = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat("MusicVolume", volumeCache);
            if (instance != null && instance.fadeRoutine == null) instance.ApplyVolume();
        }
    }

    public static string CurrentTrack => instance != null ? instance.current : null;

    public static string CurrentDisplayName
    {
        get
        {
            string t = CurrentTrack;
            if (string.IsNullOrEmpty(t)) return "—";
            return DisplayNames.TryGetValue(t, out string n) ? n : t;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Create()
    {
        if (instance != null) return;
        GameObject go = new GameObject("[Music]");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<Music>();
        instance.sources = new AudioSource[2];
        for (int i = 0; i < 2; i++)
        {
            AudioSource a = go.AddComponent<AudioSource>();
            a.playOnAwake = false;
            a.spatialBlend = 0f;
            a.volume = 0f;
            a.ignoreListenerPause = true;
            instance.sources[i] = a;
        }
        SceneManager.sceneLoaded += (s, m) => instance?.OnScene(s.name);
        instance.OnScene(SceneManager.GetActiveScene().name);
    }

    private void OnScene(string sceneName)
    {
        if (SilentScenes.Contains(sceneName))
        {
            playlistMode = false;
            Stop();
        }
        else if (sceneName == MenuScene)
        {
            playlistMode = false;
            StartTrack(MenuTrack, true, SceneFade);
        }
        else if (!playlistMode)
        {
            // Menüden oyuna girildi: liste her zaman ilk parçayla başlar.
            playlistMode = true;
            StartTrack(FirstInGame, false, SceneFade);
        }
        // Oyun sahneleri arasında geçişte liste kaldığı yerden devam eder.
    }

    private void Update()
    {
        if (!playlistMode || advancing) return;
        AudioSource s = sources[active];
        if (s.clip == null) return;

        bool nearEnd = s.isPlaying && s.clip.length - s.time <= TrackCrossfade;
        bool ended = !s.isPlaying && s.time <= 0.01f && fadeRoutine == null;
        if (nearEnd || ended) StartTrack(PickNext(), false, TrackCrossfade);
    }

    private string PickNext()
    {
        if (InGame.Length <= 1) return InGame[0];
        string next;
        do next = InGame[Random.Range(0, InGame.Length)];
        while (next == current);
        return next;
    }

    /// <summary>Belirli bir müziği döngüde çalar (ör. özel bir sahne için).</summary>
    public static void Play(string track)
    {
        if (instance == null) return;
        instance.playlistMode = false;
        instance.StartTrack(track, true, SceneFade);
    }

    /// <summary>Oyun içi çalma listesine geri döner.</summary>
    public static void ResumePlaylist()
    {
        if (instance == null || instance.playlistMode) return;
        instance.playlistMode = true;
        instance.StartTrack(instance.PickNext(), false, SceneFade);
    }

    public static void Stop()
    {
        if (instance == null || instance.current == null) return;
        instance.current = null;
        instance.Crossfade(null, false, SceneFade);
    }

    private void StartTrack(string track, bool loop, float fadeTime)
    {
        if (string.IsNullOrEmpty(track) || (current == track && sources[active].isPlaying)) return;
        AudioClip clip = Resources.Load<AudioClip>(Folder + track);
        if (clip == null)
        {
            Debug.LogWarning("Music: '" + Folder + track + "' bulunamadı.");
            return;
        }
        current = track;
        Crossfade(clip, loop, fadeTime);
    }

    private void Crossfade(AudioClip clip, bool loop, float fadeTime)
    {
        AudioSource from = sources[active];
        active = 1 - active;
        AudioSource to = sources[active];
        to.Stop();
        to.clip = clip;
        to.loop = loop;
        to.volume = 0f;
        to.time = 0f;
        if (clip != null) to.Play();
        if (fadeRoutine != null) StopCoroutine(fadeRoutine);
        fadeRoutine = StartCoroutine(FadeRoutine(from, to, fadeTime));
    }

    private IEnumerator FadeRoutine(AudioSource from, AudioSource to, float time)
    {
        advancing = true;
        float startFrom = from.volume;
        float t = 0f;
        while (t < time)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / time);
            // eşit güçlü geçiş: ortada ses düşmez
            from.volume = startFrom * Mathf.Cos(k * Mathf.PI * 0.5f);
            to.volume = to.clip != null ? TargetVolume() * Mathf.Sin(k * Mathf.PI * 0.5f) : 0f;
            yield return null;
        }
        from.Stop();
        from.clip = null;
        fadeRoutine = null;
        advancing = false;
        ApplyVolume();
    }

    private static float TargetVolume() => TrackVolume * Volume;

    private void ApplyVolume()
    {
        AudioSource s = sources[active];
        if (s != null) s.volume = s.clip != null ? TargetVolume() : 0f;
    }
}
