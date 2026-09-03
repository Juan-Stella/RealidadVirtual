using UnityEngine;

namespace FrankaVR.Welding
{
    /// Efectos del arco: luz parpadeante, chispas y sonido.
    ///
    /// Las chispas usan un pool propio de objetos pequeños en vez de ParticleSystem:
    /// es determinista, barato en mobile y no depende de configurar modulos en el
    /// Inspector. Con 40 chispas alcanza y sobra visualmente.
    ///
    /// El sonido es didactico, no decorativo: los soldadores reconocen el arco correcto
    /// por el ruido ("como freir tocino"). Aca el tono y el volumen siguen la calidad
    /// del arco, asi que el alumno aprende a escuchar la longitud de arco.
    public class WeldingFX : MonoBehaviour
    {
        [Header("Referencias")]
        public WeldingTorch Torch;
        public Transform    ArcOrigin;    // normalmente el Tip del soldador

        [Header("Luz")]
        public Light ArcLight;
        public float BaseIntensity = 6f;
        public float FlickerAmount = 0.35f;
        public float LightRange = 2.5f;
        public Color ArcColor = new Color(0.85f, 0.92f, 1f);

        [Header("Destello del charco")]
        public Renderer PoolGlow;         // quad pequeño sobre la pieza (opcional)

        [Header("Chispas")]
        public int   SparkPoolSize = 40;
        public float SparkRate = 60f;     // chispas por segundo con arco estable
        public float SparkSpeed = 1.6f;
        public float SparkLife = 0.45f;
        public float SparkSize = 0.0035f;
        public float SparkGravity = 6f;
        public Color SparkColor = new Color(1f, 0.7f, 0.25f);

        [Header("Audio")]
        public AudioSource ArcAudio;      // loop del arco
        public AudioSource OneShotAudio;  // cebado / pegado
        public AudioClip   StrikeClip;
        public AudioClip   StickClip;

        Transform[] _sparks;
        Vector3[]   _sparkVel;
        float[]     _sparkTtl;
        int _next;
        float _spawnAcc;
        float _flickerPhase;
        ArcState _prevState = ArcState.Off;

        void Start()
        {
            BuildSparkPool();
            if (ArcLight != null)
            {
                ArcLight.type = LightType.Point;
                ArcLight.color = ArcColor;
                ArcLight.range = LightRange;
                ArcLight.enabled = false;
            }
            if (PoolGlow != null) PoolGlow.enabled = false;
            if (ArcAudio != null) { ArcAudio.loop = true; ArcAudio.spatialBlend = 1f; ArcAudio.volume = 0f; }
        }

        void BuildSparkPool()
        {
            int n = Mathf.Max(4, SparkPoolSize);
            _sparks   = new Transform[n];
            _sparkVel = new Vector3[n];
            _sparkTtl = new float[n];

            for (int i = 0; i < n; i++)
            {
                GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = "Spark_" + i;
                Collider c = go.GetComponent<Collider>();
                if (c != null) Object.Destroy(c); // las chispas no colisionan
                Renderer r = go.GetComponent<Renderer>();
                if (r != null && r.material != null) r.material.color = SparkColor;
                go.transform.SetParent(transform, false);
                go.transform.localScale = Vector3.one * SparkSize;
                go.SetActive(false);
                _sparks[i] = go.transform;
            }
        }

        void Update()
        {
            bool arcing = Torch != null && Torch.Arcing;
            float q = Torch != null ? Torch.ArcQuality : 0f;

            UpdateLight(arcing, q);
            UpdateAudio(arcing, q);
            UpdateSparks(arcing, q);
            UpdateOneShots();
        }

        void UpdateLight(bool arcing, float q)
        {
            if (ArcLight == null) return;
            ArcLight.enabled = arcing;
            if (!arcing) { if (PoolGlow != null) PoolGlow.enabled = false; return; }

            // Parpadeo: un arco inestable titila mucho mas que uno bien mantenido.
            _flickerPhase += Time.deltaTime * (18f + 30f * (1f - q));
            float flick = 1f + FlickerAmount * (1f - q + 0.3f) * Mathf.Sin(_flickerPhase * 3.1f) * Mathf.Sin(_flickerPhase * 1.7f);
            ArcLight.intensity = BaseIntensity * Mathf.Clamp(flick, 0.4f, 1.8f);
            if (ArcOrigin != null) ArcLight.transform.position = ArcOrigin.position;

            if (PoolGlow != null)
            {
                PoolGlow.enabled = true;
                if (ArcOrigin != null) PoolGlow.transform.position = ArcOrigin.position;
            }
        }

        void UpdateAudio(bool arcing, float q)
        {
            if (ArcAudio == null) return;
            if (arcing)
            {
                if (!ArcAudio.isPlaying && ArcAudio.clip != null) ArcAudio.Play();
                // Arco correcto: chisporroteo parejo y agudo. Arco largo: mas grave e irregular.
                ArcAudio.volume = Mathf.Lerp(ArcAudio.volume, 0.35f + 0.45f * q, Time.deltaTime * 8f);
                ArcAudio.pitch  = Mathf.Lerp(ArcAudio.pitch,  0.75f + 0.45f * q, Time.deltaTime * 8f);
            }
            else
            {
                ArcAudio.volume = Mathf.MoveTowards(ArcAudio.volume, 0f, Time.deltaTime * 4f);
                if (ArcAudio.volume <= 0.001f && ArcAudio.isPlaying) ArcAudio.Stop();
            }
        }

        void UpdateOneShots()
        {
            if (Torch == null) return;
            ArcState s = Torch.State;
            if (s != _prevState && OneShotAudio != null)
            {
                if (s == ArcState.Stable && StrikeClip != null) OneShotAudio.PlayOneShot(StrikeClip);
                if (s == ArcState.Stuck  && StickClip  != null) OneShotAudio.PlayOneShot(StickClip);
            }
            _prevState = s;
        }

        void UpdateSparks(bool arcing, float q)
        {
            float dt = Time.deltaTime;

            // Emision: un arco malo salpica MAS que uno bueno (eso es lo real).
            if (arcing && ArcOrigin != null)
            {
                float rate = SparkRate * (0.5f + 1.2f * (1f - q));
                _spawnAcc += rate * dt;
                int spawn = Mathf.FloorToInt(_spawnAcc);
                _spawnAcc -= spawn;
                for (int i = 0; i < spawn; i++) SpawnSpark();
            }

            // Integracion de las chispas vivas.
            for (int i = 0; i < _sparks.Length; i++)
            {
                if (_sparkTtl[i] <= 0f) continue;
                _sparkTtl[i] -= dt;
                if (_sparkTtl[i] <= 0f) { _sparks[i].gameObject.SetActive(false); continue; }
                _sparkVel[i] += Vector3.down * (SparkGravity * dt);
                _sparks[i].position += _sparkVel[i] * dt;
            }
        }

        void SpawnSpark()
        {
            if (_sparks == null || ArcOrigin == null) return;
            int i = _next;
            _next = (_next + 1) % _sparks.Length;

            _sparks[i].position = ArcOrigin.position;
            _sparks[i].gameObject.SetActive(true);
            _sparkTtl[i] = SparkLife * Random.Range(0.6f, 1.2f);

            Vector3 dir = Random.onUnitSphere;
            if (Vector3.Dot(dir, Vector3.up) < 0f) dir = -dir; // salpicar hacia arriba
            _sparkVel[i] = dir * (SparkSpeed * Random.Range(0.5f, 1.3f));
        }
    }
}
