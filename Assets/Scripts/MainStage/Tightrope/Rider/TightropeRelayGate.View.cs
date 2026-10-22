using System.Collections.Generic;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Player;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

namespace CurtainCall.Tightrope
{
    public sealed partial class TightropeRelayGate
    {
        sealed class RelayPoleView
        {
            public Transform Root, Bar, LeftCable, RightCable, Pad, Link;
            public TextMeshPro Label;
        }

        sealed class RelayPathView
        {
            public int Lane;
            public Transform Strip;
        }

        readonly List<RelayPoleView> relayPoleViews = new();
        readonly List<RelayPathView> relayPathViews = new();
        Transform relayViewRoot;
        Mesh relayBoxMesh;
        Material relayViewMaterial;
        TMP_FontAsset relayRuntimeFont, relayLabelFont;
        MaterialPropertyBlock relayTint;
        static readonly Color RelayCyan = new(.1f, .9f, 1f);
        static readonly Color RelayGreen = new(.2f, 1f, .4f);
        static readonly Color RelayOrange = new(1f, .55f, .08f);
        static readonly Color RelayIdle = new(.3f, .37f, .43f);

        public Transform PoleVisual(int index) => index >= 0 && index < relayPoleViews.Count ? relayPoleViews[index].Bar : null;

        public string LocalHint
        {
            get
            {
                var local = NetworkPlayer.Local;
                if (!IsActive || course == null || local == null || local.State != PlayerState.Normal) return null;
                int held = HeldPole(local);
                string warning = OutboundWarning || ReturnWarning
                    ? $"\n줄이 {WarningRemaining:0.0}초 뒤 끊어져요! 동료가 봉을 다시 잡아 주세요" : "";
                if (held >= 0)
                {
                    bool far = IsFarPole(held);
                    string task = far ? (ReturnOpen ? "남은 동료의 길을 유지 중 · 모두 건널 때까지 버티세요" : "반대편 봉을 함께 잡을 동료를 기다리세요")
                        : ReturnOpen ? "반대편 동료가 길을 잡았어요 · 놓고 내 줄로 건너세요"
                        : OutboundOpen ? "가운데 통로 연결 중 · 건넌 동료가 반대편 봉을 잡을 때까지 버티세요"
                        : "출발 봉을 함께 잡을 동료를 기다리세요";
                    return $"{task}\n봉 유지 {HoldRemaining(held):0.0}초 · {RelayHolderCount(far)}/{RequiredHolders}명 · F / Space 놓기" + warning;
                }
                float distance = course.GetDistance(local.transform.position);
                if (distance < EntryDistance - 7f || distance > ExitDistance + 7f) return null;
                for (int i = 0; i < PoleCount; i++)
                    if (CanUsePole(i, local))
                        return IsFarPole(i) ? $"F로 초록색 봉 잡기 · {RequiredHolders}명이 잡으면 뒤에 남은 동료의 줄이 이어져요" + warning
                            : $"F로 청록색 봉 잡기 · {RequiredHolders}명이 잡으면 {TransitLane + 1}번 통로가 이어져요" + warning;
                if (distance >= GapTo)
                    return ReturnOpen ? "남은 동료의 줄이 이어졌어요 · 봉 담당은 모두 건널 때까지 유지" + warning
                        : $"건넌 사람은 초록색 봉으로! {RelayHolderCount(true)}/{RequiredHolders}명 · 남은 동료의 길을 열어 주세요" + warning;
                return OutboundOpen ? $"{TransitLane + 1}번 통로로 건너세요 · 건넌 사람은 반대편 초록색 봉 잡기" + warning
                    : $"앞쪽 줄이 끊겼어요 · 청록색 봉 {RelayHolderCount(false)}/{RequiredHolders}명이 잡으면 {TransitLane + 1}번 통로 연결" + warning;
            }
        }

        int RelayHolderCount(bool far)
        {
            int count = 0;
            for (int i = 0; i < PoleCount; i++)
                if (IsFarPole(i) == far && HolderAt(i) != ulong.MaxValue) count++;
            return count;
        }

