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
        public WeldingEnvironmentPolish Guidance;

        [Header("Inicio")]
        public bool PrepareOnStart = true;
        [Range(0, 4)] public int SelectedExercise = 0;
        public CourseModeV5 Mode = CourseModeV5.Training;

        [Header("Controles PC de prueba")]
        public bool KeyboardShortcuts = true;

        [Header("Estado (solo lectura)")]
        public CoursePhaseV5 Phase = CoursePhaseV5.Selection;
        public string CurrentOrderId = "N1";
        public string LastResultPath;
        public string ResultSaveError;
        [Tooltip("Vacio: carpeta Resultados en persistentDataPath. Puede cambiarse para pruebas.")]
        public string ResultsDirectoryOverride;
        public bool UsesPipeIndexer => CurrentOrderId == "N5" && Mode == CourseModeV5.Training;
        bool _resultSaved;

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
            if (Guidance == null) Guidance = FindObjectOfType<WeldingEnvironmentPolish>();
        }

        void Start()
        {
            if (PrepareOnStart) PrepareSelected();
            Phase = CoursePhaseV5.Selection;
        }

        void Update()
        {
            if (Guidance != null)
                Guidance.SetGuidanceVisible(Phase != CoursePhaseV5.Running || Mode == CourseModeV5.Training);
            if (Task != null && Task.State == SessionState.Finished && Phase == CoursePhaseV5.Running)
            {
                Phase = CoursePhaseV5.Results;
                SaveResult();
            }

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
            if (Input.GetKeyDown(KeyCode.G) && Phase == CoursePhaseV5.Results) SaveResult();
            if (Input.GetKeyDown(KeyCode.Z)) IndexPipe(-15f);
            if (Input.GetKeyDown(KeyCode.X)) IndexPipe(15f);
        }

        public bool IndexPipe(float degrees)
        {
            if (!UsesPipeIndexer || Phase != CoursePhaseV5.Running || Workpieces == null || Task == null) return false;
            if (Task.Torch != null && (Task.Torch.SparkRequested || Task.Torch.Arcing || Task.Torch.Stuck)) return false;
            return Workpieces.IndexPipe(degrees);
        }

        public bool SaveResult()
        {
            if (Task == null || Task.State != SessionState.Finished) return false;
            if (_resultSaved) return true;
            try
            {
                LastResultPath = WeldingSessionReport.Save(Task, Mode.ToString(),
                    UsesPipeIndexer, ResultsDirectoryOverride);
                ResultSaveError = "";
                _resultSaved = true;
                return true;
            }
            catch (System.Exception ex)
            {
                ResultSaveError = "No se pudo guardar. G: reintentar";
                Debug.LogWarning("[Resultados] " + ex.Message);
                return false;
            }
        }

        public void SelectExercise(int index)
        {
            if (Phase == CoursePhaseV5.Running) return;
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
            if (Task != null) Task.ResetTask();
            if (Workpieces != null) Workpieces.BuildFor(o);
            if (Task != null) Task.LoadWorkOrder(o.Id);
        }

        public void StartTraining()
        {
            if (Phase == CoursePhaseV5.Running) return;
            Mode = CourseModeV5.Training;
            BeginSelected();
        }

        public void StartExam()
        {
            if (Phase == CoursePhaseV5.Running) return;
            Mode = CourseModeV5.Exam;
            BeginSelected();
        }

        void BeginSelected()
        {
            _resultSaved = false;
            LastResultPath = "";
            ResultSaveError = "";
            PrepareSelected();
            if (Task != null) Task.StartTask();
            Phase = CoursePhaseV5.Running;
        }

        public void FinishExercise()
        {
            if (Phase != CoursePhaseV5.Running) return;
            if (Task != null) Task.FinishTask();
            Phase = CoursePhaseV5.Results;
            SaveResult();
        }

        public void RestartExercise()
        {
            BeginSelected();
        }

        public void BackToMenu()
        {
            if (Task != null) Task.ResetTask();
            Phase = CoursePhaseV5.Selection;
        }

        public string PhaseLabel()
        {
            switch (Phase)
            {
                case CoursePhaseV5.Selection: return "SELECCION DE EJERCICIO";
                case CoursePhaseV5.Briefing:  return "INSTRUCCIONES";
                case CoursePhaseV5.Running:   return Mode == CourseModeV5.Training
                    ? (UsesPipeIndexer ? "PRACTICA POR SECTORES" : "ENTRENAMIENTO") : "EXAMEN";
                case CoursePhaseV5.Results:   return "RESULTADOS";
                default: return "";
            }
        }
    }
}
