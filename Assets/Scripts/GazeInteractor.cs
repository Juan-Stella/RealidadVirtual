using UnityEngine;

/// <summary>
/// Interaccion por MIRADA: la forma nativa de interactuar en Cardboard.
///
/// Cardboard no tiene controles de mano y, como mucho, un solo boton en el visor.
/// El patron estandar de la plataforma es apuntar con la cabeza y CONFIRMAR POR
/// PERMANENCIA (dwell): si mantenes la mirada sobre algo durante un tiempo, se
/// activa. Un anillo que se completa da el feedback de cuanto falta.
///
/// Sumarlo tiene dos ventajas concretas:
///  - el proyecto se puede usar sin gamepad, solo con el casco
///  - demuestra la interaccion propia de la plataforma, no una portada de escritorio
///
/// El puntero se dibuja SIEMPRE a distancia fija de la camara. Si se pegara al
/// objeto mirado, cambiaria de profundidad todo el tiempo y forzaria a los ojos a
/// reenfocar sin parar: molesto y cansador en estereo.
/// </summary>
public class GazeInteractor : MonoBehaviour
{
    [Header("Referencias")]
    public Camera playerCamera;

    [Header("Puntero")]
    [Tooltip("Distancia fija del puntero, en metros.")]
    public float reticleDistance = 2f;

    [Tooltip("Radio del punto central, en metros a la distancia del puntero.")]
    public float reticleRadius = 0.012f;

    public Color idleColor = new Color(1f, 1f, 1f, 0.5f);
    public Color hoverColor = new Color(1f, 0.85f, 0.2f, 0.95f);

    [Header("Confirmacion por permanencia")]
    [Tooltip("Segundos que hay que sostener la mirada para activar.")]
    public float dwellSeconds = 1.2f;

    [Tooltip("Alcance del rayo, en metros.")]
    public float maxDistance = 12f;
    public LayerMask InteractionMask = ~0;

    public GazeButton Current { get; private set; }
    public float Progress { get; private set; }   // 0..1

    Transform ring;
    Transform dot;
    Material dotMat, ringMat;
    float held;
    float _distance;
    bool _waitForExit;   // tras activar, hay que sacar la mirada antes de volver a activar

    static readonly int ColorId = Shader.PropertyToID("_Color");
    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    void Start()
    {
        if (playerCamera == null) playerCamera = Camera.main;
        Build();
    }

    void Build()
    {
        if (playerCamera == null) return;

        dot = MakeQuad("GazeDot", reticleRadius * 2f, out dotMat);
        ring = MakeQuad("GazeRing", reticleRadius * 5f, out ringMat);
        if (ring != null) ring.localScale = Vector3.zero;
        if (dot != null) dot.GetComponent<Renderer>().sortingOrder = 5001; // el punto sobre el anillo
    }

    Transform MakeQuad(string name, float size, out Material mat)
    {
        mat = null;
        // Encima de todo (como el texto de la interfaz): si no, los paneles del HUD, del menu y
        // del documento CONTROLES tapan el puntero justo cuando se mira un boton.
        var sh = Resources.Load<Shader>("FrankaVRUIText");
        if (sh == null) sh = FindShader("Sprites/Default", "UI/Default", "Unlit/Transparent");
        if (sh == null) return null;

        var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = name;
        go.GetComponent<Collider>().enabled = false;
        Destroy(go.GetComponent<Collider>());   // el puntero no debe chocar con nada

        var t = go.transform;
        t.SetParent(playerCamera.transform, false);
        t.localPosition = new Vector3(0f, 0f, reticleDistance);
        t.localRotation = Quaternion.identity;
        t.localScale = Vector3.one * size;

        mat = new Material(sh);
        Renderer renderer = go.GetComponent<Renderer>();
        renderer.material = mat;
        renderer.sortingOrder = 5000;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return t;
    }

