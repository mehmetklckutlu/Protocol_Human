using UnityEngine;
using UnityEngine.Tilemaps;

public class TacticalCamera : MonoBehaviour
{
    [Header("Karaktere Odaklanma (Focus)")]
    [SerializeField] private Transform targetToFocus;
    [SerializeField] private KeyCode focusKey = KeyCode.Space;
    [SerializeField] private float focusSmoothTime = 0.25f;

    [Header("Kamera Kaydırma Hızı")]
    [SerializeField] private float panSpeed = 15f;
    [SerializeField] private float edgeBorderThickness = 20f;
    [SerializeField] private bool useEdgeScrolling = true;

    [Header("Zoom (Yakınlaşma) Ayarları")]
    [SerializeField] private float zoomSpeed = 5f;
    [SerializeField] private float minZoom = 3f;
    [SerializeField] private float maxZoom = 15f;

    [Header("Harita Sınırları (Tilemap)")]
    [Tooltip("Zemin Tilemap'i. Kamera merkezi sadece boyalı tile'ların üzerinde gezebilir.")]
    [SerializeField] private Tilemap boundsTilemap;
    [Tooltip("Tile'ların dışına ne kadar taşılabileceği (dünya birimi). 0 = tam tile üzerinde kalır.")]
    [SerializeField] private float edgeMargin = 0f;

    public static TacticalCamera Instance { get; private set; }
    private bool keepFollowing = false; // sırası gelen birimi sürekli takip et (oyuncu müdahale edene kadar)

    private Camera cam;
    private Vector3 dragOrigin;
    private bool isDragging = false;
    private bool isFocusing = false;
    private Vector3 currentVelocity = Vector3.zero;

    // Boyalı her tile'ın dünya uzayındaki dikdörtgeni
    private readonly System.Collections.Generic.List<Rect> tileRects = new System.Collections.Generic.List<Rect>();

    private void Awake()
    {
        Instance = this;
        cam = GetComponent<Camera>();
        RecalculateBounds();
    }

    // Tilemap'e sonradan tile eklenir/silinirse bunu çağır
    public void RecalculateBounds()
    {
        tileRects.Clear();
        if (boundsTilemap == null) return;

        boundsTilemap.CompressBounds();
        BoundsInt cells = boundsTilemap.cellBounds;

        foreach (Vector3Int cell in cells.allPositionsWithin)
        {
            if (!boundsTilemap.HasTile(cell)) continue;

            Vector3 min = boundsTilemap.CellToWorld(cell);
            Vector3 max = boundsTilemap.CellToWorld(cell + new Vector3Int(1, 1, 0));
            Vector2 lo = new Vector2(Mathf.Min(min.x, max.x), Mathf.Min(min.y, max.y));
            Vector2 hi = new Vector2(Mathf.Max(min.x, max.x), Mathf.Max(min.y, max.y));
            tileRects.Add(new Rect(lo.x - edgeMargin, lo.y - edgeMargin,
                                   hi.x - lo.x + edgeMargin * 2f, hi.y - lo.y + edgeMargin * 2f));
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(focusKey) && targetToFocus != null)
        {
            keepFollowing = false;
            isFocusing = true;
        }

        HandleMouseDrag();

        if (isFocusing)
        {
            SmoothFollowTarget();
        }
        else if (!isDragging)
        {
            HandleEdgeAndKeyMovement();
        }

        HandleZoom();
        ClampCameraPosition();
    }

    private void SmoothFollowTarget()
    {
        if (targetToFocus == null)
        {
            isFocusing = false;
            return;
        }

        Vector3 targetPos = new Vector3(targetToFocus.position.x, targetToFocus.position.y, transform.position.z);
        transform.position = Vector3.SmoothDamp(transform.position, targetPos, ref currentVelocity, focusSmoothTime);

        if (!keepFollowing && Vector2.Distance(transform.position, targetPos) < 0.05f)
        {
            isFocusing = false;
        }
    }

    private void HandleMouseDrag()
    {
        if (Input.GetMouseButtonDown(2))
        {
            dragOrigin = cam.ScreenToWorldPoint(Input.mousePosition);
            isDragging = true;
            isFocusing = false;
            keepFollowing = false;
        }

        if (Input.GetMouseButton(2))
        {
            Vector3 difference = dragOrigin - cam.ScreenToWorldPoint(Input.mousePosition);
            transform.position += difference;
        }

        if (Input.GetMouseButtonUp(2))
        {
            isDragging = false;
        }
    }

    private void HandleEdgeAndKeyMovement()
    {
        Vector3 direction = Vector3.zero;

        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) direction.y += 1;
        if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) direction.y -= 1;
        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) direction.x -= 1;
        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) direction.x += 1;

        if (useEdgeScrolling)
        {
            if (Input.mousePosition.y >= Screen.height - edgeBorderThickness) direction.y += 1;
            if (Input.mousePosition.y <= edgeBorderThickness) direction.y -= 1;
            if (Input.mousePosition.x <= edgeBorderThickness) direction.x -= 1;
            if (Input.mousePosition.x >= Screen.width - edgeBorderThickness) direction.x += 1;
        }

        if (direction != Vector3.zero)
        {
            isFocusing = false;
            keepFollowing = false;
            transform.position += direction.normalized * (panSpeed * Time.deltaTime);
        }
    }

    private void HandleZoom()
    {
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scroll) > 0.01f)
        {
            cam.orthographicSize -= scroll * zoomSpeed;
            cam.orthographicSize = Mathf.Clamp(cam.orthographicSize, minZoom, maxZoom);
        }
    }

    // Kamera merkezi boyalı tile'ların dışına çıkamaz; çıkarsa en yakın tile'a geri çekilir
    private void ClampCameraPosition()
    {
        Vector3 pos = transform.position;

        if (tileRects.Count > 0)
        {
            Vector2 p = new Vector2(pos.x, pos.y);
            float bestSqr = float.MaxValue;
            Vector2 best = p;

            for (int i = 0; i < tileRects.Count; i++)
            {
                Rect r = tileRects[i];
                Vector2 q = new Vector2(Mathf.Clamp(p.x, r.xMin, r.xMax), Mathf.Clamp(p.y, r.yMin, r.yMax));
                float sqr = (q - p).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = q;
                    if (sqr == 0f) break; // zaten bir tile'ın üzerinde
                }
            }

            pos.x = best.x;
            pos.y = best.y;
        }

        pos.z = -10f;
        transform.position = pos;
    }

    // Sırası gelen birime kamerayı götürür ve hareket ederken takip eder
    public void FollowUnit(Transform t)
    {
        if (t == null) return;
        targetToFocus = t;
        isFocusing = true;
        keepFollowing = true;
    }

    public void SetTarget(Transform newTarget)
    {
        targetToFocus = newTarget;
        isFocusing = true;
    }

#if UNITY_EDITOR
    // Seçiliyken kameranın gezebileceği tile'ları sarı çerçeveyle gösterir
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        foreach (Rect r in tileRects)
            Gizmos.DrawWireCube(r.center, new Vector3(r.width, r.height, 0f));
    }
#endif
}