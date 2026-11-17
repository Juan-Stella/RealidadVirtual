using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

/// <summary>
/// Estaciones de camara fijas, con fundido a negro al cambiar.
///
/// POR QUE ASI Y NO CAMINANDO:
/// Cardboard da 3 grados de libertad (girar la cabeza), no 6: no hay tracking de
/// posicion. Mover la camara de forma continua sin que el cuerpo se mueva es la
/// receta clasica del mareo (conflicto vestibulo-visual). Saltar entre puntos
/// fijos con un fundido corto evita ese conflicto: es la solucion de confort
/// estandar en VR movil, no un atajo.
///
/// Una de las estaciones solo ROTA para seguir la pinza, sin trasladarse: da
/// seguimiento util para el trabajo fino sin generar movimiento propio.
/// </summary>
public class ViewpointManager : MonoBehaviour
{
    [System.Serializable]
    public class Station
    {
        public string label = "Estacion";
        public Transform anchor;

        [Tooltip("Si esta asignado, la camara ROTA para seguirlo (no se traslada).")]
        public Transform lookAtTarget;
    }

    [Header("Referencias")]
    public Camera playerCamera;
    public List<Station> stations = new List<Station>();

    [Header("Fundido")]
    [Tooltip("Duracion de cada mitad del fundido, en segundos.")]
    public float fadeSeconds = 0.18f;

    public int Index { get; private set; }
    public string CurrentLabel =>
        (stations.Count > 0 && Index < stations.Count) ? stations[Index].label : "";

    Transform fadeQuad;
    Material fadeMat;
    float fadeT = 1f;      // 1 = transparente
    int pendingIndex = -1;

    static readonly int ColorId = Shader.PropertyToID("_Color");

    void Start()
    {
        if (playerCamera == null) playerCamera = Camera.main;
        BuildFadeQuad();
        if (stations.Count > 0) Apply(0);
    }

    void Update()
    {
        var gp = Gamepad.current;
        if (gp != null && gp.buttonWest.wasPressedThisFrame) Next();

        UpdateFade();
        FollowLookAt();
    }

    public void Next()
    {
        if (stations.Count < 2) return;
        pendingIndex = (Index + 1) % stations.Count;
        fadeT = 1f; // arranca el fundido a negro
    }

    void UpdateFade()
    {
        if (fadeMat == null) return;

        if (pendingIndex >= 0)
        {
            // Bajando a negro
            fadeT -= Time.deltaTime / Mathf.Max(0.01f, fadeSeconds);
            if (fadeT <= 0f)
            {
                fadeT = 0f;
                Apply(pendingIndex);
                pendingIndex = -1;
            }
        }
        else if (fadeT < 1f)
        {
            // Volviendo de negro
            fadeT += Time.deltaTime / Mathf.Max(0.01f, fadeSeconds);
            if (fadeT > 1f) fadeT = 1f;
        }

        SetFadeAlpha(1f - fadeT);
    }

    void Apply(int i)
    {
        if (i < 0 || i >= stations.Count) return;
        Index = i;

        var st = stations[i];
        if (st.anchor == null || playerCamera == null) return;

        playerCamera.transform.SetPositionAndRotation(st.anchor.position, st.anchor.rotation);
    }

    /// <summary>Solo rota hacia el objetivo; nunca traslada la camara.</summary>
    void FollowLookAt()
    {
        if (stations.Count == 0 || Index >= stations.Count) return;
        var st = stations[Index];
        if (st.lookAtTarget == null || playerCamera == null) return;

        Vector3 dir = st.lookAtTarget.position - playerCamera.transform.position;
        if (dir.sqrMagnitude < 0.0001f) return;

        // Suave, para no dar tirones bruscos a la vista.
        playerCamera.transform.rotation = Quaternion.Slerp(
            playerCamera.transform.rotation,
            Quaternion.LookRotation(dir, Vector3.up),
            Time.deltaTime * 3f);
    }

    void BuildFadeQuad()
    {
        if (playerCamera == null) return;

        var shader = FindShader("Sprites/Default", "UI/Default", "Unlit/Transparent");
        if (shader == null) return; // sin shader adecuado: corte seco, sin fundido

        var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = "FadeQuad";
        Destroy(go.GetComponent<Collider>());

        fadeQuad = go.transform;
        fadeQuad.SetParent(playerCamera.transform, false);
        fadeQuad.localPosition = new Vector3(0f, 0f, playerCamera.nearClipPlane + 0.02f);
        fadeQuad.localRotation = Quaternion.identity;
        fadeQuad.localScale = Vector3.one * 0.5f;

        fadeMat = new Material(shader);
        go.GetComponent<Renderer>().material = fadeMat;
        SetFadeAlpha(0f);
    }

    void SetFadeAlpha(float a)
    {
        if (fadeMat == null) return;
        fadeMat.SetColor(ColorId, new Color(0f, 0f, 0f, Mathf.Clamp01(a)));
        if (fadeQuad != null) fadeQuad.gameObject.SetActive(a > 0.001f);
    }

    static Shader FindShader(params string[] names)
    {
        foreach (var n in names)
        {
            var s = Shader.Find(n);
            if (s != null) return s;
        }
        return null;
    }
}
