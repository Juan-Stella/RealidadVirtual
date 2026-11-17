using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace FrankaVR.Welding
{
    /// <summary>
    /// Nave industrial decorativa alrededor de la celda: linea de soldadura robotizada con
    /// cinta en movimiento, robots, AGV, puente grua y almacen.
    /// Solo escenografia: sin colliders, sin input y sin tocar IK, fisica ni puntuacion.
    /// La crea WeldingEnvironmentPolish en Play y se destruye con su raiz.
    /// </summary>
    public class FactoryLineScenery : MonoBehaviour
    {
        public float ConveyorSpeed = 0.12f;
        public float AgvSpeed = 0.55f;

        // Nave. La celda original ocupa x[-4,4], z[-4,4] con techo a 3 m.
        const float X0 = -18f, X1 = 6f, Z0 = -12f, Z1 = 4.05f, H = 7f;
        // Linea paralela a la valla de la celda: visible detras del robot desde el puesto.
        const float LineX = -7.5f, LineZ0 = -8f, LineZ1 = 3f, BeltTop = 0.86f;
        const float PartZ0 = -7.6f, PartZ1 = 2.6f;
        const float RobotX = -8.75f;

        class LineRobot
        {
            public Transform Turret, Shoulder, Elbow, Spark;
            public Light Glow;
            public Renderer Green, Amber;
            public float Z, Phase, Weld;
        }

        readonly List<Object> _owned = new List<Object>();
        readonly List<LineRobot> _robots = new List<LineRobot>();
        readonly List<Transform> _parts = new List<Transform>();
        Transform _root, _agv, _crane, _trolley;
        Renderer _beacon, _hiddenWall;
        Material _belt, _glowWhite, _glowGreen, _glowAmber, _glowArc, _lampOff;
        Material _steel, _dark, _galva, _yellow, _blue, _orange, _wood, _wall, _concrete;
        TextMesh _counter;
        Vector3[] _agvPath;
        int _agvIndex, _produced = 128;
        float _ground, _beltOffset;
        bool _lite, _oldFog;
        Color _oldFogColor;
        FogMode _oldFogMode;
        float _oldFogStart, _oldFogEnd;

        void Awake()
        {
            _root = transform;
            _lite = Application.isMobilePlatform;
            Renderer floor = GameObject.Find("Piso")?.GetComponent<Renderer>();
            _ground = floor != null ? floor.bounds.max.y : 0f;
            BuildMaterials();
            BuildShell();
            BuildCellFence();
            BuildConveyor();
            for (int i = 0; i < 3; i++) BuildRobot(i, -5.2f + i * 2.8f);
            BuildAgv();
            BuildCrane();
            BuildStorage();
            BuildSigns();
            ApplyFog();
        }

        // ------------------------------------------------------------ materiales
        Material Lit(string name, Color color, float metallic = 0f, float smoothness = 0.25f)
        {
            Material material = new Material(Shader.Find("Standard"));
            material.name = "FrankaVR_Line_" + name;
            material.color = color;
            material.SetFloat("_Metallic", metallic);
            material.SetFloat("_Glossiness", smoothness);
            _owned.Add(material);
            return material;
        }

        // Sin iluminacion: no depende de variantes de emision del shader Standard.
        Material Glow(string name, Color color)
        {
            Shader shader = Shader.Find("Sprites/Default");
            Material material = new Material(shader != null ? shader : Shader.Find("Standard"));
            material.name = "FrankaVR_Line_" + name;
            material.color = color;
            _owned.Add(material);
            return material;
        }

        Texture2D Pattern(string name, System.Func<int, int, float> value)
        {
            const int size = 64;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGB24, true);
            texture.name = "FrankaVR_Line_" + name;
            texture.wrapMode = TextureWrapMode.Repeat;
            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float v = value(x, y);
                    pixels[y * size + x] = new Color(v, v, v);
                }
            texture.SetPixels(pixels);
            texture.Apply(true, true);
            _owned.Add(texture);
            return texture;
        }

        void BuildMaterials()
        {
            _steel = Lit("Steel", new Color(0.50f, 0.53f, 0.56f), 0.7f, 0.45f);
            _dark = Lit("Dark", new Color(0.11f, 0.13f, 0.15f), 0.5f, 0.3f);
            _galva = Lit("Galvanized", new Color(0.62f, 0.66f, 0.69f), 0.6f, 0.4f);
            _yellow = Lit("SafetyYellow", new Color(0.96f, 0.70f, 0.10f));
            _blue = Lit("MachineBlue", new Color(0.09f, 0.27f, 0.40f), 0.3f, 0.35f);
            _orange = Lit("RobotOrange", new Color(0.93f, 0.38f, 0.07f), 0.15f, 0.55f);
            _wood = Lit("Pallet", new Color(0.55f, 0.41f, 0.25f));
            _wall = Lit("HallWall", new Color(0.63f, 0.66f, 0.67f));
            _concrete = Lit("HallFloor", new Color(0.36f, 0.38f, 0.39f), 0f, 0.3f);
            _concrete.mainTexture = Pattern("Concrete", (x, y) =>
                (x < 1 || y < 1 ? 0.72f : 1f) * (0.66f + 0.20f * Mathf.PerlinNoise(x * 0.07f, y * 0.07f) +
                0.12f * Mathf.PerlinNoise(x * 0.5f + 9f, y * 0.5f + 4f)));
            _concrete.mainTextureScale = new Vector2(12f, 8f);
            _belt = Lit("Belt", new Color(0.20f, 0.21f, 0.22f), 0f, 0.15f);
            _belt.mainTexture = Pattern("Belt", (x, y) => y % 16 < 2 ? 0.55f : 0.85f + 0.10f * Mathf.PerlinNoise(x * 0.4f, y * 0.4f));
            _belt.mainTextureScale = new Vector2(1f, 22f);
            _glowWhite = Glow("Lamp", new Color(0.92f, 0.97f, 1f));
            _glowGreen = Glow("Green", new Color(0.20f, 1f, 0.40f));
            _glowAmber = Glow("Amber", new Color(1f, 0.68f, 0.10f));
            _glowArc = Glow("Arc", new Color(0.75f, 0.88f, 1f));
            _lampOff = Lit("LampOff", new Color(0.16f, 0.17f, 0.17f), 0f, 0.5f);
        }

        // ------------------------------------------------------------ piezas basicas
        Transform Part(Transform parent, string name, Vector3 localPosition, Vector3 scale, Material material,
            PrimitiveType shape = PrimitiveType.Cube, bool shadows = true)
        {
            GameObject go = GameObject.CreatePrimitive(shape);
            go.name = name;
            Collider collider = go.GetComponent<Collider>();
            if (collider != null) { collider.enabled = false; Destroy(collider); }
            Transform t = go.transform;
            t.SetParent(parent, false);
            t.localPosition = localPosition;
            t.localScale = scale;
            Renderer renderer = go.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            if (!shadows)
            {
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
            return t;
        }

        Transform Group(Transform parent, string name, Vector3 localPosition)
        {
            Transform t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = localPosition;
            return t;
        }

        TextMesh Label(Transform parent, string text, Vector3 localPosition, float yaw, float width, float height, Color color)
        {
            GameObject go = new GameObject("LineLabel");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            TextMesh mesh = go.AddComponent<TextMesh>();
            mesh.text = text; mesh.fontSize = 72; mesh.characterSize = 0.035f;
            mesh.anchor = TextAnchor.MiddleCenter; mesh.alignment = TextAlignment.Center; mesh.color = color;
            UseDepthTestedText(mesh);
            Bounds bounds = go.GetComponent<Renderer>().localBounds;
            go.transform.localScale = Vector3.one * Mathf.Min(width / Mathf.Max(0.001f, bounds.size.x),
                height / Mathf.Max(0.001f, bounds.size.y));
            return mesh;
        }


        /// <summary>Material de cartel con prueba de profundidad, compartido por todos los rotulos.</summary>
        public static void UseDepthTestedText(TextMesh mesh)
        {
            Renderer renderer = mesh.GetComponent<Renderer>();
            if (_textMaterial == null)
            {
                Shader shader = Resources.Load<Shader>("FrankaVRText3D");
                if (shader == null || renderer.sharedMaterial == null) return;
                _textMaterial = new Material(shader);
                _textMaterial.name = "FrankaVR_Line_Text";
                _textMaterial.mainTexture = renderer.sharedMaterial.mainTexture;
            }
            renderer.sharedMaterial = _textMaterial;
        }
        static Material _textMaterial;

        // ------------------------------------------------------------ nave
        void BuildShell()
        {
            float midX = (X0 + X1) * 0.5f, midZ = (Z0 + Z1) * 0.5f, lenX = X1 - X0, lenZ = Z1 - Z0, g = _ground;
            Part(_root, "HallFloor", new Vector3(midX, g - 0.03f, midZ), new Vector3(lenX, 0.05f, lenZ), _concrete, PrimitiveType.Cube, false);
            Part(_root, "HallRoof", new Vector3(midX, g + H + 0.05f, midZ), new Vector3(lenX, 0.1f, lenZ), _dark, PrimitiveType.Cube, false);
            Part(_root, "HallWallWest", new Vector3(X0, g + H * 0.5f, midZ), new Vector3(0.2f, H, lenZ), _wall, PrimitiveType.Cube, false);
            Part(_root, "HallWallEast", new Vector3(X1, g + H * 0.5f, midZ), new Vector3(0.2f, H, lenZ), _wall, PrimitiveType.Cube, false);
            Part(_root, "HallWallSouth", new Vector3(midX, g + H * 0.5f, Z0), new Vector3(lenX, H, 0.2f), _wall, PrimitiveType.Cube, false);
            Part(_root, "HallWallNorth", new Vector3((X0 - 6f) * 0.5f, g + H * 0.5f, Z1), new Vector3(-6f - X0, H, 0.2f), _wall, PrimitiveType.Cube, false);
            Part(_root, "HallWallNorthUpper", new Vector3(0f, g + 3f + (H - 3f) * 0.5f, Z1), new Vector3(12f, H - 3f, 0.2f), _wall, PrimitiveType.Cube, false);

            // Zocalo y franja de seguridad, como en la celda.
            Part(_root, "HallBandWest", new Vector3(X0 + 0.11f, g + 0.6f, midZ), new Vector3(0.02f, 1.2f, lenZ), _blue, PrimitiveType.Cube, false);
            Part(_root, "HallBandSouth", new Vector3(midX, g + 0.6f, Z0 + 0.11f), new Vector3(lenX, 1.2f, 0.02f), _blue, PrimitiveType.Cube, false);
            Part(_root, "HallBandNorth", new Vector3((X0 - 6f) * 0.5f, g + 0.6f, Z1 - 0.11f), new Vector3(-6f - X0, 1.2f, 0.02f), _blue, PrimitiveType.Cube, false);
            Part(_root, "HallStripeWest", new Vector3(X0 + 0.12f, g + 1.22f, midZ), new Vector3(0.02f, 0.04f, lenZ), _yellow, PrimitiveType.Cube, false);
            Part(_root, "HallStripeSouth", new Vector3(midX, g + 1.22f, Z0 + 0.12f), new Vector3(lenX, 0.04f, 0.02f), _yellow, PrimitiveType.Cube, false);
            Part(_root, "HallStripeNorth", new Vector3((X0 - 6f) * 0.5f, g + 1.22f, Z1 - 0.12f), new Vector3(-6f - X0, 0.04f, 0.02f), _yellow, PrimitiveType.Cube, false);

            // Lucernarios altos.
            Material sky = Glow("Skylight", new Color(0.62f, 0.78f, 0.93f));
            for (float z = Z0 + 2f; z < Z1 - 1f; z += 3f)
                Part(_root, "Skylight", new Vector3(X0 + 0.12f, g + 5.2f, z), new Vector3(0.02f, 1.0f, 2.2f), sky, PrimitiveType.Cube, false);
            for (float x = X0 + 2f; x < X1 - 1f; x += 3f)
                Part(_root, "Skylight", new Vector3(x, g + 5.2f, Z0 + 0.12f), new Vector3(2.2f, 1.0f, 0.02f), sky, PrimitiveType.Cube, false);

            // Cerchas y campanas de iluminacion.
            for (float x = X0 + 2f; x < X1; x += 4f)
            {
                Part(_root, "RoofTruss", new Vector3(x, g + H - 0.2f, midZ), new Vector3(0.18f, 0.35f, lenZ), _steel, PrimitiveType.Cube, false);
                foreach (float z in new[] { -9.5f, -5.6f, -1.5f, 2f })
                {
                    if (x > -4.6f && z > -4.5f) continue; // sobre la celda ya hay luminarias
                    Part(_root, "HighBay", new Vector3(x, g + H - 0.72f, z), new Vector3(0.55f, 0.07f, 0.55f), _dark, PrimitiveType.Cylinder, false);
                    Part(_root, "HighBayLens", new Vector3(x, g + H - 0.80f, z), new Vector3(0.44f, 0.012f, 0.44f), _glowWhite, PrimitiveType.Cylinder, false);
                    Part(_root, "HighBayRod", new Vector3(x, g + H - 0.50f, z), new Vector3(0.02f, 0.30f, 0.02f), _dark, PrimitiveType.Cube, false);
                }
            }

            // Columnas y conducto de ventilacion.
            for (float x = X0 + 0.4f; x < -5f; x += 5.8f)
            {
                Part(_root, "Column", new Vector3(x, g + H * 0.5f, Z1 - 0.3f), new Vector3(0.3f, H, 0.3f), _steel);
                Part(_root, "Column", new Vector3(x, g + H * 0.5f, Z0 + 0.3f), new Vector3(0.3f, H, 0.3f), _steel);
            }
            Transform duct = Part(_root, "VentDuct", new Vector3(-9f, g + 5.0f, Z0 + 1.1f), new Vector3(0.6f, 8.5f, 0.6f), _galva, PrimitiveType.Cylinder, false);
            duct.localRotation = Quaternion.Euler(0f, 0f, 90f);

            // Porton de carga en el fondo sur.
            Part(_root, "DockDoor", new Vector3(-1f, g + 2f, Z0 + 0.13f), new Vector3(4f, 4f, 0.05f), _galva, PrimitiveType.Cube, false);
            for (int i = 1; i < 10; i++)
                Part(_root, "DockDoorRib", new Vector3(-1f, g + i * 0.4f, Z0 + 0.165f), new Vector3(4f, 0.025f, 0.02f), _dark, PrimitiveType.Cube, false);
            foreach (float x in new[] { -3.1f, 1.1f })
                Part(_root, "DockDoorFrame", new Vector3(x, g + 2.1f, Z0 + 0.16f), new Vector3(0.2f, 4.2f, 0.1f), _yellow, PrimitiveType.Cube, false);
            Part(_root, "DockDoorLintel", new Vector3(-1f, g + 4.15f, Z0 + 0.16f), new Vector3(4.4f, 0.2f, 0.1f), _yellow, PrimitiveType.Cube, false);

            // Senalizacion de piso: pasillo peatonal y perimetro de la linea.
            Material green = Lit("Walkway", new Color(0.10f, 0.42f, 0.26f));
            Part(_root, "Walkway", new Vector3(-5.35f, g + 0.002f, -3.75f), new Vector3(1.2f, 0.004f, 14.5f), green, PrimitiveType.Cube, false);
            foreach (float x in new[] { -6.7f, -10.4f })
                Part(_root, "LineMarking", new Vector3(x, g + 0.003f, -2.5f), new Vector3(0.07f, 0.004f, 12f), _yellow, PrimitiveType.Cube, false);
            foreach (float z in new[] { -8.5f, 3.5f })
                Part(_root, "LineMarking", new Vector3(-8.55f, g + 0.003f, z), new Vector3(3.77f, 0.004f, 0.07f), _yellow, PrimitiveType.Cube, false);

            if (_lite) return;
            foreach (float z in new[] { -6f, -2.5f, 1f })
            {
                Light light = Group(_root, "LineLight", new Vector3(-9.5f, g + 4f, z)).gameObject.AddComponent<Light>();
                light.type = LightType.Point;
                light.range = 7.5f;
                light.intensity = 1.1f;
                light.color = new Color(0.86f, 0.93f, 1f);
                light.shadows = LightShadows.None;
            }
        }

        // La pared izquierda de la celda pasa a ser una valla: deja ver la linea.
        void BuildCellFence()
        {
            _hiddenWall = GameObject.Find("ParedIzq")?.GetComponent<Renderer>();
            if (_hiddenWall == null) return;
            _hiddenWall.enabled = false;
            float g = _ground, x = -4f;
            for (int i = 0; i <= 8; i++)
                Part(_root, "FencePost", new Vector3(x, g + 1.05f, -4f + i), new Vector3(0.06f, 2.1f, 0.06f), _yellow);
            Part(_root, "FenceTopRail", new Vector3(x, g + 2.1f, 0f), new Vector3(0.05f, 0.05f, 8f), _yellow);
            Part(_root, "FenceLowRail", new Vector3(x, g + 0.32f, 0f), new Vector3(0.04f, 0.04f, 8f), _yellow);
            Part(_root, "FenceKickPlate", new Vector3(x, g + 0.08f, 0f), new Vector3(0.03f, 0.16f, 8f), _dark);
            Material panel = Glow("FencePanel", new Color(0.55f, 0.70f, 0.80f, 0.10f));
            Part(_root, "FencePanel", new Vector3(x, g + 1.2f, 0f), new Vector3(0.01f, 1.75f, 8f), panel, PrimitiveType.Cube, false);
        }

        // ------------------------------------------------------------ linea
        void BuildConveyor()
        {
            float g = _ground, length = LineZ1 - LineZ0, midZ = (LineZ0 + LineZ1) * 0.5f;
            Part(_root, "ConveyorBelt", new Vector3(LineX, g + BeltTop - 0.015f, midZ), new Vector3(0.8f, 0.03f, length), _belt);
            foreach (float side in new[] { -0.43f, 0.43f })
                Part(_root, "ConveyorRail", new Vector3(LineX + side, g + BeltTop, midZ), new Vector3(0.05f, 0.12f, length + 0.1f), _galva);
            for (int i = 0; i < 8; i++)
            {
                float z = LineZ0 + 0.15f + i * (length - 0.3f) / 7f;
                foreach (float side in new[] { -0.4f, 0.4f })
                    Part(_root, "ConveyorLeg", new Vector3(LineX + side, g + 0.4f, z), new Vector3(0.05f, 0.8f, 0.05f), _dark);
                Part(_root, "ConveyorBrace", new Vector3(LineX, g + 0.28f, z), new Vector3(0.8f, 0.04f, 0.04f), _dark);
            }
            foreach (float z in new[] { LineZ0, LineZ1 })
                Part(_root, "ConveyorRoller", new Vector3(LineX, g + BeltTop - 0.06f, z), new Vector3(0.12f, 0.41f, 0.12f), _steel, PrimitiveType.Cylinder)
                    .localRotation = Quaternion.Euler(0f, 0f, 90f);
            Part(_root, "ConveyorMotor", new Vector3(LineX + 0.62f, g + 0.62f, LineZ0 + 0.3f), new Vector3(0.3f, 0.3f, 0.42f), _blue);

            // Tuneles de alimentacion e inspeccion: ocultan la reposicion de piezas.
            Tunnel("ALIMENTACION", LineZ0 + 0.55f);
            Tunnel("INSPECCION", LineZ1 - 0.55f);

            const int count = 8;
            for (int i = 0; i < count; i++)
            {
                Transform part = Group(_root, "LinePart", new Vector3(LineX, g + BeltTop, PartZ0 + i * (PartZ1 - PartZ0) / count));
                Part(part, "PartPlate", new Vector3(0f, 0.01f, 0f), new Vector3(0.5f, 0.02f, 0.36f), _steel);
                foreach (float side in new[] { -0.2f, 0.2f })
                    Part(part, "PartRail", new Vector3(side, 0.06f, 0f), new Vector3(0.05f, 0.08f, 0.36f), _galva);
                Part(part, "PartTube", new Vector3(0f, 0.075f, 0f), new Vector3(0.35f, 0.05f, 0.05f), _galva);
                _parts.Add(part);
            }

            // Materia prima junto a la alimentacion.
            for (int i = 0; i < 2; i++)
            {
                Vector3 p = new Vector3(LineX - 0.2f + i * 1.3f, g, LineZ0 - 1.6f);
                Part(_root, "StockPallet", p + Vector3.up * 0.07f, new Vector3(1.0f, 0.14f, 0.8f), _wood);
                for (int k = 0; k < 4 + i * 2; k++)
                    Part(_root, "StockPlate", p + Vector3.up * (0.16f + k * 0.035f), new Vector3(0.8f, 0.03f, 0.6f), _steel);
            }
        }

        void Tunnel(string text, float z)
        {
            float g = _ground;
            foreach (float side in new[] { -0.52f, 0.52f })
                Part(_root, "TunnelSide", new Vector3(LineX + side, g + 1.19f, z), new Vector3(0.04f, 0.66f, 1.1f), _blue);
            Part(_root, "TunnelTop", new Vector3(LineX, g + 1.54f, z), new Vector3(1.08f, 0.04f, 1.1f), _blue);
            Part(_root, "TunnelLamp", new Vector3(LineX + 0.545f, g + 1.42f, z), new Vector3(0.012f, 0.05f, 0.9f), _glowGreen, PrimitiveType.Cube, false);
            Label(_root, text, new Vector3(LineX + 0.55f, g + 1.22f, z), -90f, 0.95f, 0.16f, Color.white);
        }

        void BuildRobot(int index, float z)
        {
            float g = _ground;
            LineRobot robot = new LineRobot { Z = z, Phase = index * 2.1f };
            Part(_root, "RobotPedestal", new Vector3(RobotX, g + 0.15f, z), new Vector3(0.5f, 0.15f, 0.5f), _dark, PrimitiveType.Cylinder);
            robot.Turret = Group(_root, "LineRobot" + (index + 1), new Vector3(RobotX, g + 0.3f, z));
            Part(robot.Turret, "Turret", new Vector3(0f, 0.11f, 0f), new Vector3(0.42f, 0.11f, 0.42f), _orange, PrimitiveType.Cylinder);
            Part(robot.Turret, "ShoulderMount", new Vector3(-0.05f, 0.30f, 0f), new Vector3(0.22f, 0.20f, 0.26f), _orange);
            robot.Shoulder = Group(robot.Turret, "Shoulder", new Vector3(0f, 0.4f, 0f));
            Part(robot.Shoulder, "ShoulderJoint", Vector3.zero, new Vector3(0.30f, 0.16f, 0.30f), _dark, PrimitiveType.Cylinder)
                .localRotation = Quaternion.Euler(90f, 0f, 0f);
            Part(robot.Shoulder, "UpperArm", new Vector3(0f, 0.4f, 0f), new Vector3(0.16f, 0.8f, 0.18f), _orange);
            robot.Elbow = Group(robot.Shoulder, "Elbow", new Vector3(0f, 0.8f, 0f));
            Part(robot.Elbow, "ElbowJoint", Vector3.zero, new Vector3(0.24f, 0.13f, 0.24f), _dark, PrimitiveType.Cylinder)
                .localRotation = Quaternion.Euler(90f, 0f, 0f);
            Part(robot.Elbow, "Forearm", new Vector3(0f, 0.31f, 0f), new Vector3(0.12f, 0.62f, 0.13f), _orange);
            Part(robot.Elbow, "Wrist", new Vector3(0f, 0.65f, 0f), new Vector3(0.11f, 0.05f, 0.11f), _dark, PrimitiveType.Cylinder);
            Part(robot.Elbow, "Torch", new Vector3(0f, 0.78f, 0f), new Vector3(0.035f, 0.09f, 0.035f), _steel, PrimitiveType.Cylinder);
            robot.Spark = Part(robot.Elbow, "WeldSpark", new Vector3(0f, 0.88f, 0f), Vector3.one * 0.05f, _glowArc, PrimitiveType.Sphere, false);
            if (!_lite)
            {
                robot.Glow = robot.Spark.gameObject.AddComponent<Light>();
                robot.Glow.type = LightType.Point;
                robot.Glow.range = 2.2f;
                robot.Glow.color = new Color(0.70f, 0.85f, 1f);
                robot.Glow.shadows = LightShadows.None;
                robot.Glow.intensity = 0f;
            }

            // Armario de control y baliza de estado.
            Part(_root, "RobotCabinet", new Vector3(RobotX - 0.95f, g + 0.55f, z - 0.55f), new Vector3(0.45f, 1.1f, 0.6f), _galva);
            Part(_root, "RobotCabinetPanel", new Vector3(RobotX - 0.72f, g + 0.80f, z - 0.55f), new Vector3(0.01f, 0.28f, 0.36f), _dark);
            Vector3 pole = new Vector3(RobotX - 0.95f, g, z - 0.55f);
            Part(_root, "AndonPole", pole + Vector3.up * 1.25f, new Vector3(0.04f, 0.15f, 0.04f), _dark, PrimitiveType.Cylinder);
            robot.Amber = Part(_root, "AndonAmber", pole + Vector3.up * 1.45f, new Vector3(0.10f, 0.05f, 0.10f), _lampOff, PrimitiveType.Cylinder, false).GetComponent<Renderer>();
            robot.Green = Part(_root, "AndonGreen", pole + Vector3.up * 1.56f, new Vector3(0.10f, 0.05f, 0.10f), _lampOff, PrimitiveType.Cylinder, false).GetComponent<Renderer>();

            // Cartel colgante, legible desde la celda.
            Part(_root, "StationSign", new Vector3(RobotX - 0.5f, g + 2.75f, z), new Vector3(0.04f, 0.30f, 1.0f), _dark, PrimitiveType.Cube, false);
            Label(_root, "ESTACION R" + (index + 1), new Vector3(RobotX - 0.47f, g + 2.75f, z), -90f, 0.9f, 0.2f, Color.white);
            foreach (float side in new[] { -0.4f, 0.4f })
                Part(_root, "StationSignRod", new Vector3(RobotX - 0.5f, g + 4.85f, z + side), new Vector3(0.012f, 3.9f, 0.012f), _dark, PrimitiveType.Cube, false);
            _robots.Add(robot);
        }

        void BuildAgv()
        {
            float g = _ground;
            _agvPath = new[] { new Vector3(-5.3f, g, -5.7f), new Vector3(3.0f, g, -5.7f), new Vector3(3.0f, g, -10.4f), new Vector3(-5.3f, g, -10.4f) };
            _agv = Group(_root, "LineAGV", _agvPath[0]);
            Part(_agv, "AgvSkirt", new Vector3(0f, 0.07f, 0f), new Vector3(0.74f, 0.10f, 1.14f), _dark);
            Part(_agv, "AgvBody", new Vector3(0f, 0.20f, 0f), new Vector3(0.70f, 0.20f, 1.10f), _yellow);
            Part(_agv, "AgvDeck", new Vector3(0f, 0.315f, 0f), new Vector3(0.62f, 0.03f, 1.0f), _dark);
            Part(_agv, "AgvScanner", new Vector3(0f, 0.20f, 0.56f), new Vector3(0.16f, 0.05f, 0.16f), _dark, PrimitiveType.Cylinder);
            Part(_agv, "AgvPallet", new Vector3(0f, 0.38f, -0.05f), new Vector3(0.60f, 0.10f, 0.80f), _wood);
            Part(_agv, "AgvCrate", new Vector3(0f, 0.60f, -0.05f), new Vector3(0.52f, 0.34f, 0.70f), _blue);
            Part(_agv, "AgvBeaconBase", new Vector3(0.26f, 0.35f, 0.44f), new Vector3(0.05f, 0.03f, 0.05f), _dark, PrimitiveType.Cylinder);
            _beacon = Part(_agv, "AgvBeacon", new Vector3(0.26f, 0.41f, 0.44f), new Vector3(0.07f, 0.03f, 0.07f), _glowAmber, PrimitiveType.Cylinder, false).GetComponent<Renderer>();
            // Carril guiado.
            Material lane = Lit("AgvLane", new Color(0.10f, 0.30f, 0.55f));
            for (int i = 0; i < _agvPath.Length; i++)
            {
                Vector3 a = _agvPath[i], b = _agvPath[(i + 1) % _agvPath.Length];
                Vector3 size = Mathf.Abs(a.x - b.x) > 0.1f ? new Vector3(Mathf.Abs(a.x - b.x), 0.004f, 0.06f) : new Vector3(0.06f, 0.004f, Mathf.Abs(a.z - b.z));
                Part(_root, "AgvLane", (a + b) * 0.5f + Vector3.up * 0.0035f, size, lane, PrimitiveType.Cube, false);
            }
        }

        void BuildCrane()
        {
            float g = _ground, midZ = (Z0 + Z1) * 0.5f;
            foreach (float z in new[] { Z0 + 0.5f, Z1 - 0.6f })
                Part(_root, "CraneRunway", new Vector3(-11.4f, g + 5.55f, z), new Vector3(12.8f, 0.3f, 0.25f), _steel, PrimitiveType.Cube, false);
            _crane = Group(_root, "BridgeCrane", new Vector3(-13.2f, g + 5.95f, 0f));
            foreach (float side in new[] { -0.45f, 0.45f })
                Part(_crane, "CraneGirder", new Vector3(side, 0f, midZ), new Vector3(0.3f, 0.5f, Z1 - Z0 - 1.1f), _yellow, PrimitiveType.Cube, false);
            _trolley = Group(_crane, "CraneTrolley", new Vector3(0f, 0f, -4f));
            Part(_trolley, "TrolleyBody", new Vector3(0f, 0.05f, 0f), new Vector3(1.2f, 0.3f, 0.9f), _dark, PrimitiveType.Cube, false);
            Part(_trolley, "TrolleyCable", new Vector3(0f, -0.95f, 0f), new Vector3(0.025f, 0.85f, 0.025f), _dark, PrimitiveType.Cylinder, false);
            Part(_trolley, "TrolleyHook", new Vector3(0f, -1.9f, 0f), new Vector3(0.22f, 0.24f, 0.12f), _yellow);
        }

        void BuildStorage()
        {
            float g = _ground;
            System.Random random = new System.Random(7);
            Material[] loads = { _wood, _blue, _galva, _wood };
            // Estanteria de palets contra la pared oeste.
            for (int bay = 0; bay <= 4; bay++)
            {
                float z = -10.5f + bay * 3f;
                foreach (float x in new[] { -17.55f, -16.65f })
                    Part(_root, "RackUpright", new Vector3(x, g + 2.3f, z), new Vector3(0.08f, 4.6f, 0.08f), _blue);
                if (bay == 4) break;
                for (int level = 0; level < 3; level++)
                {
                    float y = g + 1.4f + level * 1.45f;
                    foreach (float x in new[] { -17.55f, -16.65f })
                        Part(_root, "RackBeam", new Vector3(x, y, z + 1.5f), new Vector3(0.06f, 0.10f, 3f), _orange);
                    if (_lite) continue;
                    for (int slot = 0; slot < 2; slot++)
                    {
                        if (random.Next(5) == 0) continue;
                        float height = 0.45f + 0.15f * random.Next(4);
                        Vector3 p = new Vector3(-17.1f, y + 0.05f, z + 0.8f + slot * 1.4f);
                        Part(_root, "RackPallet", p + Vector3.up * 0.05f, new Vector3(1.0f, 0.10f, 1.1f), _wood);
                        Part(_root, "RackLoad", p + Vector3.up * (0.10f + height * 0.5f), new Vector3(0.9f, height, 1.0f), loads[random.Next(loads.Length)]);
                    }
                }
                if (_lite) continue;
                for (int slot = 0; slot < 2; slot++)
                {
                    float height = 0.5f + 0.15f * random.Next(3);
                    Vector3 p = new Vector3(-17.1f, g, z + 0.8f + slot * 1.4f);
                    Part(_root, "RackPallet", p + Vector3.up * 0.07f, new Vector3(1.0f, 0.14f, 1.1f), _wood);
                    Part(_root, "RackLoad", p + Vector3.up * (0.14f + height * 0.5f), new Vector3(0.9f, height, 1.0f), loads[random.Next(loads.Length)]);
                }
            }
            // Producto terminado a la salida de la linea y cilindros de gas.
            for (int i = 0; i < 3; i++)
            {
                Vector3 p = new Vector3(-12.2f - i * 1.3f, g, 2.6f);
                Part(_root, "FinishedPallet", p + Vector3.up * 0.07f, new Vector3(1.0f, 0.14f, 0.8f), _wood);
                for (int k = 0; k <= i + 1; k++)
                    Part(_root, "FinishedCrate", p + Vector3.up * (0.33f + k * 0.38f), new Vector3(0.85f, 0.36f, 0.7f), k % 2 == 0 ? _blue : _galva);
            }
            Material gas = Lit("GasCylinder", new Color(0.35f, 0.42f, 0.30f), 0.4f, 0.5f);
            for (int i = 0; i < 5; i++)
                Part(_root, "GasCylinder", new Vector3(-11.9f + i * 0.3f, g + 0.7f, -11.2f), new Vector3(0.23f, 0.7f, 0.23f), gas, PrimitiveType.Cylinder);
            Part(_root, "GasCylinderRack", new Vector3(-11.3f, g + 0.9f, -11.05f), new Vector3(1.7f, 0.05f, 0.05f), _yellow);
        }

        void BuildSigns()
        {
            float g = _ground;
            Vector3 board = new Vector3(-10f, g + 3.7f, Z1 - 0.15f);
            Part(_root, "LineBoard", board, new Vector3(5.2f, 1.7f, 0.06f), _dark, PrimitiveType.Cube, false);
            Part(_root, "LineBoardAccent", board + new Vector3(0f, -0.78f, -0.035f), new Vector3(5.2f, 0.05f, 0.01f), _glowGreen, PrimitiveType.Cube, false);
            Label(_root, "LINEA 2  ·  SOLDADURA ROBOTIZADA", board + new Vector3(0f, 0.42f, -0.04f), 0f, 4.7f, 0.42f, Color.white);
            _counter = Label(_root, "PRODUCCION  000 / 240     ESTADO  EN MARCHA", board + new Vector3(0f, -0.25f, -0.04f), 0f, 4.7f, 0.30f,
                new Color(0.45f, 1f, 0.60f));
            UpdateCounter();
            Label(_root, "SALIDA", new Vector3(-1f, g + 4.6f, Z0 + 0.25f), 180f, 1.6f, 0.4f, new Color(0.45f, 1f, 0.60f));
        }

        void UpdateCounter()
        {
            if (_counter != null)
                _counter.text = "PRODUCCION  " + _produced.ToString("000") + " / 240     ESTADO  EN MARCHA";
        }

        void ApplyFog()
        {
            _oldFog = RenderSettings.fog; _oldFogColor = RenderSettings.fogColor; _oldFogMode = RenderSettings.fogMode;
            _oldFogStart = RenderSettings.fogStartDistance; _oldFogEnd = RenderSettings.fogEndDistance;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.21f, 0.24f, 0.27f);
            RenderSettings.fogStartDistance = 16f;
            RenderSettings.fogEndDistance = 70f;
        }

        // ------------------------------------------------------------ animacion
        void Update()
        {
            float dt = Mathf.Min(Time.deltaTime, 0.05f), t = Time.time;

            _beltOffset = Mathf.Repeat(_beltOffset - ConveyorSpeed * dt * 2f, 1f);
            _belt.mainTextureOffset = new Vector2(0f, _beltOffset);
            foreach (Transform part in _parts)
            {
                Vector3 p = part.localPosition;
                p.z += ConveyorSpeed * dt;
                if (p.z > PartZ1)
                {
                    p.z -= PartZ1 - PartZ0;
                    _produced = _produced >= 240 ? 1 : _produced + 1;
                    UpdateCounter();
                }
                part.localPosition = p;
            }

            foreach (LineRobot robot in _robots)
            {
                bool busy = false;
                foreach (Transform part in _parts)
                    if (Mathf.Abs(part.localPosition.z - robot.Z) < 0.34f) { busy = true; break; }
                robot.Weld = Mathf.MoveTowards(robot.Weld, busy ? 1f : 0f, dt * 1.2f);
                float w = Mathf.SmoothStep(0f, 1f, robot.Weld), ph = robot.Phase;
                robot.Turret.localRotation = Quaternion.Euler(0f, w * 7f * Mathf.Sin(t * 0.9f + ph), 0f);
                robot.Shoulder.localRotation = Quaternion.Euler(0f, 0f, -Mathf.Lerp(14f, 30f + 1.5f * Mathf.Sin(t * 1.3f + ph), w));
                robot.Elbow.localRotation = Quaternion.Euler(0f, 0f, -Mathf.Lerp(70f, 95f + 2f * Mathf.Sin(t * 1.1f + ph), w));
                bool arc = robot.Weld > 0.97f;
                float flicker = arc ? 0.6f + 0.4f * Mathf.PerlinNoise(t * 22f, ph) : 0f;
                robot.Spark.localScale = Vector3.one * (0.09f * flicker);
                if (robot.Glow != null) robot.Glow.intensity = 3.2f * flicker;
                robot.Green.sharedMaterial = arc ? _glowGreen : _lampOff;
                robot.Amber.sharedMaterial = arc ? _lampOff : _glowAmber;
            }

            if (_agv != null)
            {
                Vector3 target = _agvPath[_agvIndex], to = target - _agv.localPosition;
                if (to.magnitude < 0.05f) _agvIndex = (_agvIndex + 1) % _agvPath.Length;
                else
                {
                    Quaternion look = Quaternion.LookRotation(to.normalized, Vector3.up);
                    _agv.localRotation = Quaternion.RotateTowards(_agv.localRotation, look, 70f * dt);
                    // Avanza cuando ya esta orientado: gira en el lugar como un AGV real.
                    if (Quaternion.Angle(_agv.localRotation, look) < 12f)
                        _agv.localPosition = Vector3.MoveTowards(_agv.localPosition, target, AgvSpeed * dt);
                }
                _beacon.enabled = Mathf.Repeat(t, 0.8f) < 0.4f;
            }

            if (_crane != null)
            {
                Vector3 p = _crane.localPosition;
                p.x = -13.6f + 2.4f * Mathf.Sin(t * 0.05f);
                _crane.localPosition = p;
                _trolley.localPosition = new Vector3(0f, 0f, -4f + 3.5f * Mathf.Sin(t * 0.07f + 1f));
            }
        }

        void OnDestroy()
        {
            if (_hiddenWall != null) _hiddenWall.enabled = true;
            RenderSettings.fog = _oldFog; RenderSettings.fogColor = _oldFogColor; RenderSettings.fogMode = _oldFogMode;
            RenderSettings.fogStartDistance = _oldFogStart; RenderSettings.fogEndDistance = _oldFogEnd;
            foreach (Object owned in _owned) if (owned != null) Destroy(owned);
            if (_textMaterial != null) { Destroy(_textMaterial); _textMaterial = null; }
        }
    }
}
