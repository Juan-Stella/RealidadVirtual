#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using FrankaVR.Welding;

namespace FrankaVR.EditorTools
{
    /// Construye la celda de soldadura completa desde el menu de Unity.
    /// Todo se genera con primitivas, sin assets externos: mesa, pieza segun la orden de
    /// trabajo, costura, soldador, paneles, iluminacion y careta VR.
    ///
    /// Menu: Franka > Soldadura > ...
    public static class WeldingCellBuilder
    {
        const string ROOT = "CeldaSoldadura";

        // ---------------------------------------------------------------- menu

        [MenuItem("Franka/Soldadura/1. Construir celda completa")]
        public static void BuildAll()
        {
            GameObject root = GetOrCreate(ROOT, null);
            BuildEnvironment(root.transform);
            BuildStation(root.transform);
            BuildWorkpiece(root.transform, WorkOrder.ById("N1"));
            BuildPanels(root.transform);
            BuildLighting(root.transform);
            Debug.Log("Celda de soldadura construida. Siguiente paso: 'Franka/Soldadura/2. Montar soldador en la brida' con la brida seleccionada.");
        }

        [MenuItem("Franka/Soldadura/2. Montar soldador en la brida")]
        public static void MountTorchOnSelection()
        {
            GameObject sel = Selection.activeGameObject;
            if (sel == null)
            {
                EditorUtility.DisplayDialog("Falta seleccion",
                    "Seleccione primero la brida del robot (panda_link8 o panda_hand) en la jerarquia.", "OK");
                return;
            }
            BuildTorch(sel.transform);
        }

        [MenuItem("Franka/Soldadura/3. Montar careta VR en la camara")]
        public static void MountHelmetOnSelection()
        {
            GameObject sel = Selection.activeGameObject;
            if (sel == null)
            {
                EditorUtility.DisplayDialog("Falta seleccion",
                    "Seleccione primero la camara VR (Main Camera) en la jerarquia.", "OK");
                return;
            }
            BuildHelmet(sel.transform);
        }

        [MenuItem("Franka/Soldadura/Pieza/Orden N1 - Cordon sobre chapa")] static void P1() => Rebuild("N1");
        [MenuItem("Franka/Soldadura/Pieza/Orden N2 - Junta a tope")]       static void P2() => Rebuild("N2");
        [MenuItem("Franka/Soldadura/Pieza/Orden N3 - Junta en T")]         static void P3() => Rebuild("N3");
        [MenuItem("Franka/Soldadura/Pieza/Orden N4 - Solape chapa fina")]  static void P4() => Rebuild("N4");
        [MenuItem("Franka/Soldadura/Pieza/Orden N5 - Cano a tope")]        static void P5() => Rebuild("N5");

        static void Rebuild(string id)
        {
            GameObject root = GetOrCreate(ROOT, null);
            Transform old = root.transform.Find("Pieza");
            if (old != null) Object.DestroyImmediate(old.gameObject);
            BuildWorkpiece(root.transform, WorkOrder.ById(id));

            WeldingTaskManager task = Object.FindObjectOfType<WeldingTaskManager>();
            if (task != null) task.LoadWorkOrder(id);
            Debug.Log("Pieza reconstruida para la orden " + id);
        }

        // ---------------------------------------------------------- construccion

        static void BuildEnvironment(Transform parent)
        {
            GameObject env = GetOrCreate("Entorno", parent);

            Prim(PrimitiveType.Plane, "Piso", env.transform,
                                    Vector3.zero, Vector3.one * 1.2f, Col(0.16f, 0.16f, 0.18f));

            // Paredes bajas: dan referencia de escala y contienen la luz del arco en VR.
            MakeWall(env.transform, "ParedFondo",  new Vector3(0f, 1.5f, 4f),  new Vector3(12f, 3f, 0.1f));
            MakeWall(env.transform, "ParedIzq",    new Vector3(-4f, 1.5f, 0f), new Vector3(0.1f, 3f, 8f));
            MakeWall(env.transform, "ParedDer",    new Vector3(4f, 1.5f, 0f),  new Vector3(0.1f, 3f, 8f));

            // Biombo de soldadura: elemento reconocible de taller.
            Prim(PrimitiveType.Cube, "Biombo", env.transform,
                 new Vector3(0f, 1.0f, 1.6f), new Vector3(2.4f, 2.0f, 0.05f), Col(0.45f, 0.16f, 0.14f));
        }

