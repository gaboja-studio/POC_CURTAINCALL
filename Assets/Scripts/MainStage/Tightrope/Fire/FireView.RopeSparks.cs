using System.Collections.Generic;
using UnityEngine;

namespace CurtainCall.Tightrope.Fire
{
    public sealed partial class FireView
    {
        [SerializeField] ParticleSystem ropeAnchorSparks, ropeFuseEmbers;
        bool HasRopeSparks => ropeAnchorSparks && ropeFuseEmbers;

        sealed class RopeSparks
        {
            public ParticleSystem near, far, fuse;
            public Light glow;
        }

        readonly List<RopeSparks> ropeSparks = new();
        int sparkSegment = -1;

        void PresentRopeSparks()
        {
            int index = fire.PendingSegment;
            if (SegmentIntensity(index) < 0f)
            {
                HideRopeSparks(false);
                return;
            }
            if (sparkSegment != index)
            {
                HideRopeSparks(true);
                sparkSegment = index;
            }

            int lane = fire.Settings.segments[index].lane - 1;
            fire.GetSegmentSpans(index, segmentSpans);
            if (lane < 0 || lane >= course.LaneCount) { HideRopeSparks(true); return; }
            float fuseSeconds = Mathf.Min(RopeDissolve.WindowSeconds, fire.Settings.warningSeconds);
            float remaining = fire.WarningRemaining(index);
            bool burning = remaining <= fuseSeconds;
            float progress = fuseSeconds > 0f ? Mathf.Clamp01(1f - remaining / fuseSeconds) : 1f;
            while (ropeSparks.Count < segmentSpans.Count)
            {
                var spark = new RopeSparks
                {
                    near = Instantiate(ropeAnchorSparks, transform),
                    far = Instantiate(ropeAnchorSparks, transform),
                    fuse = Instantiate(ropeFuseEmbers, transform)
                };
                spark.glow = spark.fuse.GetComponent<Light>();
                ropeSparks.Add(spark);
            }

            for (int piece = 0; piece < ropeSparks.Count; piece++)
            {
                var spark = ropeSparks[piece];
                if (piece >= segmentSpans.Count) { StopSparks(spark, true); continue; }
                var span = segmentSpans[piece];
                var near = course.GetLanePosition(lane, span.x) + Vector3.up * .02f;
                var far = course.GetLanePosition(lane, span.y) + Vector3.up * .02f;
                spark.near.transform.position = near;
                spark.far.transform.position = far;
                spark.fuse.transform.position = Vector3.Lerp(near, far, progress);
                SetSparks(spark.near, !burning);
                SetSparks(spark.far, !burning);
                SetSparks(spark.fuse, burning);
                if (spark.glow) spark.glow.enabled = burning;
            }
        }

        void HideRopeSparks(bool clear)
        {
            if (sparkSegment == -1 && !clear) return;
            foreach (var spark in ropeSparks) StopSparks(spark, clear);
            sparkSegment = -1;
        }

        static void StopSparks(RopeSparks spark, bool clear)
        {
            if (spark.glow) spark.glow.enabled = false;
            StopSpray(spark.near, clear);
            StopSpray(spark.far, clear);
            StopSpray(spark.fuse, clear);
        }

        static void StopSpray(ParticleSystem spray, bool clear)
        {
            if (clear) spray.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            else SetSparks(spray, false);
        }

        static void SetSparks(ParticleSystem spray, bool emit)
        {
            if (emit && !spray.isEmitting) spray.Play(false);
            else if (!emit && spray.isEmitting) spray.Stop(false, ParticleSystemStopBehavior.StopEmitting);
        }
    }
}
