using System.Collections.Generic;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Player;
using UnityEngine;

namespace CurtainCall.WaitingRoom
{
    /// <summary>상점 F 상호작용 대상(수화기·배송구 물건).</summary>
    public interface IShopTarget
    {
        /// <summary>내 발 위치에서 닿는 거리. 닿지 않으면 음수.</summary>
        float Reach(Vector3 feet);

        /// <summary>F를 짧게 눌렀다(가장 가까운 대상 하나만 받는다).</summary>
        void Interact();
    }

    /// <summary>
    /// 내 캐릭터 기준 상점 상호작용 선택. 수화기 바로 아래에 배송구가 있어 둘 다 닿으므로, 가장 가까운 대상 하나에만 F를 준다.
    /// 접속 중이면 <see cref="NetworkPlayer.Local"/>, 접속하지 않은 씬 확인에서는 씬에 놓인 플레이어를 쓴다.
    /// </summary>
    public static class ShopReach
    {
        static readonly List<IShopTarget> targets = new();
        static PlayerInputReader offlineReader;
        static int pumpedFrame = -1;

        /// <summary>이번 프레임에 F 안내를 띄울 대상(없으면 null).</summary>
        public static IShopTarget Focused { get; private set; }

        public static void Add(IShopTarget target)
        {
            if (!targets.Contains(target)) targets.Add(target);
        }

        public static void Remove(IShopTarget target)
        {
            targets.Remove(target);
            if (Focused == target) Focused = null;
        }

        /// <summary>대상들의 Update에서 부른다. 프레임마다 한 번만 고르고 F를 전달한다.</summary>
        public static void Pump()
        {
            if (pumpedFrame == Time.frameCount) return;
            pumpedFrame = Time.frameCount;
            Focused = null;

            var reader = LocalReader();
            if (reader == null || !reader.isActiveAndEnabled || InputGate.IsBlocked) return;

            Vector3 feet = reader.transform.position;
            float best = float.MaxValue;
            foreach (var target in targets)
            {
                float distance = target.Reach(feet);
                if (distance < 0f || distance >= best) continue;
                best = distance;
                Focused = target;
            }
            if (Focused != null && reader.Current.InteractTapped) Focused.Interact();
        }

        /// <summary>내 캐릭터 발 위치.</summary>
        public static bool TryLocalFeet(out Vector3 feet)
        {
            var reader = LocalReader();
            feet = reader != null ? reader.transform.position : default;
            return reader != null;
        }

        static PlayerInputReader LocalReader()
        {
            if (NetworkPlayer.All.Count > 0)
                return NetworkPlayer.Local != null ? NetworkPlayer.Local.GetComponent<PlayerInputReader>() : null;
            if (offlineReader == null) offlineReader = Object.FindFirstObjectByType<PlayerInputReader>();
            return offlineReader;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            targets.Clear();
            offlineReader = null;
            pumpedFrame = -1;
            Focused = null;
        }
    }
}
