using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Maneja el brazo con un gamepad Bluetooth. Dos modos:
///
///   JOINT (junta por junta)      IK (mover la punta)
///   -----------------------      -------------------
///   LB / RB : junta anterior/sig  Stick izq : mover objetivo en X / Z
///   Stick izq Y : mover junta     Stick der Y : mover objetivo en Y (altura)
///
///   Y (buttonNorth) : cambiar de modo
///   A (buttonSouth) : volver a pose home
///
/// Requiere el paquete Input System, con Active Input Handling en "Both".
/// </summary>
public class GamepadDriver : MonoBehaviour
{
    public enum Mode { Joint, IK }

    [Header("Referencias")]
    public PandaArm arm;
    [Tooltip("Opcional. Si es null, el modo IK queda deshabilitado.")]
    public PandaIK ik;

    [Header("Modo JOINT")]
    [Tooltip("Velocidad de la junta seleccionada, en grados/segundo.")]
    public float degPerSecond = 60f;

    [Header("Modo IK")]
    [Tooltip("Velocidad del objetivo, en metros/segundo.")]
    public float metersPerSecond = 0.25f;

    [Tooltip("Caja donde puede moverse el objetivo, relativa a la base del robot.")]
    public Vector3 targetBoundsMin = new Vector3(-0.7f, 0.05f, -0.7f);
    public Vector3 targetBoundsMax = new Vector3( 0.7f, 1.10f,  0.7f);

    [Tooltip("Radio maximo desde la base. El Panda alcanza ~1.27 m; 1.10 deja margen. " +
             "Sin esto, el stick puede llevar el objetivo fuera de alcance y parece que la IK falla.")]
    public float maxReachMeters = 1.10f;

    [Header("General")]
    [Range(0f, 0.5f)] public float deadzone = 0.15f;

    public Mode CurrentMode { get; private set; } = Mode.Joint;
    public int SelectedJoint { get; private set; }

    bool prevLB, prevRB;

    void Start()
    {
        // Respetar como quedo configurado en el Inspector en vez de pisarlo.
        // Si dejaste PandaIK.solving tildado, arranca directo en modo IK; asi se
        // puede probar la IK moviendo el objetivo a mano, sin gamepad.
        bool ikReady = ik != null && ik.target != null && ik.endEffector != null;
        CurrentMode = (ikReady && ik.solving) ? Mode.IK : Mode.Joint;

        if (ik != null && CurrentMode == Mode.Joint) ik.solving = false;
    }

    void Update()
    {
        var gp = Gamepad.current;
        if (gp == null || arm == null) return;

        if (gp.buttonNorth.wasPressedThisFrame) ToggleMode();
        if (gp.buttonSouth.wasPressedThisFrame) GoHome();

        if (CurrentMode == Mode.Joint) UpdateJointMode(gp);
        else UpdateIKMode(gp);
    }

    void ToggleMode()
    {
        if (ik == null || ik.target == null || ik.endEffector == null)
        {
            Debug.LogWarning("[Franka] Modo IK no disponible: falta PandaIK, target o endEffector.");
            return;
        }

        if (CurrentMode == Mode.Joint)
        {
            // Al entrar en IK, poner el objetivo donde ya esta la herramienta,
            // asi el brazo no pega un salto.
            ik.target.position = ik.endEffector.position;
            arm.SyncTargetsToActual();
            ik.solving = true;
            CurrentMode = Mode.IK;
        }
        else
        {
            ik.solving = false;
            arm.SyncTargetsToActual();
            CurrentMode = Mode.Joint;
        }
    }

    void GoHome()
    {
        if (ik != null) ik.solving = false;
        CurrentMode = Mode.Joint;
        arm.ResetHome();
    }

    void UpdateJointMode(Gamepad gp)
    {
        bool lb = gp.leftShoulder.isPressed;
        bool rb = gp.rightShoulder.isPressed;
        if (rb && !prevRB) SelectedJoint = (SelectedJoint + 1) % PandaArm.N;
        if (lb && !prevLB) SelectedJoint = (SelectedJoint + PandaArm.N - 1) % PandaArm.N;
        prevLB = lb; prevRB = rb;

        float axis = Dead(gp.leftStick.ReadValue().y);
        if (axis != 0f) arm.Nudge(SelectedJoint, axis * degPerSecond * Time.deltaTime);
    }

    void UpdateIKMode(Gamepad gp)
    {
        if (ik == null || ik.target == null) return;

        Vector2 ls = gp.leftStick.ReadValue();
        float ry = Dead(gp.rightStick.ReadValue().y);

        Vector3 delta = new Vector3(Dead(ls.x), ry, Dead(ls.y)) * metersPerSecond * Time.deltaTime;
        if (delta == Vector3.zero) return;

        // Mover en el marco de la base del robot, y recortar a la caja permitida.
        Vector3 local = arm.transform.InverseTransformPoint(ik.target.position) + delta;
        local = Vector3.Max(targetBoundsMin, Vector3.Min(targetBoundsMax, local));

        // Ademas recortar por radio: la caja tiene esquinas fuera del alcance del brazo.
        if (local.magnitude > maxReachMeters) local = local.normalized * maxReachMeters;

        ik.target.position = arm.transform.TransformPoint(local);
    }

    float Dead(float v) => Mathf.Abs(v) > deadzone ? v : 0f;
}
