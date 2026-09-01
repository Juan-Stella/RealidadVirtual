using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Boton que se activa mirandolo (ver GazeInteractor).
/// Necesita un Collider para que el rayo lo detecte.
/// </summary>
[RequireComponent(typeof(Collider))]
public class GazeButton : MonoBehaviour
{
    public string label = "Boton";

    [Tooltip("Objeto al que se le cambia el color al mirarlo.")]
    public Renderer highlight;

    public Color idleColor = new Color(0.25f, 0.28f, 0.32f);
    public Color hoverColor = new Color(0.95f, 0.75f, 0.15f);

    public UnityEvent onActivate;

    /// <summary>Accion por codigo. La usa la celda para enganchar sin la API del editor.</summary>
    public System.Action onActivateAction;

    static readonly int ColorId = Shader.PropertyToID("_Color");
    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    void Start() => Paint(idleColor);

    public void OnGazeEnter() => Paint(hoverColor);
    public void OnGazeExit() => Paint(idleColor);

    public void Activate()
    {
        onActivate?.Invoke();
        onActivateAction?.Invoke();
        Paint(idleColor);
    }

    void Paint(Color c)
    {
        if (highlight == null) return;
        var m = highlight.material;
        if (m.HasProperty(BaseColorId)) m.SetColor(BaseColorId, c);
        if (m.HasProperty(ColorId)) m.SetColor(ColorId, c);
    }
}
