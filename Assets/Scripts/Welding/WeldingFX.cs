using UnityEngine;

namespace FrankaVR.Welding
{
    /// Efectos del arco: luz parpadeante, chispas estiradas, humo y sonido.
    ///
    /// Todo procedural, sin assets externos ni ParticleSystem: pools propios de objetos
    /// simples. Es determinista, barato en mobile y no depende de configurar modulos en
    /// el Inspector.
    ///
    /// Las chispas se ESTIRAN a lo largo de su velocidad. Una chispa real es una gota de
    /// metal incandescente que deja una estela: un cubo rigido se ve a punto fijo, un
    /// cubo alargado en la direccion en que viaja se lee como chispa.
    ///
    /// El sonido es didactico, no decorativo: los soldadores reconocen el arco correcto
    /// por el ruido. Aca el tono y el volumen siguen la calidad del arco, asi que el
    /// alumno aprende a escuchar la longitud de arco.
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
        public Renderer PoolGlow;
        [Tooltip("Si faltan ArcLight/PoolGlow, los crea automaticamente en runtime.")]
        public bool AutoCreateArcVisuals = true;
        public float PoolGlowDiameter = 0.026f;
        public Color PoolGlowColor = new Color(1f, 0.34f, 0.05f, 1f);

        [Header("Chispas")]
        public int   SparkPoolSize = 48;
        public float SparkRate = 60f;
        public float SparkSpeed = 1.6f;
        public float SparkLife = 0.45f;
        public float SparkSize = 0.0030f;
        [Tooltip("Cuanto se estira la chispa por m/s de velocidad. Da la sensacion de estela.")]
        public float SparkStretch = 0.020f;
        public float SparkGravity = 6f;
        public Color SparkHot = new Color(1f, 0.85f, 0.45f);
        public Color SparkCool = new Color(0.85f, 0.25f, 0.05f);

        [Header("Humo")]
        public bool  EnableSmoke = true;
        public int   SmokePoolSize = 14;
        public float SmokeRate = 7f;
        public float SmokeRise = 0.28f;
        public float SmokeLife = 1.6f;
        public float SmokeStartSize = 0.012f;
        public float SmokeEndSize = 0.075f;
        public Color SmokeColor = new Color(0.32f, 0.31f, 0.30f, 0.30f);

        [Header("Audio")]
        public AudioSource ArcAudio;
        public AudioSource OneShotAudio;
        public AudioClip   StrikeClip;
        public AudioClip   StickClip;

        // --- pools ---
        Transform[] _sparks;  Vector3[] _sparkVel;  float[] _sparkTtl;  Renderer[] _sparkRend;
        Transform[] _smoke;   Vector3[] _smokeVel;  float[] _smokeTtl;  Renderer[] _smokeRend;
        int _nextSpark, _nextSmoke;
        float _sparkAcc, _smokeAcc, _flickerPhase;
        ArcState _prevState = ArcState.Off;
        Material _smokeMat;
        Material _poolGlowMat;
        GameObject _autoPoolGlowObject;
        GameObject _autoArcLightObject;

        bool HasArcOrigin => Torch != null || ArcOrigin != null;
        Vector3 EffectOrigin => Torch != null ? Torch.ArcOriginPosition
                                              : (ArcOrigin != null ? ArcOrigin.position : transform.position);
        Vector3 ImpactPoint => (Torch != null && Torch.HasArcSurface)
            ? Torch.ArcOriginPosition + Torch.PointingDir * Mathf.Max(0f, Torch.ArcLenMm) * 0.001f
            : EffectOrigin;
        Vector3 ArcMidPoint => Vector3.Lerp(EffectOrigin, ImpactPoint, 0.5f);

