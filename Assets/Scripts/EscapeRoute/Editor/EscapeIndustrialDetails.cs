using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CurtainCall.EscapeRoute.EditorTools
{
    public static partial class EscapeBackgroundBuilder
    {
        // Separate from the structure pass so manual edits to the layout are preserved.
        public static void AddIndustrialServiceDetails()
        {
            if (SceneManager.GetActiveScene().path != ScenePath || EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("A 편집 모드가 필요합니다.");
            var root = GameObject.Find(RootName + "/" + IndustrialFinishGroup)?.transform;
            if (root == null) throw new InvalidOperationException("먼저 A 사용 흔적을 적용하세요.");
            if (root.Find("04_RepairAndDistribution") != null) return;
            wornSteel = AssetDatabase.LoadAssetAtPath<Material>(Materials + "working-steel.mat");
            wornPipe = AssetDatabase.LoadAssetAtPath<Material>(Materials + "working-pipe.mat");
            patchConcrete = AssetDatabase.LoadAssetAtPath<Material>(Materials + "working-concrete-repair.mat");
            floorWear = AssetDatabase.LoadAssetAtPath<Material>(Materials + "working-floor-dirt.mat");
            leakWear = AssetDatabase.LoadAssetAtPath<Material>(Materials + "working-leak.mat");
            var p = VerticalChild(root, "04_RepairAndDistribution");
            RevealAsset(p, "벽면 정비 보관함", Industrial + "SM_CS1_WorkCabinet_01a_c1_Nanite.prefab",
                new Vector3(21.4f, 0, 34), new Vector3(.7f, 1.8f, 1.15f), wornSteel, Quaternion.Euler(0, 270, 0));
            foreach (float z in new[] { 30.8f, 47.2f })
            {
                RevealAsset(p, "배관 차단 밸브", Industrial + "SM_Pipe_Small_Valve_01.prefab",
                    new Vector3(21.9f, .95f, z), new Vector3(.44f, .45f, .55f), wornPipe);
                RevealAsset(p, "낡은 배관 압력계", Industrial + "SM_Pipe_Small_Meter.prefab",
                    new Vector3(21.82f, 2.25f, z + .8f), new Vector3(.32f, .37f, .28f), wornSteel);
            }
            ServicePipe(p, new Vector3(22.08f, 1.15f, 57), new Vector3(22.08f, 3.8f, 57), .18f);
            ServicePipe(p, new Vector3(22.08f, 3.8f, 57), new Vector3(17.7f, 3.8f, 57), .18f);
            RevealBlock(p, "추가 설치된 전기 접속함", new Vector3(22.15f, 2.3f, 40), new Vector3(.5f, .9f, .65f), wornSteel, false, false);
            for (int cable = 0; cable < 3; cable++)
            {
                var previous = new Vector3(21.85f - cable * .06f, 3.2f, 37);
                for (int segment = 1; segment <= 8; segment++)
                {
                    float t = segment / 8f;
                    var next = new Vector3(21.85f - cable * .06f, 3.2f - Mathf.Sin(t * Mathf.PI) * (.65f + cable * .1f), 37 + t * 3);
                    VerticalCable(p, "처진 접속 케이블", previous, next, .025f, wornSteel); previous = next;
                }
            }
            foreach (float z in new[] { 29f, 44.5f, 58f })
            {
                RevealBlock(p, "부분 재타설 패치", new Vector3(22.475f, 1.1f, z), new Vector3(.018f, 1.3f, 2.5f), patchConcrete, false, false);
                WallWear(p, new Vector3(22.46f, 2.2f, z), new Vector2(3.4f, 4.4f), 90);
            }
            // Edge stains and repair marks stay outside the central movement lane.
            FloorWear(p, new Vector3(21, .023f, 40), new Vector2(2.8f, 11), 0);
            FloorWear(p, new Vector3(10.6f, .023f, 55), new Vector2(1.2f, 10), 0);
            FloorWear(p, new Vector3(12.5f, .023f, 18), new Vector2(4, 5), 25);
            WallWear(p, new Vector3(18.3f, 2.9f, 19.48f), new Vector2(3, 5.5f), 0);
            WallWear(p, new Vector3(20.5f, 2.8f, 77.46f), new Vector2(5, 5.6f), 0);
            RevealAsset(p, "반출 정비함", Industrial + "SM_CS1_WorkCabinet_01a_c1_Nanite.prefab",
                new Vector3(181, 0, 38), new Vector3(1.15f, 1.8f, .7f), wornSteel, Quaternion.Euler(0, 14, 0));
            ServicePipe(p, new Vector3(179, 3, 38), new Vector3(179, 3, 49), .12f);
            ServicePipe(p, new Vector3(179, 3, 38), new Vector3(181, 1.8f, 38), .08f);
            foreach (var r in p.GetComponentsInChildren<Renderer>())
            {
                if (r.sharedMaterial != patchConcrete) continue;
                PrefabUtility.RecordPrefabInstancePropertyModifications(r);
            }
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }
    }
}
