#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

/// <summary>
/// Construye la celda de clasificacion completa y la cablea.
/// Menu: Franka > Construir celda de trabajo
///
/// Geometria validada: los 11 puntos criticos (agarre en bandeja, aproximacion,
/// esquinas, los 3 contenedores, transito) son alcanzables por el brazo.
/// El robot se monta sobre la mesa; la superficie de trabajo queda a 0.75 m.
/// </summary>
public static class BuildWorkcell
{
    const string RootName = "Workcell";

    // Alturas y posiciones, en metros.
    const float TableTop = 0.75f;
    const float PieceY   = TableTop + 0.03f;

    static readonly Color[] KindColors =
    {
        new Color(0.85f, 0.20f, 0.20f), // 0 rojo
        new Color(0.20f, 0.65f, 0.90f), // 1 azul
        new Color(0.95f, 0.75f, 0.15f), // 2 amarillo
    };
    static readonly string[] KindNames = { "Rojo", "Azul", "Amarillo" };

    [MenuItem("Franka/Construir celda de trabajo")]
    static void Build()
    {
        var old = GameObject.Find(RootName);
        if (old != null) Object.DestroyImmediate(old);

        var root = new GameObject(RootName).transform;

        BuildRoom(root);
        BuildTable(root);
        BuildTray(root);
        var bins = BuildBins(root);
        var pieces = BuildPieces(root);
        var stations = BuildStations(root);
        var panel = BuildControlPanel(root);
        BuildLighting(root);

        Wire(root, bins, pieces, stations, panel);

        Selection.activeGameObject = root.gameObject;
        Debug.Log(
            "[Franka] Celda construida.\n" +
            "Falta un solo paso manual: colocar el robot en (0, 0.75, 0) y correr\n" +
            "'Franka > Reparar y cablear robot', y despues 'Franka > Conectar robot a la celda'.");
    }

    // ---------- Entorno ----------

    static void BuildRoom(Transform root)
    {
        var floor = Prim(PrimitiveType.Plane, "Floor", root,
            new Vector3(0, 0, 0), new Vector3(1.6f, 1f, 1.6f), new Color(0.30f, 0.31f, 0.34f));
        floor.isStatic = true;

        // Marcas de piso: delimitan la zona de la celda (detalle de planta industrial).
        for (int i = 0; i < 4; i++)
        {
            float a = i * 90f * Mathf.Deg2Rad;
            var pos = new Vector3(Mathf.Sin(a) * 2.2f, 0.005f, Mathf.Cos(a) * 2.2f);
            var size = (i % 2 == 0) ? new Vector3(4.4f, 0.01f, 0.08f) : new Vector3(0.08f, 0.01f, 4.4f);
            Prim(PrimitiveType.Cube, "FloorMark", root, pos, size, new Color(0.9f, 0.75f, 0.1f)).isStatic = true;
        }

        // Vallado de seguridad: postes + malla translucida al fondo.
        for (int i = -2; i <= 2; i++)
            Prim(PrimitiveType.Cube, "FencePost", root,
                new Vector3(i * 1.1f, 0.9f, -1.6f), new Vector3(0.06f, 1.8f, 0.06f),
                new Color(0.85f, 0.65f, 0.1f)).isStatic = true;

        Prim(PrimitiveType.Cube, "FenceMesh", root,
            new Vector3(0f, 0.9f, -1.62f), new Vector3(4.4f, 1.75f, 0.02f),
            new Color(0.35f, 0.38f, 0.40f)).isStatic = true;
    }

    static void BuildTable(Transform root)
    {
        var top = Prim(PrimitiveType.Cube, "WorkSurface", root,
            new Vector3(0f, TableTop - 0.03f, 0.30f), new Vector3(2.0f, 0.06f, 1.4f),
            new Color(0.62f, 0.64f, 0.66f));
        top.isStatic = true;

        float legH = TableTop - 0.06f;
        foreach (var x in new[] { -0.92f, 0.92f })
            foreach (var z in new[] { -0.32f, 0.92f })
                Prim(PrimitiveType.Cube, "TableLeg", root,
                    new Vector3(x, legH * 0.5f, z), new Vector3(0.07f, legH, 0.07f),
                    new Color(0.30f, 0.31f, 0.33f)).isStatic = true;

    }