        void UpdateViews()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) return;
            if (!isActiveAndEnabled || course == null || !course.HasRelayGap) { HideViews(); return; }
            if (relayViewRoot == null || relayPoleViews.Count != PoleCount) BuildRelayViews();
            if (relayViewRoot == null) return;
            relayViewRoot.gameObject.SetActive(true);
            var camera = Camera.main;
            Quaternion facing = Quaternion.LookRotation(course.Forward);
            for (int i = 0; i < relayPoleViews.Count; i++)
            {
                var view = relayPoleViews[i];
                var pole = PhysicalPole(i);
                bool available = pole != null && IsPoleAvailable(i);
                view.Root.gameObject.SetActive(pole != null);
                if (pole == null) continue;
                bool far = IsFarPole(i), occupied = HolderAt(i) != ulong.MaxValue;
                bool warning = far ? ReturnWarning : OutboundWarning;
                Color color = warning ? RelayOrange : far ? RelayGreen : RelayCyan;
                Color poleColor = available || occupied ? color : RelayIdle;
                Vector3 feet = PoleFeet(i), grip = pole.Grip.position;
                view.Bar.SetPositionAndRotation(grip, facing);
                view.Bar.localScale = new Vector3(1.15f, .12f, .12f);
                view.Pad.SetPositionAndRotation(feet + Vector3.up * .055f, facing);
                view.Pad.localScale = new Vector3(.65f, .025f, .85f);
                Vector3 span = course.Right * .5f;
                SetRelayRod(view.LeftCable, grip - span, grip - span + Vector3.up * 1.6f, .045f);
                SetRelayRod(view.RightCable, grip + span, grip + span + Vector3.up * 1.6f, .045f);
                Vector3 endpoint = course.GetLanePosition(far ? PoleLane(i) : TransitLane, far ? GapTo : GapFrom);
                SetRelayRod(view.Link, grip + Vector3.up * 1.6f, endpoint + Vector3.up * 2.5f, .025f);
                TintRelay(view.Bar, poleColor); TintRelay(view.Pad, poleColor);
                TintRelay(view.LeftCable, poleColor); TintRelay(view.RightCable, poleColor);
                TintRelay(view.Link, occupied ? color : RelayIdle);
                string state = occupied ? $"유지 {HoldRemaining(i):0.0}초" : available ? "F 잡기" : "사용 불가";
                string phase = far ? "동료의 길 연결" : $"{TransitLane + 1}번 통로 연결";
                view.Label.text = $"{PoleLane(i) + 1}번 {(far ? "교대" : "출발")} 봉 · {state}\n{phase} · {RelayHolderCount(far)}/{RequiredHolders}명"
                    + (warning ? $"\n{(far ? ReturnWarningRemaining : OutboundWarningRemaining):0.0}초 뒤 줄 끊김" : "");
                view.Label.color = poleColor;
                view.Label.transform.position = grip + Vector3.up * 1.05f;
                if (camera != null) view.Label.transform.rotation = camera.transform.rotation;
            }
            foreach (var path in relayPathViews)
            {
                bool transit = path.Lane == TransitLane;
                bool open = transit ? OutboundOpen : ReturnOpen;
                bool warning = transit ? OutboundWarning : ReturnWarning;
                // 실제로 연결된 줄만 덧칠한다. 소각된 구간에 가짜 발판을 표시하지 않는다.
                open &= LandingWaveMath.SharesSupport(course.GetUsableSpans(path.Lane), GapFrom + .02f, GapTo - .02f);
                // Prepared relay rope carries its own round, striped appearance.
                // Keep the old strip only for scenes without the prepared asset.
                open &= course.ArtProfile == null || course.ArtProfile.relayRope == null;
                path.Strip.gameObject.SetActive(open);
                if (!open) continue;
                path.Strip.SetPositionAndRotation(course.GetLanePosition(path.Lane, (GapFrom + GapTo) * .5f) + Vector3.up * .075f, facing);
                path.Strip.localScale = new Vector3(.1f, .02f, Mathf.Max(.01f, GapTo - GapFrom));
                TintRelay(path.Strip, warning ? RelayOrange : transit ? RelayCyan : RelayGreen);
            }
        }

        void BuildRelayViews()
        {
            DestroyViews();
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            if (shader == null) return;
            // Unity 네이티브 객체는 MonoBehaviour 필드 초기화가 아닌 실행 시점에 만든다.
            relayTint ??= new MaterialPropertyBlock();
            relayViewMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            relayBoxMesh = new Mesh { name = "Relay gate display box", hideFlags = HideFlags.HideAndDontSave };
            relayBoxMesh.vertices = new[] { new Vector3(-.5f,-.5f,-.5f), new Vector3(.5f,-.5f,-.5f), new Vector3(.5f,.5f,-.5f), new Vector3(-.5f,.5f,-.5f),
                new Vector3(-.5f,-.5f,.5f), new Vector3(.5f,-.5f,.5f), new Vector3(.5f,.5f,.5f), new Vector3(-.5f,.5f,.5f) };
            relayBoxMesh.triangles = new[] { 0,2,1,0,3,2, 4,5,6,4,6,7, 0,4,7,0,7,3, 1,2,6,1,6,5, 3,7,6,3,6,2, 0,1,5,0,5,4 };
            relayBoxMesh.RecalculateBounds();
            relayViewRoot = new GameObject("Relay gate displays (runtime)").transform;
            relayViewRoot.SetParent(transform, false);
            var source = Resources.Load<Font>("Fonts/Pretendard-Medium/Pretendard-Medium 1");
            relayRuntimeFont = source != null ? TMP_FontAsset.CreateFontAsset(source) : null;
            if (relayRuntimeFont != null) relayRuntimeFont.hideFlags = HideFlags.DontSave;
            relayLabelFont = relayRuntimeFont != null ? relayRuntimeFont : Resources.Load<TMP_FontAsset>("Fonts/Pretendard-Medium/Pretendard-Medium SDF");
            var lanes = new HashSet<int> { TransitLane };
            for (int i = 0; i < PoleCount; i++)
            {
                var root = new GameObject($"Relay pole {i}").transform; root.SetParent(relayViewRoot, false);
                var view = new RelayPoleView { Root = root, Bar = RelayBox(root, "Bar"), Pad = RelayBox(root, "Stand here"),
                    LeftCable = RelayBox(root, "Left cable"), RightCable = RelayBox(root, "Right cable"), Link = RelayBox(root, "Connection cable") };
                var label = new GameObject("Pole instructions", typeof(RectTransform), typeof(TextMeshPro));
                label.transform.SetParent(root, false);
                view.Label = label.GetComponent<TextMeshPro>(); view.Label.font = relayLabelFont;
                view.Label.fontSize = 2.2f; view.Label.alignment = TextAlignmentOptions.Center;
                view.Label.textWrappingMode = TextWrappingModes.NoWrap; view.Label.rectTransform.sizeDelta = new Vector2(4f, 1f);
                relayPoleViews.Add(view); lanes.Add(PoleLane(i));
            }
            foreach (int lane in lanes) relayPathViews.Add(new RelayPathView { Lane = lane, Strip = RelayBox(relayViewRoot, $"Open lane {lane + 1}") });
        }

        Transform RelayBox(Transform parent, string name)
        {
            var box = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            box.transform.SetParent(parent, false); box.GetComponent<MeshFilter>().sharedMesh = relayBoxMesh;
            var renderer = box.GetComponent<MeshRenderer>(); renderer.sharedMaterial = relayViewMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            var profile = course != null ? course.ArtProfile : null;
            if (profile != null && name == "Bar") TightropeArtFit.Box(box.transform, profile.barX);
            else if (profile != null && name.Contains("cable")) TightropeArtFit.Box(box.transform, profile.cableY);
            return box.transform;
        }

        void TintRelay(Transform item, Color color)
        {
            relayTint.SetColor("_BaseColor", color); relayTint.SetColor("_Color", color);
            item.GetComponent<Renderer>().SetPropertyBlock(relayTint);
            TightropeArtFit.Tint(item, color);
        }

        static void SetRelayRod(Transform rod, Vector3 from, Vector3 to, float width)
        {
            rod.SetPositionAndRotation((from + to) * .5f, Quaternion.FromToRotation(Vector3.up, to - from));
            rod.localScale = new Vector3(width, Vector3.Distance(from, to), width);
        }

        void HideViews() { if (relayViewRoot != null) relayViewRoot.gameObject.SetActive(false); }

        void DestroyViews()
        {
            if (relayViewRoot != null) { relayViewRoot.gameObject.SetActive(false); Destroy(relayViewRoot.gameObject); }
            if (relayBoxMesh != null) Destroy(relayBoxMesh);
            if (relayViewMaterial != null) Destroy(relayViewMaterial);
            if (relayRuntimeFont != null)
            {
                foreach (var atlas in relayRuntimeFont.atlasTextures) if (atlas != null) Destroy(atlas);
                Destroy(relayRuntimeFont.material); Destroy(relayRuntimeFont);
            }
            relayViewRoot = null; relayBoxMesh = null; relayViewMaterial = null;
            relayRuntimeFont = relayLabelFont = null; relayPoleViews.Clear(); relayPathViews.Clear();
        }
    }
}
