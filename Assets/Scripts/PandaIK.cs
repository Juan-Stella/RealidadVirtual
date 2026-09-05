using UnityEngine;

/// <summary>
/// Cinematica inversa de POSICION Y ORIENTACION para el Panda.
///
/// Restringe la POSICION de la punta y la DIRECCION en que apunta la pinza,
/// dejando libre su giro sobre si misma: para agarrar el giro no importa, y dejarlo
/// suelto le devuelve un grado de libertad al brazo.
///
/// Metodo: Jacobiano geometrico + minimos cuadrados amortiguados (DLS).
///     dTheta = J^T (J J^T + lambda^2 I)^-1 e
///
/// J es 6x7 (3 filas de posicion + 3 de orientacion), asi que (J J^T) es 6x6 y se
/// resuelve por eliminacion gaussiana con pivoteo parcial: sin librerias externas.
///
/// POR QUE HACE FALTA CONTROLAR LA ORIENTACION:
/// Con IK de posicion sola, la punta llega al lugar pedido pero con una orientacion
/// arbitraria. Medido en la zona de trabajo de la celda, la herramienta terminaba
/// hasta 72 grados desviada de apuntar hacia abajo: imposible agarrar una pieza.
/// Con orientacion, ese error baja a menos de 0.5 grados.
///
/// Funciona como SERVO: un paso por FixedUpdate leyendo los transforms REALES del
/// brazo. Nunca se desincroniza de la fisica y no necesita un modelo aparte.
///
/// El amortiguamiento (lambda) evita que se dispare cerca de singularidades, donde
/// una pseudo-inversa pura daria velocidades enormes.
/// </summary>
[RequireComponent(typeof(PandaArm))]
public class PandaIK : MonoBehaviour
{
    const int M = 6; // filas de la tarea: 3 posicion + 3 orientacion

    [Header("Referencias")]
    [Tooltip("Punta de la herramienta. Usar panda_hand_tcp del modelo importado.")]
    public Transform endEffector;

    [Tooltip("Objetivo a alcanzar. Suele ser una esfera pequenia movible.")]
    public Transform target;

    [Header("Orientacion")]
    [Tooltip("Peso de la alineacion frente a la posicion. 0 = ignorar orientacion.")]
    [Range(0f, 1f)] public float orientationWeight = 0.5f;

    [Tooltip("Direccion, en el mundo, hacia la que debe apuntar la pinza. " +
             "Abajo (0,-1,0) es lo correcto para agarrar desde arriba.")]
    public Vector3 approachDirection = Vector3.down;

    [Tooltip("Referencia para deducir cual es el eje de la pinza. Asignar panda_hand: " +
             "la direccion mano -> punta ES el eje de aproximacion, sin importar como " +
             "haya quedado la convencion de ejes al importar el URDF.")]
    public Transform toolReference;

    [Header("Solver")]
    // Defaults validados con la cinematica real del Panda sobre la zona de trabajo
    // de la celda: 79/80 objetivos resueltos, mediana 48 pasos (~0.96 s a 50 Hz),
    // error 2.8 mm de posicion y 0.1 grados de orientacion.
    [Tooltip("Amortiguamiento. Mas alto = mas estable pero mas lento.")]
    [Range(0.01f, 0.5f)] public float lambda = 0.10f;

    [Tooltip("Maximo avance hacia el objetivo por paso, en metros.")]
    public float maxStepMeters = 0.05f;

    [Tooltip("Maximo giro corregido por paso, en grados.")]
    public float maxStepDegrees = 8.6f;

    [Tooltip("Fraccion de la velocidad maxima real del Panda. 0.6 da un movimiento " +
             "fluido y realista; mas alto lo hace mas brusco.")]
    [Range(0.1f, 1f)] public float speedFactor = 0.6f;

    [Tooltip("ANTI-WINDUP. Cuanto puede adelantarse el target comandado respecto al angulo " +
             "REAL de la junta, en grados. PandaArm.Nudge acumula sobre su propio target y " +
             "nunca lo sincroniza con la realidad: si el brazo va atrasado, la IK sigue " +
             "sumando incrementos y el comando se dispara, con sobrepaso y oscilacion al " +
             "llegar. 2-4 grados va bien; mas bajo es mas firme pero mas lento.")]
    [Range(0.5f, 15f)] public float maxLeadDegrees = 3f;

