using System;
using CurtainCall.HumanCannon;
using UnityEngine;

namespace CurtainCall.EscapeRoute
{
    // Presentation only. Normal gameplay supplies the same course clock on all peers.
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class EscapeConveyorProcession : MonoBehaviour
    {
        [Serializable]
        struct CutPiece
        {
            public MeshRenderer renderer;
            public Vector3 hangingCenter;
        }

        [Serializable]
        struct Carrier
        {
            public Transform hanger;
            public Transform opposite;
            public GameObject load;
            public Renderer costume;
            public Color outfitColor;
            public int outfitPattern;
            public float firstContact;
            public Transform separated;
            public CutPiece[] pieces;
        }

        [SerializeField] Carrier[] carriers = Array.Empty<Carrier>();
        [SerializeField] Transform saw;
        [SerializeField, Min(0.01f)] float trackLength = 36f;
        [SerializeField] float cutAt = 18f;
        [SerializeField, Min(0.01f)] float speed = 2.25f;
        [SerializeField] Vector3 intake;
        const float FeedSeconds = 1.35f;
        const float PieceDelay = .085f;
        const float DropSeconds = 0.52f;
        const float DropAcceleration = 13f;
        static readonly int VariantColor = Shader.PropertyToID("_VariantColor");
        static readonly int VariantPattern = Shader.PropertyToID("_VariantPattern");

        Bounds motionBounds;
        Matrix4x4 boundsFrame;
        double lastTime = double.NaN;

        void OnEnable()
        {
            RefreshOutfits();
            if (!Application.IsPlaying(gameObject)) return;
            if (!saw) { enabled = false; return; }
            lastTime = double.NaN;
        }

        void UpdateMotionBounds(Matrix4x4 frame)
        {
            boundsFrame = frame;
            motionBounds = new Bounds(saw.position, Vector3.zero);
            foreach (var blade in saw.GetComponentsInChildren<Renderer>(true))
            {
                float radius = Vector3.Distance(saw.position, blade.bounds.center) + blade.bounds.extents.magnitude;
                motionBounds.Encapsulate(new Bounds(saw.position, Vector3.one * (2f * radius)));
            }
            foreach (var carrier in carriers)
            {
                AddTravelBounds(carrier.hanger, trackLength, false);
                if (carrier.opposite) AddTravelBounds(carrier.opposite, trackLength, false);
                AddTravelBounds(carrier.load.transform, cutAt, true);
                if (carrier.separated)
                    motionBounds.Encapsulate(new Bounds(transform.TransformPoint(intake), Vector3.one * 5f));
            }
        }

        // The production characters share outfit materials. Store their catalog selection
        // with the scene and restore the two masked costume slots once on load.
        public void RefreshOutfits()
        {
            var properties = new MaterialPropertyBlock();
            foreach (var carrier in carriers)
            {
                ApplyOutfit(carrier.costume, carrier, properties);
                if (carrier.pieces != null)
                    foreach (var piece in carrier.pieces) ApplyOutfit(piece.renderer, carrier, properties);
            }
        }

        static void ApplyOutfit(Renderer mesh, Carrier carrier, MaterialPropertyBlock properties)
        {
            if (!mesh) return;
            var materials = mesh.sharedMaterials;
            for (int slot = 0; slot < materials.Length; slot++)
            {
                if (!materials[slot] || !materials[slot].HasProperty(VariantColor)) continue;
                mesh.GetPropertyBlock(properties, slot);
                properties.SetColor(VariantColor, carrier.outfitColor);
                properties.SetFloat(VariantPattern, carrier.outfitPattern);
                mesh.SetPropertyBlock(properties, slot);
            }
        }

        // The line is fixed scene geometry. Sweep the actual moving renderers at startup.
        // Include hidden loads and the drop so an empty stretch in view still wakes the line.
        void AddTravelBounds(Transform part, float end, bool falls)
        {
            var atStart = transform.position - part.position;
            var travel = transform.TransformVector(Vector3.right * end);
            var drop = transform.TransformVector(Vector3.down * (DropAcceleration * DropSeconds * DropSeconds));
            foreach (var renderer in part.GetComponentsInChildren<Renderer>(true))
            {
                var local = renderer.localBounds;
                var matrix = renderer.localToWorldMatrix;
                var extents = local.extents;
                for (int i = 0; i < 8; i++)
                {
                    var corner = local.center + new Vector3((i & 1) == 0 ? -extents.x : extents.x,
                        (i & 2) == 0 ? -extents.y : extents.y, (i & 4) == 0 ? -extents.z : extents.z);
                    var start = matrix.MultiplyPoint3x4(corner) + atStart;
                    var finish = start + travel;
                    motionBounds.Encapsulate(start);
                    motionBounds.Encapsulate(finish);
                    if (falls)
                        motionBounds.Encapsulate(finish + drop);
                }
            }
        }

        void LateUpdate()
        {
            if (!Application.IsPlaying(gameObject)) return;
            var frame = transform.localToWorldMatrix;
            if (frame != boundsFrame) UpdateMotionBounds(frame);
            if (!EscapeSceneryView.InRange(motionBounds, 100f)) return;

            var course = HumanCannonHazardCourse.Current;
            double time = course != null ? course.Clock : Time.timeAsDouble;
            if (time == lastTime) return;
            lastTime = time;
            Present(time);
        }

        void Present(double time)
        {
            float travel = (float)((time * speed) % trackLength);
            saw.localRotation = Quaternion.AngleAxis((float)(time % 1d) * 360f, Vector3.forward);
            for (int i = 0; i < carriers.Length; i++)
            {
                var carrier = carriers[i];
                float span = carrier.opposite ? trackLength * .5f : trackLength;
                float x = Mathf.Repeat(travel + 2f + i * span / carriers.Length, trackLength);
                carrier.hanger.localPosition = new Vector3(x, 0f, 0f);
                float loadedX = x;
                if (carrier.opposite)
                {
                    float otherX = Mathf.Repeat(x + span, trackLength);
                    carrier.opposite.localPosition = new Vector3(otherX, 0f, 0f);
                    loadedX = Mathf.Min(x, otherX);
                }

                // The cut and drop lie in the first half of the line. Only that hanger
                // needs the pair's load. Its empty counterpart continues on the same rail.
                if (carrier.separated)
                {
                    if (loadedX < carrier.firstContact)
                    {
                        float relativeX = carrier.load.transform.parent == carrier.hanger ? loadedX - x : loadedX;
                        carrier.load.transform.localPosition = Vector3.right * relativeX;
                    }
                    Show(carrier.load, loadedX < carrier.firstContact);
                    Feed(carrier, (loadedX - carrier.firstContact) / speed);
                    continue;
                }
                // Older scenes have a single intact falling load.
                float dropTime = Mathf.Max(0f, loadedX - cutAt) / speed;
                bool visible = dropTime < DropSeconds;
                if (visible)
                {
                    float loadX = Mathf.Min(loadedX, cutAt);
                    var load = carrier.load.transform;
                    if (load.parent == carrier.hanger) loadX -= x;
                    var position = new Vector3(loadX, -DropAcceleration * dropTime * dropTime, 0f);
                    if (load.localPosition != position) load.localPosition = position;
                }
                if (carrier.load.activeSelf != visible) carrier.load.SetActive(visible);
            }
        }

        static void Show(GameObject visual, bool show)
        {
            if (visual.activeSelf != show) visual.SetActive(show);
        }

        void Feed(Carrier carrier, float sinceCut)
        {
            var pieces = carrier.pieces;
            bool feeding = sinceCut >= 0 && sinceCut < FeedSeconds + (pieces.Length - 1) * PieceDelay;
            Show(carrier.separated.gameObject, feeding);
            if (!feeding) return;

            for (int i = 0; i < pieces.Length; i++)
            {
                var piece = pieces[i];
                var part = piece.renderer.transform;
                float progress = Mathf.Clamp01((sinceCut - i * PieceDelay) / FeedSeconds);
                bool inside = progress >= 1f;
                if (piece.renderer.enabled == inside) piece.renderer.enabled = !inside;
                if (inside) continue;

                Vector3 start = piece.hangingCenter;
                // Preserve each body's left/right ordering while it tumbles into the open throat.
                Vector3 target = intake - Vector3.right * carrier.firstContact;
                target.x += Mathf.Clamp(start.x * .4f, -.55f, .55f);
                target.z += Mathf.Clamp(start.z * .7f, -.35f, .35f);
                float drift = Mathf.Sin(progress * Mathf.PI);
                Vector3 position = Vector3.Lerp(start, target, Mathf.SmoothStep(0, 1, progress));
                position.x += Mathf.Clamp(start.x * .52f, -.68f, .68f) * drift;
                position.z += (i % 2 == 0 ? .35f : -.35f) * drift;
                position.y = Mathf.Lerp(start.y, target.y, progress * progress);
                part.localPosition = position;
                float turn = progress * (i % 2 == 0 ? 126f : -105f);
                part.localRotation = Quaternion.Euler(turn * .36f, turn * .23f, turn);
            }
        }
    }
}
