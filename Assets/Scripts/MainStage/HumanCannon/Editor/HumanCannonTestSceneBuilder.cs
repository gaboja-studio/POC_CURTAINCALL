using System;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Network.Session;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering.Universal;

namespace CurtainCall.HumanCannon.EditorTools
{
    /// <summary>Task-040의 빈 전용 씬만 채운다. 기존 오브젝트가 있으면 덮어쓰지 않는다.</summary>
    public static class HumanCannonTestSceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/Tests/MainStage/HumanCannon/HumanCannon.unity";

        [MenuItem("Tools/CurtainCall/인간 대포/빈 테스트 씬 구성")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Play를 종료한 뒤 구성하세요.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("열린 씬을 먼저 저장하세요.");
            var cameraPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Prefabs/Controllers/Camera/PlayerFollowCamera.prefab");
            if (cameraPrefab == null) throw new InvalidOperationException("공용 PlayerFollowCamera 프리팹이 없습니다.");
            var previousScene = SceneManager.GetActiveScene();
            var scene = SceneManager.GetSceneByPath(ScenePath);
            bool openedHere = !scene.IsValid() || !scene.isLoaded;
            if (openedHere) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            if (scene.rootCount != 0)
            {
                if (openedHere) EditorSceneManager.CloseScene(scene, true);
                throw new InvalidOperationException("HumanCannon 씬에 오브젝트가 있어 덮어쓰지 않았습니다.");
            }
            SceneManager.SetActiveScene(scene);
            try
            {
                var setup = new GameObject("HumanCannonTest");
                var system = setup.AddComponent<HumanCannonSystem>();
                setup.AddComponent<NetworkSessionTestUI>();
                setup.AddComponent<HumanCannonTestHud>();

                var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
                ground.name = "Ground";
                ground.transform.SetPositionAndRotation(new Vector3(0, -0.5f, 12), Quaternion.identity);
                ground.transform.localScale = new Vector3(30, 1, 60);

                var stationObject = new GameObject("Cannon");
                stationObject.transform.position = new Vector3(0, 0, 3);
                var station = stationObject.AddComponent<HumanCannonStation>();
                Transform boarding = Point("BoardingPoint", stationObject.transform, new Vector3(0, 0, -1.5f));
                Transform seat = Point("Seat", stationObject.transform, new Vector3(0, 1.2f, 0));
                Transform muzzle = Point("Muzzle", stationObject.transform, new Vector3(0, 2, 1.5f));
                var barrel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                barrel.name = "Barrel";
                barrel.transform.SetParent(stationObject.transform, false);
                barrel.transform.localPosition = new Vector3(0, 0.9f, 0);
                barrel.transform.localRotation = Quaternion.Euler(65, 0, 0);
                barrel.transform.localScale = new Vector3(1.4f, 1.2f, 1.4f);
                UnityEngine.Object.DestroyImmediate(barrel.GetComponent<Collider>());
                SetReference(station, "boardingPoint", boarding);
                SetReference(station, "seat", seat);
                SetReference(station, "muzzle", muzzle);
                var systemData = new SerializedObject(system);
                var list = systemData.FindProperty("stations");
                list.arraySize = 1;
                list.GetArrayElementAtIndex(0).objectReferenceValue = station;
                systemData.ApplyModifiedPropertiesWithoutUndo();

                var spawnObject = new GameObject("SpawnPoints");
                var spawns = spawnObject.AddComponent<PlayerSpawnPoints>();
                var spawnData = new SerializedObject(spawns);
                var points = spawnData.FindProperty("points");
                points.arraySize = 4;
                for (int i = 0; i < 4; i++)
                    points.GetArrayElementAtIndex(i).objectReferenceValue = Point("Player" + (i + 1), spawnObject.transform,
                        new Vector3((i - 1.5f) * 1.5f, 0.05f, 0));
                spawnData.ApplyModifiedPropertiesWithoutUndo();

                var cameraObject = new GameObject("Main Camera");
                cameraObject.tag = "MainCamera";
                cameraObject.AddComponent<Camera>();
                cameraObject.AddComponent<UniversalAdditionalCameraData>();
                cameraObject.AddComponent<AudioListener>();
                cameraObject.AddComponent<CinemachineBrain>();
                cameraObject.transform.position = new Vector3(0, 9, -12);
                cameraObject.transform.LookAt(new Vector3(0, 0, 8));
                PrefabUtility.InstantiatePrefab(cameraPrefab, scene);
                var light = new GameObject("Directional Light").AddComponent<Light>();
                light.gameObject.AddComponent<UniversalAdditionalLightData>();
                light.type = LightType.Directional;
                light.intensity = 1f;
                light.transform.rotation = Quaternion.Euler(50, -30, 0);
                HumanCannonDistanceMarkersBuilder.Build(ground, station);
                var trampoline = HumanCannonTrampolineBuilder.Build(station);
                HumanCannonLandingZoneBuilder.Build(trampoline);
                if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("테스트 씬 저장 실패");
                Debug.Log("[HumanCannon] 전용 테스트 씬 저장 완료. Play → LAN Host → 대포 앞 F.");
            }
            finally
            {
                if (previousScene.IsValid() && previousScene.isLoaded) SceneManager.SetActiveScene(previousScene);
            }
        }

        static Transform Point(string name, Transform parent, Vector3 position)
        {
            var point = new GameObject(name).transform;
            point.SetParent(parent, false);
            point.localPosition = position;
            return point;
        }

        static void SetReference(UnityEngine.Object target, string field, UnityEngine.Object value)
        {
            var data = new SerializedObject(target);
            data.FindProperty(field).objectReferenceValue = value;
            data.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
