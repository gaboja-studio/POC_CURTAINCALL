using CurtainCall.Damage;
using CurtainCall.Settings;
using UnityEngine;
using UnityEngine.Rendering;

namespace CurtainCall.WaitingRoom.Pursuit
{
    /// <summary>Concepts/Reclaimed: repaired troupe shell, stage collar, capture stains and ground direction.</summary>
    public sealed class PursuitAppearance : MonoBehaviour
    {
        public Material GroundMaterial;
        public GameObject[] AdvancedDetails;
        public AnimationClip AdvancedWalk, AdvancedRun;
        public int CaptureCount { get; private set; }
        public bool IsAdvanced { get; private set; }
        public bool CueVisible => groundRenderer != null && groundRenderer.enabled;
        public Color CueColor { get; private set; }
        public float CueAngle { get; private set; }
        PursuitActor actor;
        Renderer[] surfaces;
        MaterialPropertyBlock props;
        MaterialPropertyBlock groundProps;
        Mesh mesh;
        GameObject ground;
        MeshRenderer groundRenderer;
        float nextGroundUpdate;
        const int Sectors = 12, Rings = 3;
        readonly RaycastHit[] hits = new RaycastHit[32];
        readonly Vector3[] vertices = new Vector3[(Sectors + 1) * (Rings + 1)];
        readonly Color[] colors = new Color[(Sectors + 1) * (Rings + 1)];
        public void SetCaptureCount(int count) => CaptureCount = Mathf.Max(0, count);
        public static int CueState(PursuitBehaviour behaviour) => behaviour == PursuitBehaviour.Prepare || behaviour == PursuitBehaviour.Chase || behaviour == PursuitBehaviour.Capture ? 2 :
            behaviour == PursuitBehaviour.Patrol || behaviour == PursuitBehaviour.Return ? 0 : 1;
        void Start()
        {
            actor = GetComponent<PursuitActor>(); surfaces = GetComponentsInChildren<SkinnedMeshRenderer>(true); props = new MaterialPropertyBlock();
            if (GroundMaterial == null) return;
            ground = new GameObject("Pursuit facing light", typeof(MeshFilter), typeof(MeshRenderer));
            ground.transform.SetParent(transform, false);
            groundRenderer = ground.GetComponent<MeshRenderer>(); groundRenderer.sharedMaterial = GroundMaterial;
            groundRenderer.shadowCastingMode = ShadowCastingMode.Off; groundRenderer.receiveShadows = false;
            mesh = new Mesh { name = "Pursuit floor cue" }; mesh.MarkDynamic();
            var uv = new Vector2[vertices.Length];
            for (int r = 0; r <= Rings; r++) for (int s = 0; s <= Sectors; s++)
                uv[r * (Sectors + 1) + s] = new Vector2(s / (float)Sectors, r / (float)Rings);
            groundProps = new MaterialPropertyBlock();
            var triangles = new int[Sectors * Rings * 6]; int index = 0;
            for (int r = 0; r < Rings; r++) for (int s = 0; s < Sectors; s++) {
                int a = r * (Sectors + 1) + s, b = a + Sectors + 1;
                triangles[index++] = a; triangles[index++] = b; triangles[index++] = a + 1;
                triangles[index++] = a + 1; triangles[index++] = b; triangles[index++] = b + 1;
            }
            mesh.vertices = vertices; mesh.uv = uv; mesh.triangles = triangles; ground.GetComponent<MeshFilter>().sharedMesh = mesh;
        }
        void LateUpdate()
        {
            if (actor == null) return;
            var d = PursuitDirector.Current; var rule = GameSettings.WaitingRoom.Pursuit;
            IsAdvanced = actor.IsActivated && d != null && d.CurrentStageIndex + 1 >= rule.AdvancedAppearanceStage;
            if (AdvancedDetails != null) foreach (var detail in AdvancedDetails) if (detail != null && detail.activeSelf != IsAdvanced) detail.SetActive(IsAdvanced);
            foreach (var surface in surfaces) if (surface != null) {
                surface.GetPropertyBlock(props);
                props.SetFloat("_BloodAmount", InjuryBloodEffects.IsVisible ? Mathf.Clamp01(CaptureCount * rule.BloodPerCapture) : 0);
                props.SetFloat("_Threat", IsAdvanced ? 1 : 0); surface.SetPropertyBlock(props);
            }
            if (groundRenderer == null) return;
            bool visible = actor.IsActivated && rule.DirectionCue && actor.IsRendered;
            var camera = Camera.main;
            if (visible && camera != null) {
                var point = camera.WorldToViewportPoint(transform.position);
                visible = point.z > 0 && point.x > -.2f && point.x < 1.2f && point.y > -.2f && point.y < 1.2f
                    && Vector3.SqrMagnitude(camera.transform.position-transform.position) < 30*30;
            }
            groundRenderer.enabled = visible;
            if (!visible || Time.unscaledTime < nextGroundUpdate) return;
            nextGroundUpdate = Time.unscaledTime + .1f;
            int state = CueState(actor.Behaviour);
            CueColor = state == 2 ? rule.ChaseCueColor : state == 1 ? rule.AlertCueColor : rule.PatrolCueColor;
            CueAngle = Mathf.Clamp(d.CurrentStageRules.FieldOfView, 1, 180);
            DrawGround(rule);
        }
        void DrawGround(PursuitSettings rule)
        {
            // Evaluate the fade per pixel, so the low-cost floor mesh never draws a polygonal outline.
            groundProps.SetFloat("_EdgeSoftness", rule.DirectionCueSoftness);
            groundRenderer.SetPropertyBlock(groundProps);
            // Project into the current storey only. Clip at walls and reject unsupported/far-below ground.
            var origin = transform.position;
            var forward = transform.forward; // Match the actual field-of-view axis, not an unseen remembered target.
            for (int s = 0; s <= Sectors; s++) {
                float angle = (s / (float)Sectors - .5f) * CueAngle;
                var ray = Quaternion.AngleAxis(angle, Vector3.up) * forward;
                float length = rule.DirectionCueLength;
                int count = Physics.RaycastNonAlloc(origin + Vector3.up * .3f, ray, hits, length, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                for (int h = 0; h < count; h++) { var hit = hits[h];
                    if (Surface(hit.collider) && Mathf.Abs(hit.normal.y) < .7f) length = Mathf.Min(length, Mathf.Max(0, hit.distance - .08f));
                }
                for (int r = 0; r <= Rings; r++) {
                    float t = r / (float)Rings; int i = r * (Sectors + 1) + s;
                    var point = origin + ray * length * t; float nearest = float.PositiveInfinity; bool found = false;
                    count = Physics.RaycastNonAlloc(point + Vector3.up * 1, Vector3.down, hits, 2.2f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                    for (int h = 0; h < count; h++) { var hit = hits[h];
                        if (!Surface(hit.collider) || hit.normal.y < .65f || hit.distance >= nearest) continue;
                        nearest = hit.distance; point.y = hit.point.y + .025f; found = true;
                    }
                    vertices[i] = transform.InverseTransformPoint(point);
                    colors[i] = new Color(CueColor.r, CueColor.g, CueColor.b, found ? rule.DirectionCueOpacity : 0);
                }
            }
            mesh.vertices = vertices; mesh.colors = colors; mesh.RecalculateBounds();
        }
        static bool Surface(Collider c) => c != null && !c.isTrigger && c.GetComponentInParent<PursuitActor>() == null
            && c.GetComponentInParent<CurtainCall.Player.PlayerMover>() == null;
        void OnDisable() { if (groundRenderer != null) groundRenderer.enabled = false; }
        void OnDestroy() { if (mesh != null) Destroy(mesh); if (ground != null) Destroy(ground); }
    }
}
