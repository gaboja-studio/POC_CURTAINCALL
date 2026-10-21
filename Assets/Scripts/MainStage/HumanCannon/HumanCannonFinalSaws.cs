using System;
using System.Collections.Generic;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Network.Session;
using CurtainCall.Player;
using CurtainCall.Tightrope.Saws;
using CurtainCall.Settings;
using UnityEngine;

namespace CurtainCall.HumanCannon
{
    /// <summary>최종 비행 구간. 최초 진입 시각을 공유하며 후발 발사는 주기를 바꾸지 않는다.</summary>
    [DefaultExecutionOrder(-20)]
    public sealed partial class HumanCannonFinalSaws : MonoBehaviour
    {
        [SerializeField] BoxCollider entryZone;
        [SerializeField] Transform[] horizontalOrigins = Array.Empty<Transform>();
        [SerializeField] Transform[] horizontalViews = Array.Empty<Transform>();
        [SerializeField] Transform verticalStart, verticalEnd, verticalView, warningView;
        [SerializeField] Transform secondaryVerticalView, secondaryWarningView;
        [Header("인간 대포 전용 임시 조정값 — 외줄 밸런스와 별개")]
        [Min(0.1f), SerializeField] float warningSeconds = 2.5f;
        [Min(0.1f), SerializeField] float travelSeconds = 4f;
        [Min(0.1f), SerializeField] float horizontalSpeed = 2f;
        [Min(0.1f), SerializeField] float horizontalHalfWidth = 8f;
        [Min(0.1f), SerializeField] float horizontalRadius = 1f;
        [Min(0.1f), SerializeField] float verticalRadius = 1.2f;
        [Min(0.01f), SerializeField] float halfThickness = 0.1f;
        [SerializeField] float[] verticalLanes = { -6, -2, 2, 6 };
        [Tooltip("예고 후 오는 두 톱도 바닥과 평행하게 눕혀 서로 다른 앞뒤 위치에서 좌우 왕복한다.")]
        [SerializeField] bool lateralPasses;

        readonly FinalSawTimeline timeline = new();
        readonly HashSet<(int, ulong)> touching = new();
        readonly HashSet<(int, ulong)> touchingNow = new();
        readonly Dictionary<ulong, Vector3> previousFeet = new();
        readonly List<ulong> stalePlayers = new();
        readonly HashSet<ulong> restartEntranceBlocked = new();
        readonly RouteResetBarrier resetBarrier = new();
        NetworkSessionManager session;
        uint revision;
        double startedAt = -1, nextSnapshot, previousClock = -1;
        MaterialPropertyBlock tint;
        Renderer[][] horizontalRenderers;
        Renderer[] warningRenderers, secondaryWarningRenderers;
        public static HumanCannonFinalSaws Current { get; private set; }
        public bool HasStarted => startedAt >= 0 && !resetBarrier.Waiting;
        public double StartedAt => startedAt;
        public double Clock => session != null && session.NetworkManager != null && session.NetworkManager.IsListening
            ? session.NetworkManager.ServerTime.Time : Time.timeAsDouble;
        public FinalSawSample Sample => FinalSawTimeline.Sample(HasStarted, startedAt, Clock, warningSeconds, travelSeconds);
        public bool HorizontalActive => HasStarted && Clock >= startedAt + warningSeconds;
        public bool VerticalActive => Sample.Phase == FinalSawPhase.Active;
        public int HorizontalCount => horizontalOrigins.Length;
        public int VerticalCount => secondaryVerticalView != null ? 2 : 1;
        public Vector3 PassNormal => lateralPasses ? transform.up : transform.right;
        public string StatusText => !HasStarted ? "최종 톱: 첫 진입 대기"
            : Sample.Phase == FinalSawPhase.Warning ? $"왕복 톱 예고 {Sample.SecondsToNext:F1}초"
            : $"왕복 톱 통과 중 · 퇴장까지 {Sample.SecondsToNext:F1}초";

