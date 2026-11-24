using System.Collections.Generic;
using System.IO;
using UnityEngine;
using FrankaVR.Welding;

namespace FrankaVR.V5
{
    public enum CoursePhaseV5 { Selection, Briefing, Running, Results }
    public enum CourseModeV5 { Training, Exam }
    public enum CourseStageV5 { Teach, Execute }

    /// <summary>
    /// Flujo de curso: seleccionar trabajo -> instrucciones -> ensenar la trayectoria ->
    /// ejecutarla con el robot -> resultado. La demostracion se exporta como datos.
    /// No reemplaza WeldingTaskManager; lo orquesta.
    /// </summary>
    public class CourseTrainingManagerV5 : MonoBehaviour
    {
        public WeldingTaskManager Task;
        public TrainingWorkpieceFactoryV5 Workpieces;
        public WeldingEnvironmentPolish Guidance;
        public WeldingTeleopController Teleop;
        public WeldPathRecorder Recorder;
        [Header("Programa")]
        public float ProgramSpeedMmS = 6f;
        public float MinProgramSpeedMmS = 2f, MaxProgramSpeedMmS = 12f;

        [Header("Inicio")]
        public bool PrepareOnStart = true;
        [Range(0, 4)] public int SelectedExercise = 0;
        public CourseModeV5 Mode = CourseModeV5.Training;

        [Header("Controles PC de prueba")]
        public bool KeyboardShortcuts = true;

