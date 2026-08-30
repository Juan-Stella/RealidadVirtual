#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Linq;
using System.Text;

/// <summary>
/// Diagnostico de rendimiento para VR en celular.
///
/// Numeros medidos sobre las mallas de franka_description:
///   visual  (.dae) : ~134.900 triangulos el brazo completo
///   colision(.stl) :    ~2.300 triangulos  (~50x mas livianas)
///
/// En estereo el celular dibuja la escena DOS veces, asi que el costo real
/// ronda los 270k tri/frame solo por el brazo. Un Dimensity 8400 deberia
/// bancarlo, pero medir primero y optimizar solo si hace falta.
/// </summary>
public static class MeshTools
{
    const int WarnTrisPerEye = 150000;

    [MenuItem("Franka/Diagnostico/Contar triangulos de la escena")]
    static void CountScene()
    {
        var filters = Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None)
                            .Where(f => f.sharedMesh != null &&
                                        f.GetComponent<Renderer>() != null &&
                                        f.GetComponent<Renderer>().enabled)
                            .ToArray();

        int total = filters.Sum(f => f.sharedMesh.triangles.Length / 3);

        var sb = new StringBuilder();
        sb.AppendLine("=== TRIANGULOS EN ESCENA ===");
        sb.AppendLine($"Renderers activos : {filters.Length}");
        sb.AppendLine($"Triangulos (1 ojo): {total:N0}");
        sb.AppendLine($"Triangulos (VR, x2): {total * 2:N0}");
        sb.AppendLine();
        sb.AppendLine("--- Top 15 objetos mas pesados ---");

        foreach (var f in filters.OrderByDescending(f => f.sharedMesh.triangles.Length).Take(15))
            sb.AppendLine($"{f.sharedMesh.triangles.Length / 3,10:N0}  {Path(f.transform)}");

        sb.AppendLine();
        sb.AppendLine(total > WarnTrisPerEye
            ? $"AVISO: por encima de {WarnTrisPerEye:N0} tri/ojo. Considerar reducir mallas."
            : "OK: dentro de un presupuesto razonable para VR en celular.");

        Debug.Log(sb.ToString());
    }

    [MenuItem("Franka/Diagnostico/Reporte del brazo Panda")]
    static void CountArm()
    {
        var arm = Object.FindAnyObjectByType<PandaArm>();
        if (arm == null) { Debug.LogWarning("[Franka] No hay ningun PandaArm en la escena."); return; }

        var sb = new StringBuilder();
        sb.AppendLine("=== TRIANGULOS POR ESLABON ===");
        int total = 0;

        foreach (var f in arm.GetComponentsInChildren<MeshFilter>())
        {
            if (f.sharedMesh == null) continue;
            int t = f.sharedMesh.triangles.Length / 3;
            total += t;
            if (t > 500) sb.AppendLine($"{t,10:N0}  {f.name}");
        }

        sb.AppendLine($"\nTOTAL brazo: {total:N0} tri  (en VR se dibuja x2 = {total * 2:N0})");
        Debug.Log(sb.ToString());
    }

    /// <summary>
    /// Desactiva los renderers de colision, que el URDF Importer a veces deja visibles
    /// y duplican el costo sin aportar nada visualmente.
    /// </summary>
    [MenuItem("Franka/Optimizar/Ocultar geometria de colision")]
    static void HideCollisionRenderers()
    {
        var arm = Object.FindAnyObjectByType<PandaArm>();
        if (arm == null) { Debug.LogWarning("[Franka] No hay ningun PandaArm en la escena."); return; }

        int n = 0;
        foreach (var r in arm.GetComponentsInChildren<Renderer>(true))
        {
            // El URDF Importer agrupa bajo objetos llamados "Collisions" y "Visuals".
            if (InsideNamed(r.transform, "Collision") && r.enabled)
            {
                Undo.RecordObject(r, "Ocultar colision");
                r.enabled = false;
                n++;
            }
        }
        Debug.Log($"[Franka] Renderers de colision desactivados: {n}");
    }

    static bool InsideNamed(Transform t, string needle)
    {
        for (var p = t; p != null; p = p.parent)
            if (p.name.IndexOf(needle, System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
        return false;
    }

    static string Path(Transform t)
    {
        var s = t.name;
        for (var p = t.parent; p != null; p = p.parent) s = p.name + "/" + s;
        return s;
    }
}
#endif