        void OnEnable()
        {
            if (Current != null && Current != this) { enabled = false; return; }
            Current = this;
            horizontalRenderers = new Renderer[horizontalViews.Length][];
            for (int i = 0; i < horizontalViews.Length; i++)
                horizontalRenderers[i] = horizontalViews[i] != null ? horizontalViews[i].GetComponentsInChildren<Renderer>(true) : Array.Empty<Renderer>();
            warningRenderers = warningView != null ? warningView.GetComponentsInChildren<Renderer>(true) : Array.Empty<Renderer>();
            secondaryWarningRenderers = secondaryWarningView != null ? secondaryWarningView.GetComponentsInChildren<Renderer>(true) : Array.Empty<Renderer>();
            NetworkPlayer.Restarted += Restart;
        }

        void Update()
        {
            if (session == null && NetworkSessionManager.Instance != null)
            {
                session = NetworkSessionManager.Instance;
                session.ConnectionStateChanged += Connection;
                Connection(session.ConnectionState);
            }
            if (session == null || !session.IsHost) return;
            if (!HasStarted)
                foreach (var player in NetworkPlayer.All)
                    if (player != null && player.IsSpawned && player.State == PlayerState.Normal
                        && TryStartFromEntrance(player.OwnerClientId, player.transform.position)) break;
            if (Clock >= nextSnapshot) { nextSnapshot = Clock + 0.5; Publish(); }
        }

        public bool IsInEntrance(Vector3 feet) => entryZone != null && entryZone.bounds.Contains(feet + Vector3.up * 0.2f);

        void RememberRemoteEntranceAtRestart(ulong actor, bool isLocal, Vector3 feet)
        {
            // 원격 재시작은 소유자의 순간이동이 도착할 때까지 호스트에 옛 위치가 남는다.
            if (!isLocal && IsInEntrance(feet)) restartEntranceBlocked.Add(actor);
        }

        bool TryStartFromEntrance(ulong actor, Vector3 feet)
        {
            if (!IsInEntrance(feet))
            {
                restartEntranceBlocked.Remove(actor);
                return false;
            }
            if (restartEntranceBlocked.Contains(actor) || !timeline.TryStart(Clock)) return false;
            startedAt = timeline.StartedAt;
            revision++;
            Publish();
            return true;
        }

        public Vector3 HorizontalPosition(int index, double now)
        {
            float elapsed = HasStarted ? Mathf.Max(0, (float)(now - startedAt - warningSeconds)) : 0;
            float side = SawMath.PingPongSide(horizontalSpeed, horizontalHalfWidth, index % 2 == 0, elapsed);
            return horizontalOrigins[index].position + DiagonalOffset(side, index);
        }

        Vector3 DiagonalOffset(float side, int index) => transform.right * side + transform.forward
            * (side * TrickSettings.DiagonalSlope(index, GameSettings.HumanCannon.SawDiagonalDegrees));

        public int TotalSawCount => horizontalOrigins.Length + (verticalView != null ? 1 : 0) + (secondaryVerticalView != null ? 1 : 0);
        public bool IsSawEnabled(int index) => TrickSettings.IncludesSaw(index, TotalSawCount, GameSettings.HumanCannon.SawDensity);

        public Vector3 VerticalPosition(double now) => VerticalPositionAt(now, 0);
        public Vector3 SecondaryVerticalPosition(double now) => VerticalPositionAt(now, 2);

