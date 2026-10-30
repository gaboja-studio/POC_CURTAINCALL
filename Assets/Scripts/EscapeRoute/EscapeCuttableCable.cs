using System;
using System.Collections.Generic;
using UnityEngine;

namespace CurtainCall.EscapeRoute
{
    /// <summary>One authored insulated strand. The saw owns authority/progress; this owns its exact curve, contacts and cut presentation.</summary>
    [ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed partial class EscapeCuttableCable : MonoBehaviour
    {
        [SerializeField] Vector3[] localPoints = { Vector3.zero, Vector3.up * 4 };
        [SerializeField, Min(.01f)] float radius = .07f;
        [SerializeField, Range(1, 12)] int curveSubdivisions = 4;
        [SerializeField, Range(6, 12)] int radialSegments = 8;
        [SerializeField] Material insulationMaterial, copperMaterial;
        [SerializeField] bool requiredForExit = true;
        [SerializeField] ParticleSystem cutSparks;
        [SerializeField] AudioSource cutAudio;
        [SerializeField, Min(.1f)] float snapSeconds = .9f;
        [SerializeField, Min(.1f)] float breakGap = .55f;
        [SerializeField, Min(0)] float droopDistance = .7f;

        Vector3[] path = Array.Empty<Vector3>();
        float[] lengths = Array.Empty<float>();
        Mesh mesh;
        MeshFilter filter;
        MeshRenderer view;
        bool cut, pendingRebuild, settled = true;
        int cutSegment;
        float cutDistance;
        Vector3 cutLocal;
        double cutAt;
        public bool IsCut => cut;
        public bool RequiredForExit => requiredForExit;
        public Vector3 CutPoint => transform.TransformPoint(cutLocal);
        public int SampleCount => path.Length;

        void OnEnable() => Rebuild();
        void OnValidate() => pendingRebuild = true;
        void Update()
        {
            if (pendingRebuild) Rebuild();
            if (!Application.isPlaying || !cut || settled) return;
            float elapsed = (float)(Time.unscaledTimeAsDouble - cutAt);
            if (elapsed > .2f && cutSparks != null && cutSparks.isEmitting)
                cutSparks.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            settled = elapsed >= Mathf.Max(.1f, snapSeconds);
            BuildMesh(Mathf.Clamp01(elapsed / Mathf.Max(.1f, snapSeconds)));
        }

        /// <summary>Editor-safe rebuild from serialized points. The temporary mesh is owned and never changes imported assets.</summary>
        public void Rebuild()
        {
            pendingRebuild = false;
            var samples = new List<Vector3>();
            int count = localPoints != null ? Mathf.Min(localPoints.Length, 64) : 0;
            int subdivisions = Mathf.Clamp(curveSubdivisions, 1, 12);
            bool valid = count >= 2 && float.IsFinite(radius) && radius > 0;
            for (int i = 0; i < count; i++) valid &= Finite(localPoints[i]);
            if (valid)
                for (int i = 0; i < count - 1; i++)
                {
                    Vector3 a = localPoints[Mathf.Max(0, i - 1)], b = localPoints[i];
                    Vector3 c = localPoints[i + 1], d = localPoints[Mathf.Min(count - 1, i + 2)];
                    for (int step = 0; step < subdivisions; step++)
                    {
                        float t = (float)step / subdivisions;
                        Vector3 point = .5f * ((2 * b) + (-a + c) * t
                            + (2 * a - 5 * b + 4 * c - d) * (t * t) + (-a + 3 * b - 3 * c + d) * (t * t * t));
                        if (samples.Count == 0 || (samples[samples.Count - 1] - point).sqrMagnitude > .0000001f) samples.Add(point);
                    }
                }
            if (valid && (samples.Count == 0 || (samples[samples.Count - 1] - localPoints[count - 1]).sqrMagnitude > .0000001f))
                samples.Add(localPoints[count - 1]);
            path = samples.ToArray(); lengths = new float[path.Length];
            for (int i = 1; i < path.Length; i++) lengths[i] = lengths[i - 1] + Vector3.Distance(path[i - 1], path[i]);
            filter = GetComponent<MeshFilter>(); view = GetComponent<MeshRenderer>();
            if (mesh == null)
            { mesh = new Mesh { name = "Owned cuttable cable", hideFlags = HideFlags.HideAndDontSave }; mesh.MarkDynamic(); }
            filter.sharedMesh = mesh;
            view.sharedMaterials = new[] { insulationMaterial, copperMaterial != null ? copperMaterial : insulationMaterial };
            if (cut && path.Length >= 2) LocateCut(transform.TransformPoint(cutLocal));
            BuildMesh(cut && !settled && Application.isPlaying
                ? Mathf.Clamp01((float)(Time.unscaledTimeAsDouble - cutAt) / Mathf.Max(.1f, snapSeconds)) : 1);
        }

        public bool ApplyCut(Vector3 worldPoint, bool playFeedback = true)
        {
            if (cut || !Finite(worldPoint)) return false;
            EnsurePath(); if (path.Length < 2) return false;
            SetCut(true, worldPoint, playFeedback); return true;
        }

        public void SetCut(bool value)
        {
            if (cut == value) return;
            EnsurePath();
            Vector3 point = path.Length > 0 ? transform.TransformPoint(path[path.Length / 2]) : transform.position;
            SetCut(value, point, false);
        }

        /// <summary>Feedback=false snaps to the authoritative final pose for late join; repeated snapshots do not replay the cut.</summary>
        public void SetCut(bool value, Vector3 worldPoint, bool playFeedback = false)
        {
            EnsurePath();
            if (value && (!Finite(worldPoint) || path.Length < 2)) return;
            if (value == cut && (!value || (worldPoint - CutPoint).sqrMagnitude <= .000001f)) return;
            bool newlyCut = value && !cut;
            if (value) LocateCut(worldPoint);
            cut = value;
            if (newlyCut)
            {
                settled = !playFeedback || !Application.isPlaying; cutAt = Time.unscaledTimeAsDouble;
                if (playFeedback)
                {
                    if (cutSparks != null)
                    {
                        cutSparks.transform.position = CutPoint;
                        cutSparks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                        var main = cutSparks.main; main.loop = false;
                        cutSparks.Play(true);
                    }
                    if (cutAudio != null && cutAudio.clip != null)
                    { cutAudio.transform.position = CutPoint; cutAudio.loop = false; cutAudio.Play(); }
                }
            }
            else if (!value)
            {
                settled = true;
                if (cutSparks != null) cutSparks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                if (cutAudio != null) cutAudio.Stop();
            }
            BuildMesh(settled ? 1 : Mathf.Clamp01((float)(Time.unscaledTimeAsDouble - cutAt) / Mathf.Max(.1f, snapSeconds)));
        }

        void EnsurePath() { if (pendingRebuild || mesh == null || path.Length < 2) Rebuild(); }
        void LocateCut(Vector3 worldPoint)
        {
            float best = float.PositiveInfinity;
            for (int i = 0; i < path.Length - 1; i++)
            {
                Vector3 a = transform.TransformPoint(path[i]), b = transform.TransformPoint(path[i + 1]);
                float t = ClosestAlong(worldPoint, a, b);
                float distance = (worldPoint - Vector3.Lerp(a, b, t)).sqrMagnitude;
                if (distance >= best) continue;
                best = distance; cutSegment = i; cutLocal = Vector3.Lerp(path[i], path[i + 1], t);
                cutDistance = Mathf.Lerp(lengths[i], lengths[i + 1], t);
            }
        }
        static float ClosestAlong(Vector3 p, Vector3 a, Vector3 b)
        { Vector3 d = b - a; return d.sqrMagnitude > .0000001f ? Mathf.Clamp01(Vector3.Dot(p - a, d) / d.sqrMagnitude) : 0; }
        static bool Finite(Vector3 v) => float.IsFinite(v.x) && float.IsFinite(v.y) && float.IsFinite(v.z);
        float WorldRadius => radius * Mathf.Max(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.y), Mathf.Abs(transform.lossyScale.z));

        void OnDisable()
        {
            if (cutSparks != null) cutSparks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (cutAudio != null) cutAudio.Stop();
            ReleaseMesh();
        }
        void OnDestroy() => ReleaseMesh();
        void ReleaseMesh()
        {
            if (mesh == null) return;
            if (filter != null && filter.sharedMesh == mesh) filter.sharedMesh = null;
            if (Application.isPlaying) Destroy(mesh); else DestroyImmediate(mesh);
            mesh = null;
        }
    }
}
