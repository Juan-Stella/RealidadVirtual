using UnityEngine;
using FrankaVR.Welding;

namespace FrankaVR.V5
{
    public enum CoursePhaseV5 { Selection, Briefing, Running, Results }
    public enum CourseModeV5 { Training, Exam }

    /// <summary>
    /// Flujo simple de curso: seleccionar ejercicio -> briefing -> practica/examen -> resultado.
    /// No reemplaza WeldingTaskManager; lo orquesta.
    /// </summary>
    public class CourseTrainingManagerV5 : MonoBehaviour
    {
        public WeldingTaskManager Task;
        public TrainingWorkpieceFactoryV5 Workpieces;

        [Header("Inicio")]
        public bool PrepareOnStart = true;
        [Range(0, 4)] public int SelectedExercise = 0;
        public CourseModeV5 Mode = CourseModeV5.Training;

        [Header("Controles PC de prueba")]
        public bool KeyboardShortcuts = true;

        [Header("Estado (solo lectura)")]
        public CoursePhaseV5 Phase = CoursePhaseV5.Selection;
        public string CurrentOrderId = "N1";

        public WorkOrder SelectedOrder
        {
            get
            {
                WorkOrder[] c = WorkOrder.Catalog;
                if (c == null || c.Length == 0) return null;
                int i = Mathf.Clamp(SelectedExercise, 0, c.Length - 1);
                return c[i];
            }
        }

        void Awake()
        {
            if (Task == null) Task = FindObjectOfType<WeldingTaskManager>();
            if (Workpieces == null) Workpieces = FindObjectOfType<TrainingWorkpieceFactoryV5>();
        }

        void Start()
        {
            if (PrepareOnStart) PrepareSelected();
            Phase = CoursePhaseV5.Selection;
        }

        void Update()
        {
            if (Task != null && Task.State == SessionState.Finished && Phase == CoursePhaseV5.Running)
                Phase = CoursePhaseV5.Results;

            if (!KeyboardShortcuts) return;

            if (Input.GetKeyDown(KeyCode.Alpha1)) SelectExercise(0);
            if (Input.GetKeyDown(KeyCode.Alpha2)) SelectExercise(1);
            if (Input.GetKeyDown(KeyCode.Alpha3)) SelectExercise(2);
            if (Input.GetKeyDown(KeyCode.Alpha4)) SelectExercise(3);
            if (Input.GetKeyDown(KeyCode.Alpha5)) SelectExercise(4);

            if (Input.GetKeyDown(KeyCode.T)) StartTraining();
            if (Input.GetKeyDown(KeyCode.Y)) StartExam();
            if (Input.GetKeyDown(KeyCode.F) && Phase == CoursePhaseV5.Running) FinishExercise();
            if (Input.GetKeyDown(KeyCode.M)) BackToMenu();
        }

        public void SelectExercise(int index)
        {
            WorkOrder[] catalog = WorkOrder.Catalog;
            if (catalog == null || catalog.Length == 0) return;
            SelectedExercise = Mathf.Clamp(index, 0, catalog.Length - 1);
            PrepareSelected();
            Phase = CoursePhaseV5.Briefing;
        }

        public void NextExercise()
        {
            int count = WorkOrder.Catalog.Length;
            SelectExercise((SelectedExercise + 1) % count);
        }

        public void PreviousExercise()
        {
            int count = WorkOrder.Catalog.Length;
            SelectExercise((SelectedExercise - 1 + count) % count);
        }

        public void PrepareSelected()
        {
            WorkOrder o = SelectedOrder;
            if (o == null) return;

            CurrentOrderId = o.Id;
            if (Workpieces != null) Workpieces.BuildFor(o);
            if (Task != null) Task.LoadWorkOrder(o.Id);
        }

        public void StartTraining()
        {
            Mode = CourseModeV5.Training;
            BeginSelected();
        }

        public void StartExam()
        {
            Mode = CourseModeV5.Exam;
            BeginSelected();
        }

        void BeginSelected()
        {
            PrepareSelected();
            if (Task != null) Task.StartTask();
            Phase = CoursePhaseV5.Running;
        }

        public void FinishExercise()
        {
            if (Task != null) Task.FinishTask();
            Phase = CoursePhaseV5.Results;
        }

        public void RestartExercise()
        {
            BeginSelected();
        }

        public void BackToMenu()
        {
            if (Task != null && Task.State == SessionState.Running) Task.ResetTask();
            Phase = CoursePhaseV5.Selection;
        }

        public string PhaseLabel()
        {
            switch (Phase)
            {
                case CoursePhaseV5.Selection: return "SELECCION DE EJERCICIO";
                case CoursePhaseV5.Briefing:  return "INSTRUCCIONES";
                case CoursePhaseV5.Running:   return Mode == CourseModeV5.Training ? "ENTRENAMIENTO" : "EXAMEN";
                case CoursePhaseV5.Results:   return "RESULTADOS";
                default: return "";
            }
        }
    }
}
