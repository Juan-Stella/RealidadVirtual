using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace FrankaVR.Welding
{
    /// <summary>Exporta las metricas existentes; no cambia el modelo ni la nota.</summary>
    public static class WeldingSessionReport
    {
        [Serializable]
        public class Result
        {
            public int schemaVersion = 2;
            public string timestampUtc, exercise, title, mode;
            public bool indexedPipe, passed;
            public bool assistedControl;
            public float score, seconds, arcOnSeconds, meanArcMm, arcDeviationMm, coveragePercent;
            public int totalPoints, good, cold, burnThrough, undercut, porosity, untouched, sticks, arcBreaks, electrodes;
            public string advice;
        }

        public static Result Snapshot(WeldingTaskManager task, string mode, bool indexedPipe)
        {
            int total = task.Seam != null ? task.Seam.Count : 0;
            return new Result
            {
                timestampUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                exercise = task.WorkOrderId,
                assistedControl = task.Teleop != null && task.Teleop.UsedTrainingAssistance,
                title = task.Order != null ? task.Order.Title : "",
                mode = mode, indexedPipe = indexedPipe, passed = task.Passed(),
                score = task.FinalScore, seconds = task.Elapsed, arcOnSeconds = task.ArcOnTime,
                meanArcMm = task.MeanArcLenMm, arcDeviationMm = task.ArcLenStdMm,
                totalPoints = total, good = task.GoodPoints, cold = task.ColdPoints,
                burnThrough = task.BurnPoints, undercut = task.UndercutPoints,
                porosity = task.PorosityPoints, untouched = task.UntouchedPoints,
                sticks = task.StickCount, arcBreaks = task.ArcBreaks, electrodes = task.ElectrodesUsed,
                coveragePercent = total > 0 ? 100f * (total - task.UntouchedPoints) / total : 0f,
                advice = Advice(task)
            };
        }

        public static string Advice(WeldingTaskManager task)
        {
            if (task.UntouchedPoints == (task.Seam != null ? task.Seam.Count : 0))
                return "Acerca el electrodo al trazo y manten Espacio para soldar.";
            int max = Mathf.Max(task.ColdPoints, task.BurnPoints, task.UndercutPoints, task.PorosityPoints);
            if (max == 0) return task.UntouchedPoints > 0 ? "Completa los tramos que quedaron sin soldar." : "Mantiene este ritmo y la longitud de arco.";
            if (max == task.BurnPoints) return "Perforacion: evita quedarte en un punto y revisa la corriente.";
            if (max == task.UndercutPoints) return "Socavado: revisa los angulos de trabajo y avance.";
            if (max == task.PorosityPoints) return "Porosidad: busca un arco corto y constante.";
            return "Falta de fusion: reduce el avance o revisa la corriente.";
        }

        public static string Save(WeldingTaskManager task, string mode, bool indexedPipe, string directoryOverride = null)
        {
            if (task.State != SessionState.Finished) throw new InvalidOperationException("La sesion no termino");
            Result result = Snapshot(task, mode, indexedPipe);
            string folder = string.IsNullOrWhiteSpace(directoryOverride)
                ? Path.Combine(Application.persistentDataPath, "Resultados") : directoryOverride;
            Directory.CreateDirectory(folder);
            string name = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff", CultureInfo.InvariantCulture) +
                "_" + result.exercise + "_" + Guid.NewGuid().ToString("N").Substring(0, 6);
            string jsonPath = Path.Combine(folder, name + ".json");
            string[] headers = { "fecha_utc", "ejercicio", "modo", "cano_indexado", "control_asistido", "nota", "aprobado", "duracion_s", "arco_s",
                "arco_medio_mm", "desvio_arco_mm", "cobertura_pct", "puntos", "correctos", "frios", "perforacion", "socavado",
                "porosidad", "sin_soldar", "pegadas", "cortes", "electrodos", "recomendacion" };
            string[] values = { result.timestampUtc, result.exercise, result.mode, result.indexedPipe.ToString(), result.assistedControl.ToString(), N(result.score),
                result.passed.ToString(), N(result.seconds), N(result.arcOnSeconds), N(result.meanArcMm), N(result.arcDeviationMm),
                N(result.coveragePercent), result.totalPoints.ToString(), result.good.ToString(), result.cold.ToString(),
                result.burnThrough.ToString(), result.undercut.ToString(), result.porosity.ToString(), result.untouched.ToString(),
                result.sticks.ToString(), result.arcBreaks.ToString(), result.electrodes.ToString(), result.advice };
            for (int i = 0; i < values.Length; i++) values[i] = "\"" + values[i].Replace("\"", "\"\"") + "\"";
            File.WriteAllText(jsonPath, JsonUtility.ToJson(result, true), new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(folder, name + ".csv"), string.Join(",", headers) + "\n" + string.Join(",", values) + "\n", new UTF8Encoding(true));
            return Path.GetFullPath(jsonPath);
        }

        static string N(float value) => value.ToString("0.###", CultureInfo.InvariantCulture);
    }
}
