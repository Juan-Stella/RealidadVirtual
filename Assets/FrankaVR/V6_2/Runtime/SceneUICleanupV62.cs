using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace FrankaVR.V6_2
{
    /// <summary>
    /// Desactiva automaticamente los HUD/paneles viejos que quedaron conviviendo con V6.
    /// No destruye la logica del simulador: solo apaga componentes y textos legacy.
    /// </summary>
    [DefaultExecutionOrder(-10000)]
    public class SceneUICleanupV62 : MonoBehaviour
    {
        [Header("Ejecucion")]
        public bool CleanupOnAwake = true;
        public bool KeepWatchingForLegacyRuntimeObjects = true;
        public float RescanInterval = 1.5f;
        [Tooltip("Los objetos legacy aparecen al arrancar: vigilar solo estos segundos. Cada barrido recorre " +
                 "todos los objetos de la escena y en el celular generaba un tiron periodico.")]
        public float WatchSeconds = 12f;

        [Header("Diagnostico")]
        public bool LogWhatWasDisabled = false;

        float _nextScan;

        static readonly HashSet<string> LegacyBehaviourNames = new HashSet<string>(StringComparer.Ordinal)
        {
            "VRTrainingHUDV5",
            "CourseWorldMenuV5",
            "CardboardStatusHUD",
            "WeldingHUD",
            "TaskHUD"
        };

        static readonly string[] LegacyRuntimeObjectNames =
        {
            "VRTrainingHUDV5_Runtime",
            "CourseMenuPanelV5",
            "TaskHUD_Text",
            "MachinePanel",
            "BriefPanel",
            "ResultPanel"
        };

        void Awake()
        {
            if (CleanupOnAwake) CleanupNow(LogWhatWasDisabled);
        }

        void OnEnable()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying && CleanupOnAwake)
                CleanupNow(LogWhatWasDisabled);
#endif
        }

        void Update()
        {
            if (!KeepWatchingForLegacyRuntimeObjects || !Application.isPlaying) return;
            if (Time.timeSinceLevelLoad > WatchSeconds) return;
            if (Time.unscaledTime < _nextScan) return;
            _nextScan = Time.unscaledTime + Mathf.Max(0.25f, RescanInterval);
            CleanupNow(false);
        }

        public static int CleanupNow(bool log = false)
        {
            int count = 0;

            MonoBehaviour[] behaviours = FindAllSceneBehaviours();
            foreach (MonoBehaviour mb in behaviours)
            {
                if (mb == null) continue;
                string typeName = mb.GetType().Name;
                if (!LegacyBehaviourNames.Contains(typeName)) continue;

                // Antes de apagar WeldingHUD/TaskHUD, ocultar tambien sus TextMesh referenciados.
                HideReferencedTextMeshes(mb, log, ref count);

                if (mb.enabled)
                {
                    mb.enabled = false;
                    count++;
                    if (log) Debug.Log("[UI Cleanup V6.2] Desactivado componente legacy: " + typeName + " en " + mb.gameObject.name);
                }
            }

            // Un solo recorrido de la escena para todos los nombres (antes era uno por nombre).
            foreach (GameObject go in FindSceneObjectsByName(LegacyRuntimeObjectNameSet))
            {
                if (go == null || !go.activeSelf) continue;
                // Nunca apagar objetos nuevos V6/V6.2 por coincidencia accidental.
                if (go.name.IndexOf("V6", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                go.SetActive(false);
                count++;
                if (log) Debug.Log("[UI Cleanup V6.2] Ocultado objeto legacy: " + go.name);
            }

            return count;
        }

        static void HideReferencedTextMeshes(MonoBehaviour mb, bool log, ref int count)
        {
            Type t = mb.GetType();
            FieldInfo[] fields = t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            foreach (FieldInfo f in fields)
            {
                if (!typeof(TextMesh).IsAssignableFrom(f.FieldType)) continue;
                TextMesh tm = null;
                try { tm = f.GetValue(mb) as TextMesh; } catch { }
                if (tm == null || tm.gameObject == null) continue;
                if (!tm.gameObject.activeSelf) continue;

                tm.gameObject.SetActive(false);
                count++;
                if (log) Debug.Log("[UI Cleanup V6.2] Ocultado TextMesh legacy: " + tm.gameObject.name + " (" + t.Name + "." + f.Name + ")");
            }
        }

        static MonoBehaviour[] FindAllSceneBehaviours()
        {
#if UNITY_2023_1_OR_NEWER
            return UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
            return UnityEngine.Object.FindObjectsOfType<MonoBehaviour>(true);
#endif
        }

        static readonly HashSet<string> LegacyRuntimeObjectNameSet = new HashSet<string>(LegacyRuntimeObjectNames, StringComparer.Ordinal);

        static GameObject[] FindSceneObjectsByName(HashSet<string> names)
        {
            List<GameObject> result = new List<GameObject>();
#if UNITY_2023_1_OR_NEWER
            GameObject[] all = UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
            GameObject[] all = UnityEngine.Object.FindObjectsOfType<GameObject>(true);
#endif
            foreach (GameObject go in all)
            {
                if (go == null || !go.scene.IsValid()) continue;
                if (names.Contains(go.name)) result.Add(go);
            }
            return result.ToArray();
        }
    }
}
