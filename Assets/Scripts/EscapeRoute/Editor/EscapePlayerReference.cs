using System;
using System.Linq;
using CurtainCall.Player;
using CurtainCall.Settings;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CurtainCall.EscapeRoute.EditorTools
{
    public static partial class EscapeBackgroundBuilder
    {
        [MenuItem("Tools/CurtainCall/탈출로/현재 캐릭터 크기 기준과 이동 연결")]
        public static void ConfigurePlayerReference()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath || EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("escape_route 편집 모드가 필요합니다.");
            var setup = scene.GetRootGameObjects().First(g => g.name == "PlaySetup");
            var player = setup.transform.Find("Player").GetComponent<PlayerController>();
            var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Prefabs/Characters/Players/Player.prefab");
            var sourceSlot = new SerializedObject(source.GetComponent<PlayerModelSlot>());
            var model = (GameObject)sourceSlot.FindProperty("modelPrefab").objectReferenceValue;
            if (model == null) throw new InvalidOperationException("현재 Player의 모델 참조가 비어 있습니다.");

            var slot = new SerializedObject(player.GetComponent<PlayerModelSlot>());
            slot.FindProperty("modelPrefab").objectReferenceValue = model;
            slot.FindProperty("fitToHeight").boolValue = true;
            slot.FindProperty("squashWhenCrouched").boolValue = sourceSlot.FindProperty("squashWhenCrouched").boolValue;
            slot.ApplyModifiedProperties();
            PrefabUtility.RecordPrefabInstancePropertyModifications(player.GetComponent<PlayerModelSlot>());
            var balance = new SerializedObject(player.GetComponent<PlayerBalance>());
            balance.FindProperty("activeOnStart").boolValue = false;
            balance.ApplyModifiedProperties();
            PrefabUtility.RecordPrefabInstancePropertyModifications(player.GetComponent<PlayerBalance>());
            var camera = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<CurtainCall.LevelPreview.LevelPreviewCamera>()).First();
            var cameraData = new SerializedObject(camera);
            cameraData.FindProperty("player").objectReferenceValue = player;
            cameraData.FindProperty("eyeHeight").floatValue = GameSettings.Base.Character.ModelHeight * .92f;
            cameraData.ApplyModifiedProperties();

            var background = scene.GetRootGameObjects().First(g => g.name == RootName).transform;
            var old = background.Find("11_DepthAndLandmarks/ScaleReference_1.8m");
            if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
            var oldReferences = background.Find("13_PlayerScaleReferences");
            if (oldReferences != null) Undo.DestroyObjectImmediate(oldReferences.gameObject);
            var references = new GameObject("13_PlayerScaleReferences").transform;
            Undo.RegisterCreatedObjectUndo(references.gameObject, "Current playable model scale references");
            references.SetParent(background, false);
            font = GetFont();
            Vector3[] feet = { new Vector3(14, 0, 31), new Vector3(53, 2, 59), new Vector3(96, 0, 22) };
            foreach (var localFeet in feet)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(model, references);
                instance.name = "현재 플레이어 모델 · " + GameSettings.Base.Character.ModelHeight.ToString("0.00") + "m";
                instance.transform.position = background.TransformPoint(localFeet);
                instance.transform.rotation = Quaternion.Euler(0, 205, 0);
                // The parent expands the map, but the character keeps its actual aspect ratio.
                instance.transform.localScale = new Vector3(1f / background.lossyScale.x, 1f / background.lossyScale.y, 1f / background.lossyScale.z);
                var humanoid = instance.GetComponent<HumanoidModel>();
                humanoid.Original.enabled = true;
                foreach (var r in humanoid.Bodies.Concat(humanoid.Limbs)) r.enabled = false;
                var animator = humanoid.Animator;
                animator.Rebind(); animator.Play("Idle", 0, 0f); animator.Update(0f);
                animator.enabled = false;
                foreach (var collider in instance.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
                foreach (var rigidbody in instance.GetComponentsInChildren<Rigidbody>(true)) rigidbody.isKinematic = true;
                var bounds = humanoid.Original.bounds;
                instance.transform.localScale *= GameSettings.Base.Character.ModelHeight / bounds.size.y;
                bounds = humanoid.Original.bounds;
                instance.transform.position += new Vector3(0, background.TransformPoint(localFeet).y - bounds.min.y, 0);
                foreach (var component in instance.GetComponentsInChildren<Component>(true))
                    if (component != null) PrefabUtility.RecordPrefabInstancePropertyModifications(component);
                Sign(references, instance.name, background.TransformPoint(localFeet) + Vector3.up * 2.2f, 90, 6);
            }
            AssetDatabase.SaveAssetIfDirty(font);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
    }
}
