using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace FrankaVR.Welding
{
    /// <summary>
    /// Graba la trayectoria que el operario ensena con el gatillo sobre la junta. Cada muestra
    /// guarda posicion y eje de la torcha, distancia boquilla-pieza, angulos, desvio y colision.
    /// Sirve para que el robot la ejecute, para evaluar al operario y como datos de demostracion
    /// (JSON/CSV) para entrenar un robot en otra aplicacion.
    /// </summary>
    public class WeldPathRecorder : MonoBehaviour
    {
        [System.Serializable]
        public struct Sample
        {
            public float t;
            public Vector3 tip, dir;
            public float along, lateral, ctwd, travel, work, quality;
            public bool collision;
            public int stroke;
        }

        [System.Serializable]
        class Dataset
        {
            public int schemaVersion = 1;
            public string exercise, wire, mode;
            public float amps, programSpeedMmS, idealCtwdMm, idealTravelDeg, idealWorkDeg;
            public Vector3 seamStart, seamEnd;
            public Sample[] samples;
        }

        public WeldingTeleopController Teleop;
        public WeldingTorch Torch;
        public WeldingTaskManager Task;
        public RobotCollisionGuard Collisions;
        [Tooltip("Lo maneja el curso: graba solo mientras se ensena.")]
        public bool Teaching;
        [Tooltip("Practica: rastro coloreado por calidad. Examen: rastro neutro.")]
        public bool ShowQualityColors = true;
        public float MinStepM = 0.0008f, MinTurnDeg = 1f, MaxSeamDistanceM = 0.03f;
        public float TrailWidth = 0.0025f;

        readonly List<Sample> _samples = new List<Sample>();
        readonly List<LineRenderer> _lines = new List<LineRenderer>();
        Material _lineMat;
        Transform _trailRoot;
        int _stroke;
        bool _wasRecording, _dirty;
        float _time, _rebuildTimer, _statsTimer;

        public IReadOnlyList<Sample> Samples => _samples;
        public int Count => _samples.Count;
        public bool RecordingNow { get; private set; }
        public string Hint { get; private set; } = "";
        public float CoveragePercent { get; private set; }
        public float QualityPercent { get; private set; }
        public int CollisionSamples { get; private set; }

        void Awake()
        {
            if (Teleop == null) Teleop = FindObjectOfType<WeldingTeleopController>();
            if (Torch == null) Torch = FindObjectOfType<WeldingTorch>();
            if (Task == null) Task = FindObjectOfType<WeldingTaskManager>();
            Shader shader = Shader.Find("Sprites/Default");
            if (shader != null) _lineMat = new Material(shader) { name = "FrankaVR_TeachTrail" };
            _trailRoot = new GameObject("TeachTrail").transform;
        }

        void Update()
        {
            if (Collisions == null) Collisions = FindObjectOfType<RobotCollisionGuard>();
            bool want = Teaching && Teleop != null && Torch != null && Teleop.TriggerHeld && !Teleop.Playing &&
                !Teleop.Approaching && Task != null && Task.State == SessionState.Running;
            Hint = "";
            Vector3 tip = Torch != null ? Torch.ArcOriginPosition : Vector3.zero;
            Vector3 p = Vector3.zero, n = Vector3.up, t = Vector3.right;
            if (want && !(Teleop.NearestSeam(tip, out p, out n, out t) && Vector3.Distance(p, tip) < MaxSeamDistanceM))
            {
                Hint = "ACERCÁ LA TORCHA A LA JUNTA PARA GRABAR";
                want = false;
            }
            if (want && !_wasRecording) _stroke++;
            _wasRecording = RecordingNow = want;
            if (want) _time += Time.deltaTime;
            if (want) TryAdd(tip, Torch.PointingDir, p, n, t);
            if (_dirty && (_rebuildTimer -= Time.deltaTime) <= 0f) { _rebuildTimer = 0.1f; RebuildTrail(); }
            if ((_statsTimer -= Time.deltaTime) <= 0f) { _statsTimer = 0.25f; UpdateStats(); }
        }

        void TryAdd(Vector3 tip, Vector3 dir, Vector3 p, Vector3 n, Vector3 t)
        {
            if (_samples.Count > 0)
            {
                Sample last = _samples[_samples.Count - 1];
                if (last.stroke == _stroke && Vector3.Distance(last.tip, tip) < MinStepM && Vector3.Angle(last.dir, dir) < MinTurnDeg) return;
            }
            WeldSeam seam = Task.Seam;
            Vector3 b = Vector3.Cross(n, t).normalized;
            WeldingHeatModel.ToolAngles(-dir, t, n, out float travel, out float work);
            Electrode e = Teleop.Machine != null ? Teleop.Machine.Current : null;
            bool surface = Torch.HasArcSurface && Torch.ArcLenMm < 40f;
            float arcQ = surface && e != null ? WeldingHeatModel.ArcLengthFactor(Torch.ArcLenMm, e) : 0f;
            float angleQ = WeldingHeatModel.AngleQuality(travel, work, seam.IdealTravelAngle, seam.IdealWorkAngle, seam.AngleToleranceDeg);
            float lateral = Vector3.Dot(tip - p, b);
            float lateralQ = Mathf.Clamp01(1f - (Mathf.Abs(lateral) - 0.0015f) / 0.003f);
            bool collision = Collisions != null && Collisions.Colliding;
            _samples.Add(new Sample
            {
                t = _time, tip = tip, dir = dir, stroke = _stroke,
                along = Vector3.Dot(p - seam.PointAt(0), t),
                lateral = lateral,
                ctwd = surface ? Torch.ArcLenMm + Torch.RemainingMm : -1f,
                travel = travel, work = work, collision = collision,
                quality = collision ? 0f : Mathf.Min(arcQ, angleQ, lateralQ)
            });
            _dirty = true;
        }

        public void Clear()
        {
            _samples.Clear();
            _time = 0f;
            _dirty = true;
            RebuildTrail();
            UpdateStats();
        }

        /// Deshace el ultimo trazo continuo (desde que se apreto el gatillo hasta que se solto).
        public void UndoStroke()
        {
            if (_samples.Count == 0) return;
            int stroke = _samples[_samples.Count - 1].stroke;
            _samples.RemoveAll(s => s.stroke == stroke);
            _dirty = true;
            RebuildTrail();
            UpdateStats();
        }

        public void SetTrailVisible(bool visible) { if (_trailRoot != null) _trailRoot.gameObject.SetActive(visible); }

        public void GetProgram(List<Vector3> positions, List<Vector3> directions)
        {
            positions.Clear(); directions.Clear();
            foreach (Sample s in _samples) { positions.Add(s.tip); directions.Add(s.dir); }
        }

        void UpdateStats()
        {
            WeldSeam seam = Task != null ? Task.Seam : null;
            if (seam == null || seam.Count < 2 || _samples.Count == 0)
            { CoveragePercent = QualityPercent = 0f; CollisionSamples = 0; return; }
            Vector3 start = seam.PointAt(0), dir = (seam.PointAt(seam.Count - 1) - start).normalized;
            int covered = 0;
            for (int i = 0; i < seam.Count; i++)
            {
                float along = Vector3.Dot(seam.PointAt(i) - start, dir);
                foreach (Sample s in _samples)
                    if (Mathf.Abs(s.along - along) < 0.002f && Mathf.Abs(s.lateral) < 0.004f) { covered++; break; }
            }
            CoveragePercent = 100f * covered / seam.Count;
            float sum = 0f; int collisions = 0;
            foreach (Sample s in _samples) { sum += s.quality; if (s.collision) collisions++; }
            QualityPercent = 100f * sum / _samples.Count;
            CollisionSamples = collisions;
        }

        Color ColorFor(Sample s)
        {
            if (!ShowQualityColors) return new Color(0.75f, 0.9f, 1f, 0.95f);
            if (s.collision) return new Color(1f, 0.1f, 0.6f, 1f);
            if (s.quality >= 0.75f) return new Color(0.25f, 1f, 0.45f, 1f);
            if (s.quality >= 0.45f) return new Color(1f, 0.75f, 0.15f, 1f);
            return new Color(1f, 0.2f, 0.12f, 1f);
        }

        // Un LineRenderer por tramo continuo del mismo color (LineRenderer no admite color por vertice).
        void RebuildTrail()
        {
            _dirty = false;
            int used = 0, i = 0;
            var points = new List<Vector3>();
            while (i < _samples.Count)
            {
                Color c = ColorFor(_samples[i]);
                int stroke = _samples[i].stroke;
                points.Clear();
                if (i > 0 && _samples[i - 1].stroke == stroke) points.Add(_samples[i - 1].tip);
                while (i < _samples.Count && _samples[i].stroke == stroke && ColorFor(_samples[i]) == c) points.Add(_samples[i++].tip);
                if (points.Count < 2) continue;
                LineRenderer line = Line(used++);
                line.positionCount = points.Count;
                line.SetPositions(points.ToArray());
                line.startColor = line.endColor = c;
            }
            for (int k = used; k < _lines.Count; k++) _lines[k].enabled = false;
        }

        LineRenderer Line(int index)
        {
            while (_lines.Count <= index)
            {
                var go = new GameObject("TeachTrailSegment");
                go.transform.SetParent(_trailRoot, false);
                var line = go.AddComponent<LineRenderer>();
                line.useWorldSpace = true;
                line.sharedMaterial = _lineMat;
                line.widthMultiplier = TrailWidth;
                line.numCapVertices = 2;
                line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                line.receiveShadows = false;
                _lines.Add(line);
            }
            _lines[index].enabled = true;
            return _lines[index];
        }

        /// Exporta la demostracion: basePath + ".json" y basePath + ".csv".
        public void Export(string basePath, string exercise, string mode, float programSpeedMmS)
        {
            WeldSeam seam = Task != null ? Task.Seam : null;
            Electrode e = Teleop != null && Teleop.Machine != null ? Teleop.Machine.Current : null;
            var data = new Dataset
            {
                exercise = exercise, mode = mode, programSpeedMmS = programSpeedMmS,
                wire = e != null ? e.Name : "", amps = Teleop != null && Teleop.Machine != null ? Teleop.Machine.Amperage : 0f,
                idealCtwdMm = e != null && Torch != null ? e.ArcLenIdealMm + Torch.RemainingMm : 0f,
                idealTravelDeg = seam != null ? seam.IdealTravelAngle : 0f, idealWorkDeg = seam != null ? seam.IdealWorkAngle : 0f,
                seamStart = seam != null && seam.Count > 0 ? seam.PointAt(0) : Vector3.zero,
                seamEnd = seam != null && seam.Count > 0 ? seam.PointAt(seam.Count - 1) : Vector3.zero,
                samples = _samples.ToArray()
            };
            File.WriteAllText(basePath + ".json", JsonUtility.ToJson(data, true), new UTF8Encoding(false));
            var inv = CultureInfo.InvariantCulture;
            var csv = new StringBuilder("t_s,trazo,x,y,z,dir_x,dir_y,dir_z,avance_mm,desvio_mm,distancia_mm,angulo_avance,angulo_trabajo,calidad,colision\n");
            foreach (Sample s in _samples)
                csv.AppendFormat(inv, "{0:0.###},{1},{2:0.#####},{3:0.#####},{4:0.#####},{5:0.####},{6:0.####},{7:0.####},{8:0.##},{9:0.##},{10:0.##},{11:0.#},{12:0.#},{13:0.###},{14}\n",
                    s.t, s.stroke, s.tip.x, s.tip.y, s.tip.z, s.dir.x, s.dir.y, s.dir.z, s.along * 1000f, s.lateral * 1000f,
                    s.ctwd, s.travel, s.work, s.quality, s.collision ? 1 : 0);
            File.WriteAllText(basePath + ".csv", csv.ToString(), new UTF8Encoding(true));
        }

        void OnDestroy()
        {
            if (_lineMat != null) Destroy(_lineMat);
            if (_trailRoot != null) Destroy(_trailRoot.gameObject);
        }
    }
}
