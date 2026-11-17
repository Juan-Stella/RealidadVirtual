using UnityEngine;

/// <summary>
/// Conecta un GazeButton existente con ViewpointManager sin configurar UnityEvent a mano.
/// Util para poner un boton "Cambiar vista" en el panel fisico de la celda.
/// </summary>
[RequireComponent(typeof(GazeButton))]
public class ViewpointGazeButton : MonoBehaviour
{
    public ViewpointManager Viewpoints;
    [Tooltip("-1 = siguiente estacion. 0..N = ir a estacion concreta.")]
    public int StationIndex = -1;

    GazeButton _button;

    void Awake()
    {
        _button = GetComponent<GazeButton>();
        if (Viewpoints == null) Viewpoints = FindObjectOfType<ViewpointManager>();
        if (_button != null) _button.onActivateAction += Activate;
    }

    void OnDestroy()
    {
        if (_button != null) _button.onActivateAction -= Activate;
    }

    void Activate()
    {
        if (Viewpoints == null) return;
        if (StationIndex < 0) Viewpoints.Next();
        else Viewpoints.GoTo(StationIndex);
    }
}
