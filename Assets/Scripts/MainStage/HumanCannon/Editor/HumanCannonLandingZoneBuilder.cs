using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CurtainCall.HumanCannon.EditorTools
{
    public static class HumanCannonLandingZoneBuilder
    {
        [MenuItem("Tools/CurtainCall/인간 대포/테스트 쿠션 추가")]
        public static void AddToOpenScene()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || scene.isDirty
                || scene.path != HumanCannonTestSceneBuilder.ScenePath)
                throw new InvalidOperationException("Play 종료 후 저장된 HumanCannon 전용 씬을 여세요.");
            HumanCannonTrampoline trampoline = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.GetComponentInChildren<HumanCannonLandingZone>(true) != null)
                    throw new InvalidOperationException("기존 쿠션은 덮어쓰지 않습니다.");
                if (root.TryGetComponent<HumanCannonTrampoline>(out var found)) trampoline = found;
            }
            Build(trampoline);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("쿠션 씬 저장 실패");
        }

        public static void Build(HumanCannonTrampoline trampoline)
        {
            if (trampoline == null) throw new InvalidOperationException("트램폴린이 필요합니다.");
            const string path = "Assets/Scenes/Tests/MainStage/HumanCannon/Materials/landing-cushion.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (shader == null) throw new InvalidOperationException("URP 셰이더가 없습니다.");
                material = new Material(shader) { name = "landing-cushion" };
                material.SetColor("_BaseColor", new Color(1f, 0.38f, 0.1f));
                AssetDatabase.CreateAsset(material, path);
            }
            var cushion = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cushion.name = "LandingCushion";
            // +X 쪽 6m: 반동 후 D를 누르는 동안 이동해 중앙에 착지할 수 있다.
            cushion.transform.position = new Vector3(trampoline.transform.position.x + 6f, 0.1f, trampoline.transform.position.z);
            cushion.transform.localScale = new Vector3(4f, 0.2f, 4f);
            cushion.GetComponent<Renderer>().sharedMaterial = material;
            cushion.AddComponent<HumanCannonLandingZone>();
        }
    }
}
