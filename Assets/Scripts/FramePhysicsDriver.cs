using UnityEngine;

/// <summary>
/// Fisica sincronizada con el dibujado: en cada cuadro avanza exactamente el tiempo de ese
/// cuadro, en subpasos de hasta MaxSubstep, y antes de cada subpaso corre el servo de la IK.
///
/// Con el paso fijo de Unity, segun lo que durara el cuadro entraban 0, 1, 2 o 3 pasos de
/// fisica: el brazo avanzaba 0, 1.2, 2.5 o 3.7 mm por cuadro a velocidad constante y se veia
/// a los saltos (las ArticulationBody no se interpolan). Asi avanza lo mismo en cada cuadro.
/// FixedUpdate sigue existiendo (soldadura, deposicion): solo cambia quien mueve la fisica.
/// </summary>
[DefaultExecutionOrder(1000)] // despues del teleoperador: este cuadro ya usa el objetivo nuevo
public class FramePhysicsDriver : MonoBehaviour
{
    [Tooltip("Subpaso maximo de fisica (s). 1/120 da 2 subpasos a 60 fps.")]
    public float MaxSubstep = 1f / 120f;
    [Tooltip("Tope de subpasos por cuadro: un cuadro muy largo se simula mas lento, sin trabar.")]
    public int MaxSubsteps = 8;

    public static bool Active { get; private set; }
    SimulationMode _previous;
    PandaIK[] _servos = new PandaIK[0];
    float _nextScan;

    void OnEnable()
    {
        _previous = Physics.simulationMode;
        Physics.simulationMode = SimulationMode.Script;
        Active = true;
    }

    void OnDisable()
    {
        Physics.simulationMode = _previous == SimulationMode.Script ? SimulationMode.FixedUpdate : _previous;
        Active = false;
    }

    void Update()
    {
        if (Time.unscaledTime >= _nextScan)
        {
            _nextScan = Time.unscaledTime + 1f;
            _servos = FindObjectsByType<PandaIK>(FindObjectsSortMode.None);
        }
        float frame = Mathf.Min(Time.deltaTime, MaxSubstep * MaxSubsteps);
        if (frame <= 0f) return;
        int steps = Mathf.Max(1, Mathf.CeilToInt(frame / MaxSubstep - 1e-3f));
        float h = frame / steps;
        for (int i = 0; i < steps; i++)
        {
            foreach (PandaIK servo in _servos)
                if (servo != null && servo.isActiveAndEnabled) servo.ServoStep(h);
            Physics.Simulate(h);
        }
    }
}
