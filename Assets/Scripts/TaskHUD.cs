using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Panel de informacion en world space, anclado a la camara.
///
/// En VR un Canvas en Screen Space NO sirve: se dibuja plano sobre los dos ojos y
/// se ve roto o imposible de enfocar. La informacion tiene que vivir en el mundo,
/// a una distancia comoda (1 a 2 m) para que los ojos converjan sin esfuerzo.
///
/// Muestra la tarea, el modo de control y la junta seleccionada, todo junto,
/// porque adentro del casco no hay consola ni inspector.
/// </summary>
public class TaskHUD : MonoBehaviour
{
    [Header("Referencias")]
    public TaskManager task;
    public GamepadDriver driver;
    public PandaArm arm;
    public PandaIK ik;
    public ViewpointManager viewpoints;
    public GraspDetector graspDetector;
    public GazeInteractor gaze;
    public Camera playerCamera;

    [Header("Colocacion")]
    [Tooltip("Distancia frente a la camara, en metros. 1 a 2 m es comodo en VR.")]
    public float distance = 1.5f;
    public float verticalOffset = -0.5f;

    [Header("Aspecto")]
    public int fontSize = 60;
    public float characterSize = 0.011f;
    public Color textColor = new Color(0.95f, 0.96f, 1f);

    TextMesh text;
    Transform panel;

    void Start()
    {
        if (playerCamera == null) playerCamera = Camera.main;

        var go = new GameObject("TaskHUD_Text");
        panel = go.transform;

        text = go.AddComponent<TextMesh>();
        text.anchor = TextAnchor.MiddleCenter;
        text.alignment = TextAlignment.Center;
        text.fontSize = fontSize;
        text.characterSize = characterSize;
        text.color = textColor;

        var mr = go.GetComponent<MeshRenderer>();
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
    }

    void Update()
    {
        // START arranca o reinicia la ronda.
        var gp = Gamepad.current;
        if (gp != null && task != null && gp.startButton.wasPressedThisFrame)
        {
            if (task.state == TaskManager.State.Running) task.ResetAll();
            else task.StartRound();
        }
    }

    void LateUpdate()
    {
        if (playerCamera == null || text == null) return;

        var cam = playerCamera.transform;
        panel.position = cam.position + cam.forward * distance + cam.up * verticalOffset;
        panel.rotation = Quaternion.LookRotation(panel.position - cam.position, cam.up);

        text.text = Compose();
    }

    string Compose()
    {
        var sb = new System.Text.StringBuilder();

        if (task != null) sb.AppendLine(task.StatusText()).AppendLine();

        if (driver != null && arm != null)
        {
            if (driver.CurrentMode == GamepadDriver.Mode.Joint)
            {
                int j = driver.SelectedJoint;
                sb.AppendLine($"[JUNTA]  J{j + 1} = {arm.CurrentDeg(j):F1}\u00B0");
            }
            else
            {
                if (ik != null)
                    sb.AppendLine($"[IK]  {ik.ErrorMeters * 100f:F1} cm   {ik.ErrorDegrees:F0}\u00B0");
                else
                    sb.AppendLine("[IK]");
            }
        }

        if (graspDetector != null)
            sb.AppendLine(graspDetector.Held != null ? "PIEZA SUJETA" : "pinza libre");

        if (viewpoints != null) sb.Append($"vista: {viewpoints.CurrentLabel}");

        if (gaze != null && gaze.Current != null)
            sb.Append($"\n> {gaze.Current.label}  {gaze.Progress * 100f:F0}%");

        return sb.ToString();
    }
}
