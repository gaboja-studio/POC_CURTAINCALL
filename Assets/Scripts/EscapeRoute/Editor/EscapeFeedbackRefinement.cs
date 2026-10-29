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
        const string FeedbackGroup = "33_TraversalFeedback";

        public static void RefineCurrentEscapeFeedback()
        {
            var scene = SceneManager.GetActiveScene();
            if (Application.isPlaying || !EscapeDevelopmentSetup.IsEscapeScene(scene))
                throw new InvalidOperationException("Escape edit scene required");
            var bg = GameObject.Find(RootName).transform;
            if (bg.Find(FeedbackGroup) != null) throw new InvalidOperationException("Feedback already applied; edit saved objects");
            CompletionMaterials();
            var root = VerticalChild(bg, FeedbackGroup);
            root.localScale = new Vector3(1/bg.lossyScale.x, 1/bg.lossyScale.y, 1/bg.lossyScale.z);
            // This rail belonged to the old small A05 platform and now cuts through the cooperative work area.
            foreach (var part in bg.Find("30_SealedJoints").GetComponentsInChildren<Renderer>().Where(r=>
                r.name.StartsWith("정비대",StringComparison.Ordinal) && Mathf.Abs(r.bounds.center.x-93)<.05f
                && r.bounds.center.y>-6 && r.bounds.center.y<-3 && r.bounds.center.z>=25 && r.bounds.center.z<=31))
                part.gameObject.SetActive(false);
            // Keep one original swing per stair break, within standing interaction range of the takeoff lip.
            foreach (var pole in UnityEngine.Object.FindObjectsByType<HumanCannonPole>(FindObjectsSortMode.None)
                .Where(p => p.name.StartsWith("Descent_", StringComparison.Ordinal) && p.name.EndsWith("_2", StringComparison.Ordinal)))
                HideFeedbackPole(bg, pole);
            var trampoline = UnityEngine.Object.FindAnyObjectByType<HumanCannonTrampoline>();
            var data = new SerializedObject(trampoline);
            data.FindProperty("carryHandleHeight").floatValue = .8f;
            data.ApplyModifiedProperties();
            if (scene.path == ScenePath) trampoline.transform.position += Vector3.up * .08f;
            RefineOpposedVents(bg, root);
            if (scene.path == VariantBPath) RefineBLongCannonChain(bg, root);
            if (scene.path == VariantCPath) RefineCContinuousSwings(bg, root);
            RegisterAllPoles();
            var system = UnityEngine.Object.FindAnyObjectByType<HumanCannonSystem>();
            data = new SerializedObject(system);
            SetFinaleArray(data, "stations", UnityEngine.Object.FindObjectsByType<HumanCannonStation>(FindObjectsSortMode.None)
                .OrderBy(s => s.name, StringComparer.Ordinal).ToArray());
            data.ApplyModifiedProperties();
            Physics.SyncTransforms(); EditorSceneManager.MarkSceneDirty(scene);
        }

        static void HideFeedbackPole(Transform bg, HumanCannonPole pole)
        {
            Undo.RecordObject(pole.gameObject, "Simplify authored swing route");
            pole.gameObject.SetActive(false);
            foreach (var part in bg.GetComponentsInChildren<Transform>())
                if (part.name == pole.name + " 천장 매립 하중보" || part.name == pole.name + "_LowerSuspensionBeam"
                    || part.name == pole.name + "_FixedDropRod")
                    part.gameObject.SetActive(false);
        }

        static void RefineOpposedVents(Transform bg, Transform root)
        {
            var old = bg.Find(MidEncounterGroup + "/04_NorthVents_AlternateLanes");
            var originals = old.GetComponentsInChildren<HumanCannonHazard>().OrderBy(h => h.transform.parent.name).ToArray();
            if (originals.Length != 4) throw new InvalidOperationException("Expected four existing corridor vents");
            var vents = VerticalChild(root, "SixOpposedFlamePairs");
            var added = new System.Collections.Generic.List<HumanCannonHazard>();
            // Instantiate before moving the originals so every copy keeps the original world-space cue data.
            var all = new HumanCannonHazard[12];
            Array.Copy(originals, all, 4);
            for (int i = 4; i < all.Length; i++)
            {
                var clone = UnityEngine.Object.Instantiate(originals[i % 2].transform.parent.gameObject, vents);
                Undo.RegisterCreatedObjectUndo(clone, "Reuse original flame device");
                all[i] = clone.GetComponentInChildren<HumanCannonHazard>(); added.Add(all[i]);
            }
            for (int i = 0; i < all.Length; i++)
            {
                bool left = i % 2 == 0; float z = 60 + i / 2 * 9;
                var h = all[i]; var cue = h.GetComponent<HumanCannonHazardCue>();
                var at = new Vector3(left ? 121.35f : 123.65f, -6.8f, z);
                var group = h.transform.parent;
                group.SetParent(vents, true); group.position += at - cue.origin;
                group.name = "Pair_" + (i / 2 + 1) + (left ? "_Left" : "_Right");
                Undo.RecordObject(cue, "Opposed alternate flame timing");
                cue.origin = cue.destination = at;
                cue.warningSeconds = .65f; cue.activeSeconds = 1.6f; cue.restSeconds = 2.25f;
                cue.phase = left ? 0 : 2.25f;
                float nozzleX = left ? 119.7f : 125.3f;
                BMass(vents, "배출구 하우징_" + i, new Vector3(nozzleX, -7, z), new Vector3(.45f, 1.8f, 2.7f), wornSteel);
                BMass(vents, "배출구 내부_" + i, new Vector3(nozzleX + (left ? .25f : -.25f), -7, z), new Vector3(.08f, 1.3f, 2.1f), bRecess, false);
                VerticalCable(vents, "화염 배관_" + i, new Vector3(left ? 115.6f : 126, -6.8f, z), new Vector3(nozzleX, -6.8f, z), .55f, wornSteel);
                if (left) BLight(vents, new Vector3(118, -2, z), new Vector3(122, -8, z), 230, 16);
                EditorUtility.SetDirty(cue);
            }
            old.gameObject.SetActive(false);
            var data = new SerializedObject(UnityEngine.Object.FindAnyObjectByType<HumanCannonHazardCourse>());
            var list = data.FindProperty("hazards"); int count = list.arraySize;
            list.arraySize += added.Count;
            for (int i = 0; i < added.Count; i++) list.GetArrayElementAtIndex(count + i).objectReferenceValue = added[i];
            data.ApplyModifiedProperties();
        }

        static void FeedbackTerrace(Transform root, string name, Vector3 center, Vector2 size)
        {
            SpaceTerrace(root, name, center.x-size.x*.5f, center.x+size.x*.5f,
                center.z-size.y*.5f, center.z+size.y*.5f, center.y, -90);
            Ensure<HumanCannonLandingZone>(root.Find(name).gameObject);
            // Only the rear edge is railed; incoming flight and the next device remain unobstructed.
            SpaceEdgeRail(root, center + new Vector3(-size.x*.5f, 0, size.y*.5f), center + new Vector3(size.x*.5f, 0, size.y*.5f));
            BLight(root, center + new Vector3(-2, 7, 2), center, 450, 23);
        }

        static void FeedbackAim(HumanCannonStation station, Vector3 target, float seconds)
        {
            Undo.RecordObject(station.transform, "Face next cannon landing");
            station.transform.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(target-station.transform.position, Vector3.up));
            AimCompletionCannon(station, target, seconds);
            var data = new SerializedObject(station);
            data.FindProperty("boardingDistance").floatValue = 2.75f;
            data.FindProperty("airControlSpeed").floatValue = 6;
            data.ApplyModifiedProperties();
        }

        static void RefineBLongCannonChain(Transform bg, Transform root)
        {
            var old = bg.Find(TraversalSpacePrefix + "B/03_StaggeredServiceTerraces"); old.gameObject.SetActive(false);
            var poles = UnityEngine.Object.FindObjectsByType<HumanCannonPole>(FindObjectsSortMode.None);
            foreach (var p in poles.Where(p => p.name.StartsWith("B03_", StringComparison.Ordinal))) HideFeedbackPole(bg, p);
            var stations = UnityEngine.Object.FindObjectsByType<HumanCannonStation>(FindObjectsSortMode.None);
            var entry = stations.Single(s => s.name == "03_Cannon");
            FeedbackAim(entry, new Vector3(60, 4, 70), 2.25f);
            FeedbackTerrace(root, "B 장거리 첫 대포 정비대", new Vector3(61, 4, 69.5f), new Vector2(7, 7));
            FeedbackTerrace(root, "B 역방향 비행 도착대", new Vector3(34.5f, 1, 82.5f), new Vector2(6, 6));
            FeedbackTerrace(root, "B 그네 뒤 마지막 대포대", new Vector3(45, .5f, 82), new Vector2(6, 6));
            var relay = stations.Single(s => s.name == "B03_UpperRelayCannon");
            Undo.RecordObject(relay.transform, "Lengthen cannon relay"); relay.transform.position = new Vector3(62, 4, 68);
            FeedbackAim(relay, new Vector3(34.5f, 1, 82.5f), 2.3f);
            var last = CompletionCannon(root, "B03_FinalRelayCannon", new Vector3(45, .5f, 82), new Vector3(67, -6, 84), 2.05f);
            FeedbackAim(last, new Vector3(67, -6, 84), 2.05f);
            Ensure<HumanCannonLandingZone>(bg.GetComponentsInChildren<BoxCollider>().Single(c=>c.name=="아래층 도착부").gameObject);
            var preview = EditorSceneManager.OpenPreviewScene(OriginalCannonPath);
            try
            {
                var source = preview.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<HumanCannonPole>(true)).First();
                var p = OriginalPole(root, source, "B03_MidRelaySwing", new Vector3(38.5f, 3, 82.5f), new Vector3(1, 0, -.08f).normalized, 20);
                TuneFlowPole(root, p, p.Grip.position, 5, 38, 7.2f);
            }
            finally { EditorSceneManager.ClosePreviewScene(preview); }
            FusionFloorMarks(root, new Vector3(36.5f, 1.02f, 82.5f), Vector3.right);
            FusionFloorMarks(root, new Vector3(60, 4.02f, 70), new Vector3(2, 0, -2));
        }

        static void RefineCContinuousSwings(Transform bg, Transform root)
        {
            var second = UnityEngine.Object.FindObjectsByType<HumanCannonPole>(FindObjectsSortMode.None).Single(p => p.name == "C05_ExitTurningSwing");
            Undo.RecordObject(second.transform, "Continue swing chain over the chasm");
            second.transform.rotation = Quaternion.LookRotation(Vector3.forward);
            foreach (var part in bg.Find("31_FlowRefinement").Cast<Transform>().Where(t => t.name.StartsWith(second.name+"_", StringComparison.Ordinal)))
                part.gameObject.SetActive(false);
            TuneFlowPole(root, second, new Vector3(119, -1, 30), 5, 38, 7.2f);
            var preview = EditorSceneManager.OpenPreviewScene(OriginalCannonPath);
            try
            {
                var source = preview.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<HumanCannonPole>(true)).First();
                var p = OriginalPole(root, source, "C05_ChainThirdSwing", new Vector3(119, -2, 36), new Vector3(3, 0, 5).normalized, 20);
                TuneFlowPole(root, p, p.Grip.position, 5, 38, 7.2f);
                p = OriginalPole(root, source, "C05_ChainFinalSwing", new Vector3(122, -3.6f, 41), new Vector3(3, 0, 2.5f).normalized, 20);
                TuneFlowPole(root, p, p.Grip.position, 5, 38, 7.2f);
            }
            finally { EditorSceneManager.ClosePreviewScene(preview); }
            FusionFloorMarks(root, new Vector3(125, -7.98f, 43.5f), Vector3.back);
            BLight(root, new Vector3(117, 4, 36), new Vector3(121, -3, 38), 450, 20);
        }
    }
}
