using UnityEngine;
using FrankaVR.V5;
using FrankaVR.Welding;

namespace FrankaVR.V6
{
    /// <summary>
    /// Panel lateral limpio con resumen del curso y atajos.
    /// No reemplaza la logica del curso; solo mejora presentacion.
    /// </summary>
    public class CourseWorldMenuV6 : MonoBehaviour
    {
        public CourseTrainingManagerV5 Course;
        public Camera PlayerCamera;
        public float Distance = 1.8f;
        public float SideOffset = -0.72f;
        public float VerticalOffset = 0.02f;
        public bool FollowCameraYaw = true;

        Transform _root;
        TextMesh _title, _exercise, _list, _keys;
        Material _bgMat;

        void Start()
        {
            if (Course == null) Course = FindObjectOfType<CourseTrainingManagerV5>();
            if (PlayerCamera == null) PlayerCamera = Camera.main;
            Build();
        }

        void Build()
        {
            if (PlayerCamera == null) return;
            GameObject root = new GameObject("CourseWorldMenuV6_Runtime");
            _root = root.transform;

            GameObject bg = GameObject.CreatePrimitive(PrimitiveType.Quad);
            bg.transform.SetParent(_root, false);
            bg.transform.localScale = new Vector3(0.92f, 0.72f, 1f);
            SafeDestroy(bg.GetComponent<Collider>());
            Shader sh = Shader.Find("Sprites/Default");
            if (sh != null)
            {
                _bgMat = new Material(sh); _bgMat.color = new Color(0.025f, 0.035f, 0.05f, 0.88f);
                bg.GetComponent<Renderer>().sharedMaterial = _bgMat;
            }

            _title = MakeText("Title", new Vector3(0f, 0.26f, -0.004f), 66, 0.0085f, new Color(0.95f, 0.98f, 1f), TextAnchor.MiddleCenter);
            _exercise = MakeText("Exercise", new Vector3(0f, 0.15f, -0.004f), 54, 0.0078f, new Color(0.82f, 0.90f, 1f), TextAnchor.MiddleCenter);
            _list = MakeText("List", new Vector3(-0.34f, -0.02f, -0.004f), 48, 0.0065f, new Color(0.85f, 0.90f, 0.95f), TextAnchor.MiddleLeft);
            _keys = MakeText("Keys", new Vector3(-0.34f, -0.25f, -0.004f), 42, 0.0062f, new Color(0.70f, 0.78f, 0.87f), TextAnchor.MiddleLeft);
        }

        TextMesh MakeText(string name, Vector3 localPos, int fontSize, float charSize, Color color, TextAnchor anchor)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(_root, false);
            go.transform.localPosition = localPos;
            TextMesh tm = go.AddComponent<TextMesh>();
            tm.anchor = anchor;
            tm.alignment = anchor == TextAnchor.MiddleLeft ? TextAlignment.Left : TextAlignment.Center;
            tm.fontSize = fontSize;
            tm.characterSize = charSize;
            tm.color = color;
            return tm;
        }

        void LateUpdate()
        {
            if (_root == null || PlayerCamera == null) return;
            Transform cam = PlayerCamera.transform;
            Vector3 fwd = cam.forward; fwd.y = 0f; if (fwd.sqrMagnitude < 1e-6f) fwd = Vector3.forward; else fwd.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, fwd).normalized;
            _root.position = cam.position + fwd * Distance + right * SideOffset + Vector3.up * VerticalOffset;
            _root.rotation = FollowCameraYaw
                ? Quaternion.LookRotation(_root.position - cam.position, Vector3.up)
                : Quaternion.identity;
            UpdateText();
        }

        void UpdateText()
        {
            _title.text = "CURSO DE SOLDADURA";
            if (Course != null && Course.SelectedOrder != null)
            {
                _exercise.text = Course.SelectedOrder.Id + "  ·  " + Course.SelectedOrder.Title + "\n" + Course.PhaseLabel();
            }
            else _exercise.text = "Seleccione un ejercicio";

            _list.text =
                "EJERCICIOS\n" +
                BuildExerciseLine(0, "N1  Placa") + "\n" +
                BuildExerciseLine(1, "N2  Tope") + "\n" +
                BuildExerciseLine(2, "N3  Filete T") + "\n" +
                BuildExerciseLine(3, "N4  Solape") + "\n" +
                BuildExerciseLine(4, "N5  Caño a tope");

            _keys.text =
                "ATAJOS\n" +
                "1-5  Elegir ejercicio\n" +
                "T    Entrenamiento\n" +
                "Y    Examen\n" +
                "F    Finalizar\n" +
                "M    Volver al menu\n" +
                "\nMOVERSE EN EL ESPACIO\n" +
                "Flechas  Desplazar\n" +
                "RePag/AvPag  Subir/Bajar\n" +
                "Shift  Rapido\n" +
                "Mouse derecho  Mirar";
        }

        string BuildExerciseLine(int idx, string label)
        {
            bool sel = Course != null && Course.SelectedExercise == idx;
            return (sel ? "▶ " : "   ") + label;
        }

        static void SafeDestroy(Object obj)
        {
            if (obj == null) return;
            if (Application.isPlaying) Destroy(obj);
            else DestroyImmediate(obj);
        }

        void OnDestroy() { if (_bgMat != null) SafeDestroy(_bgMat); }
    }
}