        void Start()
        {
            if (AutoCreateArcVisuals) EnsureArcVisuals();
            BuildSparkPool();
            if (EnableSmoke) BuildSmokePool();

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

        void EnsureArcVisuals()
        {
            if (ArcLight == null)
            {
                _autoArcLightObject = new GameObject("AutoArcLight");
                _autoArcLightObject.transform.SetParent(transform, false);
                ArcLight = _autoArcLightObject.AddComponent<Light>();
            }

            if (PoolGlow == null)
            {
                _autoPoolGlowObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                _autoPoolGlowObject.name = "AutoWeldPoolGlow";
                Collider c = _autoPoolGlowObject.GetComponent<Collider>();
                if (c != null) Destroy(c);
                _autoPoolGlowObject.transform.SetParent(transform, false);
                PoolGlow = _autoPoolGlowObject.GetComponent<Renderer>();

                Shader sh = Shader.Find("Standard");
                if (sh == null) sh = Shader.Find("Sprites/Default");
                if (sh != null)
                {
                    _poolGlowMat = new Material(sh);
                    if (_poolGlowMat.HasProperty("_Color")) _poolGlowMat.SetColor("_Color", PoolGlowColor);
                    if (_poolGlowMat.HasProperty("_EmissionColor"))
                    {
                        _poolGlowMat.EnableKeyword("_EMISSION");
                        _poolGlowMat.SetColor("_EmissionColor", PoolGlowColor * 4f);
                    }
                    PoolGlow.material = _poolGlowMat;
                }
                _autoPoolGlowObject.SetActive(false);
            }
        }

        GameObject MakePooled(string name, PrimitiveType type, Vector3 scale, Material mat, Color col)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            Collider c = go.GetComponent<Collider>();
            if (c != null) Object.Destroy(c);           // las particulas no colisionan
            Renderer r = go.GetComponent<Renderer>();
            if (r != null)
            {
                if (mat != null) r.material = mat;
                if (r.material != null) r.material.color = col;
            }
            go.transform.SetParent(transform, false);
            go.transform.localScale = scale;
            go.SetActive(false);
            return go;
        }

        void BuildSparkPool()
        {
            int n = Mathf.Max(4, SparkPoolSize);
            _sparks = new Transform[n]; _sparkVel = new Vector3[n];
            _sparkTtl = new float[n];   _sparkRend = new Renderer[n];
            for (int i = 0; i < n; i++)
            {
                GameObject go = MakePooled("Spark_" + i, PrimitiveType.Cube,
                                           Vector3.one * SparkSize, null, SparkHot);
                _sparks[i] = go.transform;
                _sparkRend[i] = go.GetComponent<Renderer>();
            }
        }

        void BuildSmokePool()
        {
            // Material transparente compartido por todo el humo (una sola instancia).
            Shader sh = Shader.Find("Legacy Shaders/Transparent/Diffuse");
            if (sh == null) sh = Shader.Find("Sprites/Default");
            if (sh != null) _smokeMat = new Material(sh);

            int n = Mathf.Max(2, SmokePoolSize);
            _smoke = new Transform[n]; _smokeVel = new Vector3[n];
            _smokeTtl = new float[n];  _smokeRend = new Renderer[n];
            for (int i = 0; i < n; i++)
            {
                GameObject go = MakePooled("Smoke_" + i, PrimitiveType.Sphere,
                                           Vector3.one * SmokeStartSize, _smokeMat, SmokeColor);
                _smoke[i] = go.transform;
                _smokeRend[i] = go.GetComponent<Renderer>();
            }
        }

        void Update()
        {
            bool arcing = Torch != null && Torch.Arcing;
            float q = Torch != null ? Torch.ArcQuality : 0f;

            UpdateLight(arcing, q);
            UpdateAudio(arcing, q);
            UpdateSparks(arcing, q);
            if (EnableSmoke) UpdateSmoke(arcing, q);
            UpdateOneShots();
        }

        void UpdateLight(bool arcing, float q)
        {
            if (ArcLight == null) return;
            ArcLight.enabled = arcing;
            if (!arcing)
            {
                if (PoolGlow != null) PoolGlow.enabled = false;
                if (_autoPoolGlowObject != null) _autoPoolGlowObject.SetActive(false);
                return;
            }

            // Un arco inestable titila mucho mas que uno bien mantenido.
            _flickerPhase += Time.deltaTime * (18f + 30f * (1f - q));
            float flick = 1f + FlickerAmount * (1f - q + 0.3f)
                        * Mathf.Sin(_flickerPhase * 3.1f) * Mathf.Sin(_flickerPhase * 1.7f);
            ArcLight.intensity = BaseIntensity * Mathf.Clamp(flick, 0.4f, 1.8f);
            if (HasArcOrigin) ArcLight.transform.position = ArcMidPoint;

            if (PoolGlow != null)
            {
                if (_autoPoolGlowObject != null) _autoPoolGlowObject.SetActive(true);
                PoolGlow.enabled = true;
                if (HasArcOrigin)
                {
                    PoolGlow.transform.position = ImpactPoint + Vector3.up * 0.0015f;
                    Vector3 n = Torch != null ? -Torch.PointingDir : Vector3.up;
                    PoolGlow.transform.rotation = Quaternion.FromToRotation(Vector3.up, n.normalized);
                    PoolGlow.transform.localScale = new Vector3(PoolGlowDiameter, 0.0025f, PoolGlowDiameter);
                }
            }
        }

