using CurtainCall.Tightrope;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CurtainCall.HumanCannon
{
    public sealed partial class HumanCannonPole
    {
        Transform commonVisual;
        GameObject shownVisual;
        Vector3 authoredVisualScale, authoredGripPosition, authoredBarPosition, authoredBarScale;
        Quaternion authoredVisualRotation, authoredBarRotation;
        float authoredBarWidth;
        CableVisual commonLeftCable, commonRightCable;
        PoleCountdownView countdown;
        public Transform CommonVisual => commonVisual;
        public bool PresentationVisible { get; set; } = true;

        void EnsureCommonVisual()
        {
            if (!Application.isPlaying || !isActiveAndEnabled) return;
            RefreshCommonVisual();
        }

        void RefreshCommonVisual()
        {
            var source = Rules.Visual(IsSwitch || linkedGate != null);
            if (source == null) { DestroyCommonVisual(); return; }
            if (shownVisual == source && commonVisual != null) return;
            DisableVisual(visualBar); DisableVisual(leftCable); DisableVisual(rightCable);
            DestroyCommonVisual();
            var instance = Instantiate(source);
            instance.name = "Common pole visual";
            // 표시만 씬 루트에 둔다. 배치 부모의 비균일 스케일/전단이 봉과 줄을 늘이지 않게 한다.
            SceneManager.MoveGameObjectToScene(instance, gameObject.scene);
            commonVisual = instance.transform;
            shownVisual = source;
            authoredVisualScale = source.transform.localScale;
            authoredVisualRotation = source.transform.localRotation;
            var sourceGrip = commonVisual.Find("Grip");
            authoredGripPosition = sourceGrip != null ? commonVisual.InverseTransformPoint(sourceGrip.position) : Vector3.zero;
            visualBar = commonVisual.Find("VisualBar");
            leftAnchor = commonVisual.Find("LeftAnchor"); rightAnchor = commonVisual.Find("RightAnchor");
            leftCable = SelectCable("LeftSwingCable", "LeftCable");
            rightCable = SelectCable("RightSwingCable", "RightCable");
            if (visualBar != null)
            {
                authoredBarPosition = visualBar.localPosition;
                authoredBarRotation = visualBar.localRotation;
                authoredBarScale = visualBar.localScale;
                authoredBarWidth = MeasureBarWidth(visualBar);
                commonLeftCable = CaptureCable(leftCable, leftAnchor);
                commonRightCable = CaptureCable(rightCable, rightAnchor);
            }
            foreach (var collider in instance.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            PresentCommonRoot();
        }

        Transform SelectCable(string swingName, string fallbackName)
        {
            var swing = commonVisual.Find(swingName);
            var fallback = commonVisual.Find(fallbackName);
            var selected = swing != null ? swing : fallback;
            if (selected != null) selected.gameObject.SetActive(true);
            if (swing != null && fallback != null) fallback.gameObject.SetActive(false);
            return selected;
        }

        void PresentCommonRoot()
        {
            if (commonVisual == null) return;
            commonVisual.SetPositionAndRotation(Grip.position, Grip.rotation * authoredVisualRotation);
            commonVisual.localScale = authoredVisualScale;
            commonVisual.position -= commonVisual.TransformVector(authoredGripPosition);
            commonVisual.gameObject.SetActive(PresentationVisible && isActiveAndEnabled);
            if (visualBar == null) return;
            float ratio = authoredBarWidth > .0001f ? Rules.BarWidth(IsSwitch || linkedGate != null) / authoredBarWidth : 1f;
            visualBar.localScale = Vector3.Scale(authoredBarScale, new Vector3(ratio, 1f, 1f));
            // 봉 길이만 바꾸고 줄의 높이/굵기는 유지한다. 앵커의 수평 간격은 봉과 함께 바뀐다.
            Vector3 axis = Vector3.ProjectOnPlane(authoredBarRotation * Vector3.right, Vector3.up).normalized;
            ResizeAnchor(commonLeftCable, axis, ratio);
            ResizeAnchor(commonRightCable, axis, ratio);
        }

        void ResizeAnchor(CableVisual cable, Vector3 axis, float ratio)
        {
            if (cable == null) return;
            Vector3 position = cable.AnchorPosition;
            cable.Anchor.localPosition = position + axis * (Vector3.Dot(position - authoredBarPosition, axis) * (ratio - 1f));
        }

        sealed class CableVisual
        {
            public Transform Cable, Anchor;
            public Vector3 Scale, Span, AnchorPosition;
            public Quaternion Rotation;
        }

        CableVisual CaptureCable(Transform cable, Transform anchor)
        {
            if (cable == null || anchor == null) return null;
            Vector3 offset = leftAnchor != null && rightAnchor != null
                ? anchor.position - (leftAnchor.position + rightAnchor.position) * .5f
                : Vector3.ProjectOnPlane(anchor.position - visualBar.position, Vector3.up);
            return new CableVisual
            {
                Cable = cable, Anchor = anchor, Scale = cable.localScale, Rotation = cable.localRotation,
                AnchorPosition = anchor.localPosition,
                Span = commonVisual.InverseTransformVector(anchor.position - visualBar.position - offset)
            };
        }

        void PresentCommonCable(CableVisual cable, Vector3 barPosition)
        {
            if (cable == null || cable.Span.sqrMagnitude <= .000001f) return;
            Vector3 offset = leftAnchor != null && rightAnchor != null
                ? cable.Anchor.position - (leftAnchor.position + rightAnchor.position) * .5f
                : Vector3.ProjectOnPlane(cable.Anchor.position - Grip.position, Vector3.up);
            Vector3 end = barPosition + offset;
            Vector3 span = commonVisual.InverseTransformVector(cable.Anchor.position - end);
            if (span.sqrMagnitude <= .000001f) return;
            cable.Cable.position = (cable.Anchor.position + end) * .5f;
            cable.Cable.localRotation = Quaternion.FromToRotation(cable.Span, span) * cable.Rotation;
            var scale = cable.Scale;
            scale.y *= span.magnitude / cable.Span.magnitude;
            cable.Cable.localScale = scale;
        }

        static float MeasureBarWidth(Transform bar)
        {
            float min = float.PositiveInfinity, max = float.NegativeInfinity;
            foreach (var filter in bar.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null) continue;
                var bounds = filter.sharedMesh.bounds;
                var matrix = bar.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                for (int i = 0; i < 8; i++)
                {
                    var corner = bounds.center + Vector3.Scale(bounds.extents,
                        new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    float x = matrix.MultiplyPoint3x4(corner).x;
                    min = Mathf.Min(min, x); max = Mathf.Max(max, x);
                }
            }
            return float.IsFinite(min) && float.IsFinite(max) ? (max - min) * bar.TransformVector(Vector3.right).magnitude : 0f;
        }

        // 종료 경로에서는 외형을 만들지 않는다. 씬 unload 때 표시 루트가 먼저 사라질 수 있다.
        internal void HidePresentation()
        {
            if (commonVisual != null) commonVisual.gameObject.SetActive(false);
            countdown?.Hide();
        }

        internal void PresentCountdown(float seconds, float swingAngle, bool hasRiders)
        {
            if (!hasRiders || !PresentationVisible || !isActiveAndEnabled) { countdown?.Hide(); return; }
            if (!Application.isPlaying || SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
            countdown ??= PoleCountdownView.Create(transform);
            Vector3 position = BarPosition(swingAngle) + Vector3.up * Rules.CountdownHeight;
            var camera = Camera.main;
            // The digits are on the panel's local -Z face.
            Vector3 forward = camera != null ? Vector3.ProjectOnPlane(position - camera.transform.position, Vector3.up) : Forward;
            countdown?.Show(seconds, position,
                forward == Vector3.zero ? Quaternion.identity : Quaternion.LookRotation(forward));
        }
        void OnDisable() => HidePresentation();
        void OnDestroy()
        {
            countdown?.Destroy();
            DestroyCommonVisual();
        }

        void DestroyCommonVisual()
        {
            if (commonVisual != null)
            {
                commonVisual.gameObject.SetActive(false);
                if (Application.isPlaying) Destroy(commonVisual.gameObject); else DestroyImmediate(commonVisual.gameObject);
            }
            commonVisual = null;
            shownVisual = null;
            commonLeftCable = commonRightCable = null;
        }

        static void DisableVisual(Transform part)
        {
            if (part == null) return;
            foreach (var renderer in part.GetComponentsInChildren<Renderer>(true)) renderer.enabled = false;
            foreach (var collider in part.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
        }
    }
}