        static void MakeWall(Transform p, string name, Vector3 pos, Vector3 scale)
            => Prim(PrimitiveType.Cube, name, p, pos, scale, Col(0.22f, 0.23f, 0.26f));

        static void BuildStation(Transform parent)
        {
            GameObject st = GetOrCreate("Estacion", parent);

            // Mesa de trabajo con parrilla metalica.
            Prim(PrimitiveType.Cube, "MesaTapa", st.transform,
                 new Vector3(0.55f, 0.75f, 0f), new Vector3(0.9f, 0.05f, 0.7f), Col(0.30f, 0.31f, 0.33f));
            for (int i = 0; i < 4; i++)
            {
                float x = 0.55f + (i % 2 == 0 ? -0.40f : 0.40f);
                float z = (i < 2 ? -0.30f : 0.30f);
                Prim(PrimitiveType.Cube, "Pata" + i, st.transform,
                     new Vector3(x, 0.375f, z), new Vector3(0.05f, 0.75f, 0.05f), Col(0.25f, 0.25f, 0.27f));
            }

            // Base del robot.
            Prim(PrimitiveType.Cylinder, "BaseRobot", st.transform,
                 new Vector3(0f, 0.05f, 0f), new Vector3(0.35f, 0.05f, 0.35f), Col(0.20f, 0.20f, 0.22f));

            // Gabinete de la maquina de soldar.
            GameObject cab = Prim(PrimitiveType.Cube, "MaquinaSoldar", st.transform,
                                  new Vector3(-0.75f, 0.30f, 0.55f), new Vector3(0.45f, 0.60f, 0.35f),
                                  Col(0.55f, 0.35f, 0.10f));
            WeldingMachine machine = cab.GetComponent<WeldingMachine>();
            if (machine == null) machine = cab.AddComponent<WeldingMachine>();

            // Porta-electrodos.
            Prim(PrimitiveType.Cylinder, "PortaElectrodos", st.transform,
                 new Vector3(-0.75f, 0.68f, 0.55f), new Vector3(0.06f, 0.10f, 0.06f), Col(0.6f, 0.6f, 0.62f));

            // Gestor de tarea vive en la estacion.
            GameObject mgr = GetOrCreate("GestorTarea", parent);
            if (mgr.GetComponent<WeldingTaskManager>() == null) mgr.AddComponent<WeldingTaskManager>();
        }

