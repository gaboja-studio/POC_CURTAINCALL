using System.Collections.Generic;
using CurtainCall.Dev;
using CurtainCall.Network.PlayerSync;
using UnityEngine;

namespace CurtainCall.HumanCannon
{
    /// <summary>
    /// Dev 치트 창(F9)의 인간 대포 순간이동 지점(Task-089): 각 대포 탑승 위치(코스 순서), 최종 목적지 착지판 위.
    /// 탈출로처럼 대포를 쓰는 다른 공간에서도 같은 지점이 나온다. 탑승·비행 판정은 바꾸지 않는다.
    /// </summary>
    static class HumanCannonDevTeleports
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register() => DevCheats.RegisterTeleportSource(Collect);

        static void Collect(NetworkPlayer local, List<DevCheats.TeleportPoint> points)
        {
            var system = HumanCannonSystem.Current;
            if (system != null && system.isActiveAndEnabled)
                for (int i = 0; i < system.StationCount; i++)
                {
                    var station = system.Station(i);
                    if (station == null || !station.isActiveAndEnabled) continue;
                    var boarding = station.BoardingPoint;
                    points.Add(new DevCheats.TeleportPoint($"대포 {i + 1} 탑승 위치", boarding.position, boarding.rotation));
                }
            var destination = HumanCannonDestination.Current;
            var zone = destination != null && destination.isActiveAndEnabled ? destination.Zone : null;
            if (zone == null) return;
            // 착지판 콜라이더 윗면 가운데. 콜라이더가 없으면 착지판 위치.
            var collider = zone.GetComponentInChildren<Collider>();
            Vector3 top = collider != null ? new Vector3(collider.bounds.center.x, collider.bounds.max.y, collider.bounds.center.z) : zone.transform.position;
            points.Add(new DevCheats.TeleportPoint("대포 · 최종 목적지", top, zone.transform.rotation));
        }
    }
}
