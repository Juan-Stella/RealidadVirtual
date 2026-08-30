#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Text;

/// <summary>
/// Repara y cablea un Panda ya importado, sin tener que reimportarlo.
///
/// Arregla los tres problemas que hacen que el brazo se descontrole:
///
///   1. COLLIDERS "_sc" INTERPENETRADOS  <- la causa principal
///      El URDF de franka trae 9 links "_sc" con cilindros y esferas que se
///      SUPERPONEN a proposito con la geometria real. En ROS no molestan porque
///      MoveIt los usa solo para chequear auto-colision. En Unity son colliders
///      fisicos reales metidos unos dentro de otros, y PhysX aplica fuerzas
///      enormes para separarlos: el brazo se sacude sin importar el damping.
///
///   2. MASAS FALTANTES
///      Las inercias del xacro de franka solo se emiten para Gazebo. Sin ellas
///      Unity asigna 1 kg por eslabon y el ajuste del drive se vuelve una loteria.
///
///   3. BASE SUELTA / COMPONENTE Controller
///      Sin "Immovable" en link0 el robot sale volando. Y el Controller que
///      agrega el importador pisa stiffness y damping en cada frame.
///
/// Menu: Franka > Reparar y cablear robot
/// </summary>
public static class PandaSetup
{
    // Masas reales, de inertial.yaml de franka_description.
    static readonly Dictionary<string, float> Masses = new Dictionary<string, float>
    {
        { "panda_link0", 0.629769f },
        { "panda_link1", 4.970684f },
        { "panda_link2", 0.646926f },
        { "panda_link3", 3.228604f },
        { "panda_link4", 3.587895f },
        { "panda_link5", 1.225946f },
        { "panda_link6", 1.666555f },
        { "panda_link7", 0.735522f },
        { "panda_hand",  0.730000f },
    };

    [MenuItem("Franka/Reparar y cablear robot")]
    static void Repair()
    {
        var root = FindRoot();
        if (root == null)
        {
            EditorUtility.DisplayDialog("Franka",
                "No encontre el robot en la escena.\n\n" +
                "Buscá un GameObject llamado 'panda' (o que contenga 'panda_link0').", "OK");
            return;
        }

        Undo.RegisterFullObjectHierarchyUndo(root, "Reparar Panda");
        var log = new StringBuilder();
        log.AppendLine($"=== REPARANDO '{root.name}' ===");

        log.AppendLine(RemoveController(root));
        log.AppendLine(DisableSelfCollision(root));
        log.AppendLine(ApplyMasses(root));
        log.AppendLine(FixBase(root));
        log.AppendLine(WireComponents(root));

        Debug.Log(log.ToString());
        EditorUtility.SetDirty(root);
    }

    static GameObject FindRoot()
    {
        var arm = Object.FindAnyObjectByType<PandaArm>();
        if (arm != null) return arm.gameObject;

        foreach (var b in Object.FindObjectsByType<ArticulationBody>(FindObjectsSortMode.None))
            if (b.name == "panda_link0")
                return b.transform.parent != null ? b.transform.parent.gameObject : b.gameObject;

        var go = GameObject.Find("panda");
        return go;
    }

    // --- 1. Quitar el Controller que agrega el importador ---
    static string RemoveController(GameObject root)
    {
        int n = 0;
        foreach (var c in root.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (c == null) continue;
            if (c.GetType().Name == "Controller")
            {
                Undo.DestroyObjectImmediate(c);
                n++;
            }
        }
        return n > 0
            ? $"[OK] Controller eliminado ({n})."
            : "[--] No habia componente Controller.";
    }

    // --- 2. Desactivar los colliders de auto-colision (la causa del descontrol) ---
    static string DisableSelfCollision(GameObject root)
    {
        int n = 0;
        foreach (var col in root.GetComponentsInChildren<Collider>(true))
        {
            if (!UnderSelfCollisionLink(col.transform, root.transform)) continue;
            if (!col.enabled) continue;

            Undo.RecordObject(col, "Desactivar collider _sc");
            col.enabled = false;
            n++;
        }
        return n > 0
            ? $"[OK] Colliders de auto-colision '_sc' desactivados ({n}). Esta era la causa principal del descontrol."
            : "[--] No habia colliders '_sc' (el modelo ya esta limpio).";
    }

    static bool UnderSelfCollisionLink(Transform t, Transform stopAt)
    {
        for (var p = t; p != null && p != stopAt.parent; p = p.parent)
            if (p.name.EndsWith("_sc")) return true;
        return false;
    }

    // --- 3. Cargar las masas reales ---
    static string ApplyMasses(GameObject root)
    {
        int n = 0; float total = 0f;
        foreach (var b in root.GetComponentsInChildren<ArticulationBody>(true))
        {
            if (!Masses.TryGetValue(b.name, out float m)) continue;
            Undo.RecordObject(b, "Masa");
            b.mass = m;
            total += m; n++;
        }
        return n > 0
            ? $"[OK] Masas reales aplicadas a {n} eslabones (total {total:F2} kg)."
            : "[!!] No pude aplicar masas: los nombres de los eslabones no coinciden.";
    }

    // --- 4. Anclar la base ---
    static string FixBase(GameObject root)
    {
        ArticulationBody link0 = null;
        foreach (var b in root.GetComponentsInChildren<ArticulationBody>(true))
            if (b.name == "panda_link0") { link0 = b; break; }

        if (link0 == null)
        {
            var t = FindDeep(root.transform, "panda_link0");
            if (t == null) return "[!!] No encontre panda_link0.";
            link0 = Undo.AddComponent<ArticulationBody>(t.gameObject);
        }

        Undo.RecordObject(link0, "Anclar base");
        link0.immovable = true;
        return "[OK] Base anclada (panda_link0.immovable = true).";
    }

