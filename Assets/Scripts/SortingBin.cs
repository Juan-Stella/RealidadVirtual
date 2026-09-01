using UnityEngine;

/// <summary>
/// Contenedor de clasificacion. Acepta piezas de su categoria y avisa al TaskManager.
/// La luz indicadora da feedback inmediato: verde si acerto, rojo si se equivoco.
/// </summary>
[RequireComponent(typeof(BoxCollider))]
public class SortingBin : MonoBehaviour
{
    [Tooltip("Categoria que acepta este contenedor.")]
    public int kind;

    [Tooltip("Objeto que hace de luz indicadora (se le cambia el color).")]
    public Renderer indicator;

    public Color idleColor = new Color(0.25f, 0.25f, 0.28f);
    public Color okColor = new Color(0.15f, 0.9f, 0.3f);
    public Color errorColor = new Color(0.95f, 0.2f, 0.2f);

    [Tooltip("Segundos que la luz queda encendida tras un evento.")]
    public float flashSeconds = 1.2f;

    public System.Action<SortingBin, Graspable, bool> OnPieceDropped;

    float flashUntil;

    static readonly int ColorId = Shader.PropertyToID("_Color");
    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

    void Start()
    {
        var bc = GetComponent<BoxCollider>();
        bc.isTrigger = true;
        Light(idleColor);
    }

    void Update()
    {
        if (flashUntil > 0f && Time.time > flashUntil)
        {
            flashUntil = 0f;
            Light(idleColor);
        }
    }

    void OnTriggerEnter(Collider other)
    {
        var g = other.GetComponentInParent<Graspable>();
        if (g == null || g.held) return; // solo cuenta si ya la solto

        bool correct = g.kind == kind;
        Light(correct ? okColor : errorColor);
        flashUntil = Time.time + flashSeconds;

        OnPieceDropped?.Invoke(this, g, correct);
    }

    void Light(Color c)
    {
        if (indicator == null) return;
        var m = indicator.material;
        if (m.HasProperty(BaseColorId)) m.SetColor(BaseColorId, c);
        if (m.HasProperty(ColorId)) m.SetColor(ColorId, c);
        if (m.HasProperty(EmissionId)) m.SetColor(EmissionId, c * 0.8f);
    }
}
