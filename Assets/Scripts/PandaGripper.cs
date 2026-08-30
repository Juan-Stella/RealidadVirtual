using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Controla la pinza del Panda (los dos dedos).
///
/// Ojo: los dedos NO son juntas revolute como el brazo, son PRISMATICAS
/// (deslizan en vez de rotar). Eso cambia dos cosas:
///   - se usa el drive lineal, en METROS, no en grados
///   - se lee jointPosition[0] directamente en metros (no hay conversion de radianes)
///
/// Del URDF: panda_finger_joint1 y panda_finger_joint2, recorrido 0 .. 0.04 m
/// cada dedo, o sea una apertura total de 0 a 8 cm.
///
/// Gatillos del gamepad: RT abre, LT cierra.
/// </summary>
public class PandaGripper : MonoBehaviour
{
    [Header("Juntas de los dedos")]
    [Tooltip("panda_leftfinger (contiene panda_finger_joint1)")]
    public ArticulationBody leftFinger;

    [Tooltip("panda_rightfinger (contiene panda_finger_joint2)")]
    public ArticulationBody rightFinger;

    [Header("Recorrido (metros, por dedo)")]
    public float minOpen = 0.0f;
    public float maxOpen = 0.04f;

    [Header("Drive")]
    [Tooltip("Rigidez del dedo. Mas alto = agarra mas firme.")]
    public float stiffness = 2000f;
    public float damping = 100f;

    [Tooltip("Fuerza maxima de agarre, en newtons. El Panda real da ~70 N.")]
    public float forceLimit = 70f;

    [Header("Velocidad")]
    [Tooltip("Metros por segundo de apertura/cierre.")]
    public float metersPerSecond = 0.03f;

    [Range(0f, 0.5f)] public float deadzone = 0.1f;

    float opening;

    /// <summary>Apertura actual comandada, por dedo, en metros.</summary>
    public float Opening => opening;

    /// <summary>Apertura total de la pinza (los dos dedos), en metros.</summary>
    public float TotalGap => opening * 2f;

    void Start()
    {
        opening = maxOpen; // arranca abierta
        Apply();
    }

    void Update()
    {
        var gp = Gamepad.current;
        if (gp == null) return;

        float open  = gp.rightTrigger.ReadValue();
        float close = gp.leftTrigger.ReadValue();

        if (open  < deadzone) open  = 0f;
        if (close < deadzone) close = 0f;

        float delta = (open - close) * metersPerSecond * Time.deltaTime;
        if (delta == 0f) return;

        SetOpening(opening + delta);
    }

    /// <summary>Fija la apertura por dedo, en metros.</summary>
    public void SetOpening(float meters)
    {
        opening = Mathf.Clamp(meters, minOpen, maxOpen);
        Apply();
    }

    public void Open()  => SetOpening(maxOpen);
    public void Close() => SetOpening(minOpen);

    void Apply()
    {
        Drive(leftFinger);
        Drive(rightFinger);
    }

    void Drive(ArticulationBody finger)
    {
        if (finger == null) return;

        // Junta prismatica: el URDF Importer la mapea al eje X del drive,
        // y el target va en METROS (a diferencia de las revolute, que van en grados).
        var d = finger.xDrive;
        d.stiffness = stiffness;
        d.damping = damping;
        d.forceLimit = forceLimit;
        d.target = opening;
        finger.xDrive = d;
    }
}
