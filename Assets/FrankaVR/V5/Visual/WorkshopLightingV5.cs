using UnityEngine;
using UnityEngine.Rendering;

namespace FrankaVR.V5
{
    /// <summary>Iluminacion industrial simple y barata para Cardboard.</summary>
    public class WorkshopLightingV5 : MonoBehaviour
    {
        public TrainingWorkpieceFactoryV5 Workpieces;
        public bool ApplyOnStart = true;
        public Color Ambient = new Color(0.28f, 0.31f, 0.35f);
        [Range(0.2f, 1.5f)] public float AmbientIntensity = 0.72f;
        public float KeyIntensity = 0.72f;
        public float FillIntensity = 0.32f;

        Light _key, _fill;

        void Start()
        {
            if (Workpieces == null) Workpieces = FindObjectOfType<TrainingWorkpieceFactoryV5>();
            if (ApplyOnStart) Apply();
        }

        public void Apply()
        {
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = Ambient;
            RenderSettings.ambientIntensity = AmbientIntensity;

            _key = FindOrCreateLight("WorkshopKeyV5", LightType.Directional);
            _key.transform.rotation = Quaternion.Euler(48f, -32f, 0f);
            _key.color = new Color(0.94f, 0.96f, 1f);
            _key.intensity = KeyIntensity;
            _key.shadows = LightShadows.None;

            _fill = FindOrCreateLight("WorkshopFillV5", LightType.Point);
            Vector3 center = Workpieces != null ? Workpieces.WorkCenter : transform.position;
            float y = Workpieces != null ? Workpieces.WorkTopY : center.y;
            _fill.transform.position = new Vector3(center.x + 0.55f, y + 0.75f, center.z - 0.55f);
            _fill.color = new Color(0.65f, 0.76f, 1f);
            _fill.intensity = FillIntensity;
            _fill.range = 2.4f;
            _fill.shadows = LightShadows.None;
        }

        Light FindOrCreateLight(string name, LightType type)
        {
            GameObject go = GameObject.Find(name);
            if (go == null)
            {
                go = new GameObject(name);
                go.transform.SetParent(transform, false);
            }
            Light l = go.GetComponent<Light>();
            if (l == null) l = go.AddComponent<Light>();
            l.type = type;
            return l;
        }
    }
}
