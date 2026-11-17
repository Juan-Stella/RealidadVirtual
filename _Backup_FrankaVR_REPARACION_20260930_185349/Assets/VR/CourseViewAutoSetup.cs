using UnityEngine;

/// <summary>
/// Crea tres estaciones de vista alrededor de la zona de trabajo:
/// 0) posicion actual del usuario, 1) lateral, 2) isometrica alta.
/// Se ejecuta una sola vez al arrancar si ViewpointManager no tiene estaciones.
/// </summary>
public class CourseViewAutoSetup : MonoBehaviour
{
    public ViewpointManager Viewpoints;
    public Camera PlayerCamera;
    public Transform Focus;
    public float LateralAngleDeg = 62f;
    public float IsoAngleDeg = -48f;
    public float IsoExtraHeight = 0.35f;
    public bool BuildOnlyIfEmpty = true;

    void Start()
    {
        if (Viewpoints == null) Viewpoints = FindObjectOfType<ViewpointManager>();
        if (PlayerCamera == null) PlayerCamera = Camera.main;
        if (Viewpoints == null || PlayerCamera == null) return;
        if (BuildOnlyIfEmpty && Viewpoints.stations.Count > 0) return;

        Vector3 focusPos = ResolveFocusPosition();
        Transform focusT = MakeAnchor("ViewFocus", focusPos, Quaternion.identity);

        Vector3 p0 = PlayerCamera.transform.position;
        Vector3 offset = p0 - focusPos;
        Vector3 flat = new Vector3(offset.x, 0f, offset.z);
        if (flat.magnitude < 0.5f) flat = new Vector3(0f, 0f, -1.2f);

        Transform a0 = MakeAnchor("View_Operador", p0, Quaternion.identity);

        Vector3 lat = Quaternion.Euler(0f, LateralAngleDeg, 0f) * flat;
        Transform a1 = MakeAnchor("View_Lateral", focusPos + lat + Vector3.up * offset.y, Quaternion.identity);

        Vector3 iso = Quaternion.Euler(0f, IsoAngleDeg, 0f) * flat;
        Transform a2 = MakeAnchor("View_Isometrica", focusPos + iso + Vector3.up * (offset.y + IsoExtraHeight), Quaternion.identity);

        Viewpoints.stations.Clear();
        Viewpoints.stations.Add(new ViewpointManager.Station { label = "Operador", anchor = a0, lookAtTarget = focusT });
        Viewpoints.stations.Add(new ViewpointManager.Station { label = "Lateral", anchor = a1, lookAtTarget = focusT });
        Viewpoints.stations.Add(new ViewpointManager.Station { label = "Isometrica", anchor = a2, lookAtTarget = focusT });

        // No dependemos del orden de Start() entre componentes: aplicamos la primera vista aqui mismo.
        Viewpoints.SnapTo(0);
    }

    Vector3 ResolveFocusPosition()
    {
        if (Focus != null)
        {
            Renderer r = Focus.GetComponentInChildren<Renderer>();
            return r != null ? r.bounds.center : Focus.position;
        }

        FrankaVR.Welding.PipeTrainingWorkpiece pipe = FindObjectOfType<FrankaVR.Welding.PipeTrainingWorkpiece>();
        if (pipe != null)
        {
            Renderer r = pipe.GetComponentInChildren<Renderer>();
            return r != null ? r.bounds.center : pipe.transform.position;
        }

        GameObject chapa = GameObject.Find("Chapa");
        if (chapa != null)
        {
            Renderer r = chapa.GetComponentInChildren<Renderer>();
            return r != null ? r.bounds.center : chapa.transform.position;
        }

        return PlayerCamera.transform.position + PlayerCamera.transform.forward * 1.5f;
    }

    Transform MakeAnchor(string name, Vector3 pos, Quaternion rot)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(transform, true);
        go.transform.SetPositionAndRotation(pos, rot);
        return go.transform;
    }
}
