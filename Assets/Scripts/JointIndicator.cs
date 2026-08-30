using UnityEngine;

/// <summary>
/// Indicador visible DENTRO de VR. En el casco no se ve la consola ni el inspector,
/// asi que hace falta feedback en la escena. Hace dos cosas:
///
///   1. Panel de texto flotante anclado a la camara (world space, no screen space:
///      un Canvas en screen space NO se ve bien en estereo).
///   2. Tinta de color el eslabon de la junta seleccionada.
///
/// Usa TextMesh (componente legacy incorporado) para no depender de TextMeshPro.
/// Ponelo en cualquier GameObject de la escena y asignale las referencias.
/// </summary>
public class JointIndicator : MonoBehaviour
{
    [Header("Referencias")]
    public PandaArm arm;
    public GamepadDriver driver;
    public PandaIK ik;

    [Tooltip("Camara del jugador. Si queda vacio, usa Camera.main.")]
    public Camera playerCamera;

    [Header("Panel HUD")]
    [Tooltip("Distancia del panel frente a la camara, en metros. 1-2 m es comodo en VR.")]
    public float hudDistance = 1.4f;

    [Tooltip("Desplazamiento vertical respecto al centro de la vista.")]
    public float hudVerticalOffset = -0.45f;

    public int fontSize = 60;
    public float characterSize = 0.012f;
    public Color textColor = new Color(0.95f, 0.95f, 1f);

    [Header("Resaltado de junta")]
    public bool highlightSelectedJoint = true;
    public Color highlightColor = new Color(1f, 0.85f, 0.2f);

    TextMesh text;
    Transform hud;

    // Materiales originales para poder restaurarlos.
    // Built-in usa "_Color"; URP usa "_BaseColor". Soportamos ambos para que el
    // resaltado siga funcionando si el proyecto migra de pipeline.
    static readonly int ColorId = Shader.PropertyToID("_Color");
    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    static bool Tintable(Material m) =>
        m != null && (m.HasProperty(ColorId) || m.HasProperty(BaseColorId));

    static Color ReadColor(Material m) =>
        m.HasProperty(BaseColorId) ? m.GetColor(BaseColorId) : m.GetColor(ColorId);

    static void WriteColor(Material m, Color c)
    {
        if (m.HasProperty(BaseColorId)) m.SetColor(BaseColorId, c);
        if (m.HasProperty(ColorId)) m.SetColor(ColorId, c);
    }

    Renderer[][] jointRenderers;
    Color[][] originalColors;
    int highlighted = -1;

    void Start()
    {
        if (playerCamera == null) playerCamera = Camera.main;
        BuildHud();
        if (highlightSelectedJoint) CacheRenderers();
    }

    void BuildHud()
    {
        var go = new GameObject("HUD_Text");
        hud = go.transform;

        text = go.AddComponent<TextMesh>();
        text.anchor = TextAnchor.MiddleCenter;
        text.alignment = TextAlignment.Center;
        text.fontSize = fontSize;
        text.characterSize = characterSize;
        text.color = textColor;

        // El shader de fuente por defecto no escribe profundidad; en VR conviene
        // que el panel se dibuje encima de la escena para que siempre se lea.
        var mr = go.GetComponent<MeshRenderer>();
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
    }

    /// <summary>Guarda los renderers de cada eslabon, sin invadir los eslabones hijos.</summary>
    void CacheRenderers()
    {
        jointRenderers = new Renderer[PandaArm.N][];
        originalColors = new Color[PandaArm.N][];

        for (int i = 0; i < PandaArm.N; i++)
        {
            if (arm == null || arm.joints[i] == null)
            {
                jointRenderers[i] = new Renderer[0];
                originalColors[i] = new Color[0];
                continue;
            }

            var list = new System.Collections.Generic.List<Renderer>();
            Collect(arm.joints[i].transform, arm.joints[i].transform, list);

            // Solo conservamos renderers cuyo material tenga color tintable.
            // Los materiales importados desde .dae no siempre usan el shader Standard.
            var tintable = new System.Collections.Generic.List<Renderer>();
            var colors = new System.Collections.Generic.List<Color>();
            foreach (var r in list)
            {
                var m = r.material; // instancia el material (copia por renderer)
                if (Tintable(m))
                {
                    tintable.Add(r);
                    colors.Add(ReadColor(m));
                }
            }

            jointRenderers[i] = tintable.ToArray();
            originalColors[i] = colors.ToArray();
        }
    }

    /// <summary>Recorre hijos pero se detiene al cruzar a otro ArticulationBody.</summary>
    static void Collect(Transform root, Transform t, System.Collections.Generic.List<Renderer> outList)
    {
        if (t != root && t.GetComponent<ArticulationBody>() != null) return;

        var r = t.GetComponent<Renderer>();
        if (r != null) outList.Add(r);

        for (int i = 0; i < t.childCount; i++) Collect(root, t.GetChild(i), outList);
    }

    void LateUpdate()
    {
        if (playerCamera == null || text == null) return;

        // Anclar el panel frente a la camara, encarado hacia el usuario.
        Transform cam = playerCamera.transform;
        hud.position = cam.position + cam.forward * hudDistance + cam.up * hudVerticalOffset;
        hud.rotation = Quaternion.LookRotation(hud.position - cam.position, cam.up);

        text.text = BuildLabel();

        if (highlightSelectedJoint) UpdateHighlight();
    }

    string BuildLabel()
    {
        if (driver == null || arm == null) return "";

        if (driver.CurrentMode == GamepadDriver.Mode.Joint)
        {
            int j = driver.SelectedJoint;
            return $"MODO: JUNTA\nJ{j + 1}  =  {arm.CurrentDeg(j),6:F1}\u00B0\n"
                 + $"[{PandaArm.Limits[j].x:F0}\u00B0 .. {PandaArm.Limits[j].y:F0}\u00B0]";
        }

        string err = ik != null ? $"{ik.ErrorMeters * 100f:F1} cm" : "-";
        string state = (ik != null && ik.Reached) ? "ALCANZADO" : "moviendo...";
        return $"MODO: IK (punta)\nerror: {err}\n{state}";
    }

    void UpdateHighlight()
    {
        int want = (driver.CurrentMode == GamepadDriver.Mode.Joint) ? driver.SelectedJoint : -1;
        if (want == highlighted) return;

        Restore(highlighted);
        if (want >= 0) Tint(want, highlightColor);
        highlighted = want;
    }

    void Tint(int i, Color c)
    {
        if (jointRenderers == null || i < 0 || i >= jointRenderers.Length) return;
        foreach (var r in jointRenderers[i]) WriteColor(r.material, c);
    }

    void Restore(int i)
    {
        if (jointRenderers == null || i < 0 || i >= jointRenderers.Length) return;
        for (int k = 0; k < jointRenderers[i].Length; k++)
            WriteColor(jointRenderers[i][k].material, originalColors[i][k]);
    }
}
