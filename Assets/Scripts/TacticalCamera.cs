using UnityEngine;

public class TacticalCamera : MonoBehaviour
{
    [Header("Karaktere Odaklanma (Focus)")]
    [SerializeField] private Transform targetToFocus; // Odaklanılacak karakterin Transform'u
    [SerializeField] private KeyCode focusKey = KeyCode.Space;
    [SerializeField] private float focusSmoothTime = 0.25f; // Hedefe varış süresi (sn)

    [Header("Kamera Kaydırma Hızı")]
    [SerializeField] private float panSpeed = 15f;
    [SerializeField] private float edgeBorderThickness = 20f;
    [SerializeField] private bool useEdgeScrolling = true;

    [Header("Zoom (Yakınlaşma) Ayarları")]
    [SerializeField] private float zoomSpeed = 5f;
    [SerializeField] private float minZoom = 3f;
    [SerializeField] private float maxZoom = 15f;

    [Header("Harita Sınırları (Clamp)")]
    [SerializeField] private Vector2 minLimits = new Vector2(-5f, -5f);
    [SerializeField] private Vector2 maxLimits = new Vector2(25f, 25f);

    private Camera cam;
    private Vector3 dragOrigin;
    private bool isDragging = false;
    private bool isFocusing = false;
    private Vector3 currentVelocity = Vector3.zero; // SmoothDamp için hız referansı

    private void Awake()
    {
        cam = GetComponent<Camera>();
    }

    private void Update()
    {
        // 1. Space tuşuna basıldığında odaklanmayı başlat
        if (Input.GetKeyDown(focusKey) && targetToFocus != null)
        {
            isFocusing = true;
        }

        // 2. Farenin orta tuşuyla sürükleme
        HandleMouseDrag();

        // 3. Odaklanma durumu veya manuel kaydırma
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

    // Karakteri yumuşakça merkeze alma
    private void SmoothFollowTarget()
    {
        if (targetToFocus == null)
        {
            isFocusing = false;
            return;
        }

        Vector3 targetPos = new Vector3(targetToFocus.position.x, targetToFocus.position.y, transform.position.z);

        // Pürüzsüz geçiş (SmoothDamp)
        transform.position = Vector3.SmoothDamp(transform.position, targetPos, ref currentVelocity, focusSmoothTime);

        // Hedefe yeterince yaklaştıysa odaklanma modundan çık
        if (Vector2.Distance(transform.position, targetPos) < 0.05f)
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
            isFocusing = false; // Oyuncu haritayı tuttuğu an odaklanmayı kes
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

        // Oyuncu klavye veya fare kenarıyla hareket ettirirse odaklanmayı kes
        if (direction != Vector3.zero)
        {
            isFocusing = false;
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

    private void ClampCameraPosition()
    {
        Vector3 pos = transform.position;
        pos.x = Mathf.Clamp(pos.x, minLimits.x, maxLimits.x);
        pos.y = Mathf.Clamp(pos.y, minLimits.y, maxLimits.y);
        pos.z = -10f;
        transform.position = pos;
    }

    // Kod üzerinden dinamik hedef değiştirmek isterseniz (ör. Tur sırası diğer karaktere geçtiğinde)
    public void SetTarget(Transform newTarget)
    {
        targetToFocus = newTarget;
        isFocusing = true;
    }
}