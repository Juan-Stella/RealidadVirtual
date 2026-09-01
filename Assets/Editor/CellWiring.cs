#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Text;

/// <summary>
/// Conecta el robot ya reparado con la celda de trabajo.
///
/// Va en un archivo aparte de PandaSetup A PROPOSITO: PandaSetup solo depende del
/// robot, asi que sirve aunque los scripts de la celda no esten en el proyecto.
/// Si este archivo esta pero faltan los scripts de la celda, Unity no compila.
/// </summary>
public static class CellWiring
{
    [MenuItem("Franka/Conectar robot a la celda")]
    static void ConnectToCell()
    {
        var root = FindRoot();
        if (root == null) { Debug.LogWarning("[Franka] No encontre el robot."); return; }

        var task = Object.FindAnyObjectByType<TaskManager>();
        var hud = Object.FindAnyObjectByType<TaskHUD>();
        var vp = Object.FindAnyObjectByType<ViewpointManager>();
        if (task == null)
        {
            EditorUtility.DisplayDialog("Franka",
                "No encontre la celda. Corré primero 'Franka > Construir celda de trabajo'.", "OK");
            return;
        }

        var sb = new StringBuilder("=== CONECTANDO ROBOT A LA CELDA ===\n");

        var hand = FindDeep(root.transform, "panda_hand");
        var arm = root.GetComponent<PandaArm>();
        var ik = root.GetComponent<PandaIK>();
        var grip = root.GetComponent<PandaGripper>();

        // Zona de agarre: trigger entre los dedos.
        GraspDetector det = Object.FindAnyObjectByType<GraspDetector>();
        if (det == null && hand != null)
        {
            var go = new GameObject("GraspZone");
            Undo.RegisterCreatedObjectUndo(go, "GraspZone");
            go.transform.SetParent(hand, false);

            // Ubicarla EN la punta de la herramienta, en vez de suponer un offset
            // sobre un eje local: la convencion de ejes cambia al importar el URDF.
            var tcpNow = FindDeep(root.transform, "panda_hand_tcp");
            go.transform.position = tcpNow != null ? tcpNow.position : hand.position;
            go.transform.rotation = hand.rotation;

            var bc = go.AddComponent<BoxCollider>();
            bc.size = new Vector3(0.07f, 0.07f, 0.05f);
            bc.isTrigger = true;

            det = go.AddComponent<GraspDetector>();
            sb.AppendLine("[OK] Zona de agarre creada entre los dedos.");
        }
        else sb.AppendLine("[--] Zona de agarre ya existia.");

        if (det != null)
        {
            Undo.RecordObject(det, "Cablear GraspDetector");
            det.gripper = grip;
            det.attachPoint = hand;
        }

        Undo.RecordObject(task, "Cablear TaskManager");
        task.arm = arm;
        task.graspDetector = det;
        sb.AppendLine("[OK] TaskManager conectado al brazo y a la pinza.");

        // El GamepadDriver vive en la celda para que todo el control quede junto.
        var driver = Object.FindAnyObjectByType<GamepadDriver>();
        if (driver == null && task != null)
        {
            driver = Undo.AddComponent<GamepadDriver>(task.gameObject);
            sb.AppendLine("[OK] GamepadDriver agregado al CellController.");
        }
        if (driver != null)
        {
            Undo.RecordObject(driver, "Cablear GamepadDriver");
            driver.arm = arm;
            driver.ik = ik;
        }

        if (hud != null)
        {
            Undo.RecordObject(hud, "Cablear TaskHUD");
            hud.arm = arm; hud.ik = ik; hud.driver = driver;
            hud.graspDetector = det; hud.viewpoints = vp; hud.task = task;
            sb.AppendLine("[OK] HUD conectado.");
        }

        // Tercera estacion: sigue la pinza rotando, sin trasladarse.
        if (vp != null && hand != null && vp.stations.Count >= 3)
        {
            vp.stations[2].label = "Seguimiento";
            vp.stations[2].lookAtTarget = hand;
            sb.AppendLine("[OK] Estacion 'Seguimiento' apunta a la pinza.");
        }

        // La IK deduce el eje de la pinza a partir de mano -> punta.
        if (ik != null && hand != null)
        {
            Undo.RecordObject(ik, "Tool reference");
            ik.toolReference = hand;
            sb.AppendLine("[OK] PandaIK: referencia de herramienta = panda_hand.");
        }

        // El objetivo de IK arranca donde esta la herramienta.
        if (ik != null && ik.target == null)
        {
            var t = new GameObject("IKTarget");
            Undo.RegisterCreatedObjectUndo(t, "IKTarget");
            var tcp = FindDeep(root.transform, "panda_hand_tcp");
            t.transform.position = tcp != null ? tcp.position : root.transform.position;
            ik.target = t.transform;
            sb.AppendLine("[OK] IKTarget creado en la punta de la herramienta.");
        }

        Debug.Log(sb.ToString());
    }


    static GameObject FindRoot()
    {
        var arm = Object.FindAnyObjectByType<PandaArm>();
        if (arm != null) return arm.gameObject;
        return GameObject.Find("panda");
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
