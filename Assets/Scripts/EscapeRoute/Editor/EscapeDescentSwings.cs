using System;
using System.Linq;
using CurtainCall.HumanCannon;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CurtainCall.EscapeRoute.EditorTools
{
    public static partial class EscapeBackgroundBuilder
    {
        public static void CompleteADescent()
        {
            var scene = SceneManager.GetActiveScene();
            if (Application.isPlaying || scene.path != ScenePath) throw new InvalidOperationException("A edit scene required");
            var background = GameObject.Find(RootName).transform;
            if (background.Find(CompletionGroup) != null) throw new InvalidOperationException("Already built; edit current structure");
            CompletionMaterials();
            var old = background.Find(DepthGroup + "/04A_Descent_plus2_to_minus22"); PreserveDepthObject(old.gameObject);
            var group = VerticalChild(background, CompletionGroup); group.localScale = new Vector3(1 / background.lossyScale.x, 1 / background.lossyScale.y, 1 / background.lossyScale.z);
            var descent = VerticalChild(group, "04_StairSwingDescent");
            var preview = EditorSceneManager.OpenPreviewScene(OriginalCannonPath);
            try
            {
                var source = preview.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<HumanCannonPole>(true)).First();
                // Replace the six provisional bar shapes too; keep their grip locations and original control rules.
                var existing = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<HumanCannonPole>()).ToArray();
                foreach (var pole in existing)
                {
                    var data = new SerializedObject(pole);
                    float ceiling = ((Transform)data.FindProperty("leftAnchor").objectReferenceValue).position.y;
                    OriginalPole(VerticalChild(group, "Reused_" + pole.transform.parent.name + "_" + pole.name), source,
                        "Original_" + pole.name + "_" + Mathf.RoundToInt(pole.Grip.position.x), pole.Grip.position, pole.Forward, ceiling);
                    PreserveDepthObject(pole.gameObject);
                }
                DepthDeck(descent, "03 연결 참", 62, 72.5f, 60.5f, 62.5f, 2);
                for (int flight = 0; flight < 3; flight++)
                {
                    float z = 60.5f - flight * 19, y = 2 - flight * 8;
                    var stage = VerticalChild(descent, "Stage_" + (flight + 1));
                    for (int i = 0; i < 12; i++) BFloor(stage, "넓힌 하행 상단 " + i, 63, 71, z - (i + 1) * .35f, z - i * .35f, y - (i + 1) * .2f, .5f);
                    CompletionDeck(stage, "도약 대기대", 63, 71, z - 6.2f, z - 4.2f, y - 2.4f);
                    OriginalPole(stage, source, "Descent_" + flight + "_1", new Vector3(67, y - .1f, z - 7.3f), Vector3.back, y + 12);
                    OriginalPole(stage, source, "Descent_" + flight + "_2", new Vector3(67, y - 1.6f, z - 11.8f), Vector3.back, y + 12);
                    CompletionDeck(stage, "그네 이후 회복대", 63, 71, z - 16.2f, z - 14.2f, y - 6.4f);
                    for (int i = 0; i < 8; i++) BFloor(stage, "넓힌 하행 하단 " + i, 63, 71, z - 16.2f - (i + 1) * .35f, z - 16.2f - i * .35f, y - 6.4f - (i + 1) * .2f, .5f);
                    // Only side rails: open departure/arrival edges read clearly as intentional swing gaps.
                    foreach (float x in new[] {63f, 71f})
                    {
                        SpaceEdgeRail(stage, new Vector3(x, y, z), new Vector3(x, y - 2.4f, z - 4.2f));
                        SpaceEdgeRail(stage, new Vector3(x, y - 6.4f, z - 16.2f), new Vector3(x, y - 8, z - 19));
                    }
                    BLight(stage, new Vector3(72, y + 2, z - 14), new Vector3(67, y - 6.4f, z - 15), 420, 22);
                    FusionFloorMarks(stage, new Vector3(67, y - 2.38f, z - 5.2f), Vector3.back);
                }
            }
            finally { EditorSceneManager.ClosePreviewScene(preview); }
            CompletionDeck(descent, "하부 회복 연결 참", 63, 71, 1.5f, 3.5f, -22);
            BWallZ(descent, 55, 3.5f, 62.5f, -86, 36, 3); BWallZ(descent, 74, 3.5f, 60.5f, -86, 36, 3);
            BMass(descent, "하행 설비동 덮개", new Vector3(65, 37.5f, 33), new Vector3(22, 3, 62));
            BFloor(descent, "하행 공동 집수 바닥", 56.5f, 72.5f, 3.5f, 62.5f, -86, 3);
            BMass(descent, "폐쇄된 상부 우회문", new Vector3(76, 2.5f, 28.5f), new Vector3(3, 5, 13), bRecess);
            foreach (float z in new[] {20f, 40f, 57f})
            {
                VerticalCylinder(descent, "깊은 벽 매립 압력관", new Vector3(58, -25, z), .7f, 105, bSteel);
                BMass(descent, "벽면 수직 설비 홈", new Vector3(56.7f, -25, z), new Vector3(.4f, 105, 2), bRecess, false);
            }
            RegisterAllPoles(); CompleteEscapePresentation(); Physics.SyncTransforms(); EditorSceneManager.MarkSceneDirty(scene);
        }

        static void CompletionDeck(Transform p, string name, float x1, float x2, float z1, float z2, float y)
        {
            BFloor(p, name, x1, x2, z1, z2, y, 1.5f);
            var floor = p.Find(name); Ensure<HumanCannonLandingZone>(floor.gameObject);
            foreach (float x in new[]{x1,x2}) SpaceEdgeRail(p, new Vector3(x,y,z1), new Vector3(x,y,z2));
        }
    }
}
