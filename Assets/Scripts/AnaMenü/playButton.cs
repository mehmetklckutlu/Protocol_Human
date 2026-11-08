using UnityEngine;
using UnityEngine.SceneManagement;

public enum MenuButtonType { Oyna, Cikis }

[RequireComponent(typeof(SpriteRenderer))]
public class playButton : MonoBehaviour
{
    [Header("Buton")]
    public MenuButtonType buttonType = MenuButtonType.Oyna;
    [Tooltip("Oyna butonu bu sahneyi açar. Bulunamazsa Build Settings'teki 1 numaralı sahne açılır.")]
    public string sceneToLoad = "Buyuk Level";

    [Header("Fare Üstündeyken")]
    [SerializeField] private float hoverScale = 1.08f;
    [SerializeField] private Color hoverColor = new Color(0.75f, 1f, 1f, 1f);
    [SerializeField] private float pressedScale = 0.95f;

    private SpriteRenderer sr;
    private Camera cam;
    private Vector3 baseScale;
    private Color baseColor;
    private bool pressed;

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        baseScale = transform.localScale;
        baseColor = sr.color;
    }

    private void Update()
    {
        if (cam == null) cam = Camera.main;
        if (cam == null) return;

        bool hover = IsMouseOver();

        if (hover && Input.GetMouseButtonDown(0)) pressed = true;

        if (Input.GetMouseButtonUp(0))
        {
            if (pressed && hover) Click();
            pressed = false;
        }

        float s = hover ? (pressed ? pressedScale : hoverScale) : 1f;
        transform.localScale = Vector3.Lerp(transform.localScale, baseScale * s, Time.unscaledDeltaTime * 15f);
        sr.color = hover ? hoverColor : baseColor;
    }

    private bool IsMouseOver()
    {
        Vector3 mp = Input.mousePosition;
        mp.z = Mathf.Abs(cam.transform.position.z - transform.position.z);
        Vector3 world = cam.ScreenToWorldPoint(mp);
        Bounds b = sr.bounds;
        world.z = b.center.z;
        return b.Contains(world);
    }

    private void Click()
    {
        if (buttonType == MenuButtonType.Oyna) PlayGame();
        else ExitGame();
    }

    public void PlayGame()
    {
        if (!string.IsNullOrEmpty(sceneToLoad) && Application.CanStreamedLevelBeLoaded(sceneToLoad))
            SceneManager.LoadScene(sceneToLoad);
        else
            SceneManager.LoadScene(1);
    }

    public void ExitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
