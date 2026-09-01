using UnityEngine;

/// <summary>Pieza que el robot puede agarrar. El "kind" define en que contenedor va.</summary>
[RequireComponent(typeof(Rigidbody))]
public class Graspable : MonoBehaviour
{
    [Tooltip("Categoria de la pieza. Debe coincidir con la de un SortingBin.")]
    public int kind;

    [HideInInspector] public bool held;

    Rigidbody rb;
    Collider[] colliders;
    Vector3 spawnPos;
    Quaternion spawnRot;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        colliders = GetComponentsInChildren<Collider>();
        spawnPos = transform.position;
        spawnRot = transform.rotation;
    }

    /// <summary>
    /// Agarre por union cinematica: al cerrar la pinza la pieza se vuelve kinematic
    /// y se emparenta a la mano. Es lo que usan casi todos los demos de robotica en
    /// Unity, porque sostener por friccion contra un ArticulationBody es muy
    /// inestable: la pieza vibra o se escapa sola.
    /// </summary>
    public void AttachTo(Transform hand)
    {
        if (held) return;
        held = true;
        rb.isKinematic = true;

        // Apagar los colliders mientras esta sujeta.
        // Un cuerpo kinematic empuja a los dinamicos sin recibir reaccion: si el
        // collider de la pieza sigue activo dentro de los dedos, los abre a la
        // fuerza y desestabiliza todo el brazo.
        SetColliders(false);

        transform.SetParent(hand, worldPositionStays: true);
    }

    public void Detach()
    {
        if (!held) return;
        held = false;
        transform.SetParent(null, worldPositionStays: true);
        SetColliders(true);
        rb.isKinematic = false;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
    }

    void SetColliders(bool on)
    {
        if (colliders == null) return;
        foreach (var c in colliders) if (c != null) c.enabled = on;
    }

    /// <summary>Devuelve la pieza a su posicion inicial (para reiniciar la ronda).</summary>
    public void ResetToSpawn()
    {
        Detach();
        transform.SetPositionAndRotation(spawnPos, spawnRot);
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
    }
}