    // --- 5. Cablear PandaArm / PandaIK / PandaGripper ---
    static string WireComponents(GameObject root)
    {
        var sb = new StringBuilder();

        var arm = root.GetComponent<PandaArm>();
        if (arm == null) arm = Undo.AddComponent<PandaArm>(root);

        Undo.RecordObject(arm, "Cablear PandaArm");
        arm.joints = new ArticulationBody[PandaArm.N];
        int found = 0;
        for (int i = 0; i < PandaArm.N; i++)
        {
            var t = FindDeep(root.transform, $"panda_link{i + 1}");
            if (t == null) continue;
            arm.joints[i] = t.GetComponent<ArticulationBody>();
            if (arm.joints[i] != null) found++;
        }
        sb.AppendLine($"[OK] PandaArm: {found}/7 juntas asignadas.");

        var ik = root.GetComponent<PandaIK>();
        if (ik == null) ik = Undo.AddComponent<PandaIK>(root);
        Undo.RecordObject(ik, "Cablear PandaIK");
        var tcp = FindDeep(root.transform, "panda_hand_tcp");
        if (tcp != null) ik.endEffector = tcp;
        ik.solving = false; // nunca debe arrancar resolviendo sola
        var tgt = GameObject.Find("IKTarget");
        if (tgt != null) ik.target = tgt.transform;
        sb.AppendLine($"[OK] PandaIK: efector {(tcp != null ? "OK" : "NO ENCONTRADO")}, " +
                      $"objetivo {(tgt != null ? "OK" : "falta IKTarget en la escena")}, solving = false.");

        var grip = root.GetComponent<PandaGripper>();
        if (grip == null) grip = Undo.AddComponent<PandaGripper>(root);
        Undo.RecordObject(grip, "Cablear PandaGripper");
        var lf = FindDeep(root.transform, "panda_leftfinger");
        var rf = FindDeep(root.transform, "panda_rightfinger");
        if (lf != null) grip.leftFinger = lf.GetComponent<ArticulationBody>();
        if (rf != null) grip.rightFinger = rf.GetComponent<ArticulationBody>();
        sb.AppendLine($"[OK] PandaGripper: dedos {(lf != null && rf != null ? "OK" : "faltan")}.");

        sb.Append("\nListo. Dale Play: el brazo deberia ir a la pose home y quedarse firme.");
        return sb.ToString();
    }

    // ================= Diagnostico =================

    /// <summary>
    /// Imprime, para cada junta: el angulo comandado, el real, y el error.
    /// Sirve para saber si el brazo esta obedeciendo o si algo lo esta moviendo.
    /// Correr DURANTE Play.
    /// </summary>
    [MenuItem("Franka/Diagnostico/Leer angulos de las juntas")]
    static void ReadAngles()
    {
        var arm = Object.FindAnyObjectByType<PandaArm>();
        if (arm == null) { Debug.LogWarning("[Franka] No hay PandaArm en la escena."); return; }

        if (!Application.isPlaying)
        {
            Debug.LogWarning("[Franka] Corré esto DURANTE Play: fuera de Play los drives no se aplican.");
            return;
        }

        var sb = new StringBuilder("=== ANGULOS DE LAS JUNTAS (grados) ===\n");
        sb.AppendLine($"{"junta",-8}{"comandado",12}{"real",10}{"error",10}   estado");

        float worst = 0f;
        for (int i = 0; i < PandaArm.N; i++)
        {
            if (arm.joints[i] == null) { sb.AppendLine($"J{i + 1,-7}  <sin asignar>"); continue; }

            float cmd = arm.TargetDeg(i);
            float act = arm.CurrentDeg(i);
            float err = Mathf.Abs(cmd - act);
            worst = Mathf.Max(worst, err);

            string flag = err < 1f ? "OK" : (err < 5f ? "flojo" : "NO OBEDECE");
            sb.AppendLine($"J{i + 1,-7}{cmd,12:F1}{act,10:F1}{err,10:F1}   {flag}");
        }

        sb.AppendLine();
        sb.AppendLine($"Drive: stiffness {arm.stiffness}, damping {arm.damping}, forceLimit {arm.forceLimit}");

        if (worst < 1f)
            sb.AppendLine("=> El brazo esta OBEDECIENDO. Si la pose no es la que esperabas, revisá 'home'.");
        else if (worst < 5f)
            sb.AppendLine("=> Obedece pero le falta fuerza: subí stiffness.");
        else
            sb.AppendLine("=> NO OBEDECE. Algo lo esta moviendo (IK prendida, Controller, o colliders en conflicto), " +
                          "o el drive es demasiado debil para las masas.");

        var ik = Object.FindAnyObjectByType<PandaIK>();
        if (ik != null && ik.solving)
            sb.AppendLine("AVISO: PandaIK.solving esta PRENDIDO: la IK esta comandando el brazo.");

        Debug.Log(sb.ToString());
    }

    static Transform FindDeep(Transform root, string name)
    {
        if (root.name == name) return root;
        for (int i = 0; i < root.childCount; i++)
        {
            var r = FindDeep(root.GetChild(i), name);
            if (r != null) return r;
        }
        return null;
    }
}
#endif
