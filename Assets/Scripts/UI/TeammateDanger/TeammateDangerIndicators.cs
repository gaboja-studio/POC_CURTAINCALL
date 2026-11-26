using System.Collections.Generic;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Settings;
using CurtainCall.Tightrope;
using CurtainCall.UI.SceneIntegration;
using UnityEngine;

namespace CurtainCall.UI.TeammateDanger
{
    public enum TeammateDangerKind { None, Hanging, Cut, Fall }

    /// <summary>
    /// 위험한 동료 표시(2026-11-26 M3 사용자 결정). 내 화면에만 그리며 네트워크로 보내는 것이 없다.
    /// - 대상: 줄에 매달린 동료(남은 버티기 시간), 방금 팔다리를 잃은 동료, 낙하로 다친 동료(표시 시간).
    /// - 화면 밖: 화면 가장자리에 원형 아이콘(글자·프로필 없음). 원 안 아이콘 = 위험 종류, 원 테두리 = 남은 시간, 화살촉 = 방향.
    /// - 화면 안: 몸 테두리를 벽 너머에서도 그리고(남은 시간만큼만 선명), 머리 위에 원형 아이콘.
    /// 공연·탈출로(매달림은 공통 외줄 매달림 시스템)·대기실 추격(부상)에서 같은 규칙. 수치는 GameSettings › 월드 UI › '위험한 동료 표시'.
    /// </summary>
    // 매달림 자세 등 다른 LateUpdate가 모델을 옮긴 뒤의 위치를 읽도록 가장 늦게 실행한다.
    [DefaultExecutionOrder(10000)]
    public sealed class TeammateDangerIndicators : MonoBehaviour
    {
        sealed class Tracked
        {
            public int Mark;
            public float InjuredAt = float.NegativeInfinity;
            public TeammateDangerKind InjuryKind;
            public SilhouetteOutline Outline;
            public Renderer[] Body;
            public Animator Rig;
            public float BodyRefreshAt;
        }

        public static TeammateDangerIndicators Instance { get; private set; }
        readonly Dictionary<NetworkPlayer, Tracked> tracked = new();
        readonly List<NetworkPlayer> stale = new();
        TeammateDangerHudView hud;
        GameObject hudSource;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Create()
        {
            if (Instance != null) return;
            var go = new GameObject(nameof(TeammateDangerIndicators));
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<TeammateDangerIndicators>();
        }

        /// <summary>지금 이 화면에서 동료가 어떤 위험인지와 남은 비율(0~1). 표시·검사용.</summary>
        public static TeammateDangerKind Evaluate(NetworkPlayer player, out float remaining)
        {
            remaining = -1f;
            if (Instance == null || player == null || !Instance.tracked.TryGetValue(player, out var entry)) return TeammateDangerKind.None;
            return Instance.Danger(player, entry, GameSettings.WorldUi, out remaining);
        }

        void LateUpdate()
        {
            var settings = GameSettings.WorldUi;
            var local = NetworkPlayer.Local;
            var view = Camera.main;
            bool active = settings != null && settings.teammateDangerEnabled && local != null && local.IsSpawned && view != null;
            int used = 0;
            stale.Clear();
            foreach (var pair in tracked) if (pair.Key == null || !pair.Key.IsSpawned) stale.Add(pair.Key);
            foreach (var gone in stale) Forget(gone);

            foreach (var player in NetworkPlayer.All)
            {
                if (player == null || !player.IsSpawned || player == local) continue;
                if (!tracked.TryGetValue(player, out var entry))
                {
                    // 처음 본 부상 기록은 과거 일이므로 표시하지 않는다(늦게 들어옴·장소 전환).
                    tracked[player] = entry = new Tracked { Mark = player.InjuryMark };
                }
                ObserveInjury(player, entry);
                float remaining = -1f;
                var kind = active ? Danger(player, entry, settings, out remaining) : TeammateDangerKind.None;
                if (kind == TeammateDangerKind.None) { DropOutline(entry); continue; }
                if (EnsureHud(settings) == null) { DropOutline(entry); continue; }
                Present(player, entry, kind, remaining, settings, view, ref used);
            }
            if (hud != null) hud.HideFrom(used);
        }

        void ObserveInjury(NetworkPlayer player, Tracked entry)
        {
            int mark = player.InjuryMark;
            if (mark == entry.Mark) return;
            entry.Mark = mark;
            entry.InjuredAt = Time.time;
            entry.InjuryKind = NetworkPlayer.InjuryOrigin(mark) == NetworkPlayer.DeathOrigin.Fall ? TeammateDangerKind.Fall : TeammateDangerKind.Cut;
        }

