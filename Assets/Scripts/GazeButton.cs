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
    MaterialPropertyBlock colors;

    public static GazeButton CreateUI(Transform parent, string text, Vector3 position, float width,
        System.Action action, Material background)
    {
        GameObject root = new GameObject("Boton_" + text);
        root.layer = 5;
        root.transform.SetParent(parent, false);
        root.transform.localPosition = position;
        BoxCollider collider = root.AddComponent<BoxCollider>();
        collider.isTrigger = true;
        collider.size = new Vector3(width, 0.055f, 0.02f);
        GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.layer = 5;
        quad.transform.SetParent(root.transform, false);
        quad.transform.localScale = new Vector3(width, 0.055f, 1f);
        Collider old = quad.GetComponent<Collider>();
        old.enabled = false;
        Destroy(old);
        Renderer renderer = quad.GetComponent<Renderer>();
        renderer.sharedMaterial = background;
        renderer.sortingOrder = -5;
        GazeButton button = root.AddComponent<GazeButton>();
        button.label = text;
        button.highlight = renderer;
        button.idleColor = new Color(0.08f, 0.24f, 0.30f, 0.95f);
        button.onActivateAction = action;
        GameObject caption = new GameObject("Etiqueta");
        caption.transform.SetParent(root.transform, false);
        caption.transform.localPosition = new Vector3(0f, 0f, -0.012f);
        TextMesh mesh = caption.AddComponent<TextMesh>();
        mesh.text = text;
        mesh.anchor = TextAnchor.MiddleCenter;
        mesh.alignment = TextAlignment.Center;
        mesh.fontSize = 48;
        mesh.characterSize = 0.007f;
        Renderer captionRenderer = caption.GetComponent<Renderer>();
        captionRenderer.sortingOrder = 10;
        Bounds b = captionRenderer.localBounds;
        caption.transform.localScale = Vector3.one * Mathf.Min(1f, (width - 0.02f) / Mathf.Max(0.001f, b.size.x),
            0.034f / Mathf.Max(0.001f, b.size.y));
        return button;
    }

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
        var m = highlight.sharedMaterial;
        if (m == null) return;
        if (colors == null) colors = new MaterialPropertyBlock();
        highlight.GetPropertyBlock(colors);
        if (m.HasProperty(BaseColorId)) colors.SetColor(BaseColorId, c);
        if (m.HasProperty(ColorId)) colors.SetColor(ColorId, c);
        highlight.SetPropertyBlock(colors);
    }
}
