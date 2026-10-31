#if UNITY_EDITOR
using System;
using System.Linq;
using CurtainCall.Cameras;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CurtainCall.EscapeRoute.Editor
{
    /// <summary>Migrates only the authored connection. Waiting-room furniture/tutorials are untouched.</summary>
    public static class EscapeLoadingConnectionBuilder
    {
        [MenuItem("Tools/CurtainCall/Escape/Use Loading Entrance")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Keep Play stopped.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save pending scene edits first.");
            var waiting = SceneManager.GetSceneByPath(EscapeWaitingRoomLinkBuilder.WaitingPath);
            if (!waiting.isLoaded) waiting = EditorSceneManager.OpenScene(EscapeWaitingRoomLinkBuilder.WaitingPath, OpenSceneMode.Single);
            var escape = SceneManager.GetSceneByPath(EscapeWaitingRoomLinkBuilder.EscapePath);
            if (!escape.isLoaded) escape = EditorSceneManager.OpenScene(EscapeWaitingRoomLinkBuilder.EscapePath, OpenSceneMode.Additive);
            var source = waiting.GetRootGameObjects().Single(r => r.name == "BasementEscapeConnection");
            var campaign = escape.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<EscapeCampaignContent>(true)).Single();
            var destination = escape.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<EscapeSceneArrival>(true)).SingleOrDefault();
            if (destination == null)
            {
                if (source.transform.Find("FirstPersonAtStairEnd") == null)
                    throw new InvalidOperationException("Source stairs missing; restore the authored connection before migrating.");
                var copy = UnityEngine.Object.Instantiate(source);
                copy.name = "EscapeArrivalStairs";
                SceneManager.MoveGameObjectToScene(copy, escape);
                copy.transform.SetParent(campaign.transform, true);
                UnityEngine.Object.DestroyImmediate(copy.GetComponent<EscapeEntrance>());
                var hud = copy.transform.Find("AssemblyCountdownHUD") ?? copy.transform.Find("AssemblyStatusHUD");
                if (hud != null) UnityEngine.Object.DestroyImmediate(hud.gameObject);
                destination = copy.AddComponent<EscapeSceneArrival>();
                Set(destination, "assemblyArea", copy.transform.Find("FourPlayerAssembly").GetComponent<BoxCollider>());
                Set(destination, "firstPersonArea", copy.transform.Find("FirstPersonAtStairEnd").GetComponent<BoxCollider>());
                Set(destination, "forwardBarrier", copy.transform.Find("AssemblyDepartureDoor").gameObject);
                Set(destination, "returnBarrier", copy.transform.Find("DepartureReturnSeal").gameObject);
                Set(destination, "approachView", AssetDatabase.LoadAssetAtPath<CameraViewPreset>("Assets/Scenes/Tests/WaitingRoom/Base/Prefabs/WaitingRoomCloseThirdPerson.asset"));
                foreach (var label in copy.GetComponentsInChildren<TMP_Text>(true)) label.text = "탈출로\n계단을 내려가세요";
                copy.transform.Find("AssemblyDepartureDoor").gameObject.SetActive(false);
            }
            // W ends at its closed forward wall; A alone owns stairs and the passage after loading.
            foreach (var child in source.transform.Cast<Transform>().ToArray())
            {
                if (child.name == "AssemblyCountdownHUD" || child.name == "AssemblyStatusHUD") continue;
                if (child.position.x < -21.01f) UnityEngine.Object.DestroyImmediate(child.gameObject);
            }
            var sourceHud = source.transform.Find("AssemblyCountdownHUD");
            if (sourceHud != null) sourceHud.name = "AssemblyStatusHUD";
            foreach (var label in source.GetComponentsInChildren<TMP_Text>(true)) label.text = "탈출 준비\n플레이 중인 전원이 모이면 출발";
            source.transform.Find("AssemblyDepartureDoor").gameObject.SetActive(true);
            Seal(source.transform.Find("DepartureReturnSeal"), false);
            var sourceAnchor = source.transform.Find("FourPlayerAssembly");
            var sourceSeal = source.transform.Find("DepartureReturnSeal");
            var destinationSeal = destination.transform.Find("DepartureReturnSeal");
            destinationSeal.SetPositionAndRotation(destination.AssemblyAnchor.TransformPoint(sourceAnchor.InverseTransformPoint(sourceSeal.position)),
                destination.AssemblyAnchor.rotation * Quaternion.Inverse(sourceAnchor.rotation) * sourceSeal.rotation);
            destinationSeal.localScale = sourceSeal.localScale;
            destinationSeal.gameObject.SetActive(true);
            // Matching appearance does not require overlapping world coordinates. The transfer uses
            // assembly-local poses, so move A and its arrival corridor beyond the whole W footprint.
            var campaignData = new SerializedObject(campaign);
            campaignData.FindProperty("campaignPosition").vector3Value = new Vector3(-200, -16, 118.3f);
            campaignData.ApplyModifiedPropertiesWithoutUndo();
            campaign.PlaceCampaign();
            // Re-serialize the settings asset to remove the obsolete assemblySeconds field.
            var settings = AssetDatabase.LoadAssetAtPath<CurtainCall.Settings.EscapeRouteSettings>("Assets/Resources/Settings/GameSettings/EscapeRouteSettings.asset");
            EditorUtility.SetDirty(settings); AssetDatabase.SaveAssetIfDirty(settings);
            EditorSceneManager.MarkSceneDirty(waiting); EditorSceneManager.MarkSceneDirty(escape);
            if (!EditorSceneManager.SaveScene(waiting) || !EditorSceneManager.SaveScene(escape)) throw new InvalidOperationException("Scene save failed.");
            SceneManager.SetActiveScene(waiting);
            Debug.Log("[EscapeLink] Saved W assembly and A arrival/stairs. All-player loading transition; no assembly timer.");
        }
        static void Seal(Transform wall, bool visible)
        {
            // The matching closed rear wall also hides the outside once W is unloaded.
            wall.position = new Vector3(-14.3f, -7.7f, 93.35f);
            wall.localScale = new Vector3(.2f, 4.6f, 4.6f);
            wall.gameObject.SetActive(visible);
        }
        static void Set(UnityEngine.Object target, string field, UnityEngine.Object value)
        {
            var data = new SerializedObject(target);
            data.FindProperty(field).objectReferenceValue = value;
            data.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
#endif
