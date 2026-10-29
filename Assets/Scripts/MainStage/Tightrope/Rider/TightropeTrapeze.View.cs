using CurtainCall.Network.PlayerSync;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

namespace CurtainCall.Tightrope
{
    public sealed partial class TightropeTrapeze
    {
        sealed class PoleView
        {
            public LineRenderer Bar, LeftCable, RightCable, BodyLink;
            public TextMeshPro Label;
        }
        [Tooltip("공중그네 봉 위에 봉 이름·잡기 안내·유지 시간 문구를 띄운다. 꺼도 봉 색과 HUD 안내는 유지한다.")]
        [SerializeField] bool showPoleLabels;
        Transform trapezeViewRoot;
        readonly PoleView[] views = new PoleView[PoleTotal];
        Material trapezeMaterial;
        TMP_FontAsset trapezeRuntimeFont;
        MaterialPropertyBlock trapezeTint;

        public Transform PoleVisual(int index) => index >= 0 && index < PoleTotal && views[index]?.Bar != null ? views[index].Bar.transform : null;
        public Vector3 GripPoint(NetworkPlayer player)
        {
            if (!FindHeld(player, out int index, out bool lower)) return player != null ? player.transform.position : Vector3.zero;
            return lower ? Feet(index, false) + Vector3.up * .15f
                : Feet(index, false) + Vector3.up * poles[index].Physics.HangLength;
        }

        public string LocalHint
        {
            get
            {
                var local = NetworkPlayer.Local;
                if (!Active || local == null) return null;
                if (FindHeld(local, out int index, out bool lower))
                {
                    string time = $" · 유지 {Mathf.Max(0f, (float)(poles[index].Until - Clock)):0.0}초";
                    if (lower) return "동료 몸에 매달렸어요 · 반동에 맞춰 Space로 먼저 날아가기\n공중 WASD 조절 · 다음 봉에 가까워지면 F 잡기" + time;
                    if (poles[index].Lower != Nobody)
                        return "W/S로 두 사람 반동 키우기 · 아래 동료가 Space로 먼저 날아가요\n아래 동료가 놓은 뒤 Space로 따라가기 · 다음 봉의 동료 몸을 F로 잡기" + time;
                    return index == 2 ? "W/S 반동 · Space로 마지막 줄에 착지 · 공중 WASD 조절" + time
                        : "W/S로 반동 키우기 · 동료는 아래에서 F로 매달리기\n동료가 먼저 건넜다면 Space로 따라가서 동료 몸에 F" + time;
                }
                if (IsInFlight(local))
                {
                    for (int i = 0; i < PoleTotal; i++)
                    {
                        if (CanGrab(local, i)) return $"지금 F! {i + 1}번째 봉을 잡을 수 있어요 · 공중 WASD 조절";
                        if (CanAttach(local, i)) return "지금 F! 먼저 간 동료의 몸에 매달릴 수 있어요 · 공중 WASD 조절";
                    }
                    return "공중 WASD 조절 · 다음 봉 또는 먼저 간 동료 몸에 가까이 가서 F\n마지막 낮은 봉에서는 혼자 줄로 내려올 수 있어요";
                }
                float distance = course.GetDistance(local.transform.position);
                if (distance < course.TrapezeGapFrom - 7f || distance > course.TrapezeGapTo + 4f) return null;
                for (int i = 0; i < PoleTotal; i++)
                {
                    if (CanGrab(local, i)) return $"지금 F로 {i + 1}번째 높은 봉 잡기";
                    if (CanAttach(local, i)) return "F로 봉을 잡은 동료 몸에 매달리기 · 반동이 생기면 Space로 먼저 날아가기";
                }
                return distance < course.TrapezeGapFrom
                    ? "공중그네 · 봉 가까이에서 F로 잡기 · 점프 중에도 F 잡기\n동료 몸에도 가까이에서 F로 매달릴 수 있어요"
                    : "먼저 착지했다면 F로 동료 받기 준비 · 남은 동료는 마지막 낮은 봉으로 이동";
            }
        }

