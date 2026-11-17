using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

/// <summary>
/// Locomocion confortable para Cardboard mediante estaciones.
///
/// IMPORTANTE: NO escribe la rotacion de la Camera cada frame. En XR la camara pertenece
/// al head tracker. Este componente mueve un RIG PADRE para cambiar de puesto y deja que
/// Cardboard controle libremente hacia donde mira la cabeza.
/// </summary>
public class ViewpointManager : MonoBehaviour
{
    [System.Serializable]
    public class Station
    {
        public string label = "Estacion";
        public Transform anchor;
        [Tooltip("Si se asigna, al entrar se orienta el CUERPO/rig hacia este objetivo. La cabeza sigue libre despues.")]
        public Transform lookAtTarget;
    }

    [Header("Rig XR")]
    public Camera playerCamera;
    [Tooltip("Padre que se mueve entre estaciones. Debe ser el padre de la camara XR, no la camara misma.")]
    public Transform rigRoot;
    [Tooltip("Si la camara no tiene padre, crea automaticamente CardboardRigRoot.")]
    public bool AutoCreateRigRoot = true;
    [Tooltip("Recentra el tracker al cambiar de estacion si el plugin Cardboard esta instalado.")]
    public bool RecenterOnStationChange = false;

    [Header("Estaciones")]
    public List<Station> stations = new List<Station>();

    [Header("Fundido de confort")]
    public float fadeSeconds = 0.16f;

    [Header("Controles de prueba")]
    public bool KeyboardControls = true;
    public bool GamepadControls = true;

    public int Index { get; private set; }
    public string CurrentLabel =>
        (stations.Count > 0 && Index >= 0 && Index < stations.Count) ? stations[Index].label : "";

    Transform _fadeQuad;
    Material _fadeMat;
    float _fadeT = 1f;
    int _pendingIndex = -1;
    static readonly int ColorId = Shader.PropertyToID("_Color");

    void Awake()
    {
        if (playerCamera == null) playerCamera = Camera.main;
        EnsureRigRoot();
    }

    void Start()
    {
        BuildFadeQuad();
        if (stations.Count > 0) ApplyImmediate(0);
    }

    void Update()
    {
        if (GamepadControls)
        {
            var gp = Gamepad.current;
            if (gp != null && gp.buttonWest.wasPressedThisFrame) Next();
        }

        if (KeyboardControls)
        {
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.nKey.wasPressedThisFrame || kb.rightArrowKey.wasPressedThisFrame) Next();
                if (kb.pKey.wasPressedThisFrame || kb.leftArrowKey.wasPressedThisFrame) Previous();
                if (kb.cKey.wasPressedThisFrame) CardboardRecenterProxy.TryRecenter();
            }
        }

        UpdateFade();
    }

    public void EnsureRigRoot()
    {
        if (playerCamera == null) return;
        if (rigRoot != null) return;

        if (playerCamera.transform.parent != null)
        {
            rigRoot = playerCamera.transform.parent;
            return;
        }

        if (!AutoCreateRigRoot) return;

        GameObject go = new GameObject("CardboardRigRoot");
        Transform t = go.transform;
        t.position = playerCamera.transform.position;
        t.rotation = Quaternion.Euler(0f, playerCamera.transform.eulerAngles.y, 0f);
        playerCamera.transform.SetParent(t, true);
        rigRoot = t;
    }

    public void Next()
    {
        if (stations.Count < 2) return;
        GoTo((Index + 1) % stations.Count);
    }

    public void Previous()
    {
        if (stations.Count < 2) return;
        GoTo((Index - 1 + stations.Count) % stations.Count);
    }

    public void GoTo(int index)
    {
        if (index < 0 || index >= stations.Count) return;
        if (_fadeMat == null || fadeSeconds <= 0f)
        {
            ApplyImmediate(index);
            return;
        }

        _pendingIndex = index;
        _fadeT = 1f;
    }

    void UpdateFade()
    {
        if (_fadeMat == null) return;

        if (_pendingIndex >= 0)
        {
            _fadeT -= Time.unscaledDeltaTime / Mathf.Max(0.01f, fadeSeconds);
            if (_fadeT <= 0f)
            {
                _fadeT = 0f;
                int i = _pendingIndex;
                _pendingIndex = -1;
                ApplyImmediate(i);
            }
        }
        else if (_fadeT < 1f)
        {
            _fadeT += Time.unscaledDeltaTime / Mathf.Max(0.01f, fadeSeconds);
            if (_fadeT > 1f) _fadeT = 1f;
        }

        SetFadeAlpha(1f - _fadeT);
    }

    public void SnapTo(int i) => ApplyImmediate(i);

    void ApplyImmediate(int i)
    {
        if (i < 0 || i >= stations.Count) return;
        EnsureRigRoot();
        if (rigRoot == null || playerCamera == null) return;

        Station st = stations[i];
        if (st.anchor == null) return;
        Index = i;

        float yaw = st.anchor.eulerAngles.y;
        if (st.lookAtTarget != null)
        {
            Vector3 d = st.lookAtTarget.position - st.anchor.position;
            d.y = 0f;
            if (d.sqrMagnitude > 1e-6f)
                yaw = Quaternion.LookRotation(d.normalized, Vector3.up).eulerAngles.y;
        }

        Quaternion targetRigRot = Quaternion.Euler(0f, yaw, 0f);

        // Offset de cabeza expresado en el marco del rig. Funciona incluso si la camara
        // no es hija directa (por ejemplo XR Origin -> Camera Offset -> Main Camera).
        Vector3 localHead = rigRoot.InverseTransformPoint(playerCamera.transform.position);
        Vector3 rootPos = st.anchor.position - targetRigRot * localHead;
        rigRoot.SetPositionAndRotation(rootPos, targetRigRot);

        if (RecenterOnStationChange)
            CardboardRecenterProxy.TryRecenter();
    }

    void BuildFadeQuad()
    {
        if (playerCamera == null) return;
        Shader shader = FindShader("Sprites/Default", "UI/Default", "Unlit/Transparent");
        if (shader == null) return;

        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = "CardboardFadeQuad";
        Collider c = go.GetComponent<Collider>();
        if (c != null) Destroy(c);

        _fadeQuad = go.transform;
        _fadeQuad.SetParent(playerCamera.transform, false);
        _fadeQuad.localPosition = new Vector3(0f, 0f, playerCamera.nearClipPlane + 0.03f);
        _fadeQuad.localRotation = Quaternion.identity;
        _fadeQuad.localScale = Vector3.one * 1.2f;

        _fadeMat = new Material(shader);
        go.GetComponent<Renderer>().material = _fadeMat;
        SetFadeAlpha(0f);
    }

    void SetFadeAlpha(float a)
    {
        if (_fadeMat == null) return;
        _fadeMat.SetColor(ColorId, new Color(0f, 0f, 0f, Mathf.Clamp01(a)));
        if (_fadeQuad != null) _fadeQuad.gameObject.SetActive(a > 0.001f);
    }

    static Shader FindShader(params string[] names)
    {
        foreach (string n in names)
        {
            Shader s = Shader.Find(n);
            if (s != null) return s;
        }
        return null;
    }
}
