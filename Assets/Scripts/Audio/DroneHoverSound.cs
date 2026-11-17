using UnityEngine;

// Uçan drone'un motor uğultusu. Arka planda hep çok kısık çalar, drone hareket edince yükselir.
// UnitMovement oyuncu drone'larına (ana drone ve çağrılan dost drone'lar) bunu otomatik ekler.
[DisallowMultipleComponent]
public class DroneHoverSound : MonoBehaviour
{
    [Tooltip("Hareket halindeyken motor sesinin seviyesi (Ayarlar > Drone Motoru ile çarpılır).")]
    [SerializeField, Range(0f, 1f)] private float movingVolume = 0.22f;
    [Tooltip("Dururken arka planda sürekli çalan çok kısık rölanti uğultusu (0 = tamamen sessiz).")]
    [SerializeField, Range(0f, 1f)] private float idleVolume = 0.05f;
    [SerializeField] private float fadeSpeed = 4f;
    [SerializeField] private float movingPitch = 1.06f;

    private UnitMovement movement;
    private Unit unit;
    private AudioSource src;
    private float vol;

    private void Start()
    {
        movement = GetComponent<UnitMovement>();
        unit = GetComponent<Unit>();
        AudioClip clip = Sfx.GetClip("drone_hover");
        if (clip == null) { enabled = false; return; }

        src = gameObject.AddComponent<AudioSource>();
        src.clip = clip;
        src.loop = true;
        src.playOnAwake = false;
        src.spatialBlend = 0f;
        src.volume = 0f;
        src.time = Random.Range(0f, clip.length);
        // Yan drone'lar ana drone'dan biraz farklı perdede uğuldasın.
        if (unit != null && !unit.isMainDrone) movingPitch *= 1.12f;
    }

    private void Update()
    {
        if (src == null) return;

        bool alive = unit == null || !unit.IsDead;
        bool moving = alive && movement != null && movement.IsMoving && Time.timeScale > 0f;
        float scale = Sfx.Volume * Sfx.EngineVolume * (unit != null && !unit.isMainDrone ? 0.6f : 1f);
        float target = (moving ? movingVolume : (alive ? idleVolume : 0f)) * scale;

        vol = Mathf.MoveTowards(vol, target, Time.unscaledDeltaTime * fadeSpeed * Mathf.Max(0.05f, movingVolume));
        src.volume = vol;
        src.pitch = Mathf.Lerp(src.pitch <= 0f ? 1f : src.pitch, moving ? movingPitch : 1f, Time.unscaledDeltaTime * 3f);

        if (vol > 0.0005f && !src.isPlaying) src.Play();
        else if (vol <= 0.0005f && src.isPlaying) src.Pause();
    }
}
