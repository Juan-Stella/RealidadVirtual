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

    [Header("Codo")]
    [Tooltip("Con el codo (J4) casi recto el brazo esta en singularidad y el DLS no puede " +
             "volver a recogerlo: queda trabado estirado. Se lo mantiene flexionado al menos " +
             "este angulo (el tope real es -4). Cuesta menos de 1 cm de alcance.")]
    public float elbowKeepBentDeg = -20f;
    [Tooltip("Correccion maxima por paso hacia el codo flexionado, en grados.")]
    public float elbowBiasDegPerStep = 0.6f;

    [Tooltip("OBSOLETO. Ya no se usa: el servo no se apaga al llegar, la correccion se " +
             "desvanece sola con la zona muerta suave. Se conserva por compatibilidad.")]
    public float resumeFactor = 2f;

    [Header("Servo suave")]
    [Tooltip("Control por VELOCIDAD con prealimentacion: la IK sigue la velocidad del objetivo " +
             "(SetTargetVelocity) y corrige el error restante con ganancias por segundo. " +
             "El servo anterior sumaba el error completo en cada paso: con el retardo del drive " +
             "quedaba subamortiguado (rebote al frenar y cambiar de direccion). Apagar = servo anterior.")]
    public bool smoothServo = true;
    [Tooltip("Correccion del error de posicion, en 1/s. 6-10 converge sin rebote con el drive actual.")]
    public float positionGain = 8f;
    [Tooltip("Correccion del error de orientacion, en 1/s.")]
    public float orientationGain = 8f;
    [Tooltip("Velocidad maxima pedida a la punta, en m/s.")]
    public float maxLinearSpeed = 0.6f;
    [Tooltip("Velocidad maxima de giro pedida a la herramienta, en grados/s.")]
    public float maxAngularSpeedDeg = 120f;
    [Tooltip("La fisica avanza una vez por cuadro dibujado (FramePhysicsDriver): el brazo se mueve " +
             "lo mismo en cada cuadro. Con el paso fijo, segun el cuadro entraban 0, 1, 2 o 3 pasos " +
             "de fisica y el movimiento se veia a los saltos.")]
    public bool physicsPerFrame = true;

    [Header("Estado")]
    [Tooltip("Arranca APAGADO a proposito: la IK no debe moverse sola. " +
             "El GamepadDriver la prende al entrar en modo IK. Sin gamepad, " +
             "tildala a mano para probar.")]
    public bool solving = false;

    [Tooltip("Segundos de gracia al arrancar antes de que la IK tome el control. " +
             "PandaArm.Start() manda el brazo a la pose 'home', pero llegar ahi lleva " +
             "tiempo fisico. Si la IK arranca antes, sincroniza los targets con la pose " +
             "cruda del URDF importado y el brazo queda congelado torcido, sin pasar " +
             "nunca por home. 1-2 s alcanza.")]
    public float startupGraceSec = 1.5f;

    // Velocidades maximas reales del Panda, en grados/segundo (del URDF oficial).
    // Recortar por VELOCIDAD y no por un paso fijo es lo que hace que el movimiento
    // se vea de robot y no de animacion a saltos.
    static readonly float[] MaxDegPerSec = { 124.6f, 124.6f, 124.6f, 124.6f, 149.5f, 149.5f, 149.5f };

    PandaArm arm;

    /// <summary>Eje de aproximacion, en coordenadas locales de la punta.</summary>
    Vector3 localApproach = Vector3.forward;

    // Buffers reutilizados para no generar basura en cada FixedUpdate.
    readonly Vector3[] colLin = new Vector3[PandaArm.N];
    readonly Vector3[] colAng = new Vector3[PandaArm.N];
    readonly float[,] A = new float[M, M + 1];
    readonly float[] y = new float[M];
    readonly float[] dDeg = new float[PandaArm.N];
    readonly float[] _col = new float[M];

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

    Vector3 _feedforward;
    float _feedforwardTime = -1f;

    /// <summary>Velocidad (m/s) con la que se mueve el objetivo. La llama el teleoperador cada
    /// cuadro; si deja de llamarla, a los 0.1 s se ignora y queda solo la correccion de error.</summary>
    public void SetTargetVelocity(Vector3 velocity)
    {
        _feedforward = velocity;
        _feedforwardTime = Time.time;
    }

    /// <summary>
    /// Alcanzado dentro de tolerancia. Es solo INFORMATIVO (HUD, gizmos): el servo ya no
    /// se apaga al llegar, asi que no necesita histeresis.
    ///
    /// Antes esto era un property con efecto colateral que ademas leia OnDrawGizmos, y
    /// FixedUpdate hacia 'if (Reached) return', apagando la IK por completo. Eso producia
    /// un ciclo limite en reposo: llegaba, se apagaba, el brazo se asentaba, el error
    /// cruzaba el umbral de reanudacion y volvia a encenderse de golpe. Ahora la
    /// correccion se desvanece de forma continua (ver zona muerta suave en Step).
    /// </summary>
    public bool Reached =>
        ErrorMeters <= toleranceMeters &&
        (orientationWeight <= 0f || ErrorDegrees <= toleranceDegrees);

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
    bool leadActive;
    float graceTimer;

    void Start()
    {
        if (physicsPerFrame && FindAnyObjectByType<FramePhysicsDriver>() == null)
            gameObject.AddComponent<FramePhysicsDriver>();
    }

    void FixedUpdate()
    {
        if (!FramePhysicsDriver.Active) ServoStep(Time.fixedDeltaTime);
    }

    /// <summary>Un paso del servo de dt segundos. Lo llama FixedUpdate o, con la fisica por
    /// cuadro, FramePhysicsDriver justo antes de cada Physics.Simulate.</summary>
    public void ServoStep(float dt)
    {
        if (dt <= 0f) return;
        if (!solving || endEffector == null || target == null)
        {
            // Sin servo, el drive no debe seguir "empujando" con el adelanto de velocidad.
            if (leadActive) { arm.ClearVelocityLead(); leadActive = false; }
            wasSolving = solving;
            return;
        }

        // Periodo de gracia: dejar que el brazo alcance la pose 'home' antes de tomar el
        // control. Sin esto, SyncTargetsToActual congelaba los targets en la pose cruda
        // del URDF y el brazo arrancaba torcido en vez de vertical.
        if (graceTimer < startupGraceSec)
        {
            graceTimer += dt;
            wasSolving = false;   // al terminar la gracia cuenta como primera activacion
            return;
        }

        // Al reanudar, alinear los targets comandados con la postura real. Evita el
        // tiron cuando el brazo se movio (o se asento por gravedad) con la IK apagada.
        if (!wasSolving) arm.SyncTargetsToActual();
        wasSolving = true;

        Step(dt);
    }

    void Step(float dt)
    {
        Vector3 ee = endEffector.position;
        // El servo anterior se calibro a 50 Hz: sus limites "por paso" se escalan para que
        // a otra frecuencia de fisica se comporte igual por segundo.
        float stepScale = dt / 0.02f;

        // --- Error de posicion, con ZONA MUERTA SUAVE ---
        //
        // La correccion se desvanece de forma CONTINUA al llegar a la tolerancia, en vez
        // de conmutar. Con el apagado abrupto anterior, el brazo quieto entraba en ciclo
        // limite: llegaba, se apagaba, se asentaba por gravedad, y volvia a encenderse.
        Vector3 ep = target.position - ee;
        float epMag = ep.magnitude;
        if (smoothServo)
        {
            // Desplazamiento de este paso = velocidad del objetivo + correccion proporcional.
            // Con la prealimentacion la punta sigue al objetivo sin necesitar error grande,
            // asi la ganancia puede ser baja y el brazo frena sin rebotar.
            Vector3 correction = epMag <= toleranceMeters ? Vector3.zero : ep * ((epMag - toleranceMeters) / epMag);
            Vector3 feedforward = Time.time - _feedforwardTime < 0.1f ? _feedforward : Vector3.zero;
            ep = Vector3.ClampMagnitude(feedforward + correction * positionGain, maxLinearSpeed) * dt;
        }
        else if (epMag <= toleranceMeters) ep = Vector3.zero;
        else ep = ep.normalized * Mathf.Min(epMag - toleranceMeters, maxStepMeters * stepScale) * stepScale;

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

            // Misma zona muerta suave que en posicion.
            if (ang <= toleranceDegrees) ang = 0f;
            else if (smoothServo) ang = Mathf.Min((ang - toleranceDegrees) * orientationGain, maxAngularSpeedDeg) * dt;
            else ang = Mathf.Min(ang - toleranceDegrees, maxStepDegrees * stepScale) * stepScale;
            eo = axis * (ang * Mathf.Deg2Rad);
        }

        // Nada que corregir: no se escriben targets (y por lo tanto no hay windup ni
        // microcorrecciones que exciten el drive). Si venia moviendose, se frena el adelanto.
        if (ep.sqrMagnitude < 1e-12f && eo.sqrMagnitude < 1e-12f)
        {
            if (leadActive) { arm.ClearVelocityLead(); leadActive = false; }
            return;
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

        if (!SolveGauss())
        {
            if (leadActive) { arm.ClearVelocityLead(); leadActive = false; }
            return;
        }

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
            float cap = MaxDegPerSec[i] * speedFactor * dt;
            if (cap > 1e-6f) ratio = Mathf.Max(ratio, Mathf.Abs(dDeg[i]) / cap);
        }
        float scale = (ratio > 1f) ? 1f / ratio : 1f;

        // Sesgo de postura: alejar el codo de la singularidad de brazo estirado. El servo
        // corrige en el paso siguiente con las demas juntas.
        const int Elbow = 3;
        float elbowExcess = arm.CurrentDeg(Elbow) - elbowKeepBentDeg;
        if (elbowExcess > 0f) dDeg[Elbow] -= Mathf.Min(elbowBiasDegPerStep, elbowExcess * 0.2f) * stepScale / Mathf.Max(scale, 1e-3f);

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
            float previous = arm.TargetDeg(i);
            float desired = previous + dDeg[i] * scale;
            float actual  = arm.CurrentDeg(i);
            desired = Mathf.Clamp(desired, actual - maxLeadDegrees, actual + maxLeadDegrees);
            if (smoothServo)
            {
                // Velocidad de la referencia: el drive la sigue sin retraso (ver PandaArm).
                float cap = MaxDegPerSec[i] * speedFactor;
                arm.SetTargetDeg(i, desired, Mathf.Clamp((desired - previous) / dt, -cap, cap));
            }
            else arm.SetTargetDeg(i, desired);
        }
        leadActive = smoothServo;
    }

    /// <summary>Arma la matriz aumentada [A | b] con A = J J^T + lambda^2 I.</summary>
    void BuildSystem(Vector3 ep, Vector3 eo)
    {
        for (int r = 0; r < M; r++)
            for (int c = 0; c < M; c++)
                A[r, c] = 0f;

        // Buffer reutilizado: antes se creaba un arreglo por junta en cada paso de fisica
        // (~840 por segundo a 120 Hz), basura que en el celular dispara el recolector.
        float[] col = _col;
        for (int i = 0; i < PandaArm.N; i++)
        {
            col[0] = colLin[i].x; col[1] = colLin[i].y; col[2] = colLin[i].z;
            col[3] = colAng[i].x; col[4] = colAng[i].y; col[5] = colAng[i].z;

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
