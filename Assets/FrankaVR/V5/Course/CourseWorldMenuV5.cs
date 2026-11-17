using UnityEngine;
using FrankaVR.Welding;

namespace FrankaVR.V5
{
    /// <summary>
    /// Menu 3D muy liviano. Funciona con teclado y, si GazeInteractor/GazeButton ya existen,
    /// tambien puede activarse por mirada.
    /// </summary>
    public class CourseWorldMenuV5 : MonoBehaviour
    {
        public CourseTrainingManagerV5 Course;
        public Camera PlayerCamera;
        public float Distance = 1.75f;
        public float HorizontalOffset = 0.65f;
        public float VerticalOffset = 0.05f;
        public bool FollowCameraYaw = false;

        Transform _root;
        TextMesh _title;
        Material _panelMat;
        bool _lastVisible;

        void Start()
        {
            if (Course == null) Course = FindObjectOfType<CourseTrainingManagerV5>();
            if (PlayerCamera == null) PlayerCamera = Camera.main;
            Build();
        }

        void Build()
        {
            if (PlayerCamera == null || Course == null) return;

            GameObject rootGo = new GameObject("CourseMenuPanelV5");
            _root = rootGo.transform;
            _root.SetParent(transform, true);

            Shader sh = Shader.Find("Standard");
            if (sh == null) sh = Shader.Find("Diffuse");
            if (sh != null)
            {
                _panelMat = new Material(sh);
                _panelMat.color = new Color(0.07f, 0.09f, 0.11f, 1f);
            }

            GameObject back = GameObject.CreatePrimitive(PrimitiveType.Cube);
            back.name = "MenuBackground";
            back.transform.SetParent(_root, false);
            back.transform.localScale = new Vector3(0.92f, 0.76f, 0.025f);
            if (_panelMat != null) back.GetComponent<Renderer>().sharedMaterial = _panelMat;
            Destroy(back.GetComponent<Collider>());

            GameObject t = new GameObject("MenuText");
            t.transform.SetParent(_root, false);
            t.transform.localPosition = new Vector3(0f, 0.28f, -0.018f);
            _title = t.AddComponent<TextMesh>();
            _title.anchor = TextAnchor.MiddleCenter;
            _title.alignment = TextAlignment.Center;
            _title.fontSize = 52;
            _title.characterSize = 0.008f;
            _title.color = new Color(0.92f, 0.95f, 1f);

            WorkOrder[] catalog = WorkOrder.Catalog;
            for (int i = 0; i < catalog.Length; i++)
                MakeButton("N" + (i + 1), new Vector3(-0.30f + i * 0.15f, 0.00f, -0.035f), i);

            MakeActionButton("ENTRENAR", new Vector3(-0.21f, -0.20f, -0.035f), true);
            MakeActionButton("EXAMEN", new Vector3(0.21f, -0.20f, -0.035f), false);

            PlaceInitial();
        }

        void PlaceInitial()
        {
            if (_root == null || PlayerCamera == null) return;
            Transform c = PlayerCamera.transform;
            Vector3 flatFwd = Vector3.ProjectOnPlane(c.forward, Vector3.up).normalized;
            if (flatFwd.sqrMagnitude < 1e-5f) flatFwd = Vector3.forward;
            Vector3 flatRight = Vector3.Cross(Vector3.up, flatFwd).normalized;
            _root.position = c.position + flatFwd * Distance + flatRight * HorizontalOffset + Vector3.up * VerticalOffset;
            _root.rotation = Quaternion.LookRotation(_root.position - c.position, Vector3.up);
        }

        void MakeButton(string label, Vector3 localPos, int index)
        {
            GameObject b = MakeButtonBase("Exercise_" + label, label, localPos, new Vector3(0.12f, 0.09f, 0.035f));
            GazeButton gb = b.AddComponent<GazeButton>();
            gb.label = label;
            gb.highlight = b.GetComponent<Renderer>();
            int captured = index;
            gb.onActivateAction = () => Course.SelectExercise(captured);
        }

        void MakeActionButton(string label, Vector3 localPos, bool training)
        {
            GameObject b = MakeButtonBase(label, label, localPos, new Vector3(0.30f, 0.10f, 0.035f));
            GazeButton gb = b.AddComponent<GazeButton>();
            gb.label = label;
            gb.highlight = b.GetComponent<Renderer>();
            if (training) gb.onActivateAction = () => Course.StartTraining();
            else gb.onActivateAction = () => Course.StartExam();
        }

        GameObject MakeButtonBase(string name, string label, Vector3 localPos, Vector3 scale)
        {
            GameObject b = GameObject.CreatePrimitive(PrimitiveType.Cube);
            b.name = name;
            b.transform.SetParent(_root, false);
            b.transform.localPosition = localPos;
            b.transform.localScale = scale;

            GameObject tg = new GameObject("Text");
            tg.transform.SetParent(b.transform, false);
            tg.transform.localPosition = new Vector3(0f, 0f, -0.53f);
            TextMesh tm = tg.AddComponent<TextMesh>();
            tm.text = label;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.fontSize = 50;
            tm.characterSize = 0.09f;
            tm.color = Color.white;
            return b;
        }

        void LateUpdate()
        {
            if (Course == null || _root == null) return;
            bool visible = Course.Phase == CoursePhaseV5.Selection || Course.Phase == CoursePhaseV5.Briefing || Course.Phase == CoursePhaseV5.Results;
            if (visible != _lastVisible)
            {
                _root.gameObject.SetActive(visible);
                _lastVisible = visible;
            }
            if (!visible) return;

            WorkOrder o = Course.SelectedOrder;
            if (_title != null && o != null)
            {
                string bottom = Course.Phase == CoursePhaseV5.Results && Course.Task != null
                    ? "NOTA " + Course.Task.FinalScore.ToString("0") + "/100  |  M = MENU"
                    : "1-5 elegir  |  T entrenar  |  Y examen";
                _title.text = "CURSO DE SOLDADURA\n" + o.Id + " - " + o.Title + "\n\n" + bottom;
            }

            if (FollowCameraYaw) PlaceInitial();
        }

        void OnDestroy()
        {
            if (_panelMat != null) Destroy(_panelMat);
        }
    }
}