        TeammateDangerKind Danger(NetworkPlayer player, Tracked entry, WorldUiSettings settings, out float remaining)
        {
            remaining = -1f;
            if (player.State != PlayerState.Normal) return TeammateDangerKind.None; // 사망·리타이어·도착은 도울 수 없음
            var hangs = TightropeHangSystem.Current;
            if (hangs != null && hangs.IsHanging(player))
            {
                remaining = 1f - hangs.HangDangerRatio(player);
                return TeammateDangerKind.Hanging;
            }
            float seconds = settings != null ? Mathf.Max(.5f, settings.dangerInjurySeconds) : 6f;
            float elapsed = Time.time - entry.InjuredAt;
            if (entry.InjuryKind != TeammateDangerKind.None && elapsed >= 0f && elapsed < seconds)
            {
                remaining = 1f - elapsed / seconds;
                return entry.InjuryKind;
            }
            return TeammateDangerKind.None;
        }

        void Present(NetworkPlayer player, Tracked entry, TeammateDangerKind kind, float remaining, WorldUiSettings settings, Camera view, ref int used)
        {
            Color color = kind switch
            {
                TeammateDangerKind.Hanging => settings.dangerHangColor,
                TeammateDangerKind.Fall => settings.dangerFallColor,
                _ => settings.dangerCutColor,
            };
            Sprite sprite = kind switch
            {
                TeammateDangerKind.Hanging => settings.dangerHangIcon,
                TeammateDangerKind.Fall => settings.dangerFallIcon,
                _ => settings.dangerCutIcon,
            };
            float alpha = remaining >= 0f && remaining < settings.dangerUrgentRatio
                ? .55f + .45f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * Mathf.PI * 2.5f)) : 1f;
            // 매달린 사람은 기준 위치가 줄 잡은 곳이라 실제 보이는 몸(모델) 범위로 머리·가슴·발끝을 잡는다.
            var bounds = BodyBounds(player, entry);
            Vector3 chest = bounds.center;
            Vector3 head = new(bounds.center.x, bounds.max.y + settings.dangerHeadOffset, bounds.center.z);

