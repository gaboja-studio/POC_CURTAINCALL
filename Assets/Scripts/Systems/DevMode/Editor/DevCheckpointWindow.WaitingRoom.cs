using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CurtainCall.Dev.EditorTools
{
    public sealed partial class DevCheckpointWindow
    {
        // One per named room or separately traversable area. Coordinates are on the approach
        // side of the actual threshold, so a locked door doesn't trap a revived player.
        static IEnumerable<(string name, Vector3 feet, Vector3 facing)> WaitingRoomEntrances(bool final)
        {
            Vector3 n = Vector3.forward, s = Vector3.back, w = Vector3.left, e = Vector3.right;
            yield return ("로비 입구", new(5.6f, 0, 83.8f), s);
            yield return ("휴게실 입구", new(2.5f, 0, 78.5f), n);
            yield return ("상점 입구", new(9.3f, 0, 91.95f), e);
            yield return ("숙소 입구", new(10.4f, 0, 72.55f), e);
            yield return ("화장실 입구", new(13.75f, 0, 69.1f), s);
            yield return ("소품실 입구", final ? new(-2.5f, 0, 59.35f) : new(-8.2f, 0, 94.75f), final ? e : n);
            yield return ("무대 입구 통로", final ? new(-9.7f, 0, 91.7f) : new(-6f, 0, 63.1f), final ? n : s);
            yield return ("공연 스테이지 입구", final ? new(-9.7f, 0, 162.9f) : new(-6f, 0, -8.15f), final ? n : s);
            yield return ("전문가 대기실 입구", new(10.3f, 0, 97.4f), e);
            yield return ("연습실 입구", new(-22.6f, 0, 90.9f), new(-1, 0, 1));
            yield return ("작업실 입구", final ? new(-4.3f, 0, 58.17f) : new(-13.38f, 0, 98.95f), final ? w : n);
            yield return ("화물 적재장 입구", new(-25.9f, 0, 68.65f), w);
            yield return ("무대 장치 보관실 입구", new(-29.1f, 0, 62.15f), s);
            yield return ("배관 구역 입구", new(16.05f, 0, 60.7f), s);
            yield return ("폐기물 보관소 입구", new(23.45f, 0, 46.75f), s);
            yield return ("계단 입구", new(5.25f, 0, 100.55f), n);
            yield return ("기계실 입구", new(2.3f, -5, 103.0f), s);
            yield return ("관리실 연결 통로 입구", new(-3.15f, -5, 100.85f), w);
            yield return ("관리실 입구", new(-7.2f, -5, 101.1f), n);
            yield return ("탈의실 입구", new(-10.4f, -5, 105.05f), w);
            yield return ("탈출로 연결 통로 입구", new(-3.25f, -10, 91.15f), w);
            yield return ("탈출로 입구", new(-8.25f, -10, 91.15f), w);
            if (!final) yield break;
            yield return ("긴 복도 입구", new(14.3f, 0, 48.35f), w);
            yield return ("이전 세대 숙소 입구", new(-2f, 0, 46.45f), s);
            yield return ("이전 세대 화장실 입구", new(-14.7f, 0, 37.55f), s);
            yield return ("이전 세대 소품실 입구", new(-3.2f, 0, 37.55f), s);
            yield return ("창고 입구", new(-19.8f, 0, 41.4f), w);
            yield return ("설비실 입구", new(24.75f, 2.3f, 72.2f), n);
        }

        static int SeedWaitingRoom(Scene scene, DevCheckpointRoute route)
        {
            var layout = scene.GetRootGameObjects().FirstOrDefault(root => root.name == "WaitingRoom_LayoutA");
            if (layout == null) return 0;
            Physics.SyncTransforms();
            var placed = new List<(string label, Vector3 feet, Vector3 facing)>();
            foreach (var (name, feet, direction) in WaitingRoomEntrances(layout.transform.Find("FinalLayout_Swap") != null))
            {
                var forward = direction.normalized;
                var right = Vector3.Cross(Vector3.up, forward);
                Vector3? stand = null;
                // Look a short distance around the doorway, without snapping to a different floor.
                foreach (float back in new[] { 0f, .45f, .9f, -.45f, 1.35f })
                {
                    foreach (float side in new[] { 0f, -.35f, .35f, -.7f, .7f })
                    {
                        var candidate = feet - forward * back + right * side;
                        if (!TryFloor(scene, candidate + Vector3.up * .5f, 1.05f, out var ground)) continue;
                        if (Physics.OverlapCapsule(ground + Vector3.up * .35f, ground + Vector3.up * 1.45f,
                            .31f, ~0, QueryTriggerInteraction.Ignore).Any(c => c.gameObject.scene == scene)) continue;
                        stand = ground;
                        break;
                    }
                    if (stand.HasValue) break;
                }
                if (stand == null) throw new InvalidOperationException($"{scene.name}: {name}에 설 수 있는 바닥을 찾지 못했습니다 ({feet}).");
                placed.Add((name, stand.Value, forward));
            }

            var data = new SerializedObject(route);
            var points = data.FindProperty("points");
            foreach (var (label, feet, facing) in placed) Append(points, route, label, feet, facing, radius: 1.25f);
            data.ApplyModifiedProperties();
            return route.Points.Count;
        }
    }
}
