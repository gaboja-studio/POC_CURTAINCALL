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
        CameraViewPreset seatedView, flightView, carryingView;
        bool requestedCannonView;
        bool hadCarryFraming;
        int carryFrame = -1;

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
                || cannonView.LocalPlayer != player || (controller != null && controller.Scheme != scheme))
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
                carryingView = Instantiate(free);
                flightView = Instantiate(first);
                seatedView.hideFlags = flightView.hideFlags = carryingView.hideFlags = HideFlags.DontSave;
                seatedView.TransitionDuration = 0.18f;
                flightView.TransitionDuration = 0f; // 발사 시작 프레임부터 눈 위치에서 비행한다.
                carryingView.FieldOfView = 68f;
                carryingView.LookAhead = 0f;
                carryingView.TransitionDuration = 0.18f;
            }
            // 원본/연습 모두 확정된 손잡이 소유권으로 전환한다. 운반 중 수평 시선은 몸/장치 방향에도 적용한다.
            var carried = HumanCannonCarry.Held(player.OwnerClientId);
            bool carrying = carried != null;
            bool carryCamera = stationIndex < 0 && !IsFlying && carrying;
            if (carryCamera) UpdateCarryFraming(carried);
            else hadCarryFraming = false;
            // 탑승/비행이 운반보다 우선하며, 사망자는 위에서 요청을 해제해 공용 관전을 사용한다.
            cannonView.RequestView(this, stationIndex >= 0 ? seatedView : carryCamera ? carryingView : flightView,
                stationIndex >= 0 || IsFlying ? 200 : carrying ? 100 : 50);
            HumanCannonViewInput.SetRequested(cannonView, this, true);
            requestedCannonView = true;
        }

        void UpdateCarryFraming(IHumanCannonCarry carried)
        {
            // PreparingView는 한 프레임에 여러 번 불린다. 줌 보간은 한 번만 진행한다.
            bool advanceSmoothing = carryFrame != Time.frameCount;
            carryFrame = Time.frameCount;
            var feet = player.transform.position;
            var bounds = new Bounds(feet + Vector3.up, new Vector3(1.2f, 2.2f, 1.2f));
            var half = carried.CarryHalfSize;
            bounds.Encapsulate(new Bounds(carried.CarryFocus, new Vector3(half.x * 2f, 0.8f, half.y * 2f)));
            bounds.Encapsulate(carried.Handle.position);
            foreach (var other in NetworkPlayer.All)
            {
                if (other == null || other == player || !other.IsSpawned || other.State != PlayerState.Normal) continue;
                var offset = other.transform.position - carried.CarryFocus;
                if ((!system.IsFlying(other.OwnerClientId) && !other.IsAirborne) || offset.y < -2f || offset.y > 30f
                    || Vector3.ProjectOnPlane(offset, Vector3.up).sqrMagnitude > 18f * 18f) continue;
                float height = other.TryGetComponent(out PlayerMover body) ? body.BodyHeight : 2f;
                bounds.Encapsulate(new Bounds(other.transform.position + Vector3.up * (height * 0.5f),
                    new Vector3(1.2f, height + 0.4f, 1.2f)));
            }
            float targetHeight = Mathf.Max(1.4f, bounds.center.y - feet.y);
            float blend = advanceSmoothing ? 1f - Mathf.Exp(-Time.deltaTime * 5f) : 0f;
            carryingView.LookHeight = hadCarryFraming
                ? Mathf.Lerp(carryingView.LookHeight, targetHeight, blend) : targetHeight;
            var pivot = feet + Vector3.up * carryingView.LookHeight;
            float aspect = Camera.main != null ? Camera.main.aspect : Screen.width / (float)Mathf.Max(1, Screen.height);
            float distance = HumanCannonCarryFraming.FitDistance(bounds, pivot, carryingView.FieldOfView, aspect);
            // 빠르게 들어온 동료는 즉시 포함하고, 멀어졌을 때만 천천히 확대한다.
            carryingView.Distance = !hadCarryFraming || distance > carryingView.Distance ? distance
                : Mathf.Lerp(carryingView.Distance, distance, blend);
            float elevation = Mathf.Max(3.5f, carryingView.Distance * 0.45f);
            carryingView.Height = carryingView.LookHeight + elevation;
            // 아래쪽 공전의 최저점도 운반자 발보다 높은 pivot 아래로 내려가지 않는다.
            carryingView.PitchLimit = HumanCannonCarryFraming.SafePitchLimit(carryingView.Distance, elevation);
            hadCarryFraming = true;
        }

        void ClearCannonViewRequest()
        {
            if (cannonView == null || !requestedCannonView) return;
            cannonView.ReleaseView(this);
            HumanCannonViewInput.SetRequested(cannonView, this, false);
            requestedCannonView = false;
            hadCarryFraming = false;
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
            if (carryingView != null) Destroy(carryingView);
            seatedView = flightView = carryingView = null;
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