    [Header("Tolerancia")]
    public float toleranceMeters = 0.005f;
    public float toleranceDegrees = 3f;

    [Tooltip("Histeresis: reanuda recien cuando el error supera la tolerancia por " +
             "este factor. Evita que se prenda y apague sin parar.")]
    public float resumeFactor = 2f;

    [Header("Estado")]
    [Tooltip("Arranca APAGADO a proposito: la IK no debe moverse sola. " +
             "El GamepadDriver la prende al entrar en modo IK. Sin gamepad, " +
             "tildala a mano para probar.")]
    public bool solving = false;

    // Velocidades maximas reales del Panda, en grados/segundo (del URDF oficial).
    // Recortar por VELOCIDAD y no por un paso fijo es lo que hace que el movimiento
    // se vea de robot y no de animacion a saltos.
    static readonly float[] MaxDegPerSec = { 124.6f, 124.6f, 124.6f, 124.6f, 149.5f, 149.5f, 149.5f };

    PandaArm arm;
    bool settled;

    /// <summary>Eje de aproximacion, en coordenadas locales de la punta.</summary>
    Vector3 localApproach = Vector3.forward;

    // Buffers reutilizados para no generar basura en cada FixedUpdate.
    readonly Vector3[] colLin = new Vector3[PandaArm.N];
    readonly Vector3[] colAng = new Vector3[PandaArm.N];
    readonly float[,] A = new float[M, M + 1];
    readonly float[] y = new float[M];
    readonly float[] dDeg = new float[PandaArm.N];

    public float ErrorMeters =>
        (endEffector != null && target != null)
            ? Vector3.Distance(endEffector.position, target.position) : 0f;

    /// <summary>Desvio, en grados, entre el eje de la pinza y la direccion deseada.</summary>
    public float ErrorDegrees
    {
        get
        {
            if (endEffector == null) return 0f;
            return Vector3.Angle(ToolAxisWorld(), approachDirection.normalized);
        }
    }

    public Vector3 ToolAxisWorld() => endEffector.TransformDirection(localApproach).normalized;

    /// <summary>
    /// Con histeresis: una vez alcanzado, no reanuda hasta apartarse bastante.
    /// SOLO LECTURA: el estado se actualiza en UpdateSettled(), una vez por FixedUpdate.
    /// Antes esto era un property con efecto colateral y OnDrawGizmos tambien lo leia,
    /// asi que el dibujado (a framerate de render) pisaba el estado del servo.
    /// </summary>
    public bool Reached => settled;

    /// <summary>Actualiza la histeresis. Llamar una sola vez por paso de fisica.</summary>
    void UpdateSettled()
    {
        float f = Mathf.Max(1f, resumeFactor);
        bool within = ErrorMeters <= toleranceMeters &&
                      (orientationWeight <= 0f || ErrorDegrees <= toleranceDegrees);
        bool outside = ErrorMeters > toleranceMeters * f ||
                       (orientationWeight > 0f && ErrorDegrees > toleranceDegrees * f);

        if (settled && outside) settled = false;
        else if (!settled && within) settled = true;
    }

    void Awake()
    {
        arm = GetComponent<PandaArm>();
        DetectToolAxis();
    }

    /// <summary>
    /// Deduce el eje de la pinza midiendolo, en vez de suponerlo.
    ///
    /// El URDF Importer convierte de ROS (Z arriba) a Unity (Y arriba), asi que dar
    /// por sentado que el eje de aproximacion es el +Z local es fragil. La direccion
    /// que va de la mano a la punta es geometricamente inequivoca.
    /// </summary>
    void DetectToolAxis()
    {
        if (endEffector == null) return;

        if (toolReference != null)
        {
            Vector3 world = endEffector.position - toolReference.position;
            if (world.sqrMagnitude > 1e-8f)
            {
                localApproach = endEffector.InverseTransformDirection(world.normalized);
                return;
            }
        }

        localApproach = Vector3.forward;
        Debug.LogWarning("[PandaIK] Sin 'Tool Reference': asumo que el eje de la pinza es " +
                         "el +Z local. Asigná panda_hand para que se mida solo.");
    }