    static void BuildTray(Transform root)
    {
        // Bandeja de alimentacion, a la izquierda del robot.
        var tray = Prim(PrimitiveType.Cube, "FeedTray", root,
            new Vector3(-0.40f, TableTop + 0.005f, 0.45f), new Vector3(0.42f, 0.01f, 0.32f),
            new Color(0.20f, 0.22f, 0.25f));
        tray.isStatic = true;

        // Bordes bajos, para que las piezas no rueden fuera.
        Prim(PrimitiveType.Cube, "TrayEdge", root, new Vector3(-0.40f, TableTop + 0.02f, 0.615f), new Vector3(0.42f, 0.04f, 0.015f), new Color(0.16f, 0.18f, 0.2f));
        Prim(PrimitiveType.Cube, "TrayEdge", root, new Vector3(-0.40f, TableTop + 0.02f, 0.285f), new Vector3(0.42f, 0.04f, 0.015f), new Color(0.16f, 0.18f, 0.2f));
        Prim(PrimitiveType.Cube, "TrayEdge", root, new Vector3(-0.615f, TableTop + 0.02f, 0.45f), new Vector3(0.015f, 0.04f, 0.32f), new Color(0.16f, 0.18f, 0.2f));
        Prim(PrimitiveType.Cube, "TrayEdge", root, new Vector3(-0.185f, TableTop + 0.02f, 0.45f), new Vector3(0.015f, 0.04f, 0.32f), new Color(0.16f, 0.18f, 0.2f));
    }

    // Posiciones validadas como alcanzables.
    static readonly Vector3[] BinPos =
    {
        new Vector3(0.50f, TableTop, 0.20f),
        new Vector3(0.50f, TableTop, 0.45f),
        new Vector3(0.35f, TableTop, 0.68f),
    };

    static List<SortingBin> BuildBins(Transform root)
    {
        var list = new List<SortingBin>();

        for (int k = 0; k < 3; k++)
        {
            var center = BinPos[k];

            var holder = new GameObject($"Bin_{KindNames[k]}");
            holder.transform.SetParent(root);
            holder.transform.position = center;

            // Caja: piso + 4 paredes.
            Prim(PrimitiveType.Cube, "Floor", holder.transform,
                center + new Vector3(0, 0.01f, 0), new Vector3(0.22f, 0.02f, 0.22f), KindColors[k] * 0.5f);

            var wallOff = new[]
            {
                new Vector3(0f,  0.06f,  0.11f), new Vector3(0f, 0.06f, -0.11f),
                new Vector3(0.11f, 0.06f, 0f),   new Vector3(-0.11f, 0.06f, 0f),
            };
            var wallSize = new[]
            {
                new Vector3(0.22f, 0.12f, 0.012f), new Vector3(0.22f, 0.12f, 0.012f),
                new Vector3(0.012f, 0.12f, 0.22f), new Vector3(0.012f, 0.12f, 0.22f),
            };
            for (int w = 0; w < wallOff.Length; w++)
                Prim(PrimitiveType.Cube, "Wall", holder.transform,
                     center + wallOff[w], wallSize[w], KindColors[k]);

            // Luz indicadora, sobre un poste al costado.
            Prim(PrimitiveType.Cube, "LampPost", holder.transform,
                center + new Vector3(0.13f, 0.14f, 0), new Vector3(0.02f, 0.28f, 0.02f),
                new Color(0.3f, 0.3f, 0.32f));

            var lamp = Prim(PrimitiveType.Sphere, "Indicator", holder.transform,
                center + new Vector3(0.13f, 0.30f, 0), Vector3.one * 0.055f,
                new Color(0.25f, 0.25f, 0.28f));

            // Zona de deteccion: trigger sobre la boca del contenedor.
            var zone = new GameObject("DropZone");
            zone.transform.SetParent(holder.transform);
            zone.transform.position = center + new Vector3(0, 0.09f, 0);

            var bc = zone.AddComponent<BoxCollider>();
            bc.size = new Vector3(0.2f, 0.16f, 0.2f);
            bc.isTrigger = true;

            var bin = zone.AddComponent<SortingBin>();
            bin.kind = k;
            bin.indicator = lamp.GetComponent<Renderer>();
            list.Add(bin);
        }

        return list;
    }