        void UpdateViews()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) return;
            if (!isActiveAndEnabled || course == null || !course.HasTrapezeGap) { HideViews(); return; }
            if (trapezeViewRoot == null) BuildViews();
            if (trapezeViewRoot == null) return;
            trapezeViewRoot.gameObject.SetActive(true);
            var camera = Camera.main;
            var local = NetworkPlayer.Local;
            for (int i = 0; i < PoleTotal; i++)
            {
                var view = views[i]; var state = poles[i];
                Vector3 feet = Feet(i, false), grip = feet + Vector3.up * state.Physics.HangLength;
                Vector3 pivot = state.Physics.Grip.position + Vector3.up * 1.5f;
                Vector3 side = course.Right * .65f;
                bool occupied = state.Upper != Nobody;
                float left = Mathf.Max(0f, (float)(state.Until - Clock));
                Color color = occupied && left < 5f ? new Color(1f, .5f, .1f)
                    : CanGrab(local, i) || CanAttach(local, i) ? Color.green : i == 2 ? new Color(.3f, 1f, .5f) : Color.cyan;
                DrawLine(view.Bar, grip - side, grip + side, color);
                DrawLine(view.LeftCable, pivot - side, grip - side, color);
                DrawLine(view.RightCable, pivot + side, grip + side, color);
                view.BodyLink.gameObject.SetActive(state.Lower != Nobody);
                if (state.Lower != Nobody) DrawLine(view.BodyLink, feet + Vector3.up * .15f, Feet(i, true) + Vector3.up * 1.6f, Color.yellow);
                view.Label.gameObject.SetActive(showPoleLabels);
                if (!showPoleLabels) continue;
                view.Label.text = i == 0 ? "1 · 출발 봉" : i == 1 ? "2 · 동료와 교대하는 봉" : "3 · 혼자 내려오는 낮은 봉";
                view.Label.text += occupied ? $"\n{(state.Lower == Nobody ? "1" : "2")}명 · {left:0.0}초"
                    : "\n가까이에서 F 잡기 · 점프 중에도 가능";
                view.Label.color = color; view.Label.transform.position = pivot + Vector3.up * .65f;
                if (camera != null) view.Label.transform.rotation = camera.transform.rotation;
            }
        }
        void BuildViews()
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            if (shader == null) return;
            // Unity 네이티브 객체는 MonoBehaviour 필드 초기화가 아닌 실행 시점에 만든다.
            trapezeTint ??= new MaterialPropertyBlock();
            trapezeMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            trapezeViewRoot = new GameObject("Trapeze display (runtime)").transform; trapezeViewRoot.SetParent(transform, false);
            var source = Resources.Load<Font>("Fonts/Pretendard-Medium/Pretendard-Medium 1");
            trapezeRuntimeFont = source != null ? TMP_FontAsset.CreateFontAsset(source) : null;
            if (trapezeRuntimeFont != null) trapezeRuntimeFont.hideFlags = HideFlags.DontSave;
            var font = trapezeRuntimeFont != null ? trapezeRuntimeFont : Resources.Load<TMP_FontAsset>("Fonts/Pretendard-Medium/Pretendard-Medium SDF");
            for (int i = 0; i < PoleTotal; i++)
            {
                var view = views[i] = new PoleView { Bar = NewLine($"Pole {i + 1}", .13f), LeftCable = NewLine("Left cable", .04f),
                    RightCable = NewLine("Right cable", .04f), BodyLink = NewLine("Two player grip", .04f) };
                var label = new GameObject("Trapeze instructions", typeof(RectTransform), typeof(TextMeshPro)); label.transform.SetParent(trapezeViewRoot, false);
                view.Label = label.GetComponent<TextMeshPro>(); view.Label.font = font; view.Label.fontSize = 2.5f;
                view.Label.alignment = TextAlignmentOptions.Center; view.Label.textWrappingMode = TextWrappingModes.NoWrap;
                view.Label.rectTransform.sizeDelta = new Vector2(5f, 1f);
            }
        }
        LineRenderer NewLine(string name, float width)
        {
            var line = new GameObject(name, typeof(LineRenderer)).GetComponent<LineRenderer>(); line.transform.SetParent(trapezeViewRoot, false);
            line.sharedMaterial = trapezeMaterial; line.positionCount = 2; line.useWorldSpace = true; line.startWidth = line.endWidth = width;
            line.numCapVertices = 4; line.shadowCastingMode = ShadowCastingMode.Off; line.receiveShadows = false; return line;
        }
        void DrawLine(LineRenderer line, Vector3 from, Vector3 to, Color color)
        {
            line.SetPosition(0, from); line.SetPosition(1, to); line.startColor = line.endColor = color;
            trapezeTint.SetColor("_BaseColor", color); trapezeTint.SetColor("_Color", color); line.SetPropertyBlock(trapezeTint);
            var profile = course != null ? course.ArtProfile : null;
            if (profile != null) TightropeArtFit.Line(line, line.name.StartsWith("Pole ") ? profile.barY : profile.cableY, from, to, color);
        }
        void HideViews() { if (trapezeViewRoot != null) trapezeViewRoot.gameObject.SetActive(false); }
        void DestroyViews()
        {
            if (trapezeViewRoot != null) { trapezeViewRoot.gameObject.SetActive(false); Destroy(trapezeViewRoot.gameObject); }
            if (trapezeMaterial != null) Destroy(trapezeMaterial);
            if (trapezeRuntimeFont != null)
            { foreach (var atlas in trapezeRuntimeFont.atlasTextures) if (atlas != null) Destroy(atlas); Destroy(trapezeRuntimeFont.material); Destroy(trapezeRuntimeFont); }
            trapezeViewRoot = null; trapezeMaterial = null; trapezeRuntimeFont = null;
        }
    }
}