            var viewport = view.WorldToViewportPoint(chest);
            float inset = settings.dangerOnScreenInset;
            bool onScreen = viewport.z > 0f && viewport.x > inset && viewport.x < 1f - inset && viewport.y > inset && viewport.y < 1f - inset;
            var area = hud.Area;
            var badge = hud.Badge(used++);
            if (badge == null) return;
            if (onScreen)
            {
                // 화면 안: 벽 너머에서도 보이는 몸 테두리(남은 시간만큼 선명) + 머리 위 아이콘(테두리 시간 표시는 몸이 맡는다).
                entry.Outline ??= new SilhouetteOutline(player.transform, color, settings.dangerOutlinePixels, priority: 30, xray: true);
                var outlineColor = color; outlineColor.a *= alpha;
                entry.Outline.Color = outlineColor;
                entry.Outline.Pixels = settings.dangerOutlinePixels;
                entry.Outline.Timer = remaining;
                entry.Outline.TimerBottom = bounds.min.y;
                entry.Outline.TimerHeight = bounds.size.y;
                var headScreen = view.WorldToScreenPoint(head);
                badge.Show(ToArea(area, headScreen), settings.dangerHeadSize, sprite, color, -1f, null, alpha);
            }
            else
            {
                DropOutline(entry);
                var screen = view.WorldToScreenPoint(chest);
                float scale = Scale(area);
                var place = EdgePlacement(screen, new Vector2(Screen.width, Screen.height), settings.dangerEdgeMargin * scale, settings.dangerEdgeBottomMargin * scale);
                badge.Show(ToArea(area, place.position), settings.dangerEdgeSize, sprite, color, remaining, place.degrees, alpha);
            }
        }

        /// <summary>
        /// 화면 밖(또는 카메라 뒤) 대상의 가장자리 자리와 화살촉 각도(위 = 0도, 반시계 +).
        /// 화면 중심에서 대상 쪽으로 직선을 그어 여백만큼 안쪽 사각형(아래는 아래 여백)과 만나는 점에 둔다.
        /// </summary>
        public static (Vector2 position, float degrees) EdgePlacement(Vector3 screenPoint, Vector2 screen, float margin, float bottomMargin = -1f)
        {
            if (bottomMargin < 0f) bottomMargin = margin;
            var center = screen * .5f;
            var dir = new Vector2(screenPoint.x, screenPoint.y) - center;
            if (screenPoint.z < 0f) dir = -dir; // 카메라 뒤: 투영이 뒤집히므로 반대쪽
            if (dir.sqrMagnitude < 1e-4f) dir = Vector2.down;
            float right = Mathf.Max(center.x + 1f, screen.x - margin), left = Mathf.Min(center.x - 1f, margin);
            float top = Mathf.Max(center.y + 1f, screen.y - margin), bottom = Mathf.Min(center.y - 1f, bottomMargin);
            float sx = Mathf.Abs(dir.x) > 1e-4f ? ((dir.x > 0f ? right : left) - center.x) / dir.x : float.MaxValue;
            float sy = Mathf.Abs(dir.y) > 1e-4f ? ((dir.y > 0f ? top : bottom) - center.y) / dir.y : float.MaxValue;
            var position = center + dir * Mathf.Min(sx, sy);
            float degrees = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
            return (position, degrees);
        }

        /// <summary>
        /// 지금 자세의 몸 범위. 사람형 뼈(머리·발·엉덩이)가 있으면 그것으로 잡는다(매달림 자세에서는 스킨 렌더러 범위가 자세를 따라가지 않음).
        /// 뼈가 없으면 캐릭터 스킨 렌더러(없으면 메시) 범위. 0.5초마다 목록을 다시 모은다.
        /// </summary>
        static Bounds BodyBounds(NetworkPlayer player, Tracked entry)
        {
            if (entry.Body == null || Time.unscaledTime >= entry.BodyRefreshAt)
            {
                var list = new List<Renderer>(player.GetComponentsInChildren<SkinnedMeshRenderer>());
                if (list.Count == 0) list.AddRange(player.GetComponentsInChildren<MeshRenderer>());
                entry.Body = list.ToArray();
                entry.Rig = null;
                foreach (var animator in player.GetComponentsInChildren<Animator>())
                    if (animator != null && animator.isHuman && animator.GetBoneTransform(HumanBodyBones.Head) != null) { entry.Rig = animator; break; }
                entry.BodyRefreshAt = Time.unscaledTime + .5f;
            }
            if (entry.Rig != null && entry.Rig.isActiveAndEnabled)
            {
                var headBone = entry.Rig.GetBoneTransform(HumanBodyBones.Head);
                var hips = entry.Rig.GetBoneTransform(HumanBodyBones.Hips);
                var footL = entry.Rig.GetBoneTransform(HumanBodyBones.LeftFoot);
                var footR = entry.Rig.GetBoneTransform(HumanBodyBones.RightFoot);
                // 잃은 다리는 숨겨져도 뼈는 남는다. 발이 없으면 엉덩이 아래 0.9m로 잡는다.
                float bottom = footL != null && footR != null ? Mathf.Min(footL.position.y, footR.position.y) - .08f
                    : hips != null ? hips.position.y - .9f : player.transform.position.y;
                float top = headBone.position.y + .22f;
                var mid = hips != null ? hips.position : headBone.position;
                return new Bounds(new Vector3(mid.x, (top + bottom) * .5f, mid.z), new Vector3(.6f, Mathf.Max(.3f, top - bottom), .6f));
            }
            bool any = false; var bounds = new Bounds(player.transform.position + Vector3.up * .9f, new Vector3(.5f, 1.8f, .5f));
            foreach (var renderer in entry.Body)
            {
                if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
                if (!any) { bounds = renderer.bounds; any = true; } else bounds.Encapsulate(renderer.bounds);
            }
            // 너무 큰 부속(로프 등)이 섞여도 사람 크기를 넘지 않게 제한한다.
            if (bounds.size.y > 3f) bounds = new Bounds(new Vector3(bounds.center.x, bounds.min.y + .9f, bounds.center.z), new Vector3(bounds.size.x, 1.8f, bounds.size.z));
            return bounds;
        }

        static Vector2 ToArea(RectTransform area, Vector2 screen)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(area, screen, null, out var local);
            return local;
        }

        static float Scale(RectTransform area)
        {
            var canvas = area.GetComponentInParent<Canvas>();
            return canvas != null ? canvas.scaleFactor : 1f;
        }

        TeammateDangerHudView EnsureHud(WorldUiSettings settings)
        {
            if (hud != null && hudSource == settings.teammateDangerHud) return hud;
            if (hud != null) Destroy(hud.gameObject);
            hud = null;
            hudSource = settings.teammateDangerHud;
            if (hudSource == null) return null;
            var go = Instantiate(hudSource, transform, false);
            hud = go.GetComponent<TeammateDangerHudView>();
            if (hud == null) { Destroy(go); return null; }
            return hud;
        }

        static void DropOutline(Tracked entry)
        {
            entry.Outline?.Dispose();
            entry.Outline = null;
        }

        void Forget(NetworkPlayer player)
        {
            // 파괴된 플레이어도 같은 참조로 찾을 수 있다.
            if (tracked.TryGetValue(player, out var entry)) DropOutline(entry);
            tracked.Remove(player);
        }

        void OnDestroy()
        {
            foreach (var entry in tracked.Values) DropOutline(entry);
            tracked.Clear();
            if (Instance == this) Instance = null;
        }
    }
}