        /// Construye la pieza y la costura segun la orden de trabajo.
        static void BuildWorkpiece(Transform parent, WorkOrder wo)
        {
            GameObject piece = GetOrCreate("Pieza", parent);
            piece.transform.position = new Vector3(0.55f, 0.775f, 0f);

            float t = wo.ThicknessMm * 0.001f;
            float len = wo.SeamLengthM;
            Color steel = Col(0.42f, 0.44f, 0.47f);

            GameObject seamGo = GetOrCreate("Costura", piece.transform);
            WeldSeam seam = seamGo.GetComponent<WeldSeam>();
            if (seam == null) seam = seamGo.AddComponent<WeldSeam>();

            GameObject a = MakeChild("ExtremoA", seamGo.transform);
            GameObject b = MakeChild("ExtremoB", seamGo.transform);

            switch (wo.Joint)
            {
                case JointType.BeadOnPlate:
                    Prim(PrimitiveType.Cube, "Chapa", piece.transform,
                         new Vector3(0f, t * 0.5f, 0f), new Vector3(0.30f, t, 0.20f), steel);
                    a.transform.position = piece.transform.position + new Vector3(-len * 0.5f, t, 0f);
                    b.transform.position = piece.transform.position + new Vector3(+len * 0.5f, t, 0f);
                    break;

                case JointType.ButtJoint:
                    Prim(PrimitiveType.Cube, "ChapaIzq", piece.transform,
                         new Vector3(0f, t * 0.5f, -0.052f), new Vector3(0.30f, t, 0.10f), steel);
                    Prim(PrimitiveType.Cube, "ChapaDer", piece.transform,
                         new Vector3(0f, t * 0.5f, +0.052f), new Vector3(0.30f, t, 0.10f), steel);
                    a.transform.position = piece.transform.position + new Vector3(-len * 0.5f, t, 0f);
                    b.transform.position = piece.transform.position + new Vector3(+len * 0.5f, t, 0f);
                    break;

                case JointType.FilletT:
                    Prim(PrimitiveType.Cube, "Alma", piece.transform,
                         new Vector3(0f, t * 0.5f, 0f), new Vector3(0.30f, t, 0.20f), steel);
                    Prim(PrimitiveType.Cube, "Ala", piece.transform,
                         new Vector3(0f, 0.06f, 0f), new Vector3(0.30f, 0.12f, t), steel);
                    // La costura corre en el vertice interior de la T.
                    a.transform.position = piece.transform.position + new Vector3(-len * 0.5f, t, t * 0.5f + 0.002f);
                    b.transform.position = piece.transform.position + new Vector3(+len * 0.5f, t, t * 0.5f + 0.002f);
                    break;

                case JointType.LapJoint:
                    Prim(PrimitiveType.Cube, "ChapaBase", piece.transform,
                         new Vector3(0f, t * 0.5f, 0f), new Vector3(0.26f, t, 0.16f), steel);
                    Prim(PrimitiveType.Cube, "ChapaSuperior", piece.transform,
                         new Vector3(0f, t * 1.5f, 0.04f), new Vector3(0.26f, t, 0.08f), steel);
                    a.transform.position = piece.transform.position + new Vector3(-len * 0.5f, t * 2f, 0f);
                    b.transform.position = piece.transform.position + new Vector3(+len * 0.5f, t * 2f, 0f);
                    break;

                case JointType.PipeButt:
                    float r = wo.PipeRadiusM;
                    Prim(PrimitiveType.Cylinder, "CanoIzq", piece.transform,
                         new Vector3(-0.09f, r, 0f), new Vector3(r * 2f, 0.08f, r * 2f), steel)
                        .transform.localEulerAngles = new Vector3(0f, 0f, 90f);
                    Prim(PrimitiveType.Cylinder, "CanoDer", piece.transform,
                         new Vector3(+0.09f, r, 0f), new Vector3(r * 2f, 0.08f, r * 2f), steel)
                        .transform.localEulerAngles = new Vector3(0f, 0f, 90f);
                    GameObject center = MakeChild("CentroCano", seamGo.transform);
                    center.transform.position = piece.transform.position + new Vector3(0f, r, 0f);
                    seam.CircleCenter = center.transform;
                    seam.CircleAxis = Vector3.right; // el cano corre en X
                    break;
            }

            seam.Start = a.transform;
            seam.End   = b.transform;
            seam.Samples = wo.Shape == SeamShape.Circle ? 72 : 60;
            seam.ApplyWorkOrder(wo);

            // Renderer del cordon.
            GameObject bead = GetOrCreate("Cordon", piece.transform);
            if (bead.GetComponent<MeshFilter>() == null)   bead.AddComponent<MeshFilter>();
            if (bead.GetComponent<MeshRenderer>() == null) bead.AddComponent<MeshRenderer>();
            WeldBeadRenderer br = bead.GetComponent<WeldBeadRenderer>();
            if (br == null) br = bead.AddComponent<WeldBeadRenderer>();
            br.Seam = seam;

            Debug.Log("Pieza para la orden " + wo.Id + ": " + wo.Title
                    + " (espesor " + wo.ThicknessMm + " mm). Asignar la capa 'Workpiece' a los colliders de la pieza.");
        }

