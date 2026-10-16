using System.Collections.Generic;
using System.Runtime.CompilerServices;
using CurtainCall.Cameras;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Player;
using UnityEngine;

namespace CurtainCall.HumanCannon
{
    public sealed partial class HumanCannonPilot
    {
        CameraViewDirector cannonView;
        CameraViewPreset seatedView, flightView;
        bool requestedCannonView;

        void UpdateCannonView()
        {
            var current = CameraViewDirector.Current;
            if (cannonView != current)
            {
                ReleaseCannonView();
                cannonView = current;
                if (cannonView != null) cannonView.PreparingView += RefreshCannonView;
            }
            RefreshCannonView();
        }

        void RefreshCannonView()
        {
            if (cannonView == null) return;
            if (!isActiveAndEnabled || system == null || player == null || player.State != PlayerState.Normal
                || cannonView.LocalPlayer != player)
            {
                ClearCannonViewRequest();
                return;
            }
            if (seatedView == null)
            {
                var first = cannonView.Settings != null ? cannonView.Settings.FirstPerson : null;
                var free = Resources.Load<CameraViewPreset>("Settings/Camera/FreeThirdPerson");
                if (first == null || free == null) return;
                seatedView = Instantiate(free);
                flightView = Instantiate(first);
                seatedView.hideFlags = flightView.hideFlags = HideFlags.DontSave;
                seatedView.TransitionDuration = 0.18f;
                flightView.TransitionDuration = 0f; // 발사 시작 프레임부터 눈 위치에서 비행한다.
            }
            // 원본/연습 모두 확정된 손잡이 소유권으로 전환한다. 시선은 몸/장치 회전에 쓰지 않는다.
            var trampoline = HumanCannonTrampoline.Current;
            bool carrying = trampoline != null && trampoline.Carrier == player.OwnerClientId;
            bool thirdPerson = stationIndex >= 0 || (!IsFlying && carrying);
            // 탑승/비행이 운반보다 우선하며, 사망자는 위에서 요청을 해제해 공용 관전을 사용한다.
            cannonView.RequestView(this, thirdPerson ? seatedView : flightView,
                stationIndex >= 0 || IsFlying ? 200 : carrying ? 100 : 50);
            HumanCannonViewInput.SetRequested(cannonView, this, true);
            requestedCannonView = true;
        }

        void ClearCannonViewRequest()
        {
            if (cannonView == null || !requestedCannonView) return;
            cannonView.ReleaseView(this);
            HumanCannonViewInput.SetRequested(cannonView, this, false);
            requestedCannonView = false;
        }

        void ReleaseCannonView()
        {
            ClearCannonViewRequest();
            if (cannonView != null) cannonView.PreparingView -= RefreshCannonView;
            cannonView = null;
            requestedCannonView = false;
        }

        void OnDisable() => ReleaseCannonView();

        void DestroyCannonView()
        {
            ReleaseCannonView();
            if (seatedView != null) Destroy(seatedView);
            if (flightView != null) Destroy(flightView);
            seatedView = flightView = null;
        }
    }

    // 코스 교체 중 이전 pilot 정리가 새 pilot의 메뉴 입력 차단을 풀지 않는다.
    static class HumanCannonViewInput
    {
        static readonly ConditionalWeakTable<CameraViewDirector, HashSet<MonoBehaviour>> sources = new();

        internal static void SetRequested(CameraViewDirector director, MonoBehaviour source, bool requested)
        {
            var active = sources.GetOrCreateValue(director);
            if (requested) active.Add(source);
            else active.Remove(source);
            active.RemoveWhere(value => value == null || !value.isActiveAndEnabled);
            director.SetInputBlocked(active.Count > 0 && InputGate.IsBlocked);
        }
    }
}