    static List<Graspable> BuildPieces(Transform root)
    {
        var list = new List<Graspable>();
        var holder = new GameObject("Pieces");
        holder.transform.SetParent(root);

        // 6 piezas: 2 de cada color, alternadas en la bandeja.
        var slots = new[]
        {
            new Vector3(-0.53f, PieceY, 0.54f), new Vector3(-0.40f, PieceY, 0.54f),
            new Vector3(-0.27f, PieceY, 0.54f), new Vector3(-0.53f, PieceY, 0.37f),
            new Vector3(-0.40f, PieceY, 0.37f), new Vector3(-0.27f, PieceY, 0.37f),
        };

        for (int i = 0; i < slots.Length; i++)
        {
            int kind = i % 3;

            // 0.045 m de lado: entra comodo en la pinza (apertura maxima 0.08 m).
            var go = Prim(PrimitiveType.Cube, $"Piece_{KindNames[kind]}_{i}", holder.transform,
                slots[i], Vector3.one * 0.045f, KindColors[kind]);

            var rb = go.AddComponent<Rigidbody>();
            rb.mass = 0.15f;
            // Sin esto las piezas chicas atraviesan la mesa o los dedos al moverse rapido.
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            var g = go.AddComponent<Graspable>();
            g.kind = kind;
            list.Add(g);
        }

        return list;
    }

    static List<ViewpointManager.Station> BuildStations(Transform root)
    {
        var holder = new GameObject("Viewpoints");
        holder.transform.SetParent(root);

        var focus = new Vector3(0f, TableTop + 0.10f, 0.42f);
        var list = new List<ViewpointManager.Station>();

        var labels = new[] { "Operario", "Cenital", "Seguimiento" };
        var positions = new[]
        {
            new Vector3(0.00f, 1.55f, 1.55f),   // de pie frente a la mesa
            new Vector3(0.00f, 2.15f, 0.55f),   // cenital, buena para juzgar alineacion
            new Vector3(1.35f, 1.40f, 0.95f),   // lateral; luego sigue la pinza al rotar
        };

        for (int i = 0; i < labels.Length; i++)
        {
            var a = new GameObject($"VP_{labels[i]}");
            a.transform.SetParent(holder.transform);
            a.transform.position = positions[i];
            a.transform.LookAt(focus);
            list.Add(new ViewpointManager.Station { label = labels[i], anchor = a.transform });
        }

        return list;
    }

