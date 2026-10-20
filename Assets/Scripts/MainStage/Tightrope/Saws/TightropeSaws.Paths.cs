using UnityEngine;
using UnityEngine.Rendering;

namespace CurtainCall.Tightrope.Saws
{
    public sealed partial class TightropeSaws
    {
        Transform pathRoot;
        Material pathMaterial;
        LineRenderer[] horizontalPaths;
        LineRenderer verticalPath, cooperativePath;

        void BuildPaths(Transform generated, TightropeSawVisualResources resources)
        {
            pathRoot = new GameObject("Saw paths (visual only)").transform;
            pathRoot.SetParent(generated, false);
            pathMaterial = resources.Unlit(new Color(1f, .025f, .015f));
            horizontalPaths = new LineRenderer[horizontalViews.Length];
            for (int i = 0; i < horizontalPaths.Length; i++)
                horizontalPaths[i] = NewPath($"Horizontal path {i + 1:00}");
            verticalPath = NewPath("Incoming path");
            cooperativePath = NewPath("Cooperative path");
            // A rebuild retires the old generated hierarchy, including these guides.
            foreach (var gate in releasedGates) gate.Path = null;
            UpdatePaths();
        }

        LineRenderer NewPath(string name)
        {
            var line = new GameObject(name, typeof(LineRenderer)).GetComponent<LineRenderer>();
            line.transform.SetParent(pathRoot, false);
            line.sharedMaterial = pathMaterial;
            line.useWorldSpace = true; line.positionCount = 2;
            line.startWidth = line.endWidth = .045f;
            line.startColor = line.endColor = Color.white;
            line.numCapVertices = 4;
            line.shadowCastingMode = ShadowCastingMode.Off; line.receiveShadows = false;
            line.enabled = false;
            return line;
        }

        static void DrawPath(LineRenderer line, Vector3 from, Vector3 to, bool visible)
        {
            line.enabled = visible && line.sharedMaterial != null && (to - from).sqrMagnitude > .0001f;
            if (!line.enabled) return;
            line.SetPosition(0, from); line.SetPosition(1, to);
        }

        void IncomingPath(LineRenderer line, int lane, float from, float to, float height, bool visible)
        {
            if (!visible) { line.enabled = false; return; }
            DrawPath(line, course.GetLanePosition(lane, Mathf.Clamp(from, 0f, course.FinishDistance)) + Vector3.up * height,
                course.GetLanePosition(lane, Mathf.Clamp(to, 0f, course.FinishDistance)) + Vector3.up * height, true);
        }

        void UpdatePaths()
        {
            if (pathRoot == null || course == null) return;
            pathRoot.gameObject.SetActive(isActiveAndEnabled);
            for (int i = 0; i < horizontalPaths.Length; i++)
            {
                Vector3 center = course.transform.position + course.Forward * settings.GetHorizontalDistance(i)
                    + Vector3.up * (settings.HorizontalCenterHeight - TeamPassageLowerOffset(i));
                Vector3 side = course.Right * settings.horizontalTravelHalfWidth;
                DrawPath(horizontalPaths[i], center - side, center + side, true);
            }
            bool incoming = TryGetVerticalPosition(out var position);
            IncomingPath(verticalPath, verticalLane, settings.verticalEndDistance, settings.verticalSpawnDistance,
                position.y - course.transform.position.y, incoming);
            IncomingPath(cooperativePath, cooperativeGateLane, CooperativeGateStopDistance,
                CooperativeGateStopDistance + settings.cooperativeGateApproachDistance,
                settings.cooperativeGateHeight * .5f, IsCooperativeGateActive && !IsCooperativeGateOpening);
            foreach (var gate in releasedGates)
            {
                bool visible = TryReleasedPosition(gate, out var bottom);
                if (visible && gate.Path == null) gate.Path = NewPath($"Released path {gate.Index}");
                if (gate.Path != null)
                    IncomingPath(gate.Path, gate.Lane, 0f, course.GetDistance(bottom), settings.cooperativeGateHeight * .5f, visible);
            }
        }
    }
}
