#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

/// <summary>
/// Genera el entorno virtual de prueba con un click:
/// Franka > Build Test Environment.
/// Usa primitivas (barato para VR en celular). Render pipeline: Built-in (shader Standard).
/// Después de generarlo, arrastrá el Panda importado a la escena, posicionalo
/// mirando hacia la mesa, y asigná sus 7 juntas en el componente PandaArm.
/// </summary>
public static class BuildTestEnvironment
{
    const string RootName = "TestEnvironment";

    [MenuItem("Franka/Build Test Environment")]
    static void Build()
    {
        // Evitar duplicados: si ya existe, lo borra y rehace.
        var existing = GameObject.Find(RootName);
        if (existing != null) Object.DestroyImmediate(existing);

        var root = new GameObject(RootName);

        // --- Piso ---
        var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
        floor.name = "Floor";
        floor.transform.SetParent(root.transform);
        floor.transform.localScale = new Vector3(1.2f, 1f, 1.2f); // plano = 10m, x1.2 = 12m
        Paint(floor, new Color(0.20f, 0.22f, 0.25f));

        // --- Mesa de trabajo (frente al robot, hacia +Z) ---
        const float tableTopY = 0.40f;
        const float tableZ = 0.55f;
        MakeBox("TableTop", root.transform,
            pos: new Vector3(0f, tableTopY - 0.02f, tableZ),
            size: new Vector3(0.8f, 0.04f, 0.6f),
            color: new Color(0.55f, 0.38f, 0.22f));

        // Patas. Cuelgan del root, NO del tablero: el tablero tiene escala no uniforme
        // (0.8, 0.04, 0.6) y los hijos la heredan, lo que las aplastaria a 1/25 de altura.
        float legH = tableTopY - 0.04f;              // llega justo abajo del tablero
        float legY = legH * 0.5f;                    // centro de la pata
        float[] lx = { -0.35f, 0.35f }, lz = { -0.25f, 0.25f };
        foreach (var x in lx)
            foreach (var z in lz)
                MakeBox("TableLeg", root.transform,
                    pos: new Vector3(x, legY, tableZ + z),
                    size: new Vector3(0.05f, legH, 0.05f),
                    color: new Color(0.30f, 0.20f, 0.12f));

        // --- Objetos manipulables sobre la mesa (con Rigidbody) ---
        SpawnProp(root.transform, "Cube_Red",    PrimitiveType.Cube,     new Vector3(-0.15f, tableTopY + 0.03f, tableZ - 0.1f), 0.06f, new Color(0.80f, 0.20f, 0.20f));
        SpawnProp(root.transform, "Cube_Green",  PrimitiveType.Cube,     new Vector3( 0.05f, tableTopY + 0.03f, tableZ + 0.05f), 0.06f, new Color(0.20f, 0.70f, 0.30f));
        SpawnProp(root.transform, "Cyl_Blue",    PrimitiveType.Cylinder, new Vector3( 0.20f, tableTopY + 0.05f, tableZ - 0.05f), 0.05f, new Color(0.25f, 0.45f, 0.85f));

        // --- Objetivo para la IK (esfera sin colisión ni física: es solo un marcador) ---
        var ikTarget = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        ikTarget.name = "IKTarget";
        ikTarget.transform.SetParent(root.transform);
        ikTarget.transform.position = new Vector3(0f, tableTopY + 0.20f, tableZ - 0.05f);
        ikTarget.transform.localScale = Vector3.one * 0.05f;
        Object.DestroyImmediate(ikTarget.GetComponent<Collider>()); // no debe chocar con el brazo
        Paint(ikTarget, new Color(1f, 0.55f, 0.1f));

        // --- Marcador de posición del jugador/cámara (para orientar el XR rig) ---
        var spawn = new GameObject("PlayerSpawn");
        spawn.transform.SetParent(root.transform);
        spawn.transform.position = new Vector3(0f, 1.6f, -1.2f); // ~altura de ojos, mirando +Z
        spawn.transform.LookAt(new Vector3(0f, tableTopY, tableZ));

        // --- Luz direccional (para VR: horneá luces después; 1 sola en tiempo real) ---
        var lightGo = new GameObject("Sun");
        lightGo.transform.SetParent(root.transform);
        var light = lightGo.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.1f;
        light.shadows = LightShadows.Soft;
        lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.45f, 0.47f, 0.5f);

        Selection.activeGameObject = root;
        Debug.Log("[Franka] Entorno de prueba generado. Ahora arrastrá el Panda a la escena y orientalo hacia la mesa (+Z).");
    }

    // ---- helpers ----

    static GameObject MakeBox(string name, Transform parent, Vector3 pos, Vector3 size, Color color)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent);
        go.transform.position = pos;
        go.transform.localScale = size;
        Paint(go, color);
        return go;
    }

    static void SpawnProp(Transform parent, string name, PrimitiveType type, Vector3 pos, float scale, Color color)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.position = pos;
        go.transform.localScale = Vector3.one * scale;
        Paint(go, color);
        var rb = go.AddComponent<Rigidbody>();
        rb.mass = 0.2f;
        go.transform.SetParent(parent);
    }

    static Shader cachedShader;

    /// <summary>
    /// Devuelve el shader opaco del pipeline activo.
    /// "Standard" solo existe en Built-in; URP usa "Universal Render Pipeline/Lit".
    /// Buscamos el de URP primero para que el script siga sirviendo si algun dia migramos.
    /// </summary>
    static Shader DefaultShader()
    {
        if (cachedShader != null) return cachedShader;

        foreach (var n in new[] { "Universal Render Pipeline/Lit", "Standard", "Diffuse" })
        {
            cachedShader = Shader.Find(n);
            if (cachedShader != null) return cachedShader;
        }

        Debug.LogError("[Franka] No se encontro un shader valido para el pipeline activo.");
        return null;
    }

    static void Paint(GameObject go, Color color)
    {
        var mr = go.GetComponent<MeshRenderer>();
        var sh = DefaultShader();
        if (sh == null) return;

        var mat = new Material(sh);
        SetColor(mat, color);
        mr.sharedMaterial = mat;
    }

    /// <summary>Built-in usa _Color; URP usa _BaseColor. Escribimos el que exista.</summary>
    static void SetColor(Material m, Color c)
    {
        if (m.HasProperty(BaseColorId)) m.SetColor(BaseColorId, c);
        if (m.HasProperty(ColorId)) m.SetColor(ColorId, c);
    }

    static readonly int ColorId = Shader.PropertyToID("_Color");
    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
}
#endif
