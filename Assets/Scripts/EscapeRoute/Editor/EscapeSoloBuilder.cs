using System;
using System.Collections.Generic;
using System.Linq;
using CurtainCall.HumanCannon;
using CurtainCall.LevelPreview;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CurtainCall.EscapeRoute.EditorTools
{
    public static partial class EscapeBackgroundBuilder
    {
        const string SoloRoot = "EscapeSoloReview_A";
        const string HazardRoot = "25_FinaleHazards_A";

        [MenuItem("Tools/CurtainCall/탈출로/A 현재 씬 혼자 플레이 준비")]
        public static void BuildASoloReview()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath || EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("A 편집 씬이 필요합니다.");
            if (GameObject.Find(SoloRoot) != null) throw new InvalidOperationException("이미 연결됐습니다. 기존 설정을 직접 편집하세요.");
            var setup = GameObject.Find(TrickSetup).transform;
            var fusion = GameObject.Find(RootName + "/" + FusionGroup).transform;
            Undo.IncrementCurrentGroup(); int undo = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Current-scene solo escape rehearsal");
            var root = new GameObject(SoloRoot);
            Undo.RegisterCreatedObjectUndo(root,"Solo rehearsal");
            var body = Undo.AddComponent<CharacterController>(root);
            body.radius = .3f; body.height = 1.5f; body.center = Vector3.up * .75f;
            body.stepOffset = .3f; body.slopeLimit = 50; body.skinWidth = .025f;
            root.transform.position = new Vector3(15,.05f,10);
            var player = Undo.AddComponent<EscapeSoloReview>(root);
            player.view = Camera.main;
            player.view.fieldOfView = 78;
            var disabled = new List<Behaviour>();
            disabled.AddRange(player.view.GetComponents<EscapeRouteFlyCamera>());
            disabled.AddRange(player.view.GetComponents<LevelPreviewCamera>());
            disabled.AddRange(UnityEngine.Object.FindObjectsByType<HumanCannonSystem>(FindObjectsSortMode.None));
            player.suspendBehaviours = disabled.ToArray();
            // Disable the old camera immediately so its Start cannot reposition or hide a player later.
            foreach (var b in disabled) { Undo.RecordObject(b,"Solo camera ownership"); b.enabled = false; }
            player.suspendObjects = new[] { GameObject.Find("PlaySetup") };
            player.cannonMuzzle = GameObject.Find(CannonGameplay).GetComponentInChildren<HumanCannonStation>().Muzzle;
            player.cannonBoarding = setup.Find("03_CannonToIsland/Boarding_Feet");
            player.bounceSurface = setup.Find(FusionAnchors + "/Bounce_Surface");
            player.ropeStarts = new[] { setup.Find("03_TightropeToArrival/Entry_Feet"), setup.Find(FusionAnchors + "/RopeEntry_Feet") };
            player.ropeEnds = new[] { setup.Find("03_TightropeToArrival/Arrival_Feet"), setup.Find(FusionAnchors + "/RopeEnd_Feet") };
            var runner = GameObject.Find(RootName + "/" + RunnerGroup).transform;
            var points = new List<Transform>(); var pivots = new List<Transform>(); var visuals = new List<Transform>();
            var source05 = fusion.Find("05_RopeDropTrampolineSwing/04_DescendingSwing_LogicPending");
            for (int i=0;i<3;i++)
            {
                points.Add(setup.Find(FusionAnchors + "/Swing_" + (i+1) + "_Grip"));
                pivots.Add(setup.Find(FusionAnchors + "/Swing_" + (i+1) + "_Pivot"));
                var g = VerticalChild(source05,"MovingSwing_"+(i+1));
                foreach (Transform child in source05.Cast<Transform>().ToArray())
                    if (child.name.StartsWith("그네 " + (i+1) + " ",StringComparison.Ordinal)) Undo.SetTransformParent(child,g,"Swing visual group");
                visuals.Add(g);
            }
            var source07 = runner.Find("04_SwingChasm_276_298");
            for(int i=0;i<3;i++)
            {
                var grip = source07.GetComponentsInChildren<Transform>().First(t=>t.name=="Anchor_SwingGrip_"+(i+1));
                points.Add(grip);
                var pivot=VerticalChild(root.transform,"07_Pivot_"+(i+1)); pivot.position = new Vector3(grip.position.x,20,78); pivots.Add(pivot);
                var g = VerticalChild(source07,"MovingSwing_"+(i+1));
                foreach(Transform child in source07.Cast<Transform>().ToArray())
                    if ((child.name=="그네 케이블 설치선" || child.name=="그네 손잡이 설치 가이드") && Mathf.Abs(child.position.x-grip.position.x)<.1f)
                        Undo.SetTransformParent(child,g,"Swing visual group");
                visuals.Add(g);
            }
            player.grips = points.ToArray(); player.pivots = pivots.ToArray(); player.swingVisuals = visuals.ToArray();
            BuildFinaleHazards(player);
            EditorUtility.SetDirty(player);
            EditorSceneManager.MarkSceneDirty(scene);
            Undo.CollapseUndoOperations(undo);
        }

        static void BuildFinaleHazards(EscapeSoloReview player)
        {
            var background=GameObject.Find(RootName).transform;
            var root=VerticalChild(background,HazardRoot);
            root.localScale = new Vector3(1/background.lossyScale.x,1/background.lossyScale.y,1/background.lossyScale.z);
            var hazards=new List<EscapeSoloHazard>();
            var steel=AssetDatabase.LoadAssetAtPath<Material>(Materials+"brutal-steel.mat");
            var wall=AssetDatabase.LoadAssetAtPath<Material>(Materials+"brutal-recess.mat");
            var red=RevealMaterial("solo-danger-red",steel,new Color(.8f,.13f,.04f));
            var fire=RevealMaterial("solo-fire",steel,new Color(1,.24f,.015f));
            fire.EnableKeyword("_EMISSION"); fire.SetColor("_EmissionColor",new Color(2,.3f,.015f)); AssetDatabase.SaveAssetIfDirty(fire);
            var wind=RevealMaterial("solo-wind",steel,new Color(.25f,.5f,.55f));
            EscapeSoloHazard Add(string name, EscapeSoloHazard.Kind kind, Vector3 from, Vector3 to, Vector3 size, float min, float max, float travel=2, float phase=0)
            {
                var g=VerticalChild(root,name);
                var hazard=Undo.AddComponent<EscapeSoloHazard>(g.gameObject);
                hazard.player=player; hazard.kind=kind; hazard.origin=from; hazard.destination=to; hazard.size=size;
                hazard.activateMinX=min; hazard.activateMaxX=max; hazard.travelSeconds=travel; hazard.phase=phase;
                var view=VerticalChild(g,"MovingVisual"); view.position=from; hazard.visual=view;
                string prefab = kind==EscapeSoloHazard.Kind.Mace ? "PerformanceArtKit/SpikeMace.prefab" : kind==EscapeSoloHazard.Kind.CrossBlades ? "PerformanceArtKit/CrossBlades.prefab" : "HumanCannonArtKit/CannonSawDiscVisual.prefab";
                if(kind==EscapeSoloHazard.Kind.Drop) RevealBlock(view,"낙하 중량물",Vector3.zero,size,wall,false,false);
                else if(kind==EscapeSoloHazard.Kind.Fire)
                {
                    for(int i=0;i<7;i++)
                    {
                        var flame=GameObject.CreatePrimitive(PrimitiveType.Sphere); Undo.RegisterCreatedObjectUndo(flame,"Fire rehearsal visual");
                        flame.transform.SetParent(view,false); flame.transform.localPosition=new Vector3(0,0,(i-3)*size.z/7);
                        flame.transform.localScale=new Vector3(size.x, size.y*(i%2==0?1:.8f),size.z/5);
                        flame.GetComponent<Renderer>().sharedMaterial=fire; Undo.DestroyObjectImmediate(flame.GetComponent<Collider>());
                    }
                }
                else if(kind==EscapeSoloHazard.Kind.Wind)
                {
                    for(int i=0;i<5;i++) RevealBlock(view,"송풍 시각선",new Vector3((i-2)*1.2f,0,0),new Vector3(.05f,.05f,size.z),wind,false,false);
                }
                else
                {
                    var asset=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Prefabs/Objects/ETCs/"+prefab);
                    var art=(GameObject)PrefabUtility.InstantiatePrefab(asset,view); Undo.RegisterCreatedObjectUndo(art,"Existing obstacle art");
                    // Keep only the existing art instance. Rehearsal contacts belong to this component.
                    foreach(var b in art.GetComponentsInChildren<MonoBehaviour>(true)) Undo.DestroyObjectImmediate(b);
                    foreach(var c in art.GetComponentsInChildren<Collider>(true)) Undo.DestroyObjectImmediate(c);
                    var renders=art.GetComponentsInChildren<Renderer>(); var bounds=renders[0].bounds;
                    foreach(var r in renders) bounds.Encapsulate(r.bounds);
                    float diameter=kind==EscapeSoloHazard.Kind.CrossBlades?2.8f:2.2f;
                    art.transform.localScale *= diameter / Mathf.Max(bounds.size.x,bounds.size.y,bounds.size.z);
                    bounds=renders[0].bounds; foreach(var r in renders) bounds.Encapsulate(r.bounds);
                    art.transform.position += from-bounds.center;
                    if(kind==EscapeSoloHazard.Kind.GroundSaw) view.rotation=Quaternion.Euler(90,0,0);
                    if(kind==EscapeSoloHazard.Kind.FrontSaw) view.rotation=Quaternion.Euler(0,90,0);
                    hazard.radius=kind==EscapeSoloHazard.Kind.CrossBlades?1.4f:1.1f;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(art.transform);
                }
                Vector3 at=kind==EscapeSoloHazard.Kind.Drop?new Vector3(to.x,.04f,to.z):new Vector3((from.x+to.x)/2,.04f,(from.z+to.z)/2);
                Vector3 footprint=kind==EscapeSoloHazard.Kind.Drop?new Vector3(size.x,.025f,size.z):kind==EscapeSoloHazard.Kind.Fire||kind==EscapeSoloHazard.Kind.Wind?new Vector3(size.x,.025f,size.z):new Vector3(Mathf.Max(1,Mathf.Abs(from.x-to.x)),.025f,Mathf.Max(1,Mathf.Abs(from.z-to.z)));
                if(kind==EscapeSoloHazard.Kind.Fire||kind==EscapeSoloHazard.Kind.Wind) at=new Vector3(from.x,.04f,from.z);
                if(kind==EscapeSoloHazard.Kind.CrossBlades) { at=new Vector3(from.x,.04f,from.z); footprint=new Vector3(3,.025f,3); }
                hazard.warning=RevealBlock(g,"예고 범위",at,footprint,red,false,false).transform;
                hazards.Add(hazard); return hazard;
            }
            // The first 24m introduces the run. Each later wave leaves lateral space and a short recovery.
            Add("01_LeftSaw",EscapeSoloHazard.Kind.SideSaw,new Vector3(176,1,68),new Vector3(176,1,88),Vector3.one,155,186,2.1f);
            Add("02_RightSaw",EscapeSoloHazard.Kind.SideSaw,new Vector3(196,1,88),new Vector3(196,1,68),Vector3.one,179,207,2.1f);
            Add("03_FrontSaw",EscapeSoloHazard.Kind.FrontSaw,new Vector3(222,1.2f,78),new Vector3(201,1.2f,78),Vector3.one,199,225,2);
            Add("04_GroundSweep",EscapeSoloHazard.Kind.GroundSaw,new Vector3(216,.55f,72),new Vector3(219,.55f,83),Vector3.one,202,226,2.6f,.4f);
            Add("05_DropLeft",EscapeSoloHazard.Kind.Drop,new Vector3(237,8,74),new Vector3(237,1.5f,74),new Vector3(4.5f,3,4),223,246,1.05f);
            Add("06_DropRight",EscapeSoloHazard.Kind.Drop,new Vector3(253,8,82),new Vector3(253,1.5f,82),new Vector3(4.5f,3,4),240,263,1.05f,.35f);
            Add("07_FireLeft",EscapeSoloHazard.Kind.Fire,new Vector3(262,1.3f,74),Vector3.zero,new Vector3(3,2.6f,5),247,272,2.2f);
            Add("08_WindRight",EscapeSoloHazard.Kind.Wind,new Vector3(265,1.5f,79),new Vector3(0,0,-2.2f),new Vector3(6,3,10),255,272,2.2f);
            // Swing landing 298–316 remains clear. The final mixture is staggered along the next 50m.
            Add("09_Mace",EscapeSoloHazard.Kind.Mace,new Vector3(321,1.8f,72),new Vector3(321,1.8f,84),Vector3.one,310,330,2.8f);
            Add("10_RightSawFinal",EscapeSoloHazard.Kind.SideSaw,new Vector3(330,1,88),new Vector3(330,1,68),Vector3.one,316,340,2.1f,.5f);
            Add("11_FrontSawFinal",EscapeSoloHazard.Kind.FrontSaw,new Vector3(361,1.2f,79),new Vector3(337,1.2f,79),Vector3.one,330,365,2.4f);
            Add("12_DropFinal",EscapeSoloHazard.Kind.Drop,new Vector3(349,8,74),new Vector3(349,1.5f,74),new Vector3(4.5f,3,4),336,360,1.05f,.5f);
            Add("13_FireRight",EscapeSoloHazard.Kind.Fire,new Vector3(355,1.3f,82),Vector3.zero,new Vector3(3,2.6f,5),340,366,2.2f,.2f);
            Add("14_CrossBlades",EscapeSoloHazard.Kind.CrossBlades,new Vector3(362,1.5f,78),Vector3.zero,Vector3.one,348,368,2.6f);
            // Static old loads would overlap the moving copies; preserve them inactive, including their fixed straps.
            foreach(var t in GameObject.Find(RootName+"/"+RunnerGroup).GetComponentsInChildren<Transform>(true))
                if(t.name=="낙하 예정 중량물 설치 형상" || t.name=="중량물 묶음 밴드" || t.name=="호이스트 인양 케이블") PreserveFusionGuide(t);
            player.hazards=hazards.ToArray();
        }
    }
}