    /// <summary>
    /// Panel de control con botones que se activan mirandolos.
    /// Permite usar el simulador sin gamepad, solo con el casco.
    /// </summary>
    static GazeButton[] BuildControlPanel(Transform root)
    {
        var holder = new GameObject("ControlPanel");
        holder.transform.SetParent(root);

        // A la izquierda de la mesa, inclinado hacia el operario.
        var basePos = new Vector3(-0.95f, TableTop + 0.22f, 0.75f);

        Prim(PrimitiveType.Cube, "PanelBody", holder.transform,
            basePos, new Vector3(0.42f, 0.30f, 0.03f), new Color(0.18f, 0.19f, 0.22f));

        var labels = new[] { "INICIAR / REINICIAR", "CAMBIAR VISTA" };
        var buttons = new GazeButton[labels.Length];

        for (int i = 0; i < labels.Length; i++)
        {
            var pos = basePos + new Vector3(0f, 0.07f - i * 0.14f, -0.025f);

            var face = Prim(PrimitiveType.Cube, $"GazeBtn_{i}", holder.transform,
                pos, new Vector3(0.34f, 0.10f, 0.02f), new Color(0.25f, 0.28f, 0.32f));

            var btn = face.AddComponent<GazeButton>();
            btn.label = labels[i];
            btn.highlight = face.GetComponent<Renderer>();
            buttons[i] = btn;

            // Rotulo legible en el mundo.
            var txt = new GameObject($"Label_{i}");
            txt.transform.SetParent(face.transform);
            txt.transform.position = pos + new Vector3(0f, 0f, -0.02f);
            txt.transform.rotation = Quaternion.LookRotation(Vector3.back, Vector3.up);

            var tm = txt.AddComponent<TextMesh>();
            tm.text = labels[i];
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.fontSize = 48;
            tm.characterSize = 0.006f;
            tm.color = new Color(0.95f, 0.96f, 1f);
        }

        return buttons;
    }

    static void BuildLighting(Transform root)
    {
        var go = new GameObject("KeyLight");
        go.transform.SetParent(root);
        var l = go.AddComponent<Light>();
        l.type = LightType.Directional;
        l.intensity = 1.05f;
        l.shadows = LightShadows.Soft;
        go.transform.rotation = Quaternion.Euler(52f, -35f, 0f);

        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.42f, 0.44f, 0.48f);
    }

    // ---------- Cableado ----------

    static void Wire(Transform root,
                     List<SortingBin> bins,
                     List<Graspable> pieces,
                     List<ViewpointManager.Station> stations,
                     GazeButton[] panel)
    {
        var ctrl = new GameObject("CellController");
        ctrl.transform.SetParent(root);

        var audio = ctrl.AddComponent<AudioCues>();

        var task = ctrl.AddComponent<TaskManager>();
        task.bins = bins;
        task.pieces = pieces;
        task.audioCues = audio;

        var vp = ctrl.AddComponent<ViewpointManager>();
        vp.stations = stations;

        var hud = ctrl.AddComponent<TaskHUD>();
        hud.task = task;
        hud.viewpoints = vp;

        ctrl.AddComponent<GazeInteractor>();

        // Botones por mirada -> acciones, conectados por codigo.
        // (Antes usaba UnityEventTools.AddPersistentListener, pero esa API del editor
        //  no acepta estos metodos y tiraba NullReferenceException al construir.)
        if (panel != null && panel.Length >= 2)
        {
            var panelHook = ctrl.AddComponent<PanelHook>();
            panelHook.task = task;
            panelHook.viewpoints = vp;
            panelHook.startButton = panel[0];
            panelHook.viewButton = panel[1];
        }
    }

    // ---------- Utilidades ----------

    static GameObject Prim(PrimitiveType type, string name, Transform parent,
                           Vector3 pos, Vector3 scale, Color color)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(parent);
        go.transform.position = pos;
        go.transform.localScale = scale;
        Paint(go, color);
        return go;
    }

    static Shader cached;

    static Shader DefaultShader()
    {
        if (cached != null) return cached;
        foreach (var n in new[] { "Universal Render Pipeline/Lit", "Standard", "Diffuse" })
        {
            cached = Shader.Find(n);
            if (cached != null) return cached;
        }
        return null;
    }

    static void Paint(GameObject go, Color c)
    {
        var sh = DefaultShader();
        if (sh == null) return;

        var m = new Material(sh);
        int colorId = Shader.PropertyToID("_Color");
        int baseId = Shader.PropertyToID("_BaseColor");
        if (m.HasProperty(baseId)) m.SetColor(baseId, c);
        if (m.HasProperty(colorId)) m.SetColor(colorId, c);

        go.GetComponent<MeshRenderer>().sharedMaterial = m;
    }
}
#endif