        static void BuildTorch(Transform flange)
        {
            GameObject torchGo = GetOrCreate("Soldador", flange);
            torchGo.transform.localPosition = Vector3.zero;

            // Cuerpo del porta-electrodo.
            Prim(PrimitiveType.Cylinder, "Cuerpo", torchGo.transform,
                 new Vector3(0f, -0.06f, 0f), new Vector3(0.028f, 0.06f, 0.028f), Col(0.12f, 0.12f, 0.14f));
            Prim(PrimitiveType.Cube, "Mordaza", torchGo.transform,
                 new Vector3(0f, -0.125f, 0f), new Vector3(0.022f, 0.03f, 0.022f), Col(0.65f, 0.45f, 0.15f));

            // Varilla del electrodo: se escala en Y a medida que se consume.
            GameObject rod = Prim(PrimitiveType.Cylinder, "Electrodo", torchGo.transform,
                                  new Vector3(0f, -0.23f, 0f), new Vector3(0.0035f, 0.09f, 0.0035f),
                                  Col(0.85f, 0.83f, 0.75f));

            GameObject tip = MakeChild("Tip", torchGo.transform);
            tip.transform.localPosition = new Vector3(0f, -0.32f, 0f);

            WeldingTorch torch = torchGo.GetComponent<WeldingTorch>();
            if (torch == null) torch = torchGo.AddComponent<WeldingTorch>();
            torch.Tip = tip.transform;
            torch.ElectrodeVisual = rod.transform;
            torch.ElectrodeAxisLocal = Vector3.down;
            torch.Machine = Object.FindObjectOfType<WeldingMachine>();

            // Efectos del arco.
            GameObject fxGo = GetOrCreate("ArcoFX", torchGo.transform);
            WeldingFX fx = fxGo.GetComponent<WeldingFX>();
            if (fx == null) fx = fxGo.AddComponent<WeldingFX>();
            fx.Torch = torch;
            fx.ArcOrigin = tip.transform;

            GameObject lightGo = GetOrCreate("LuzArco", tip.transform);
            Light lt = lightGo.GetComponent<Light>();
            if (lt == null) lt = lightGo.AddComponent<Light>();
            lt.type = LightType.Point;
            lt.range = 2.5f;
            lt.shadows = LightShadows.None; // sombras dinamicas son caras en mobile
            fx.ArcLight = lt;

            GameObject audioGo = GetOrCreate("AudioArco", tip.transform);
            AudioSource src = audioGo.GetComponent<AudioSource>();
            if (src == null) src = audioGo.AddComponent<AudioSource>();
            src.loop = true; src.playOnAwake = false; src.spatialBlend = 1f;
            fx.ArcAudio = src;

            Debug.Log("Soldador montado. IMPORTANTE: en PandaIK poner endEffector = " +
                      "Soldador/Tip, y en WeldingTorch asignar WorkpieceMask = capa 'Workpiece'.");
        }

        static void BuildHelmet(Transform cam)
        {
            GameObject visor = Prim(PrimitiveType.Quad, "CaretaVisor", cam,
                                    new Vector3(0f, 0f, 0.28f), Vector3.one * 0.6f, new Color(0.05f, 0.09f, 0.06f, 0.1f));
            Collider c = visor.GetComponent<Collider>();
            if (c != null) Object.DestroyImmediate(c);

            GameObject helmetGo = GetOrCreate("Careta", cam);
            WeldingHelmet h = helmetGo.GetComponent<WeldingHelmet>();
            if (h == null) h = helmetGo.AddComponent<WeldingHelmet>();
            h.HeadCamera = cam;
            h.VisorQuad = visor.GetComponent<Renderer>();
            h.Torch = Object.FindObjectOfType<WeldingTorch>();

            Debug.Log("Careta montada. Asignar al material del visor un shader transparente " +
                      "(por ejemplo Legacy Shaders/Transparent/Diffuse) para que el alfa funcione.");
        }

