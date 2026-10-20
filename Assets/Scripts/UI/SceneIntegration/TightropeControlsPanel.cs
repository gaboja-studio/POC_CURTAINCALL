using System.Collections.Generic;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Player;
using CurtainCall.Tightrope;
using CurtainCall.Tightrope.Saws;
using CurtainCall.Tightrope.UI;
using CurtainCall.UI.NotificationsV1;
using UnityEngine;
namespace CurtainCall.UI.SceneIntegration
{
    public sealed class TightropeControlsPanel : MonoBehaviour
    {
        public SystemBannerView banner;
        [SerializeField] float holdSeconds = 4f;
        [SerializeField] float sawDistance = 8f;
        readonly HashSet<int> warnedSaws = new();
        NetworkPlayer observed;
        bool approachShown, balanceShown;
        void OnEnable() => TightropeControlHints.ExternalPresentation = true;
        void LateUpdate()
        {
            var local = NetworkPlayer.Local;
            if (observed != local)
            {
                observed = local; approachShown = balanceShown = false; warnedSaws.Clear();
                banner.HideImmediate();
            }
            var course = TightropeCourse.Current;
            if (local == null || course == null || !course.IsTraversalActive || local.State != PlayerState.Normal
                || InputGate.IsBlocked || SceneCurtainTransition.IsActive)
            { if (banner.IsVisible) banner.HideImmediate(); return; }
            var mover = local.GetComponent<PlayerMover>();
            if (mover == null || !mover.ControlEnabled) return;
            if (mover.FreeMovement)
            {
                if (!approachShown) approachShown = banner.Show("WASD로 줄에 올라가세요", "줄 위로 걸어가면 자동으로 올라탑니다", BannerState.Normal, holdSeconds);
                return;
            }
            if (!balanceShown)
            {
                balanceShown = banner.Show("W/S로 이동하고 A/D로 중심을 잡아요", "기울어지는 반대 방향으로 균형을 잡으세요", BannerState.Normal, holdSeconds);
                return;
            }
            var saws = TightropeSaws.Current;
            if (saws == null || banner.IsVisible) return;
            for (int i = 0; i < saws.HorizontalCount; i++)
            {
                if (warnedSaws.Contains(i) || !saws.IsHorizontalMoving(i) || !saws.TryGetHorizontalPosition(i, out var position)) continue;
                if (Vector3.Distance(local.transform.position, position) > sawDistance) continue;
                if (banner.Show("SPACE로 점프해서 톱날을 피해요", "다가오는 톱날에 닿지 않도록 주의하세요", BannerState.Danger, holdSeconds)) warnedSaws.Add(i);
                break;
            }
        }
        void OnDisable()
        {
            TightropeControlHints.ExternalPresentation = false;
            if (banner != null) banner.HideImmediate();
        }
    }
}
