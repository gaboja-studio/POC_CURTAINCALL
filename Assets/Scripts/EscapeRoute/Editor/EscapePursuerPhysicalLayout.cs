using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace CurtainCall.EscapeRoute.EditorTools
{
    public static class EscapePursuerPhysicalLayout
    {
        const string RootName = "Physical pursuit platforms";
        const string Prefab = "Assets/Resources/Prefabs/Characters/Enemies/UnderstagePursuer/EscapeAPursuer.prefab";
        static Transform frame, module, root;
        static Material concrete;
        [MenuItem("Tools/CurtainCall/탈출로/추격자 벽 돌파와 노출 플랫폼 적용")]
        public static void Apply()
        {
            var scene = SceneManager.GetActiveScene();
            if (Application.isPlaying || scene.path != EscapePursuerLayoutReview.SourceScene)
                throw new InvalidOperationException("탈출로 A 원본을 편집 모드로 여세요.");
            var route = Object.FindFirstObjectByType<EscapePursuerRoute>(FindObjectsInactive.Include);
            module = route.transform;
            frame = module.GetComponentsInParent<Transform>().Single(t => t.name == "EscapeCampaignFrame");
            if (module.Find(RootName)) throw new InvalidOperationException("이미 적용했습니다. 기존 배치를 검사하세요.");
            var chase = module.GetComponentInChildren<EscapeUnderstageChase>(true);
            var walls = module.GetComponent<EscapePursuerWalls>();
            concrete = frame.GetComponentsInChildren<MeshRenderer>(true)
                .First(r => r.name == "타설 콘크리트 벽" && !r.transform.IsChildOf(module)).sharedMaterial;
            root = new GameObject(RootName).transform; root.SetParent(module, false);
            // Obsolete brown galleries are no longer routes or fallback geometry.
            var screens = module.GetComponent<EscapePursuerScreens>();
            if (screens) Object.DestroyImmediate(screens);
            StripOldBypass();
            foreach (string name in new[]{"Service galleries", "Concealed maintenance route",
                         "Core pursuit inspection openings", "Cooperative service doorway"})
            {
                var old = module.Find(name);
                if (old) Object.DestroyImmediate(old.gameObject);
            }
            var points = route.Points;
            var staged = points.Select(p => p.feet.position).ToArray();
            var encounters = new List<EscapeUnderstageChase.PhysicalEncounter>();
            for (int gateIndex = 0; gateIndex < walls.GateCount; gateIndex++)
            {
                var gate = walls.At(gateIndex);
                int point = gate.routePoint;
                if (point == 0 || point >= 48) continue;
                int until = point switch { 2 => 4, 5 => 6, 7 => 7, 8 => 11, 12 => 13, 14 => 14,
                    15 => 21, 22 => 27, 28 => 31, 33 => 34, 34 => 46, _ => 73 };
                Vector3 normal = gate.panel.parent.TransformDirection(gate.outward).normalized;
                Vector3 wallFeet = gate.panel.position;
                wallFeet.y = staged[point].y;
                float thickness = Mathf.Abs(normal.x) * gate.barrier.bounds.extents.x + Mathf.Abs(normal.z) * gate.barrier.bounds.extents.z;
                Vector3 approach = wallFeet - normal * (thickness + 1.55f);
                Vector3 exit = wallFeet + normal * (thickness + 1.65f);
                Vector3 player = points[point].playerPath.position;
                Vector3 landing = player - normal * 3f;
                if (point == 34) landing = points[38].playerPath.position - normal * 3f;
                if (point == 47) landing = frame.TransformPoint(new Vector3(140, 0, 78));
                Vector3 center = landing;
                Vector3 size = new(8, .6f, 10);
                if (point == 12)
                {
                    Vector3 finish = points[13].playerPath.position - normal * 3f;
                    center = (landing + finish) * .5f;
                    size.z = Mathf.Abs(finish.z - landing.z) + 10;
                }
                if (point == 33)
                {
                    // A long ledge on the player side of the wall keeps the whole creature visible.
                    center = (points[32].playerPath.position + points[34].playerPath.position) * .5f - normal * 3f;
                    size.z = Mathf.Abs(points[34].playerPath.position.z - points[32].playerPath.position.z) + 10;
                    landing = new Vector3(landing.x, landing.y, points[32].playerPath.position.z + 2);
                    point = 32;
                }
                if (point == 34)
                {
                    center = (landing + points[46].playerPath.position - normal * 3f) * .5f;
                    size.z = Mathf.Abs(landing.z - points[46].playerPath.position.z) + 10;
                    point = 37;
                }
                Box("Pursuit platform " + gate.routePoint, center - Vector3.up * .3f, size);
                var area = new GameObject("Pursuit area " + gate.routePoint).AddComponent<BoxCollider>();
                area.transform.SetParent(root, false); area.transform.position = center;
                area.center = Vector3.up * 2.5f; area.size = new Vector3(size.x, 5, size.z); area.isTrigger = true;
                // Approaches stay behind the concrete. Only the impact opens the crossed panel.
                Floor("Breach approach " + gate.routePoint, staged[gate.routePoint], approach, 5);
                Floor("Breach threshold " + gate.routePoint, approach, exit, 5);
                var e = new EscapeUnderstageChase.PhysicalEncounter
                {
                    label = points[gate.routePoint].label, gate = gateIndex, routePoint = point, untilPoint = until,
                    staging = Anchor("Staging " + point, staged[gate.routePoint]), approach = Anchor("Wall contact " + point, approach),
                    exit = Anchor("Wall exit " + point, exit), landing = Anchor("Landing " + point, landing),
                    platform = area, continueOutside = point == 47
                };
                encounters.Add(e);
            }
            // The final run is now beside/behind the survivors, on the player side of the north wall.
            var routeData = new SerializedObject(route);
            var serializedPoints = routeData.FindProperty("points");
            for (int i = 47; i < points.Count; i++)
            {
                Vector3 p = frame.InverseTransformPoint(points[i].playerPath.position);
                p.x -= 6; p.z = 78;
                points[i].feet.position = frame.TransformPoint(p);
                PrefabUtility.RecordPrefabInstancePropertyModifications(points[i].feet);
                var row = serializedPoints.GetArrayElementAtIndex(i);
                row.FindPropertyRelative("exposed").boolValue = true;
                row.FindPropertyRelative("finalSprint").boolValue = true;
                row.FindPropertyRelative("label").stringValue = "후방 직접 추격 " + i;
                if (i == 47) continue;
                Vector3 previous = points[i-1].feet.position;
                float length = Vector3.Distance(previous, points[i].feet.position);
                bool leap = length > 9;
                row.FindPropertyRelative("jump").boolValue = leap;
                if (leap) Box("Pursuit landing " + i, points[i].feet.position - Vector3.up * .3f, new Vector3(8, .6f, 8));
                else Floor("Final pursuit platform " + i, previous, points[i].feet.position, 8);
            }
            routeData.ApplyModifiedPropertiesWithoutUndo();
            var lever = module.Find("Pursuer fire controls");
            if (lever) { lever.position = points[62].feet.position + frame.TransformDirection(Vector3.forward * 2.5f); PrefabUtility.RecordPrefabInstancePropertyModifications(lever); }
            // Only the early wall-side route is still used. The old hidden final floors are obsolete.
            var oldRoot = module.Find("Concrete wall routes");
            if (oldRoot)
                foreach (var t in oldRoot.GetComponentsInChildren<Transform>(true).Where(t=>t.name.StartsWith("Hidden concrete floor ")).ToArray())
                    if (int.TryParse(t.name.Substring("Hidden concrete floor ".Length), out int index) && index >= 47)
                        Object.DestroyImmediate(t.gameObject);
            var actor = new SerializedObject(chase);
            var items = actor.FindProperty("encounters"); items.arraySize = encounters.Count;
            for (int i = 0; i < encounters.Count; i++)
            {
                var p = items.GetArrayElementAtIndex(i); var e = encounters[i];
                p.FindPropertyRelative("label").stringValue = e.label;
                p.FindPropertyRelative("gate").intValue = e.gate; p.FindPropertyRelative("routePoint").intValue = e.routePoint;
                p.FindPropertyRelative("untilPoint").intValue = e.untilPoint; p.FindPropertyRelative("continueOutside").boolValue = e.continueOutside;
                p.FindPropertyRelative("staging").objectReferenceValue = e.staging;
                p.FindPropertyRelative("approach").objectReferenceValue = e.approach;
                p.FindPropertyRelative("exit").objectReferenceValue = e.exit;
                p.FindPropertyRelative("landing").objectReferenceValue = e.landing;
                p.FindPropertyRelative("platform").objectReferenceValue = e.platform;
            }
            actor.ApplyModifiedPropertiesWithoutUndo();
            var settings = AssetDatabase.LoadAssetAtPath<CurtainCall.Settings.EscapeRouteSettings>("Assets/Resources/Settings/GameSettings/EscapeRouteSettings.asset");
            settings.Pursuer.concealedRunSpeed = 24;
            settings.Pursuer.exposedGrabRange = 22;
            settings.Pursuer.encounterMinimumSeconds = 3;
            EditorUtility.SetDirty(settings); AssetDatabase.SaveAssetIfDirty(settings);
            Physics.SyncTransforms();
            var wallsData = new SerializedObject(module.GetComponent<EscapePursuerWalls>());
            var gates = wallsData.FindProperty("gates");
            for (int i = gates.arraySize - 1; i >= 0; i--)
            {
                var gate = gates.GetArrayElementAtIndex(i);
                if (gate.FindPropertyRelative("routePoint").intValue < 48) continue;
                ((Transform)gate.FindPropertyRelative("panel").objectReferenceValue).name = "Permanent concrete infill";
                Object.DestroyImmediate(((Transform)gate.FindPropertyRelative("debris").objectReferenceValue).gameObject);
                gates.DeleteArrayElementAtIndex(i);
            }
            wallsData.ApplyModifiedPropertiesWithoutUndo();
            RefineGeometry(route, encounters.ToArray());
            Save(scene);
        }
        public static void Refine()
        {
            var route = Object.FindFirstObjectByType<EscapePursuerRoute>(FindObjectsInactive.Include);
            module = route.transform; frame = module.GetComponentsInParent<Transform>().Single(t=>t.name=="EscapeCampaignFrame");
            root = module.Find(RootName);
            concrete = root.GetComponentsInChildren<MeshRenderer>().First().sharedMaterial;
            var chase = module.GetComponentInChildren<EscapeUnderstageChase>(true);
            var field = typeof(EscapeUnderstageChase).GetField("encounters", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            RefineGeometry(route, (EscapeUnderstageChase.PhysicalEncounter[])field.GetValue(chase));
            Save(SceneManager.GetActiveScene());
        }
        static void RefineGeometry(EscapePursuerRoute route, EscapeUnderstageChase.PhysicalEncounter[] encounters)
        {
            var settings = AssetDatabase.LoadAssetAtPath<CurtainCall.Settings.EscapeRouteSettings>("Assets/Resources/Settings/GameSettings/EscapeRouteSettings.asset");
            settings.Pursuer.exposedGrabRange = 22;
            EditorUtility.SetDirty(settings);
            foreach (var e in encounters)
            {
                int gatePoint = module.GetComponent<EscapePursuerWalls>().At(e.gate).routePoint;
                Vector3 p = frame.InverseTransformPoint(e.landing.position);
                if (e.routePoint == 14) p.x = 45;
                if (e.routePoint == 22) { p.x = 94; p.z = 35; }
                if (e.routePoint == 28) p.z = 35;
                if (e.routePoint == 32) { p.x = 122.5f; p.z = 72; }
                if (e.routePoint == 47) p = new Vector3(140, 0, 94);
                e.landing.position = frame.TransformPoint(p);
                var area = e.platform.transform;
                var center = e.landing.position;
                if (e.routePoint == 12) center = area.position;
                if (e.routePoint == 32) center = frame.TransformPoint(new Vector3(122.5f,-8,78));
                if (e.routePoint == 37) center = area.position;
                area.position = center;
                root.Find("Pursuit platform " + gatePoint).position = center - Vector3.up * .3f;
                var legs = new[]{e.staging.position,e.approach.position,e.exit.position,e.landing.position};
                for(int j=1;j<legs.Length;j++)
                {
                    if(j<3) EscapePursuerConcreteRoutes.OpenPhysicalPassage(frame,module,root,legs[j-1],legs[j]);
                    else
                    {
                        float arc=Mathf.Max(12f,Mathf.Abs(legs[j].y-legs[j-1].y)*4+4.4f);
                        Vector3 previous=legs[j-1];
                        for(int k=1;k<=12;k++)
                        {
                            float t=k/12f;var next=Vector3.Lerp(legs[j-1],legs[j],t)+Vector3.up*(arc*t*(1-t));
                            EscapePursuerConcreteRoutes.OpenPhysicalPassage(frame,module,root,previous,next);previous=next;
                        }
                    }
                }
            }
            foreach(var t in frame.GetComponentsInChildren<Transform>())
            {
                var p=frame.InverseTransformPoint(t.position);
                if(Mathf.Abs(p.x-116.4f)<.2f && (t.name=="오래된 시설 배관"||t.name=="배관 플랜지 이음"))
                {p.y=5;t.position=frame.TransformPoint(p);PrefabUtility.RecordPrefabInstancePropertyModifications(t);}
                if(Mathf.Abs(p.x-323)<.3f && t.name=="SM_Machinery_03_LOD0")
                {p.z=98.5f;t.position=frame.TransformPoint(p);PrefabUtility.RecordPrefabInstancePropertyModifications(t);}
            }
            // Rebuild only the platforms generated by this operation after moving the final run.
            foreach(var t in root.GetComponentsInChildren<Transform>().Where(t=>t.name.StartsWith("Final pursuit platform ")||t.name.StartsWith("Pursuit landing ")).ToArray())
                Object.DestroyImmediate(t.gameObject);
            for(int i=47;i<route.Points.Count;i++)
            {
                var p=frame.InverseTransformPoint(route.Points[i].playerPath.position);p.x-=6;p.z=i>=72?78:94;
                route.Points[i].feet.position=frame.TransformPoint(p);
                if(i==47)continue;
                var a=route.Points[i-1].feet.position;var b=route.Points[i].feet.position;
                if(route.Points[i].jump)Box("Pursuit landing "+i,b-Vector3.up*.3f,new Vector3(8,.6f,8));
                else Floor("Final pursuit platform "+i,a,b,8);
                EscapePursuerConcreteRoutes.OpenPhysicalPassage(frame,module,root,a,b);
            }
            Physics.SyncTransforms();
        }
        static Transform Anchor(string name, Vector3 position)
        {
            var t = new GameObject(name).transform; t.SetParent(root, false); t.position = position; return t;
        }
        static void StripOldBypass()
        {
            var old = module.Find("A03 Service Bypass");
            if (!old) return;
            old.name = "Cannon state anchors";
            var referenced = new HashSet<Transform>();
            var data = new SerializedObject(old.GetComponent<UnderstageCannonBypass>());
            var p = data.GetIterator();
            while (p.Next(true))
                if (p.propertyType == SerializedPropertyType.ObjectReference && p.objectReferenceValue is Transform t)
                    referenced.Add(t);
            foreach (var renderer in old.GetComponentsInChildren<MeshRenderer>(true))
            {
                var go = renderer.gameObject;
                if (!referenced.Contains(go.transform) && go.transform.childCount == 0) Object.DestroyImmediate(go);
                else
                {
                    Object.DestroyImmediate(renderer);
                    if (go.TryGetComponent<MeshFilter>(out var mesh)) Object.DestroyImmediate(mesh);
                    foreach (var collider in go.GetComponents<Collider>()) Object.DestroyImmediate(collider);
                }
            }
        }
        static GameObject Box(string name, Vector3 center, Vector3 size)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name;
            go.transform.SetParent(root, false); go.transform.position = center; go.transform.localScale = size;
            go.GetComponent<MeshRenderer>().sharedMaterial = concrete;
            return go;
        }
        static void Floor(string name, Vector3 from, Vector3 to, float width)
        {
            var delta = to - from;
            if (delta.magnitude < .01f) return;
            var floor = Box(name, (from + to) * .5f - Vector3.up * .3f, new Vector3(width, .6f, delta.magnitude + 1));
            floor.transform.rotation = Quaternion.LookRotation(delta);
        }
        static void Save(Scene scene)
        {
            var references = new List<(Component component, string path, Object value)>();
            foreach (var component in module.GetComponentsInChildren<Component>(true))
            {
                if (!component) continue;
                var so = new SerializedObject(component); var p = so.GetIterator();
                while (p.Next(true))
                {
                    if (p.propertyType != SerializedPropertyType.ObjectReference) continue;
                    var value = p.objectReferenceValue;
                    Transform t = value is Component c ? c.transform : value is GameObject g ? g.transform : null;
                    if (t && !EditorUtility.IsPersistent(t) && !t.IsChildOf(module)) references.Add((component,p.propertyPath,value));
                }
            }
            PrefabUtility.SaveAsPrefabAssetAndConnect(module.gameObject, Prefab, InteractionMode.AutomatedAction);
            foreach (var r in references)
            {
                var so = new SerializedObject(r.component); so.FindProperty(r.path).objectReferenceValue = r.value; so.ApplyModifiedPropertiesWithoutUndo();
            }
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            EditorSceneManager.SaveScene(scene, EscapeMppmReviewConfig.PursuerScene, true);
            AssetDatabase.SaveAssets();
        }
    }
}