        Vector3 VerticalPositionAt(double now, int laneOffset)
        {
            var sample = FinalSawTimeline.Sample(HasStarted, startedAt, now, warningSeconds, travelSeconds);
            if (lateralPasses)
            {
                // 한 예고/통과 주기마다 출발 방향을 바꿔 좌우 끝을 번갈아 잇는다.
                bool fromLeft = (Math.Max(0, sample.CycleIndex) + laneOffset / 2) % 2 == 0;
                float side = Mathf.Lerp(-horizontalHalfWidth, horizontalHalfWidth,
                    fromLeft ? sample.Travel01 : 1f - sample.Travel01);
                Vector3 center = Vector3.Lerp(verticalStart.position, verticalEnd.position, laneOffset == 0 ? .35f : .65f);
                return center + DiagonalOffset(side, horizontalOrigins.Length + laneOffset / 2);
            }
            // 두 번째 톱은 같은 예고/통과 시각을 쓰고 네 발사 레인 중 두 칸 건너 위치한다.
            float lane = verticalLanes.Length > 0 && sample.CycleIndex >= 0
                ? verticalLanes[(int)((sample.CycleIndex % verticalLanes.Length + laneOffset) % verticalLanes.Length)] : 0;
            Vector3 position = Vector3.Lerp(verticalStart.position, verticalEnd.position, sample.Travel01) + transform.right * lane;
            float along = Vector3.Dot(position - verticalEnd.position, transform.forward);
            return position + transform.right * (along * TrickSettings.DiagonalSlope(horizontalOrigins.Length + laneOffset / 2,
                GameSettings.HumanCannon.SawDiagonalDegrees));
        }

        void LateUpdate()
        {
            double now = Clock;
            var sample = Sample;
            float spin = (float)(now * 180 % 360);
            Color warning = Mathf.Repeat((float)now, 0.5f) < 0.25f ? Color.yellow : Color.red;
            for (int i = 0; i < horizontalViews.Length && i < horizontalOrigins.Length; i++)
            {
                if (horizontalViews[i] == null || horizontalOrigins[i] == null) continue;
                horizontalViews[i].gameObject.SetActive(HasStarted && IsSawEnabled(i));
                horizontalViews[i].SetPositionAndRotation(HorizontalPosition(i, now), transform.rotation * Quaternion.Euler(0, spin, 0));
                if (HorizontalActive)
                {
                    foreach (var renderer in horizontalRenderers[i]) if (renderer != null) renderer.SetPropertyBlock(null);
                }
                else Tint(horizontalRenderers[i], warning);
            }
            if (verticalStart != null && verticalEnd != null)
            {
                PresentVertical(verticalView, warningView, warningRenderers, VerticalPosition(now),
                    IsSawEnabled(horizontalOrigins.Length) ? sample.Phase : FinalSawPhase.Dormant, spin, warning);
                if (secondaryVerticalView != null)
                    PresentVertical(secondaryVerticalView, secondaryWarningView, secondaryWarningRenderers,
                        SecondaryVerticalPosition(now), IsSawEnabled(horizontalOrigins.Length + 1) ? sample.Phase : FinalSawPhase.Dormant, spin, warning);
            }
            if (session != null && session.IsHost) HostContacts(now);
            previousClock = now;
        }

        void PresentVertical(Transform blade, Transform marker, Renderer[] renderers, Vector3 position,
            FinalSawPhase phase, float spin, Color warning)
        {
            Quaternion orientation = transform.rotation * (lateralPasses ? Quaternion.identity : Quaternion.Euler(0, 0, 90));
            if (blade != null)
            {
                blade.gameObject.SetActive(phase == FinalSawPhase.Active);
                blade.SetPositionAndRotation(position, orientation * Quaternion.Euler(0, spin, 0));
            }
            if (marker == null) return;
            marker.gameObject.SetActive(phase == FinalSawPhase.Warning);
            marker.SetPositionAndRotation(position, orientation);
            Tint(renderers, warning);
        }

        void Tint(Renderer[] renderers, Color color)
        {
            tint ??= new MaterialPropertyBlock();
            tint.SetColor("_BaseColor", color); tint.SetColor("_Color", color);
            foreach (var renderer in renderers) if (renderer != null) renderer.SetPropertyBlock(tint);
        }

        void ResetSaws()
        {
            timeline.Reset(); startedAt = -1; previousClock = -1;
            touching.Clear(); touchingNow.Clear(); previousFeet.Clear();
            restartEntranceBlocked.Clear();
        }

        void OnDisable()
        {
            NetworkPlayer.Restarted -= Restart;
            if (session != null) session.ConnectionStateChanged -= Connection;
            Unregister(); session = null;
            ResetSaws(); resetBarrier.Clear();
            if (Current == this) Current = null;
        }
    }
}
