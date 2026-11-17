using System.Collections.Generic;
using UnityEngine;

namespace FrankaVR.Welding
{
    /// <summary>
    /// El brazo atraviesa mesa y pieza en vez de quedar trabado contra ellas: se ignoran los
    /// contactos fisicos robot-entorno. Cada interferencia se muestra (eslabon en rojo y aviso)
    /// y queda marcada en la trayectoria grabada. La punta sigue protegida por GuardSurface.
    /// </summary>
    public class RobotCollisionGuard : MonoBehaviour
    {
        public Transform RobotRoot;
        public Color WarningColor = new Color(1f, 0.15f, 0.1f, 1f);
        [Tooltip("Penetracion minima para avisar, en metros.")]
        public float MinDepth = 0.002f;

        public bool Colliding { get; private set; }
        public string CollisionLabel { get; private set; } = "";

        readonly HashSet<Collider> _ignored = new HashSet<Collider>();
        readonly Collider[] _hits = new Collider[32];
        readonly List<Renderer> _tinted = new List<Renderer>();
        MaterialPropertyBlock _block;
        Collider[] _robot;
        float _scanTimer, _checkTimer;
        Transform _tintedLink;

        void Start()
        {
            if (RobotRoot == null)
            {
                PandaArm arm = FindObjectOfType<PandaArm>();
                if (arm != null) RobotRoot = arm.transform;
            }
            if (RobotRoot == null) { enabled = false; return; }
            _robot = RobotRoot.GetComponentsInChildren<Collider>(true);
            _block = new MaterialPropertyBlock();
            Scan();
        }

        void Update()
        {
            // Las piezas se regeneran al cambiar de trabajo: incorporar los colliders nuevos.
            if ((_scanTimer -= Time.unscaledDeltaTime) <= 0f) { _scanTimer = 0.5f; Scan(); }
            if ((_checkTimer -= Time.unscaledDeltaTime) <= 0f) { _checkTimer = 0.1f; Check(); }
        }

        bool IsRobot(Collider c) => c.transform == RobotRoot || c.transform.IsChildOf(RobotRoot);

        void Scan()
        {
            foreach (Collider other in FindObjectsByType<Collider>())
            {
                if (other.isTrigger || IsRobot(other) || !_ignored.Add(other)) continue;
                foreach (Collider mine in _robot)
                    if (mine != null) Physics.IgnoreCollision(mine, other, true);
            }
        }

        /// Diagnostico: colisionador propio|ajeno|profundidad mm.
        public string CollisionDetail = "";

        void Check()
        {
            Collider hitMine = null, hitOther = null;
            foreach (Collider mine in _robot)
            {
                if (mine == null || !mine.enabled || !mine.gameObject.activeInHierarchy || mine.isTrigger) continue;
                Bounds b = mine.bounds;
                int n = Physics.OverlapBoxNonAlloc(b.center, b.extents, _hits, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < n && hitOther == null; i++)
                {
                    Collider other = _hits[i];
                    if (other == null || IsRobot(other) || other.gameObject.layer == 5) continue;
                    if (Physics.ComputePenetration(mine, mine.transform.position, mine.transform.rotation,
                            other, other.transform.position, other.transform.rotation, out _, out float depth) && depth > MinDepth)
                    { hitMine = mine; hitOther = other; CollisionDetail = mine.name + "|" + other.name + "|" + (depth * 1000f).ToString("0.0"); }
                }
                if (hitOther != null) break;
            }
            Colliding = hitOther != null;
            if (!Colliding) CollisionDetail = "";
            CollisionLabel = Colliding ? "COLISIÓN: " + PartName(hitMine) + " con " + PartName(hitOther) : "";
            Tint(Colliding ? Link(hitMine) : null);
        }

        static string PartName(Collider c)
        {
            string n = c.transform.name;
            if (n.StartsWith("Bracket") || n.StartsWith("Plate") || n.StartsWith("Pipe")) return "la pieza";
            if (n.StartsWith("Mesa") || n.StartsWith("Pata")) return "la mesa";
            Transform t = c.transform;
            while (t != null && !t.name.StartsWith("panda_link")) t = t.parent;
            if (t == null) return c.attachedArticulationBody != null ? "la torcha" : n;
            switch (t.name)
            {
                case "panda_link8": return "la torcha";
                case "panda_link7": case "panda_link6": return "la muñeca";
                case "panda_link5": case "panda_link4": return "el antebrazo";
                default: return "el brazo";
            }
        }

        Transform Link(Collider c)
        {
            ArticulationBody body = c.GetComponentInParent<ArticulationBody>();
            return body != null ? body.transform : c.transform;
        }

        void Tint(Transform link)
        {
            if (link == _tintedLink) return;
            foreach (Renderer r in _tinted) if (r != null) r.SetPropertyBlock(null);
            _tinted.Clear();
            _tintedLink = link;
            if (link == null) return;
            _block.SetColor("_Color", WarningColor);
            foreach (Renderer r in link.GetComponentsInChildren<Renderer>())
            {
                // Solo el propio eslabon: los hijos articulados tienen su propio ArticulationBody.
                if (r.GetComponentInParent<ArticulationBody>() != null && r.GetComponentInParent<ArticulationBody>().transform != link) continue;
                r.SetPropertyBlock(_block);
                _tinted.Add(r);
            }
        }

        void OnDisable() => Tint(null);
    }
}
