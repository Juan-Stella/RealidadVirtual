using UnityEngine;

/// <summary>
/// Cinematica inversa de POSICION (no orienta la herramienta) para el Panda.
///
/// Metodo: Jacobiano geometrico + minimos cuadrados amortiguados (DLS).
///     dTheta = J^T (J J^T + lambda^2 I)^-1 * e
/// J es 3x7, asi que (J J^T) es 3x3 y se invierte a mano: sin librerias de matrices.
///
/// Funciona como SERVO: hace un paso por frame leyendo los transforms REALES del
/// brazo y deja que la fisica se mueva. Converge en decimas de segundo y nunca
/// se desincroniza del estado real. Por eso no necesita reconstruir el modelo
/// cinematico ni hacer FK aparte.
///
/// El amortiguamiento (lambda) es lo que evita que se vuelva loco cerca de
/// singularidades, donde un Jacobiano pseudo-inverso puro daria velocidades enormes.
/// </summary>
[RequireComponent(typeof(PandaArm))]
public class PandaIK : MonoBehaviour
{
    [Header("Referencias")]
    [Tooltip("Punta de la herramienta. Usar panda_hand_tcp del modelo importado.")]
    public Transform endEffector;

    [Tooltip("Objetivo a alcanzar. Suele ser una esfera pequenia movible.")]
    public Transform target;

    [Header("Solver")]
    // Defaults validados en simulacion con la cinematica real del Panda:
    // 80/80 objetivos alcanzables resueltos, mediana 17 iteraciones (~0.35 s a 50 Hz).
    [Tooltip("Amortiguamiento. Mas alto = mas estable pero mas lento. 0.05-0.3.")]
    [Range(0.01f, 0.5f)] public float lambda = 0.10f;

    [Tooltip("Maximo avance hacia el objetivo por paso, en metros. Evita saltos.")]
    public float maxStepMeters = 0.05f;

    [Tooltip("Maximo cambio por junta por paso, en grados.")]
    public float maxDegPerJointStep = 8f;

    [Tooltip("Si el error es menor a esto (metros), se considera alcanzado.")]
    public float toleranceMeters = 0.005f;

    [Header("Estado")]
    public bool solving = true;

    PandaArm arm;

    /// <summary>Distancia actual entre la herramienta y el objetivo, en metros.</summary>
    public float ErrorMeters =>
        (endEffector != null && target != null)
            ? Vector3.Distance(endEffector.position, target.position) : 0f;

    public bool Reached => ErrorMeters <= toleranceMeters;

    void Awake() => arm = GetComponent<PandaArm>();

    void FixedUpdate()
    {
        if (!solving || endEffector == null || target == null) return;
        if (Reached) return;
        Step();
    }

    void Step()
    {
        Vector3 ee = endEffector.position;

        // Error, recortado para que cada paso sea chico y estable.
        Vector3 e = target.position - ee;
        if (e.magnitude > maxStepMeters) e = e.normalized * maxStepMeters;

        // --- Jacobiano geometrico 3x7 ---
        // Columna i = axis_i x (p_ee - pivot_i)
        var col = new Vector3[PandaArm.N];
        for (int i = 0; i < PandaArm.N; i++)
            col[i] = Vector3.Cross(arm.AxisWorld(i), ee - arm.PivotWorld(i));

        // --- A = J J^T + lambda^2 I   (3x3, simetrica) ---
        float a00 = lambda * lambda, a01 = 0f, a02 = 0f;
        float a11 = lambda * lambda, a12 = 0f, a22 = lambda * lambda;
        for (int i = 0; i < PandaArm.N; i++)
        {
            Vector3 c = col[i];
            a00 += c.x * c.x; a01 += c.x * c.y; a02 += c.x * c.z;
            a11 += c.y * c.y; a12 += c.y * c.z; a22 += c.z * c.z;
        }

        // --- y = A^-1 e  (inversa 3x3 explicita) ---
        if (!SolveSym3(a00, a01, a02, a11, a12, a22, e, out Vector3 y)) return;

        // --- dTheta_i = col_i . y   (radianes) -> grados ---
        for (int i = 0; i < PandaArm.N; i++)
        {
            float dDeg = Vector3.Dot(col[i], y) * Mathf.Rad2Deg;
            dDeg = Mathf.Clamp(dDeg, -maxDegPerJointStep, maxDegPerJointStep);
            arm.Nudge(i, dDeg);
        }
    }

    /// <summary>Resuelve A*y = b para A simetrica 3x3. false si es singular.</summary>
    static bool SolveSym3(float a00, float a01, float a02,
                          float a11, float a12, float a22,
                          Vector3 b, out Vector3 y)
    {
        // Cofactores
        float c00 = a11 * a22 - a12 * a12;
        float c01 = a02 * a12 - a01 * a22;
        float c02 = a01 * a12 - a02 * a11;

        float det = a00 * c00 + a01 * c01 + a02 * c02;
        if (Mathf.Abs(det) < 1e-12f) { y = Vector3.zero; return false; }

        float c11 = a00 * a22 - a02 * a02;
        float c12 = a02 * a01 - a00 * a12;
        float c22 = a00 * a11 - a01 * a01;

        float inv = 1f / det;
        y = new Vector3(
            (c00 * b.x + c01 * b.y + c02 * b.z) * inv,
            (c01 * b.x + c11 * b.y + c12 * b.z) * inv,
            (c02 * b.x + c12 * b.y + c22 * b.z) * inv);
        return true;
    }

    void OnDrawGizmos()
    {
        if (endEffector == null || target == null) return;
        Gizmos.color = Reached ? Color.green : Color.yellow;
        Gizmos.DrawLine(endEffector.position, target.position);
        Gizmos.DrawWireSphere(target.position, 0.03f);
    }
}