    bool wasSolving;

    void FixedUpdate()
    {
        if (!solving || endEffector == null || target == null) { wasSolving = solving; return; }

        // Al reanudar, alinear los targets comandados con la postura real. Evita el
        // tiron inicial cuando el brazo se movio (o se asento por gravedad) con la IK apagada.
        if (!wasSolving) arm.SyncTargetsToActual();
        wasSolving = true;

        UpdateSettled();
        if (Reached) return;
        Step();
    }

    void Step()
    {
        Vector3 ee = endEffector.position;

        // --- Error de posicion, recortado ---
        Vector3 ep = target.position - ee;
        if (ep.magnitude > maxStepMeters) ep = ep.normalized * maxStepMeters;

        // --- Error de ALINEACION del eje de la pinza ---
        //
        // Se restringe solo la DIRECCION en que apunta la pinza (2 grados de
        // libertad), no su giro sobre si misma. Para agarrar, el giro da igual, y
        // dejarlo libre le devuelve un grado de libertad al brazo: medido, converge
        // en mas casos y mas rapido que fijando la orientacion completa.
        Vector3 eo = Vector3.zero;
        if (orientationWeight > 0f)
        {
            Vector3 tool = ToolAxisWorld();
            Vector3 want = approachDirection.normalized;

            Vector3 axis = Vector3.Cross(tool, want);
            float sn = axis.magnitude;
            float ang = Mathf.Atan2(sn, Vector3.Dot(tool, want)) * Mathf.Rad2Deg;

            if (sn < 1e-6f)
            {
                // Paralelos: o ya esta alineado, o esta exactamente al reves.
                // En el segundo caso hace falta un eje perpendicular cualquiera.
                if (ang > 90f) axis = Vector3.Cross(tool, Vector3.up).normalized;
                if (axis.sqrMagnitude < 1e-6f) axis = Vector3.Cross(tool, Vector3.right).normalized;
            }
            else axis /= sn;

            ang = Mathf.Clamp(ang, -maxStepDegrees, maxStepDegrees);
            eo = axis * (ang * Mathf.Deg2Rad);
        }

        // --- Jacobiano 6x7 ---
        // lineal:  col_i = axis_i x (p_ee - pivot_i)
        // angular: col_i = axis_i
        for (int i = 0; i < PandaArm.N; i++)
        {
            Vector3 ax = arm.AxisWorld(i);
            colAng[i] = ax * orientationWeight;
            colLin[i] = Vector3.Cross(ax, ee - arm.PivotWorld(i));
        }

        // --- A = J J^T + lambda^2 I  (6x6, simetrica), b = e ---
        BuildSystem(ep, eo * orientationWeight);

        if (!SolveGauss()) return;

        // --- dTheta_i = col_i . y ---
        for (int i = 0; i < PandaArm.N; i++)
        {
            float r = colLin[i].x * y[0] + colLin[i].y * y[1] + colLin[i].z * y[2]
                    + colAng[i].x * y[3] + colAng[i].y * y[4] + colAng[i].z * y[5];
            dDeg[i] = r * Mathf.Rad2Deg;
        }

        // --- Recorte por VELOCIDAD, escalando el vector COMPLETO de forma uniforme ---
        //
        // Esto es clave. Recortar cada junta por separado cambia la DIRECCION del
        // movimiento: si una satura y las otras no, la punta deja de ir hacia el
        // objetivo y el recorrido sale a los tirones. Escalar todo por el mismo
        // factor respeta el limite mas exigente y preserva la direccion.
        float ratio = 0f;
        for (int i = 0; i < PandaArm.N; i++)
        {
            float cap = MaxDegPerSec[i] * speedFactor * Time.fixedDeltaTime;
            if (cap > 1e-6f) ratio = Mathf.Max(ratio, Mathf.Abs(dDeg[i]) / cap);
        }
        float scale = (ratio > 1f) ? 1f / ratio : 1f;

        // --- Escritura de targets con ANTI-WINDUP ---
        //
        // PandaArm.Nudge acumula sobre su propio target[] y nunca lo compara con el angulo
        // real. La IK, en cambio, mide el error con los transforms REALES. Si el brazo va
        // atrasado (gravedad, inercia, drive saturado), la IK sigue viendo error grande y
        // sigue sumando: el comando se adelanta cada vez mas y, cuando el brazo alcanza,
        // sobrepasa y rebota. Ese es el picoteo.
        //
        // La cura es limitar cuanto puede adelantarse el comando respecto a la realidad.
        // El recorte solo actua cuando el brazo se esta quedando atras; mientras sigue el
        // ritmo, no interviene y la direccion del movimiento queda intacta.
        for (int i = 0; i < PandaArm.N; i++)
        {
            float desired = arm.TargetDeg(i) + dDeg[i] * scale;
            float actual  = arm.CurrentDeg(i);
            desired = Mathf.Clamp(desired, actual - maxLeadDegrees, actual + maxLeadDegrees);
            arm.SetTargetDeg(i, desired);
        }
    }

