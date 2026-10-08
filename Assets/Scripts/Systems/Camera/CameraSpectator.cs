using System.Collections.Generic;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Player;
using Unity.Cinemachine;
using UnityEngine;

namespace CurtainCall.Cameras
{
    /// <summary>
    /// 관전(2026-10-04 PM @MoHoDu): 내 캐릭터가 추락(사망)하면 카메라가 살아 있는 플레이어를 따라가고, 화면에 "OO를 관전 중입니다"를 띄운다.
    /// A/D로 살아 있는 플레이어를 차례로 바꾼다. 보고 있던 사람이 죽으면 다음 사람으로, 아무도 없으면 그대로 둔다.
    /// 재시작 등으로 내 캐릭터가 다시 살아나면 내 캐릭터로 돌아오고 관전 표시를 지운다. 관전 중에는 가림 반투명(<see cref="CameraOcclusionFade"/>)을 끈다.
    /// 사망 중에는 화면 좌우 가장자리에 옅은 붉은 딤을 깔아 사망 상태를 알린다(2026-10-04 PM, 방해되지 않게 낮은 투명도).
    /// 표시는 임시(정식 UI는 016). 카메라 리그 프리팹(PlayerFollowCamera)에 둔다.
    /// </summary>
    [RequireComponent(typeof(CinemachineCamera))]
    public sealed class CameraSpectator : MonoBehaviour
    {
        /// <summary>내 화면이 관전 중인지.</summary>
        public static bool IsSpectating { get; private set; }

        /// <summary>관전 중인 플레이어. 관전 중이 아니거나 볼 사람이 없으면 null.</summary>
        public static NetworkPlayer Target { get; private set; }

        CinemachineCamera virtualCamera;
        int lastDirection; // 지난 프레임 A/D 입력(-1/0/+1). 누른 순간에만 바꾼다
        readonly List<NetworkPlayer> alive = new();
        GUIStyle labelStyle;

        [Header("사망 딤 (임시 표시)")]
        [Tooltip("좌우 가장자리 딤 색. 알파가 가장자리 최대 진하기(낮게 둘수록 덜 방해된다).")]
        [SerializeField] Color dimColor = new(0.55f, 0f, 0f, 0.28f);
        [Tooltip("딤이 덮는 폭(화면 너비 비율, 한쪽).")]
        [SerializeField, Range(0.05f, 0.5f)] float dimWidth = 0.22f;
        [Tooltip("딤이 나타나는 시간(초).")]
        [SerializeField, Min(0f)] float dimFadeIn = 0.4f;

        Texture2D dimTexture; // 가장자리(왼쪽 0)에서 안쪽으로 옅어지는 가로 그라데이션
        float spectateStartedAt;

        void Awake() => virtualCamera = GetComponent<CinemachineCamera>();

        void OnDisable() => Stop(NetworkPlayer.Local);

        void LateUpdate()
        {
            var local = NetworkPlayer.Local;
            bool dead = local != null && (local.State == PlayerState.Fallen
                                          || (local.TryGetComponent(out PlayerBalance balance) && balance.HasFallen));
            if (!dead)
            {
                Stop(local);
                return;
            }

            if (!IsSpectating) spectateStartedAt = Time.time;
            IsSpectating = true;
            CollectAlive(local);
            if (Target == null || !alive.Contains(Target)) SetTarget(alive.Count > 0 ? alive[0] : null);

            int direction = ReadDirection(local);
            if (direction != 0 && direction != lastDirection && alive.Count > 1)
            {
                int index = Mathf.Max(0, alive.IndexOf(Target));
                SetTarget(alive[(index + direction + alive.Count) % alive.Count]);
            }
            lastDirection = direction;
        }

        void CollectAlive(NetworkPlayer local)
        {
            alive.Clear();
            foreach (var player in NetworkPlayer.All)
                if (player != null && player != local && player.State == PlayerState.Normal) alive.Add(player);
            alive.Sort((a, b) => a.Slot.CompareTo(b.Slot)); // 자리 번호 순으로 돈다
        }

        static int ReadDirection(NetworkPlayer local)
        {
            if (!local.TryGetComponent(out PlayerInputReader reader) || !reader.enabled) return 0;
            float posture = reader.Current.Posture; // A/D(균형 보정 키). 사망 중에는 균형에 쓰이지 않는다
            return posture > 0.5f ? 1 : posture < -0.5f ? -1 : 0;
        }

        void SetTarget(NetworkPlayer player)
        {
            Target = player;
            if (player != null) virtualCamera.Target.TrackingTarget = player.transform;
        }

        /// <summary>관전을 끝내고 카메라를 내 캐릭터로 돌린다.</summary>
        void Stop(NetworkPlayer local)
        {
            if (!IsSpectating) return;
            IsSpectating = false;
            Target = null;
            lastDirection = 0;
            if (local != null) virtualCamera.Target.TrackingTarget = local.transform;
        }

        void OnDestroy()
        {
            if (dimTexture != null) Destroy(dimTexture);
        }

        void OnGUI()
        {
            if (!IsSpectating) return;
            DrawDeathDim();
            labelStyle ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 22, richText = true };
            string text = Target != null
                ? $"<b>플레이어 {Target.Slot + 1}</b>를 관전 중입니다\n<size=15>A / D : 다른 플레이어 보기</size>"
                : "관전할 수 있는 플레이어가 없습니다";
            GUI.Label(new Rect(0f, 20f, Screen.width, 70f), text, labelStyle);
        }

        /// <summary>좌우 가장자리 붉은 딤. 가장자리에서 안쪽으로 옅어진다.</summary>
        void DrawDeathDim()
        {
            if (dimTexture == null)
            {
                const int size = 128;
                dimTexture = new Texture2D(size, 1, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
                for (int x = 0; x < size; x++)
                {
                    float t = 1f - x / (size - 1f);
                    dimTexture.SetPixel(x, 0, new Color(1f, 1f, 1f, t * t)); // 가장자리 쪽이 진하게
                }
                dimTexture.Apply();
            }

            float appear = dimFadeIn > 0f ? Mathf.Clamp01((Time.time - spectateStartedAt) / dimFadeIn) : 1f;
            var previous = GUI.color;
            GUI.color = new Color(dimColor.r, dimColor.g, dimColor.b, dimColor.a * appear);
            float width = Screen.width * dimWidth;
            GUI.DrawTexture(new Rect(0f, 0f, width, Screen.height), dimTexture);
            GUI.DrawTextureWithTexCoords(new Rect(Screen.width - width, 0f, width, Screen.height), dimTexture, new Rect(1f, 0f, -1f, 1f)); // 오른쪽은 좌우 뒤집기
            GUI.color = previous;
        }
    }
}