        static void BuildPanels(Transform parent)
        {
            GameObject panels = GetOrCreate("Paneles", parent);

            TextMesh machine = MakePanel(panels.transform, "PanelMaquina",
                                         new Vector3(-0.75f, 1.05f, 0.55f), TextAnchor.UpperLeft);
            TextMesh brief   = MakePanel(panels.transform, "PanelOrden",
                                         new Vector3(0.30f, 1.35f, 0.60f), TextAnchor.UpperLeft);
            TextMesh result  = MakePanel(panels.transform, "PanelResultado",
                                         new Vector3(1.10f, 1.35f, 0.60f), TextAnchor.UpperLeft);

            GameObject hudGo = GetOrCreate("HUD", panels.transform);
            WeldingHUD hud = hudGo.GetComponent<WeldingHUD>();
            if (hud == null) hud = hudGo.AddComponent<WeldingHUD>();
            hud.MachinePanel = machine;
            hud.BriefPanel = brief;
            hud.ResultPanel = result;
            hud.Machine = Object.FindObjectOfType<WeldingMachine>();
            hud.Task = Object.FindObjectOfType<WeldingTaskManager>();
            hud.Torch = Object.FindObjectOfType<WeldingTorch>();
        }

        static TextMesh MakePanel(Transform parent, string name, Vector3 pos, TextAnchor anchor)
        {
            GameObject go = GetOrCreate(name, parent);
            go.transform.position = pos;
            TextMesh tm = go.GetComponent<TextMesh>();
            if (tm == null) tm = go.AddComponent<TextMesh>();
            tm.characterSize = 0.02f;
            tm.fontSize = 90;          // fuente grande + character size chico = texto nitido
            tm.anchor = anchor;
            tm.alignment = TextAlignment.Left;
            tm.color = Color.white;
            tm.text = name;
            return tm;
        }

        static void BuildLighting(Transform parent)
        {
            GameObject lights = GetOrCreate("Iluminacion", parent);

            GameObject keyGo = GetOrCreate("LuzTaller", lights.transform);
            Light key = keyGo.GetComponent<Light>();
            if (key == null) key = keyGo.AddComponent<Light>();
            key.type = LightType.Directional;
            key.intensity = 0.45f;   // taller en penumbra: hace resaltar el arco
            key.color = new Color(0.85f, 0.88f, 1f);
            key.shadows = LightShadows.None;
            keyGo.transform.localEulerAngles = new Vector3(50f, -30f, 0f);

            GameObject fillGo = GetOrCreate("LuzBanco", lights.transform);
            Light fill = fillGo.GetComponent<Light>();
            if (fill == null) fill = fillGo.AddComponent<Light>();
            fill.type = LightType.Point;
            fill.range = 3f;
            fill.intensity = 0.6f;
            fill.color = new Color(1f, 0.95f, 0.85f);
            fill.shadows = LightShadows.None;
            fillGo.transform.position = new Vector3(0.55f, 1.8f, 0f);
        }

        // ------------------------------------------------------------- helpers

        static GameObject GetOrCreate(string name, Transform parent)
        {
            Transform existing = parent != null ? parent.Find(name) : null;
            if (existing == null && parent == null)
            {
                GameObject found = GameObject.Find(name);
                if (found != null) return found;
            }
            if (existing != null) return existing.gameObject;

            GameObject go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent, false);
            Undo.RegisterCreatedObjectUndo(go, "Crear " + name);
            return go;
        }

        static GameObject MakeChild(string name, Transform parent)
        {
            GameObject go = GetOrCreate(name, parent);
            return go;
        }

        static GameObject Prim(PrimitiveType type, string name, Transform parent,
                               Vector3 localPos, Vector3 scale, Color color)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;

            Renderer r = go.GetComponent<Renderer>();
            if (r != null)
            {
                Material m = new Material(Shader.Find("Standard"));
                m.color = color;
                r.sharedMaterial = m;
            }
            Undo.RegisterCreatedObjectUndo(go, "Crear " + name);
            return go;
        }

        static Color Col(float r, float g, float b) => new Color(r, g, b, 1f);
    }
}
#endif