    void Update()
    {
        if (playerCamera == null) return;

        var cam = playerCamera.transform;
        GazeButton hit = null;
        bool mobile = Application.platform == RuntimePlatform.Android;
        Ray ray = mobile ? new Ray(cam.position, cam.forward) : playerCamera.ScreenPointToRay(Input.mousePosition);
        float targetDistance = reticleDistance;
        if (Physics.Raycast(ray, out RaycastHit info, maxDistance, InteractionMask, QueryTriggerInteraction.Collide))
        {
            hit = info.collider.GetComponentInParent<GazeButton>();
            if (hit != null) targetDistance = info.distance;
        }
        // Sin boton: a la profundidad de lo que se mira (el puntero se dibuja encima de todo y,
        // si quedara mas lejos que el objeto, los dos ojos no lo fusionan bien).
        if (mobile && hit == null && Physics.Raycast(ray, out RaycastHit world, reticleDistance, ~0, QueryTriggerInteraction.Ignore))
            targetDistance = world.distance;
        targetDistance = Mathf.Clamp(targetDistance - 0.01f, 0.3f, reticleDistance);
        _distance = _distance <= 0f ? targetDistance
            : Mathf.Lerp(_distance, targetDistance, 1f - Mathf.Exp(-20f * Time.deltaTime));

        if (hit != Current)
        {
            if (Current != null) Current.OnGazeExit();
            Current = hit;
            held = 0f;
            _waitForExit = false;
            if (Current != null) Current.OnGazeEnter();
        }

        if (Current != null && Current.isActiveAndEnabled)
        {
            // Una activacion por mirada: quedarse mirando no repite (CONTROLES abria y cerraba).
            held += mobile && !_waitForExit ? Time.deltaTime : 0f;
            Progress = Mathf.Clamp01(held / Mathf.Max(0.05f, dwellSeconds));
            bool confirm = !mobile && Input.GetMouseButtonDown(0);
#if UNITY_ANDROID && !UNITY_EDITOR
            confirm |= Google.XR.Cardboard.Api.IsTriggerPressed;
#endif
            if (Progress >= 1f || confirm)
            {
                Current.Activate();
                held = 0f;
                Progress = 0f;
                _waitForExit = true;
            }
        }
        else Progress = 0f;
        if (dot != null) dot.gameObject.SetActive(mobile);
        if (ring != null) ring.gameObject.SetActive(mobile);

        Paint();
    }

    void OnDestroy()
    {
        if (dot != null) SafeDestroy(dot.gameObject);
        if (ring != null) SafeDestroy(ring.gameObject);
        if (dotMat != null) SafeDestroy(dotMat);
        if (ringMat != null) SafeDestroy(ringMat);
    }

    static void SafeDestroy(Object value)
    {
        if (Application.isPlaying) Destroy(value);
        else DestroyImmediate(value);
    }

    void Paint()
    {
        if (dotMat != null) SetColor(dotMat, Current != null ? hoverColor : idleColor);
        // Mismo tamano aparente a cualquier distancia.
        float d = _distance > 0f ? _distance : reticleDistance, k = d / Mathf.Max(0.01f, reticleDistance);
        if (dot != null)
        {
            dot.localPosition = new Vector3(0f, 0f, d);
            dot.localScale = Vector3.one * (reticleRadius * 2f * k);
        }

        if (ring != null)
        {
            // El anillo crece a medida que se completa: barra de progreso circular.
            float s = (Current != null) ? Mathf.Lerp(0.2f, 1f, Progress) : 0f;
            ring.localPosition = new Vector3(0f, 0f, d);
            ring.localScale = Vector3.one * (reticleRadius * 5f * s * k);
            if (ringMat != null) SetColor(ringMat, hoverColor);
        }
    }

    static void SetColor(Material m, Color c)
    {
        if (m.HasProperty(BaseColorId)) m.SetColor(BaseColorId, c);
        if (m.HasProperty(ColorId)) m.SetColor(ColorId, c);
    }

    static Shader FindShader(params string[] names)
    {
        foreach (var n in names) { var s = Shader.Find(n); if (s != null) return s; }
        return null;
    }
}
