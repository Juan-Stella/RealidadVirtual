using UnityEngine;

/// <summary>
/// Detecta cuando hay una pieza entre los dedos y la engancha al cerrar la pinza.
///
/// Va en un GameObject hijo de panda_hand, con un BoxCollider en modo trigger
/// ubicado entre los dedos. El BuildWorkcell lo crea automaticamente.
/// </summary>
public class GraspDetector : MonoBehaviour
{
    [Tooltip("La pinza. Si queda vacio lo busca en los padres.")]
    public PandaGripper gripper;

    [Tooltip("Transform al que se engancha la pieza. Normalmente panda_hand.")]
    public Transform attachPoint;

    // Las piezas son cubos de 4.5 cm, o sea 2.25 cm por lado desde el centro.
    // El umbral de cierre va apenas por encima de eso: asi la pieza se engancha
    // cuando los dedos ya practicamente la tocan, y no antes (se notaria el salto).
    [Tooltip("Apertura por dedo (m) por debajo de la cual se considera 'cerrada'.")]
    public float closedThreshold = 0.024f;

    [Tooltip("Apertura por dedo (m) por encima de la cual se suelta. " +
             "Separado del de cierre a proposito: sin esa banda muerta, agarraria " +
             "y soltaria sin parar al quedar justo en el limite.")]
    public float openThreshold = 0.031f;

    public Graspable Held { get; private set; }

    /// <summary>
    /// Suelta lo que tenga y limpia el estado. Lo llama TaskManager al reiniciar:
    /// sin esto, Held sigue apuntando a una pieza que ya volvio a su lugar, y la
    /// pinza queda "ocupada" sin sostener nada.
    /// </summary>
    public void ForceRelease()
    {
        if (Held != null) Held.Detach();
        Held = null;
        inZone.Clear();
    }

    readonly System.Collections.Generic.List<Graspable> inZone =
        new System.Collections.Generic.List<Graspable>();

    void Awake()
    {
        if (gripper == null) gripper = GetComponentInParent<PandaGripper>();
        if (attachPoint == null) attachPoint = transform.parent;
    }

    void OnTriggerEnter(Collider other)
    {
        var g = other.GetComponentInParent<Graspable>();
        if (g != null && !inZone.Contains(g)) inZone.Add(g);
    }

    void OnTriggerExit(Collider other)
    {
        var g = other.GetComponentInParent<Graspable>();
        if (g != null) inZone.Remove(g);
    }

    void FixedUpdate()
    {
        if (gripper == null) return;

        // Soltar: la pinza se abrio lo suficiente.
        if (Held != null && gripper.Opening > openThreshold)
        {
            Held.Detach();
            Held = null;
            return;
        }

        // Agarrar: pinza cerrada y hay una pieza libre en la zona.
        if (Held == null && gripper.Opening < closedThreshold)
        {
            for (int i = inZone.Count - 1; i >= 0; i--)
            {
                if (inZone[i] == null) { inZone.RemoveAt(i); continue; }
                if (inZone[i].held) continue;

                Held = inZone[i];
                Held.AttachTo(attachPoint);
                break;
            }
        }
    }
}
