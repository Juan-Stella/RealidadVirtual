using UnityEngine;

/// <summary>
/// Conecta los botones de mirada del panel con las acciones de la celda.
///
/// Va como componente de runtime (no del editor) para no depender de
/// UnityEventTools, que solo existe en el editor y no acepta estos metodos.
/// Se engancha en Start, cuando ya existen todas las referencias.
/// </summary>
public class PanelHook : MonoBehaviour
{
    public TaskManager task;
    public ViewpointManager viewpoints;
    public GazeButton startButton;
    public GazeButton viewButton;

    void Start()
    {
        if (startButton != null && task != null)
            startButton.onActivateAction = StartOrReset;

        if (viewButton != null && viewpoints != null)
            viewButton.onActivateAction = viewpoints.Next;
    }

    void StartOrReset()
    {
        if (task.state == TaskManager.State.Running) task.ResetAll();
        else task.StartRound();
    }
}
