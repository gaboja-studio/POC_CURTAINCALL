using System;
using System.Linq;
using CurtainCall.HumanCannon;
using CurtainCall.LevelPreview;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Network.Session;
using CurtainCall.Settings;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CurtainCall.EscapeRoute.EditorTools
{
    public static partial class EscapeBackgroundBuilder
    {
        const string CannonGameplay = "EscapeGameplay_A";
        const string CannonPrefab = "Assets/Resources/Prefabs/Objects/ETCs/HumanCannonArtKit/Prepared/CannonStationPrepared.prefab";
        const string FollowCameraPrefab = "Assets/Resources/Prefabs/Controllers/Camera/PlayerFollowCamera.prefab";

        [MenuItem("Tools/CurtainCall/탈출로/A03 대포와 착지 연결")]
        public static void InstallA03Cannon()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath || EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("A 편집 모드가 필요합니다.");
            if (GameObject.Find(CannonGameplay) != null || UnityEngine.Object.FindAnyObjectByType<HumanCannonSystem>() != null)
                throw new InvalidOperationException("기존 대포 연결을 직접 편집하세요.");
            var anchors = GameObject.Find(TrickSetup)?.transform;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CannonPrefab);
            if (anchors == null || prefab == null) throw new InvalidOperationException("A 기준점과 준비 대포 프리팹이 필요합니다.");
            Physics.SyncTransforms();
            var target = anchors.Find("03_CannonToIsland/Landing_Feet").position;
            if (!Physics.Raycast(target + Vector3.up * .2f, Vector3.down, out var hit, .4f) || !(hit.collider is BoxCollider))
                throw new InvalidOperationException("착지 바닥의 실제 BoxCollider를 확인하세요.");
            Undo.IncrementCurrentGroup(); int undo = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("A03 cannon and safe landing");
            var root = new GameObject(CannonGameplay);
            Undo.RegisterCreatedObjectUndo(root, "A cannon runtime");
            var system = Undo.AddComponent<HumanCannonSystem>(root);
            var cannon = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root.transform);
            Undo.RegisterCreatedObjectUndo(cannon, "A03 cannon instance");
            cannon.name = "03_Cannon";
            cannon.transform.SetPositionAndRotation(anchors.Find("03_CannonToIsland/Station_Base").position, Quaternion.Euler(0, 90, 0));
            var station = cannon.GetComponentInChildren<HumanCannonStation>();
            // Provisional neutral trajectory, using the project's gravity and the authored landing point.
            const float flightSeconds = 1.6f;
            Vector3 delta = target - station.Muzzle.position;
            var data = new SerializedObject(station);
            data.FindProperty("forwardSpeed").floatValue = Vector3.ProjectOnPlane(delta, Vector3.up).magnitude / flightSeconds;
            data.FindProperty("upwardSpeed").floatValue = (delta.y + .5f * GameSettings.Base.Character.Gravity * flightSeconds * flightSeconds) / flightSeconds;
            data.ApplyModifiedProperties();
            PrefabUtility.RecordPrefabInstancePropertyModifications(station);
            PrefabUtility.RecordPrefabInstancePropertyModifications(cannon.transform);
            var systemData = new SerializedObject(system);
            var stations = systemData.FindProperty("stations"); stations.arraySize = 1;
            stations.GetArrayElementAtIndex(0).objectReferenceValue = station;
            systemData.ApplyModifiedProperties();
            if (hit.collider.GetComponent<HumanCannonLandingZone>() == null)
                Undo.AddComponent<HumanCannonLandingZone>(hit.collider.gameObject);
            var guide = GameObject.Find(RootName + "/" + VerticalGroup + "/02_Route_0_to_minus6_to_plus2/대포 설치 자리");
            if (guide != null) { Undo.RecordObject(guide, "Hide replaced cannon guide"); guide.SetActive(false); }
            var paint = AssetDatabase.LoadAssetAtPath<Material>(Materials + "working-faded-ochre.mat");
            foreach (float z in new[] { 72f, 78.5f })
                RevealBlock(root.transform, "착지 발판 경계 도색", new Vector3(48, -5.985f, z), new Vector3(6, .01f, .09f), paint, false, false);
            EditorSceneManager.MarkSceneDirty(scene);
            Undo.CollapseUndoOperations(undo);
        }

    }
}
