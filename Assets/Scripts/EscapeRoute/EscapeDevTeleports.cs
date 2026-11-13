using System.Collections.Generic;
using CurtainCall.Dev;
using CurtainCall.Network.PlayerSync;
using UnityEngine;

namespace CurtainCall.EscapeRoute
{
    /// <summary>F9 late-route stops, relative to the authored route anchors (including the rotated campaign frame).</summary>
    static class EscapeDevTeleports
    {
        const string Prefix = "탈출로 · ";
        internal static bool IsCheckpoint(DevCheats.TeleportPoint point)
            => point.Label.StartsWith(Prefix, System.StringComparison.Ordinal);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register() => DevCheats.RegisterTeleportSource(Collect);

        static void Collect(NetworkPlayer local, List<DevCheats.TeleportPoint> points)
        {
            if (!EscapeCampaignContent.IsTraversalActive) return;
            var content = EscapeCampaignContent.Current;
            var frame = content.transform;
            var facing = Quaternion.LookRotation(frame.TransformDirection(Vector3.right), Vector3.up);
            Transform runStart = null, swingTakeoff = null, swingLanding = null, exit = null;
            foreach (var anchor in frame.GetComponentsInChildren<Transform>(true))
            {
                // A marker can be hidden; its route branch must be active.
                if (anchor.parent == null || !anchor.parent.gameObject.activeInHierarchy) continue;
                switch (anchor.name)
                {
                    case "Anchor_RunStart": runStart = anchor; break;
                    case "Anchor_SwingTakeoff": swingTakeoff = anchor; break;
                    case "Anchor_SwingLanding": swingLanding = anchor; break;
                    case "Anchor_ExitToReeds": exit = anchor; break;
                }
            }
            Add("후반 질주 시작", runStart, 0);
            Add("후반 그네 앞", swingTakeoff, 0);
            Add("후반 그네 뒤", swingLanding, 0);
            Add("마지막 화염 구간", exit, -34);
            Add("출구 절단기 앞", exit, -18);
            Add("마지막 문 앞", exit, -3);
            if (content.ExitOpen) Add("갈대밭 · 엔딩 도착", exit, 4);

            void Add(string label, Transform anchor, float alongRoute)
            {
                if (anchor != null)
                    points.Add(new DevCheats.TeleportPoint(Prefix + label,
                        anchor.position + frame.TransformVector(Vector3.right * alongRoute), facing));
            }
        }
    }
}
