using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Logica de la tarea: clasificar piezas por color en los contenedores correctos.
///
/// Estados: Briefing -> Running -> Finished.
/// Lleva metricas (tiempo, aciertos, errores) que son lo que convierte esto en un
/// simulador de entrenamiento y no en una demo suelta.
/// </summary>
public class TaskManager : MonoBehaviour
{
    public enum State { Briefing, Running, Finished }

    [Header("Referencias")]
    public List<Graspable> pieces = new List<Graspable>();
    public List<SortingBin> bins = new List<SortingBin>();
    public PandaArm arm;
    public AudioCues audioCues;
    public GraspDetector graspDetector;

    [Header("Reglas")]
    [Tooltip("Penalizacion en segundos por cada pieza mal clasificada.")]
    public float errorPenaltySeconds = 10f;

    [Header("Estado (solo lectura)")]
    public State state = State.Briefing;
    public int correct;
    public int errors;
    public float elapsed;

    readonly HashSet<Graspable> done = new HashSet<Graspable>();
    bool wasHolding;

    public int Total => pieces.Count;
    public int Remaining => Total - done.Count;
    public float Score => elapsed + errors * errorPenaltySeconds;

    void Start()
    {
        foreach (var b in bins)
            if (b != null) b.OnPieceDropped += HandleDrop;
    }

    void OnDestroy()
    {
        foreach (var b in bins)
            if (b != null) b.OnPieceDropped -= HandleDrop;
    }

    void Update()
    {
        // Sonido al agarrar y soltar.
        if (graspDetector != null && audioCues != null)
        {
            bool holding = graspDetector.Held != null;
            if (holding && !wasHolding) audioCues.Grab();
            if (!holding && wasHolding) audioCues.Release();
            wasHolding = holding;
        }

        if (state != State.Running) return;

        elapsed += Time.deltaTime;

        if (done.Count >= Total && Total > 0)
        {
            state = State.Finished;
            if (audioCues != null) audioCues.Finish();
        }
    }

    /// <summary>Arranca la ronda. El cronometro empieza aca.</summary>
    public void StartRound()
    {
        if (graspDetector != null) graspDetector.ForceRelease();
        done.Clear();
        correct = 0; errors = 0; elapsed = 0f;
        foreach (var p in pieces) if (p != null) p.ResetToSpawn();
        if (arm != null) arm.ResetHome();
        wasHolding = false;
        state = State.Running;
    }

    public void ResetAll()
    {
        if (graspDetector != null) graspDetector.ForceRelease();
        state = State.Briefing;
        done.Clear();
        correct = 0; errors = 0; elapsed = 0f;
        foreach (var p in pieces) if (p != null) p.ResetToSpawn();
        if (arm != null) arm.ResetHome();
        wasHolding = false;
    }

    void HandleDrop(SortingBin bin, Graspable piece, bool ok)
    {
        if (state != State.Running) return;
        if (done.Contains(piece)) return; // no contar dos veces

        done.Add(piece);
        if (ok) { correct++; audioCues?.Success(); }
        else    { errors++;  audioCues?.Error(); }
    }

    /// <summary>Texto para el panel del operario.</summary>
    public string StatusText()
    {
        switch (state)
        {
            case State.Briefing:
                return "CLASIFICACION DE PIEZAS\n\n" +
                       $"Colocá las {Total} piezas en el\ncontenedor de su color.\n\n" +
                       "START para comenzar";

            case State.Running:
                return $"TIEMPO  {elapsed,6:F1} s\n" +
                       $"OK {correct}   ERR {errors}\n" +
                       $"RESTAN  {Remaining}";

            default:
                return "TAREA COMPLETADA\n\n" +
                       $"Tiempo    {elapsed:F1} s\n" +
                       $"Correctas {correct}/{Total}\n" +
                       $"Errores   {errors}\n" +
                       $"PUNTAJE   {Score:F1}\n\n" +
                       "START para repetir";
        }
    }
}
