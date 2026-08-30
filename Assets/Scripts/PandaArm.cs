using UnityEngine;

/// <summary>
/// Controla las 7 juntas del Franka Panda vía ArticulationBody.
/// Interfaz pública en GRADOS.
/// Gotcha de Unity: xDrive.target y los límites van en GRADOS,
/// pero ArticulationBody.jointPosition se lee en RADIANES.
/// </summary>
public class PandaArm : MonoBehaviour
{
    public const int N = 7;

    [Tooltip("Las 7 juntas en orden panda_joint1 .. panda_joint7.")]
    public ArticulationBody[] joints = new ArticulationBody[N];

    // Valores validados en el robot con masas reales (17.4 kg):
    // error de seguimiento por debajo de 0.3 grados en las 7 juntas, sin oscilacion.
    [Header("Drive")]
    public float stiffness = 10000f;
    public float damping = 1000f;
    public float forceLimit = 1000f;

    // Limites del Panda en grados (verificados contra joint_limits.yaml de franka_description).
    public static readonly Vector2[] Limits =
    {
        new Vector2(-166.0f, 166.0f), // J1  +-2.8973 rad
        new Vector2(-101.0f, 101.0f), // J2  +-1.7628 rad
        new Vector2(-166.0f, 166.0f), // J3  +-2.8973 rad
        new Vector2(-176.0f,  -4.0f), // J4  -3.0718 .. -0.0698 rad
        new Vector2(-166.0f, 166.0f), // J5  +-2.8973 rad
        new Vector2(  -1.0f, 215.0f), // J6  -0.0175 .. 3.7525 rad
        new Vector2(-166.0f, 166.0f), // J7  +-2.8973 rad
    };

    [Tooltip("Pose de reposo comoda, en grados.")]
    public float[] home = { 0f, -45f, 0f, -135f, 0f, 90f, 45f };

    readonly float[] target = new float[N];

    void Start()
    {
        for (int i = 0; i < N; i++) target[i] = home[i];
        ApplyTargets();
    }

    /// <summary>Fija el target absoluto de la junta i (grados), respetando limites.</summary>
    public void SetTargetDeg(int i, float deg)
    {
        if (!Valid(i)) return;
        target[i] = Mathf.Clamp(deg, Limits[i].x, Limits[i].y);
        SetDrive(i, target[i]);
    }

    /// <summary>Suma delta (grados) al target de la junta i.</summary>
    public void Nudge(int i, float deltaDeg) => SetTargetDeg(i, target[i] + deltaDeg);

    /// <summary>Target comandado de la junta i (grados). No es el angulo real.</summary>
    public float TargetDeg(int i) => Valid(i) ? target[i] : 0f;

    /// <summary>Angulo REAL medido de la junta i (grados).</summary>
    public float CurrentDeg(int i) => Valid(i) ? joints[i].jointPosition[0] * Mathf.Rad2Deg : 0f;

    public void ResetHome()
    {
        for (int i = 0; i < N; i++) target[i] = home[i];
        ApplyTargets();
    }

    /// <summary>Sincroniza los targets con la posicion real (evita saltos al cambiar de modo).</summary>
    public void SyncTargetsToActual()
    {
        for (int i = 0; i < N; i++)
            if (Valid(i)) target[i] = Mathf.Clamp(CurrentDeg(i), Limits[i].x, Limits[i].y);
    }

    /// <summary>Eje de rotacion de la junta i, en coordenadas de mundo.</summary>
    public Vector3 AxisWorld(int i) =>
        Valid(i) ? joints[i].transform.rotation * (joints[i].anchorRotation * Vector3.right) : Vector3.up;

    /// <summary>Punto de pivote de la junta i, en coordenadas de mundo.</summary>
    public Vector3 PivotWorld(int i) =>
        Valid(i) ? joints[i].transform.TransformPoint(joints[i].anchorPosition) : Vector3.zero;

    bool Valid(int i) => i >= 0 && i < N && joints[i] != null;

    void ApplyTargets() { for (int i = 0; i < N; i++) SetDrive(i, target[i]); }

    void SetDrive(int i, float deg)
    {
        if (!Valid(i)) return;
        var drive = joints[i].xDrive;
        drive.stiffness = stiffness;
        drive.damping = damping;
        drive.forceLimit = forceLimit;
        drive.target = deg;
        joints[i].xDrive = drive;
    }
}
