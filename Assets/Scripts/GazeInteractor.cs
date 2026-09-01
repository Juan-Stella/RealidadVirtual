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

    public GazeButton Current { get; private set; }
    public float Progress { get; private set; }   // 0..1

    Transform ring;
    Material dotMat, ringMat;
    float held;

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

        MakeQuad("GazeDot", reticleRadius * 2f, out dotMat);
        ring = MakeQuad("GazeRing", reticleRadius * 5f, out ringMat);
        if (ring != null) ring.localScale = Vector3.zero;
    }

    Transform MakeQuad(string name, float size, out Material mat)
    {
        mat = null;
        var sh = FindShader("Sprites/Default", "UI/Default", "Unlit/Transparent");
        if (sh == null) return null;

        var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = name;
        Destroy(go.GetComponent<Collider>());   // el puntero no debe chocar con nada

        var t = go.transform;
        t.SetParent(playerCamera.transform, false);
        t.localPosition = new Vector3(0f, 0f, reticleDistance);
        t.localRotation = Quaternion.identity;
        t.localScale = Vector3.one * size;

        mat = new Material(sh);
        go.GetComponent<Renderer>().material = mat;
        return t;
    }

    void Update()
    {
        if (playerCamera == null) return;

        var cam = playerCamera.transform;
        GazeButton hit = null;

        if (Physics.Raycast(cam.position, cam.forward, out RaycastHit info, maxDistance))
            hit = info.collider.GetComponentInParent<GazeButton>();

        if (hit != Current)
        {
            if (Current != null) Current.OnGazeExit();
            Current = hit;
            held = 0f;
            if (Current != null) Current.OnGazeEnter();
        }

        if (Current != null && Current.isActiveAndEnabled)
        {
            held += Time.deltaTime;
            Progress = Mathf.Clamp01(held / Mathf.Max(0.05f, dwellSeconds));

            if (Progress >= 1f)
            {
                Current.Activate();
                held = 0f;
                Progress = 0f;
            }
        }
        else Progress = 0f;

        Paint();
    }

    void Paint()
    {
        if (dotMat != null) SetColor(dotMat, Current != null ? hoverColor : idleColor);

        if (ring != null)
        {
            // El anillo crece a medida que se completa: barra de progreso circular.
            float s = (Current != null) ? Mathf.Lerp(0.2f, 1f, Progress) : 0f;
            ring.localScale = Vector3.one * (reticleRadius * 5f * s);
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