    /// <summary>Arma la matriz aumentada [A | b] con A = J J^T + lambda^2 I.</summary>
    void BuildSystem(Vector3 ep, Vector3 eo)
    {
        for (int r = 0; r < M; r++)
            for (int c = 0; c < M; c++)
                A[r, c] = 0f;

        for (int i = 0; i < PandaArm.N; i++)
        {
            float[] col =
            {
                colLin[i].x, colLin[i].y, colLin[i].z,
                colAng[i].x, colAng[i].y, colAng[i].z
            };

            for (int r = 0; r < M; r++)
                for (int c = 0; c < M; c++)
                    A[r, c] += col[r] * col[c];
        }

        float l2 = lambda * lambda;
        for (int r = 0; r < M; r++) A[r, r] += l2;

        A[0, M] = ep.x; A[1, M] = ep.y; A[2, M] = ep.z;
        A[3, M] = eo.x; A[4, M] = eo.y; A[5, M] = eo.z;
    }

    /// <summary>Eliminacion gaussiana con pivoteo parcial. false si es singular.</summary>
    bool SolveGauss()
    {
        for (int p = 0; p < M; p++)
        {
            // Pivoteo: elegir la fila con el mayor valor absoluto en la columna.
            int best = p;
            float bestAbs = Mathf.Abs(A[p, p]);
            for (int r = p + 1; r < M; r++)
            {
                float v = Mathf.Abs(A[r, p]);
                if (v > bestAbs) { bestAbs = v; best = r; }
            }

            if (bestAbs < 1e-12f) return false;

            if (best != p)
                for (int c = p; c <= M; c++)
                {
                    float t = A[p, c]; A[p, c] = A[best, c]; A[best, c] = t;
                }

            float inv = 1f / A[p, p];
            for (int r = p + 1; r < M; r++)
            {
                float f = A[r, p] * inv;
                if (f == 0f) continue;
                for (int c = p; c <= M; c++) A[r, c] -= f * A[p, c];
            }
        }

        // Sustitucion hacia atras.
        for (int r = M - 1; r >= 0; r--)
        {
            float sum = A[r, M];
            for (int c = r + 1; c < M; c++) sum -= A[r, c] * y[c];
            y[r] = sum / A[r, r];
        }

        return true;
    }

    void OnDrawGizmos()
    {
        if (endEffector == null || target == null) return;

        Gizmos.color = Reached ? Color.green : Color.yellow;
        Gizmos.DrawLine(endEffector.position, target.position);
        Gizmos.DrawWireSphere(target.position, 0.03f);

        // Eje de la pinza (cian) contra la direccion deseada (magenta).
        if (Application.isPlaying)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawRay(endEffector.position, ToolAxisWorld() * 0.10f);
        }
        Gizmos.color = Color.magenta;
        Gizmos.DrawRay(endEffector.position, approachDirection.normalized * 0.06f);
    }
}
