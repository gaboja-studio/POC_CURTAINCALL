using CurtainCall.Network.PlayerSync;
using CurtainCall.Player;
using CurtainCall.Tightrope.Saws;
using UnityEngine;
using Unity.Netcode;

namespace CurtainCall.HumanCannon
{
    public sealed partial class HumanCannonFinalSaws
    {
        void HostContacts(double now)
        {
            if (HumanCannonDestination.Current != null && HumanCannonDestination.Current.IsResolved) return;
            touchingNow.Clear();
            foreach (var player in NetworkPlayer.All)
            {
                if (player != null && player.OwnerClientId != NetworkManager.ServerClientId) continue;
                if (player == null || !player.IsSpawned || player.State != PlayerState.Normal
                    || !player.TryGetComponent(out PlayerMover mover)) continue;
                Vector3 feet = player.transform.position;
                Vector3 before = previousFeet.TryGetValue(player.OwnerClientId, out var old) && Vector3.Distance(old, feet) < 4f ? old : feet;
                if (HorizontalActive)
                    for (int i = 0; i < horizontalOrigins.Length; i++)
                    {
                        if (horizontalOrigins[i] == null || !IsSawEnabled(i)) continue;
                        Vector3 center = HorizontalPosition(i, now);
                        Vector3 previous = previousClock >= startedAt + warningSeconds ? HorizontalPosition(i, previousClock) : center;
                        ApplyContact(player, mover, i, before, feet, previous, center, transform.up, horizontalRadius);
                    }
                if (VerticalActive && verticalStart != null && verticalEnd != null)
                {
                    var previousSample = FinalSawTimeline.Sample(HasStarted, startedAt, previousClock, warningSeconds, travelSeconds);
                    bool samePass = previousSample.Phase == FinalSawPhase.Active && previousSample.CycleIndex == Sample.CycleIndex;
                    Vector3 center = VerticalPosition(now);
                    if (IsSawEnabled(horizontalOrigins.Length))
                        ApplyContact(player, mover, horizontalOrigins.Length, before, feet,
                            samePass ? VerticalPosition(previousClock) : center, center, PassNormal, verticalRadius);
                    if (secondaryVerticalView != null && IsSawEnabled(horizontalOrigins.Length + 1))
                    {
                        center = SecondaryVerticalPosition(now);
                        ApplyContact(player, mover, horizontalOrigins.Length + 1, before, feet,
                            samePass ? SecondaryVerticalPosition(previousClock) : center, center, PassNormal, verticalRadius);
                    }
                }
                previousFeet[player.OwnerClientId] = feet;
            }
            touching.Clear(); touching.UnionWith(touchingNow);
            stalePlayers.Clear();
            foreach (var pair in previousFeet)
            {
                bool alive = false;
                foreach (var player in NetworkPlayer.All)
                    if (player != null && player.IsSpawned && player.State == PlayerState.Normal && player.OwnerClientId == pair.Key) { alive = true; break; }
                if (!alive) stalePlayers.Add(pair.Key);
            }
            foreach (ulong actor in stalePlayers) previousFeet.Remove(actor);
        }

        OwnerContactChannel ownerContacts;
        void OwnerContacts(double now)
        {
            ownerContacts ??= new OwnerContactChannel("CurtainCall.HumanCannon.FinalSaws.Contact.v2",
                () => TotalSawCount, _ => startedAt, ContactAt, (player, _) => player.ServerHazardCut());
            ownerContacts.Tick(session, now, HasStarted
                && (HumanCannonDestination.Current == null || !HumanCannonDestination.Current.IsResolved));
        }

        bool ContactAt(int index, double at, Vector3 feet, float height, float radius)
        {
            if (!HasStarted || !IsSawEnabled(index) || at < startedAt + warningSeconds) return false;
            Vector3 center, normal;
            float sawRadius;
            if (index < horizontalOrigins.Length) {
                if (horizontalOrigins[index] == null) return false;
                center = HorizontalPosition(index, at); normal = transform.up; sawRadius = horizontalRadius;
            } else {
                if (verticalStart == null || verticalEnd == null || FinalSawTimeline.Sample(true, startedAt, at,
                    warningSeconds, travelSeconds).Phase != FinalSawPhase.Active) return false;
                if (index == horizontalOrigins.Length && verticalView != null) center = VerticalPosition(at);
                else if (index == horizontalOrigins.Length + 1 && secondaryVerticalView != null) center = SecondaryVerticalPosition(at);
                else return false;
                normal = PassNormal; sawRadius = verticalRadius;
            }
            return SawMath.TryGetContact(new SawDisc(center, normal, sawRadius, halfThickness), new BodyCapsule(feet, height, radius), out _);
        }

        void ApplyContact(NetworkPlayer player, PlayerMover mover, int index, Vector3 before, Vector3 feet,
            Vector3 previousCenter, Vector3 center, Vector3 normal, float radius)
        {
            if (player.State != PlayerState.Normal) return;
            // 낮은 프레임에서도 비행과 톱 이동 사이를 검사한다. 순간이동은 이전 몸 위치를 쓰지 않는다.
            float distance = Vector3.Distance(before, feet) + Vector3.Distance(previousCenter, center);
            int steps = Mathf.Clamp(Mathf.CeilToInt(distance / 0.15f), 1, 32);
            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)steps;
                var disc = new SawDisc(Vector3.Lerp(previousCenter, center, t), normal, radius, halfThickness);
                var body = new BodyCapsule(Vector3.Lerp(before, feet, t), mover.BodyHeight, mover.BodyRadius);
                if (!SawMath.TryGetContact(disc, body, out _)) continue;
                var key = (index, player.OwnerClientId);
                touchingNow.Add(key);
                // 접촉마다 공통 순서로 한 부위만 자른다. 외줄의 부위별 절단 규칙은 가져오지 않는다.
                if (!touching.Contains(key)) player.ServerHazardCut();
                return;
            }
        }
    }
}