        [Header("Estado (solo lectura)")]
        public CoursePhaseV5 Phase = CoursePhaseV5.Selection;
        public CourseStageV5 Stage = CourseStageV5.Teach;
        public string ProgramMessage = "";
        public string LastDatasetPath;
        readonly List<Vector3> _programPositions = new List<Vector3>(), _programDirections = new List<Vector3>();
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
            if (Teleop == null) Teleop = FindObjectOfType<WeldingTeleopController>();
            if (Recorder == null) Recorder = FindObjectOfType<WeldPathRecorder>();
            if (Recorder == null) Recorder = gameObject.AddComponent<WeldPathRecorder>();
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
            if (Phase == CoursePhaseV5.Running && Stage == CourseStageV5.Execute && Teleop != null && Teleop.PlaybackDone)
                FinishExercise();
            if (Recorder != null) Recorder.SetTrailVisible(Phase != CoursePhaseV5.Selection);
            ControllerActions();

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
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) ExecuteProgram();
            if (Input.GetKeyDown(KeyCode.Z)) { if (UsesPipeIndexer) IndexPipe(-15f); else UndoStroke(); }
            if (Input.GetKeyDown(KeyCode.X)) IndexPipe(15f);
            if (Input.GetKeyDown(KeyCode.R)) ClearProgram();
            if (Input.GetKeyDown(KeyCode.Minus) || Input.GetKeyDown(KeyCode.KeypadMinus)) StepProgramSpeed(-0.5f);
            if (Input.GetKeyDown(KeyCode.Equals) || Input.GetKeyDown(KeyCode.KeypadPlus)) StepProgramSpeed(0.5f);
        }

        FrankaVR.Teleop.HandControllerInput _hand;
        FrankaVR.V6.CourseWorldMenuV6 _menu;
        float _nextHandSearch;

        // Mando ESP32: OK = clic en el boton que se mira; sin boton, en ENSENAR ejecuta.
        void ControllerActions()
        {
            if (_hand == null && Time.unscaledTime >= _nextHandSearch)
            { _nextHandSearch = Time.unscaledTime + 1f; _hand = FindAnyObjectByType<FrankaVR.Teleop.HandControllerInput>(); }
            if (_hand == null) return;
            if (_hand.ConsumeOk())
            {
                GazeInteractor gaze = FindAnyObjectByType<GazeInteractor>();
                if (_menu == null) _menu = FindAnyObjectByType<FrankaVR.V6.CourseWorldMenuV6>();
                if (gaze != null && gaze.Current != null && gaze.Current.isActiveAndEnabled) gaze.Current.Activate();
                else if (_menu != null && _menu.ControlsOpen) _menu.SetControlsOpen(false); // con el documento abierto, OK lo cierra
                else if (FrankaVR.V6.OperatorMoveV6.Active) FrankaVR.V6.OperatorMoveV6.ConfirmFromController(); // MOVERSE: OK lleva al punto marcado, nunca ejecuta
                else if (Phase == CoursePhaseV5.Running && Stage == CourseStageV5.Teach) ExecuteProgram();
            }
            if (_hand.ConsumeUndo()) { if (UsesPipeIndexer) IndexPipe(-15f); else UndoStroke(); }
            if (_hand.ConsumeClear()) ClearProgram();
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
                    UsesPipeIndexer, ResultsDirectoryOverride, Recorder, ProgramSpeedMmS);
                if (Recorder != null)
                {
                    string basePath = Path.Combine(Path.GetDirectoryName(LastResultPath), Path.GetFileNameWithoutExtension(LastResultPath) + "_trayectoria");
                    Recorder.Export(basePath, CurrentOrderId, Mode.ToString(), ProgramSpeedMmS);
                    LastDatasetPath = basePath + ".json";
                }
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
            Stage = CourseStageV5.Teach;
            ProgramMessage = "";
            if (SelectedOrder != null) ProgramSpeedMmS = SelectedOrder.RecommendedSpeedMmS;
            if (Teleop != null) { Teleop.StopPlayback(); Teleop.ArcEnabled = false; }
            if (Recorder != null)
            {
                Recorder.Clear();
                Recorder.Teaching = true;
                Recorder.ShowQualityColors = Mode == CourseModeV5.Training;
            }
        }

        /// El robot ejecuta solo la trayectoria ensenada, con arco y a la velocidad programada.
        public bool ExecuteProgram()
        {
            if (Phase != CoursePhaseV5.Running || Stage != CourseStageV5.Teach || Teleop == null || Recorder == null) return false;
            if (Recorder.Count < 10) { ProgramMessage = "GRABÁ LA TRAYECTORIA ANTES DE EJECUTAR"; return false; }
            Recorder.GetProgram(_programPositions, _programDirections);
            if (!Teleop.BeginPlayback(_programPositions, _programDirections, ProgramSpeedMmS * 0.001f))
            { ProgramMessage = "TRAYECTORIA DEMASIADO CORTA"; return false; }
            Recorder.Teaching = false;
            Teleop.ArcEnabled = false;
            Stage = CourseStageV5.Execute;
            ProgramMessage = "";
            return true;
        }

        public void ClearProgram()
        {
            if (Phase == CoursePhaseV5.Running && Stage == CourseStageV5.Teach && Recorder != null) Recorder.Clear();
        }

        public void UndoStroke()
        {
            if (Phase == CoursePhaseV5.Running && Stage == CourseStageV5.Teach && Recorder != null) Recorder.UndoStroke();
        }

        public void StepProgramSpeed(float deltaMmS)
        {
            if (Stage == CourseStageV5.Execute && Phase == CoursePhaseV5.Running) return;
            ProgramSpeedMmS = Mathf.Clamp(ProgramSpeedMmS + deltaMmS, MinProgramSpeedMmS, MaxProgramSpeedMmS);
        }

        public void FinishExercise()
        {
            if (Phase != CoursePhaseV5.Running) return;
            if (Teleop != null) Teleop.StopPlayback();
            if (Recorder != null) Recorder.Teaching = false;
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
            if (Teleop != null) Teleop.StopPlayback();
            if (Recorder != null) { Recorder.Teaching = false; Recorder.Clear(); }
            Phase = CoursePhaseV5.Selection;
        }

        public string PhaseLabel()
        {
            switch (Phase)
            {
                case CoursePhaseV5.Selection: return "SELECCION DE EJERCICIO";
                case CoursePhaseV5.Briefing:  return "INSTRUCCIONES";
                case CoursePhaseV5.Running:
                    string mode = Mode == CourseModeV5.Training ? "PRÁCTICA" : "EXAMEN";
                    return (Stage == CourseStageV5.Teach ? "ENSEÑAR" : "EJECUTANDO") + " · " + mode;
                case CoursePhaseV5.Results:   return "RESULTADOS";
                default: return "";
            }
        }
    }
}
