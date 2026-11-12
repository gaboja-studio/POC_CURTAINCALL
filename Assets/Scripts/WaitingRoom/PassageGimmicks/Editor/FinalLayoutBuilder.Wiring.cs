using System.Linq;
using CurtainCall.EscapeRoute;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CurtainCall.WaitingRoom.PassageGimmicks
{
    // 4단계: 지하 기계실의 녹슨 벽 뒤 배선. 벽(기존 배관함 복제, BreakableWall)을 부수면 배선이 드러나고,
    // 톱날 발사기(기존 PortableSaw)로 자르면 관리실 문이 탐색 장부에서 열린다. 탈출로의 절단기·배선 기능을 그대로 쓴다.
    public static partial class FinalLayoutBuilder
    {
        const string RustWallId = "final:machine-rust-wall", ManagementDoorId = "structure-a:management-door";
        const string CableMaterials = "Assets/Resources/Sounds/EscapeRouteMaterials/";

        [MenuItem("CurtainCall/Waiting Room/Final Layout/4. Build Machine Room Wiring")]
        static void WiringMenu() => Debug.Log(BuildMachineWiring());

        public static string BuildMachineWiring()
        {
            var layout = GameObject.Find(LayoutPath)?.transform;
            if (layout == null) return "WaitingRoom 씬을 여세요.";
            var source = layout.Find("B1_MachineRoom/Exploration_A/PipeBox");
            var saw = layout.GetComponentsInChildren<EscapeExitSaw>(true).FirstOrDefault(s => s.name == "PortableSaw");
            if (source == null || saw == null) return "배관함 또는 톱날 발사기를 찾지 못했습니다.";
            int undo = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("Final layout: machine room wiring");
            var old = layout.Find(RootName + "/MachineWiring"); if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
            var root = Child(Child(layout, RootName), "MachineWiring");

            // 녹슨 벽: 배관함 바로 북쪽 벽면에 같은 구조를 하나 더 세운다.
            var sb = Bounds(source); float shift = sb.size.z + .45f;
            var wallGo = Object.Instantiate(source.gameObject, root); wallGo.name = "RustWall";
            var wall = wallGo.transform; wall.SetPositionAndRotation(source.position + Vector3.forward * shift, source.rotation);
            foreach (var key in wall.Cast<Transform>().Where(t => t.name.StartsWith("Keypad")).ToArray()) Object.DestroyImmediate(key.gameObject);
            // 겉을 전부 녹슨 철판 재질(배관함 뒤판과 같은 것)로 바꿔 녹슨 벽으로 보이게 한다.
            var rust = wall.Find("BoxBack").GetComponent<Renderer>().sharedMaterial;
            foreach (var r in wall.GetComponentsInChildren<Renderer>(true)) r.sharedMaterials = Enumerable.Repeat(rust, r.sharedMaterials.Length).ToArray();
            var so = new SerializedObject(wallGo.GetComponent<BreakableWall>());
            so.FindProperty("targetId").stringValue = RustWallId; so.FindProperty("label").stringValue = "녹슨 벽";

            // 배선: 벽 안쪽에서 바닥부터 위로 선 한 가닥. 톱날(수평 원판, 발사구 높이 1.45m)이 가로지른다.
            var cableGo = new GameObject("HiddenCable", typeof(MeshFilter), typeof(MeshRenderer)); cableGo.transform.SetParent(root, false);
            cableGo.transform.position = new Vector3(sb.min.x + .16f, sb.min.y, wall.position.z);
            var cable = cableGo.AddComponent<EscapeCuttableCable>(); var cso = new SerializedObject(cable);
            var points = cso.FindProperty("localPoints"); points.arraySize = 4;
            var path = new[] { new Vector3(0, .1f, -.1f), new Vector3(.02f, .7f, .04f), new Vector3(0, 1.3f, -.03f), new Vector3(0, 1.86f, .06f) };
            for (int i = 0; i < path.Length; i++) points.GetArrayElementAtIndex(i).vector3Value = path[i];
            cso.FindProperty("radius").floatValue = .05f;
            cso.FindProperty("insulationMaterial").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Material>(CableMaterials + "cable-red.mat");
            cso.FindProperty("copperMaterial").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Material>(CableMaterials + "cable-copper-core.mat");
            cso.ApplyModifiedPropertiesWithoutUndo(); cable.Rebuild();
            var reveal = so.FindProperty("revealOnOpen"); reveal.arraySize = 1; reveal.GetArrayElementAtIndex(0).objectReferenceValue = cableGo;
            so.ApplyModifiedPropertiesWithoutUndo();
            cableGo.SetActive(false); // 벽이 무너지면 BreakableWall이 켠다. 꺼져 있는 동안은 톱날 판정도 받지 않는다.

            // 톱날 발사기(이 씬의 인스턴스만): 배선 연결, 다 자르면 관리실 문을 장부에 연다.
            var sso = new SerializedObject(saw);
            var cables = sso.FindProperty("cables"); cables.arraySize = 1; cables.GetArrayElementAtIndex(0).objectReferenceValue = cable;
            sso.FindProperty("openTarget").stringValue = ManagementDoorId;
            sso.ApplyModifiedProperties(); PrefabUtility.RecordPrefabInstancePropertyModifications(saw);

            // 조명 회로(NightPower): 배선이 잘리면 전체 조명이 한 번 꺼졌다 켜진다. 새 공간 조명도 회로에 넣는다.
            var power = layout.GetComponentInChildren<NightPower>(true);
            if (power != null)
            {
                var pso = new SerializedObject(power); var circuit = pso.FindProperty("circuit");
                for (int i = circuit.arraySize - 1; i >= 0; i--) if (circuit.GetArrayElementAtIndex(i).objectReferenceValue == null) circuit.DeleteArrayElementAtIndex(i);
                var have = Enumerable.Range(0, circuit.arraySize).Select(i => circuit.GetArrayElementAtIndex(i).objectReferenceValue).ToHashSet();
                foreach (var light in layout.Find(RootName).GetComponentsInChildren<Light>(true).Where(l => !have.Contains(l)))
                { circuit.InsertArrayElementAtIndex(circuit.arraySize); circuit.GetArrayElementAtIndex(circuit.arraySize - 1).objectReferenceValue = light; }
                var bc = pso.FindProperty("blackoutCables"); bc.arraySize = 1; bc.GetArrayElementAtIndex(0).objectReferenceValue = cable;
                pso.ApplyModifiedProperties();
            }

            Physics.SyncTransforms();
            var wb = Bounds(wall);
            var hits = Physics.OverlapBox(wb.center, wb.extents * .9f, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore)
                .Where(c => !c.transform.IsChildOf(wall)).Select(c => c.name).Distinct().ToArray();
            Undo.RegisterCreatedObjectUndo(root.gameObject, "Machine wiring");
            Undo.CollapseUndoOperations(undo); EditorSceneManager.MarkSceneDirty(layout.gameObject.scene);
            return $"녹슨 벽 x {wb.min.x:F2}~{wb.max.x:F2} z {wb.min.z:F2}~{wb.max.z:F2} / 배선 y {sb.min.y + .1f:F2}~{sb.min.y + 1.86f:F2} / 겹치는 충돌체: {(hits.Length == 0 ? "없음" : string.Join(",", hits))}";
        }
    }
}