        void UpdateAudio(bool arcing, float q)
        {
            if (ArcAudio == null) return;
            if (arcing)
            {
                if (!ArcAudio.isPlaying && ArcAudio.clip != null) ArcAudio.Play();
                // Arco correcto: parejo y agudo. Arco largo: mas grave e irregular.
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

            // Un arco malo salpica MAS que uno bueno: eso es lo real.
            if (arcing && HasArcOrigin)
            {
                _sparkAcc += SparkRate * (0.5f + 1.2f * (1f - q)) * dt;
                int spawn = Mathf.FloorToInt(_sparkAcc);
                _sparkAcc -= spawn;
                for (int i = 0; i < spawn; i++) SpawnSpark();
            }

            for (int i = 0; i < _sparks.Length; i++)
            {
                if (_sparkTtl[i] <= 0f) continue;
                _sparkTtl[i] -= dt;
                if (_sparkTtl[i] <= 0f) { _sparks[i].gameObject.SetActive(false); continue; }

                _sparkVel[i] += Vector3.down * (SparkGravity * dt);
                _sparks[i].position += _sparkVel[i] * dt;

                // Estela: orientar segun la velocidad y estirar proporcional a ella.
                float speed = _sparkVel[i].magnitude;
                if (speed > 0.01f)
                {
                    _sparks[i].rotation = Quaternion.LookRotation(_sparkVel[i]);
                    _sparks[i].localScale = new Vector3(
                        SparkSize, SparkSize, SparkSize + speed * SparkStretch);
                }

                // Se enfria mientras vuela: de amarillo a rojo.
                float age = 1f - Mathf.Clamp01(_sparkTtl[i] / SparkLife);
                if (_sparkRend[i] != null && _sparkRend[i].material != null)
                    _sparkRend[i].material.color = Color.Lerp(SparkHot, SparkCool, age);
            }
        }

        void SpawnSpark()
        {
            if (_sparks == null || !HasArcOrigin) return;
            int i = _nextSpark;
            _nextSpark = (_nextSpark + 1) % _sparks.Length;

            _sparks[i].position = ImpactPoint;
            _sparks[i].gameObject.SetActive(true);
            _sparkTtl[i] = SparkLife * Random.Range(0.6f, 1.2f);

            Vector3 dir = Random.onUnitSphere;
            if (Vector3.Dot(dir, Vector3.up) < 0f) dir = -dir;   // salpicar hacia arriba
            _sparkVel[i] = dir * (SparkSpeed * Random.Range(0.5f, 1.3f));
        }

        void UpdateSmoke(bool arcing, float q)
        {
            float dt = Time.deltaTime;

            // Arco largo / mala regulacion humea mas.
            if (arcing && HasArcOrigin)
            {
                _smokeAcc += SmokeRate * (0.6f + 0.9f * (1f - q)) * dt;
                int spawn = Mathf.FloorToInt(_smokeAcc);
                _smokeAcc -= spawn;
                for (int i = 0; i < spawn; i++) SpawnSmoke();
            }

            for (int i = 0; i < _smoke.Length; i++)
            {
                if (_smokeTtl[i] <= 0f) continue;
                _smokeTtl[i] -= dt;
                if (_smokeTtl[i] <= 0f) { _smoke[i].gameObject.SetActive(false); continue; }

                _smoke[i].position += _smokeVel[i] * dt;
                float age = 1f - Mathf.Clamp01(_smokeTtl[i] / SmokeLife);

                // Se expande y se desvanece al subir.
                float size = Mathf.Lerp(SmokeStartSize, SmokeEndSize, age);
                _smoke[i].localScale = Vector3.one * size;
                if (_smokeRend[i] != null && _smokeRend[i].material != null)
                {
                    Color c = SmokeColor;
                    c.a = SmokeColor.a * (1f - age);
                    _smokeRend[i].material.color = c;
                }
            }
        }

        void SpawnSmoke()
        {
            if (_smoke == null || !HasArcOrigin) return;
            int i = _nextSmoke;
            _nextSmoke = (_nextSmoke + 1) % _smoke.Length;

            _smoke[i].position = ImpactPoint;
            _smoke[i].localScale = Vector3.one * SmokeStartSize;
            _smoke[i].gameObject.SetActive(true);
            _smokeTtl[i] = SmokeLife * Random.Range(0.7f, 1.3f);

            _smokeVel[i] = new Vector3(Random.Range(-0.05f, 0.05f),
                                       SmokeRise * Random.Range(0.7f, 1.3f),
                                       Random.Range(-0.05f, 0.05f));
        }

        void OnDestroy()
        {
            if (_poolGlowMat != null) Destroy(_poolGlowMat);
            if (_autoPoolGlowObject != null) Destroy(_autoPoolGlowObject);
            if (_autoArcLightObject != null) Destroy(_autoArcLightObject);
            if (_smokeMat != null) Destroy(_smokeMat);
        }

    }
}
