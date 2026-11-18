using System;
using CurtainCall.HumanCannon;
using UnityEngine;

namespace CurtainCall.EscapeRoute
{
    public sealed class EscapeTankSpecimen : MonoBehaviour
    {
        [Serializable]
        struct LimbPair
        {
            public Renderer limb, extremity;
            public Vector3 modelOffset;
        }

        [SerializeField] HumanCannonHazardCue cue;
        [SerializeField] Transform model;
        [SerializeField] LimbPair[] pairs;
        [SerializeField] int stationSeed;
        int shown = -1, previousDrop = -1;
        double previousStart = double.NaN;

        void LateUpdate()
        {
            var course = HumanCannonHazardCourse.Current;
            var route = EscapeRouteTimer.Current;
            if (!course || !course.IsSynchronized || !route || !route.HasStarted || !cue || !model || pairs.Length != 4) return;
            double period = cue.warningSeconds + cue.activeSeconds + cue.restSeconds;
            int drop = (int)Math.Floor((course.Elapsed + cue.phase) / period);
            if (previousDrop == drop && previousStart == route.StartedAt) return;
            previousStart = route.StartedAt;
            previousDrop = drop;
            long startBits = BitConverter.DoubleToInt64Bits(previousStart);
            int pick = SelectPair(unchecked(stationSeed ^ (int)startBits ^ (int)(startBits >> 32)), drop);
            if (pick == shown) return;
            for (int i = 0; i < pairs.Length; i++)
            {
                pairs[i].limb.enabled = pairs[i].extremity.enabled = i == pick;
            }
            model.localPosition = pairs[pick].modelOffset;
            shown = pick;
        }

        // A shared shuffle for each group of four drops. Reserve the previous group's last
        // pair at the boundary so that a joining client can reconstruct the choice immediately.
        internal static int SelectPair(int seed, int drop)
        {
            int group = drop / 4;
            int[] order = Shuffle(seed, group);
            if (group > 0 && order[0] == Shuffle(seed, group - 1)[3])
                (order[0], order[1]) = (order[1], order[0]);
            return order[drop % 4];
        }

        static int[] Shuffle(int seed, int group)
        {
            uint state = unchecked((uint)seed + (uint)group * 0x9e3779b9u);
            int[] order = { 0, 1, 2, 3 };
            for (int i = order.Length - 1; i > 0; i--)
            {
                state = unchecked(state * 1664525u + 1013904223u);
                int j = (int)(((ulong)state * (uint)(i + 1)) >> 32);
                (order[i], order[j]) = (order[j], order[i]);
            }
            return order;
        }
    }
}
