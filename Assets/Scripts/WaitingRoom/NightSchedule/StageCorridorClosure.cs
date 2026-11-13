using System.Collections.Generic;
using CurtainCall.Campaign;
using CurtainCall.Settings;
using CurtainCall.WaitingRoom.Pursuit;
using UnityEngine;

namespace CurtainCall.WaitingRoom
{
    /// <summary>
    /// 정비 시간 무대 입구 통로 봉쇄·소등(Task-129, 2026-11-14 사용자).
    /// 밤 정비(이어지는 새벽 전환 포함) 동안 로비 → ⑥ 무대 입구 통로 입구를 다른 밤 철창과 같은 철창(<see cref="NightDoorLock"/>)으로 닫고,
    /// 통로(S06)·공연 스테이지 입구(S07) 조명을 끈다(<see cref="NightPower"/>). 쉬는 시간에는 열려 있다.
    /// 철창은 최종본 배치(로비 북쪽 통로) 씬에만 실행 중에 세운다. 씬 파일은 바꾸지 않는다.
    /// </summary>
    public static class StageCorridorClosure
    {
        /// <summary>런타임 철창 이름. <see cref="NightDoorLock"/>가 이 이름으로 잠김 규칙을 고른다.</summary>
        public const string GateName = "StageCorridorGate";
        const string RootName = "StageCorridorClosure (runtime)";
        static readonly string[] CorridorRooms = { "S06_StageCorridor", "S07_StageEntrance" };

        // 최종본 로비 북쪽 입구(FinalLayoutBuilder.Lobby: StageX0·StageX1·StageMouthZ). 양쪽 ReturnWall 안쪽 면 사이, 입구 끝(z 91.65)~벽 끝(z 91.9).
        const float MouthX0 = -14.43f, MouthX1 = -4.97f, GateZ = 91.75f;
        // 입구 높이 9.2m(StageOpenH). 머리 판(높이+0.24m) 아랫면이 천장 면과 겹쳐 깜빡이지 않게 조금 낮춘다.
        const float GateHeight = 9f;

        /// <summary>지금 무대 입구 통로를 닫고 조명을 꺼야 하는가. 모든 화면이 같은 런 상태로 정한다.</summary>
        public static bool IsClosed(CampaignSnapshot state)
            => (state.Phase == CampaignPhase.Night || state.Phase == CampaignPhase.Dawn)
               && GameSettings.WaitingRoom.CloseStageCorridorDuringMaintenance;

        static NightDoorLock gate;

        /// <summary>이 씬에 철창이 서 있고 지금 닫혀 있어 무대 입구로 갈 수 없는가(임무 문구에서 공연장 이동을 뺀다).</summary>
        public static bool BlocksStage(CampaignSnapshot state) => gate != null && gate.isActiveAndEnabled && IsClosed(state);

        /// <summary>최종본 배치(통로가 로비 북쪽)인가. 편집 도구가 남긴 표식으로 구분한다.</summary>
        public static bool IsFinalLayout(Transform layout) => layout != null && layout.Find("FinalLayout_Swap") != null;

        /// <summary>무대 입구 통로·공연 스테이지 입구의 조명. 최종본 배치가 아니면 빈 목록.</summary>
        public static Light[] CorridorLights(Transform layout)
        {
            if (!IsFinalLayout(layout)) return System.Array.Empty<Light>();
            var lights = new List<Light>();
            foreach (var room in CorridorRooms)
            {
                var t = layout.Find(room);
                if (t == null) continue;
                foreach (var light in t.GetComponentsInChildren<Light>(true))
                    if (light.type != LightType.Directional) lights.Add(light);
            }
            return lights.ToArray();
        }

        /// <summary>최종본 배치면 로비 → 통로 입구에 밤 철창을 세운다(한 번만). 모양·재질은 기존 밤 정비 철창과 같다.</summary>
        public static NightDoorLock InstallGate(Transform nightRoot)
        {
            var layout = nightRoot != null ? nightRoot.parent : null;
            if (!IsFinalLayout(layout)) return null;
            var existing = layout.Find(RootName);
            if (existing != null) return gate = existing.GetComponentInChildren<NightDoorLock>(true);

            Material iron = null;
            foreach (var other in nightRoot.GetComponentsInChildren<NightDoorLock>(true))
            {
                var renderer = other.GetComponentInChildren<Renderer>(true);
                if (renderer != null) { iron = renderer.sharedMaterial; break; }
            }

            var root = new GameObject(RootName).transform;
            root.SetParent(layout, false);
            root.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

            float width = MouthX1 - MouthX0, height = GateHeight;
            // 켜기 전에 철창을 연결해야 NightDoorLock.OnEnable이 처음부터 지금 상태(열림·닫힘)로 둔다.
            var go = new GameObject(GateName);
            go.SetActive(false);
            go.transform.SetParent(root, false);
            go.transform.SetPositionAndRotation(new Vector3((MouthX0 + MouthX1) * .5f, 0f, GateZ), Quaternion.identity);

            // WaitingRoomLayoutA.ConnectNightMaintenance의 철창과 같은 구성: 머리 판, 위쪽 끝이 기준점인 살 묶음, 통로를 막는 충돌체.
            Box(go.transform, "Header", new Vector3(0f, height + .12f, 0f), new Vector3(width + .3f, .24f, .3f), iron);
            var bars = new GameObject("Bars").transform;
            bars.SetParent(go.transform, false);
            bars.localPosition = new Vector3(0f, height, 0f);
            for (float x = -width / 2f + .09f; x < width / 2f; x += .22f)
                Box(bars, "VerticalBar", new Vector3(x, -height / 2f, 0f), new Vector3(.065f, height, .09f), iron);
            foreach (float y in new[] { -.1f, -height * .5f, -height + .1f })
                Box(bars, "CrossBar", new Vector3(0f, y, 0f), new Vector3(width, .08f, .12f), iron);
            var blocker = new GameObject("GateCollision");
            blocker.transform.SetParent(bars, false);
            var collider = blocker.AddComponent<BoxCollider>();
            collider.center = new Vector3(0f, -height / 2f, 0f);
            collider.size = new Vector3(width, height, .16f);
            // 추격자 길찾기도 닫힌 철창을 막힌 곳으로 본다(다른 밤 철창과 같음).
            blocker.AddComponent<PursuitDoorObstacle>().Source = collider;

            gate = go.AddComponent<NightDoorLock>();
            gate.Configure(bars, false);
            go.SetActive(true);
            return gate;
        }

        static void Box(Transform parent, string name, Vector3 position, Vector3 scale, Material material)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            // 살 하나하나에는 충돌체가 필요 없다. 막는 것은 GateCollision 하나다.
            Object.Destroy(box.GetComponent<Collider>());
            box.transform.SetParent(parent, false);
            box.transform.localPosition = position;
            box.transform.localScale = scale;
            if (material != null) box.GetComponent<Renderer>().sharedMaterial = material;
        }
    }
}
