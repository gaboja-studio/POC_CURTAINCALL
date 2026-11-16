using System;
using System.Collections.Generic;
using System.Linq;
using CurtainCall.HumanCannon;
using CurtainCall.Tightrope;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace CurtainCall.EscapeRoute.EditorTools
{
    public static partial class EscapeBackgroundBuilder
    {
        public const string LabyrinthScenePath = "Assets/Scenes/Levels/escape_route.unity";
        internal const string LabyrinthCopyPath = "Assets/Scenes/Tests/EscapeRoute/Pursuer/EscapeRouteLabyrinthPrototype.unity";
        const string LabyrinthRoot = "70_LabyrinthRemake";
        static Transform labyrinthFrame, labyrinthRoot;
        static readonly List<BoxCollider> labyrinthRopes = new();
        static readonly List<EscapePursuerRoute.PlayerGuide> labyrinthGuides = new();

        [MenuItem("Tools/CurtainCall/탈출로/미궁 리메이크 복사 씬 생성")]
        public static void BuildLabyrinthCopy()
        {
            var scene = SceneManager.GetActiveScene();
            if (Application.isPlaying || scene.isDirty || scene.path != EscapePursuitRemodel.ScenePath)
                throw new InvalidOperationException("저장된 EscapeRoutePursuitRemodel 원본의 편집 모드가 필요합니다.");
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(LabyrinthCopyPath))
                throw new InvalidOperationException("리메이크 복사 씬이 이미 있습니다. 원본이나 기존 작업을 덮어쓰지 않습니다.");
            if (!AssetDatabase.CopyAsset(scene.path, LabyrinthCopyPath))
                throw new InvalidOperationException("씬 복사 실패");
            scene = EditorSceneManager.OpenScene(LabyrinthCopyPath);
            labyrinthFrame = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true))
                .Single(t => t.name == "EscapeCampaignFrame");
            var background = labyrinthFrame.Find("EscapeBackground");
            if (PrefabUtility.IsPartOfPrefabInstance(labyrinthFrame.gameObject))
                PrefabUtility.UnpackPrefabInstance(labyrinthFrame.gameObject, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            labyrinthRoot = new GameObject(LabyrinthRoot).transform;
            labyrinthRoot.SetParent(background, false);
            labyrinthRoot.localScale=new Vector3(1/background.lossyScale.x,1/background.lossyScale.y,1/background.lossyScale.z);
            // Earlier builders predate the resource folder migration. Reuse current factory materials.
            Material Factory(string name) => AssetDatabase.LoadAssetAtPath<Material>("Assets/Scenes/Levels/EscapeFactoryArt/factory-" + name + ".mat")
                ?? throw new InvalidOperationException("Missing factory material " + name);
            bConcrete=Factory("concrete"); bRecess=Factory("recess"); bSteel=Factory("steel");
            wornSteel=bSteel; wornPipe=Factory("pipe"); maintenancePaint=Factory("safety-ochre"); floorWear=Factory("floor");
            bBulb=AssetDatabase.LoadAssetAtPath<Material>("Assets/Resources/Sounds/EscapeRouteMaterials/concrete-service-bulb.mat");
            labyrinthRopes.Clear(); labyrinthGuides.Clear();
            ResetLabyrinthPoles();
            var route = labyrinthFrame.GetComponentInChildren<EscapePursuerRoute>(true);
            // Preserve the reviewed course's original edges, then explicitly add independent arms.
            for (int i = 1; i < route.Points.Count; i++)
                labyrinthGuides.Add(new EscapePursuerRoute.PlayerGuide { from = route.Points[i-1].playerPath,
                    to = route.Points[i].playerPath, fromProgress = i-1, toProgress = i });

            BuildLabyrinthHalls(route);
            BuildLabyrinthEntrance(route);
            BuildLabyrinthInspectionArm(route);
            BuildLabyrinthHeatBypass(route);
            BuildLabyrinthUpperChoice(route);
            LCentralEncounter(route);
            WidenLabyrinthConveyorBay(route);
            BuildLabyrinthFinale(route);
            BuildLabyrinthNarrative();
            BuildLabyrinthPolish();
            RegisterLabyrinthDevices(route);
            RegisterLabyrinthPoles();
            LCarveNewDrops();
            LCarvePoleDrops();
            LTrimFloorSeams();
            LRemoveDuplicateBoxes();
            Physics.SyncTransforms();
            UnderstageChaseNavigation.Bake();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        static Vector3 LW(Vector3 local) => labyrinthFrame.TransformPoint(local);
        static Transform LAnchor(string name, Vector3 local)
        {
            var t = new GameObject(name).transform; t.SetParent(labyrinthRoot, false);
            t.SetPositionAndRotation(LW(local), labyrinthFrame.rotation); return t;
        }
        static BoxCollider LBox(string name, Vector3 center, Vector3 size, Material material, bool collision = true)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name;
            go.transform.SetParent(labyrinthRoot, false);
            go.transform.SetPositionAndRotation(LW(center), labyrinthFrame.rotation);
            go.transform.localScale = size; go.GetComponent<MeshRenderer>().sharedMaterial = material;
            var box = go.GetComponent<BoxCollider>();
            if (!collision) { Object.DestroyImmediate(box); return null; }
            return box;
        }
        static BoxCollider LFloor(string name, Vector3 top, float width, float depth)
        {
            var floor = LBox(name, top - Vector3.up * .4f, new(width, .8f, depth), bConcrete);
            floor.gameObject.AddComponent<HumanCannonLandingZone>();
            return floor;
        }
        static void LOpen(EscapePursuerRoute route, Vector3 center, Vector3 size)
            => EscapePursuerConcreteRoutes.OpenPhysicalVolume(labyrinthFrame, route.transform, labyrinthRoot, new Bounds(center, size));
        static BoxCollider LRope(string name, Vector3 from, Vector3 to)
        {
            float length=Vector3.Distance(from,to);
            var t=LAnchor(name,from); t.rotation=Quaternion.LookRotation(LW(to)-LW(from));
            var rope=t.gameObject.AddComponent<BoxCollider>();
            var shared=CurtainCall.Settings.GameSettings.Tightrope.Course;
            rope.center=new(0,-shared.RopeThickness*.5f,length*.5f);
            rope.size=new(shared.RopeWalkWidth,shared.RopeThickness,length);
            for(int i=0;i<12;i++)
            {
                Vector3 Point(float u)=>Vector3.Lerp(from,to,u)+Vector3.up*(.05f-.08f*Mathf.Sin(u*Mathf.PI));
                var a=Point(i/12f); var b=Point((i+1)/12f);
                var art=GameObject.CreatePrimitive(PrimitiveType.Cylinder); art.name=name+" cable "+i;
                Object.DestroyImmediate(art.GetComponent<Collider>()); art.transform.SetParent(labyrinthRoot,false);
                art.transform.SetPositionAndRotation(LW((a+b)*.5f),Quaternion.FromToRotation(Vector3.up,LW(b)-LW(a)));
                art.transform.localScale=new(.075f,Vector3.Distance(a,b)*.5f,.075f);
                art.GetComponent<MeshRenderer>().sharedMaterial=bSteel;
            }
            foreach(var end in new[]{from,to}) LBox(name+" tension socket",end+Vector3.up*.045f,new(.35f,.09f,.35f),bSteel,false);
            rope.gameObject.AddComponent<HumanCannonLandingZone>(); labyrinthRopes.Add(rope);
            return rope;
        }
        static void LGuide(string name, Vector3 from, Vector3 to, float start, float end)
        {
            labyrinthGuides.Add(new EscapePursuerRoute.PlayerGuide { from = LAnchor(name + " from", from),
                to = LAnchor(name + " to", to), fromProgress = start, toProgress = end });
        }
        static HumanCannonStation LCannon(string name,Vector3 at,Vector3 target,float seconds)
        {
            var source=labyrinthFrame.GetComponentsInChildren<HumanCannonStation>().Single(s=>s.name=="03_Cannon");
            var copy=Object.Instantiate(source.gameObject,labyrinthRoot);copy.name=name;
            copy.transform.SetPositionAndRotation(LW(at),Quaternion.LookRotation(Vector3.ProjectOnPlane(LW(target)-LW(at),Vector3.up)));
            copy.transform.localScale=Vector3.one;
            var station=copy.GetComponent<HumanCannonStation>();AimCompletionCannon(station,LW(target),seconds);return station;
        }
        static void LLamp(string name, Vector3 local, Vector3 target, float intensity = 150)
        {
            LBox(name + " fixed wall mount", local + Vector3.up * .25f, new(.55f,.5f,.4f), bSteel, false);
            if(local.y > target.y + .5f)
                LBox(name + " fixed steel upright", new(local.x,(local.y+target.y)*.5f,local.z),
                    new(.14f,local.y-target.y,.14f),wornSteel);
            var t = LAnchor(name, local);
            t.rotation = Quaternion.LookRotation(LW(target)-t.position);
            var light = t.gameObject.AddComponent<Light>(); light.type = LightType.Spot;
            light.color = new Color(.84f,.87f,.79f); light.intensity = intensity;
            light.range = 20; light.spotAngle = 78; light.innerSpotAngle = 48; light.shadows = LightShadows.None;
        }
        static void LGuard(string name, Vector3 from, Vector3 to)
        {
            Vector3 delta = to-from; float length = delta.magnitude;
            var rail = LBox(name, (from+to)*.5f+Vector3.up*.8f, new(.14f,.15f,length), wornSteel);
            rail.transform.rotation = labyrinthFrame.rotation * Quaternion.LookRotation(delta);
            int count = Mathf.CeilToInt(length/2.5f);
            for (int i=0;i<=count;i++) LBox(name+" post "+i, Vector3.Lerp(from,to,(float)i/count)+Vector3.up*.4f,
                new(.12f,.8f,.12f), wornSteel);
        }

        static void BuildLabyrinthInspectionArm(EscapePursuerRoute route)
        {
            // A new arm leaves the rope at its actual end. Nothing is placed below the bar descent.
            LOpen(route, new(63,-.5f,80), new(9,11,12));
            LOpen(route, new(85,-.5f,84), new(39,11,13));
            LRope("Labyrinth 02 inspection fork", new(60,-6,75), new(64.3f,-6,84.3f));
            LFloor("02 inspection decision apron", new(67,-6,88), 6, 8);
            LRope("Labyrinth 02 inspection approach cable", new(70,-6,84), new(75.5f,-6,84));
            LFloor("02 inspection swing departure", new(77.5f,-6,84), 4, 5);
            LPole("Labyrinth Inspection swing 01", new(82,-4.5f,86), new(6,0,3));
            LPole("Labyrinth Inspection swing 02", new(88,-3.5f,89), new(5.4f,0,-3));
            LPole("Labyrinth Inspection swing 03", new(93.4f,-4.5f,86), new(6.6f,0,-3));
            LFloor("02 inspection relay apron", new(100,-6,83), 8, 14);
            // Rejoin the same conveyor observation bay. This arm no longer skips the
            // conveyor, cooperative transport, collapsing wires and the flame approach.
            LCutNamedBox("Concrete remainder",new Bounds(new(88.3f,15,49.5f),new(5,8,11)));
            LCutNamedBox("Concrete remainder",new Bounds(new(77.5f,-13.5f,18),new(5,8,5)));
            LCannon("Labyrinth_02_InspectionRelay", new(100,-6,79), new(76,-22,14), 4.5f);
            LGuard("Inspection north rail", new(64,-6,92), new(70,-6,92));
            LGuard("Relay east rail", new(104,-6,76), new(104,-6,90));
            LGuide("02 inspection fork", new(60,-6,75), new(67,-6,84), 3, 4);
            LGuide("02 inspection approach", new(67,-6,84), new(78,-6,84), 4, 5);
            LGuide("02 inspection upward swing",new(78,-6,84),new(88,-5,89),5,6);
            LGuide("02 inspection downward swing",new(88,-5,89),new(100,-6,83),6,7);
            LGuide("02 inspection relay", new(100,-6,79), new(76,-22,14), 7, 8);
            LLamp("Inspection fixed worklight", new(67,-.8f,92), new(69,-6,85), 110);
            LLamp("Relay fixed worklight", new(104,-.8f,83), new(100,-6,81), 135);
            // Visible fixed beam and columns terminate above the drop; never form walkable stairs.
            LBox("Inspection overhead crossbeam", new(85,3.5f,90), new(42,1.1f,1.2f), bConcrete);
            LBox("Inspection anchored west pier", new(63,-22,90), new(2,52,2), bConcrete);
            LBox("Inspection anchored east pier", new(105,-22,90), new(2,52,2), bConcrete);
            LInspectionEncounter(route);
            LForkEncounter(route);
        }

        static void BuildLabyrinthHeatBypass(EscapePursuerRoute route)
        {
            // The processing room can lead through heat below, or along an exterior cable gallery.
            LOpen(route, new(136,4.5f,38), new(17,9,16));
            LOpen(route, new(140,4.5f,59), new(7,9,30));
            LOpen(route,new(148,4,55),new(11,10,25));
            // The two live nozzle banks flank this maintenance opening. Their shared
            // hazard/effect references remain intact; only the central solid casing opens.
            LCutNamedBox("벽체 매립 대형 분사함",new Bounds(new(150,2.5f,57.05f),new(6.2f,9,1)));
            LCutNamedBox("Entry platform side guard",new Bounds(new(125.15f,2.5f,32),new(1,5,3.1f)));
            LCutNamedBox("Switchback east retaining wall",new Bounds(new(129.75f,4,33),new(1,8,4)));
            LCutNamedBox("압축 출발부 남측 기초",new Bounds(new(140,3,67),new(5.5f,6,8)));
            LFloor("04 processing east choice apron", new(128,0,32), 6, 6);
            LCannon("Labyrinth_04_ExtractionLaunch",new(128,0,32),new(140,0,46),2.6f);
            LFloor("06 extraction corner apron", new(140,0,47), 5, 6);
            LPole("Labyrinth Extraction swing 01",new(146,1.5f,47),new(5,0,4.5f));
            LPole("Labyrinth Extraction swing 02",new(151,2.2f,51.5f),new(-3,0,6));
            LPole("Labyrinth Extraction swing 03",new(148,1.5f,57.5f),new(-1,0,5));
            // Receive both seats and a useful range of release angles. The open west
            // face leads to the cable; solid room returns prevent a trivial rear hop.
            LFloor("06 extraction turn island",new(147,0,64.75f),4,8.5f);
            LBox("Extraction turn room rear wall",new(147,4.5f,69.15f),new(4.6f,9,.3f),bConcrete);
            LBox("Extraction turn room east wall",new(149.15f,4.5f,64.75f),new(.3f,9,8.5f),bConcrete);
            LBox("Extraction turn room west return",new(144.85f,4.5f,66.75f),new(.3f,9,4.5f),bConcrete);
            LHallMountedLamp("Extraction turn room exit task light",new(147,6,68.85f),new(143.5f,0,62.5f),110);
            LRope("Labyrinth 06 extraction return cable",new(145,0,62.5f),new(141,0,62.5f));
            LFloor("06 extraction final swing departure",new(140,0,61.8f),2,2);
            LPole("Labyrinth Extraction swing 04",new(140,1.5f,65.4f),Vector3.forward);
            LFloor("06 extraction south join", new(140,0,69.5f), 5, 3);
            LFloor("06 flush western return",new(138.75f,0,72.5f),2.5f,3);
            // Existing east floor starts at Z71; the west A07 landing begins at Z74.
            LGuide("04 extraction diagonal", new(123,0,32), new(140,0,47), 29, 38);
            LGuide("06 extraction outward swing",new(140,0,47),new(151,.7f,51.5f),38,40);
            LGuide("06 extraction inward swing",new(151,.7f,51.5f),new(147,0,62.5f),40,42);
            LGuide("06 extraction cable",new(147,0,62.5f),new(140,0,62.5f),42,43);
            LGuide("06 extraction last swing",new(140,0,62.5f),new(140,0,72),43,44);
            LGuide("06 extraction join", new(140,0,72), new(146,0,78), 44, 47);
            LGuard("Extraction outside corner guard", new(137.5f,0,44), new(137.5f,0,50));
            LLamp("Extraction corner worklight", new(144,5,44), new(140,0,47), 110);
            LBox("Extraction lamp concrete fin", new(144,-12,44), new(1,36,2), bConcrete);
            // An opaque, continuous process divider makes the adjacent upper apron a
            // visibly separate room, rather than a free landing that skips the whole arm.
            LBox("Labyrinth extraction full-height process divider",new(154,-71,57),new(1,162,18),bConcrete);
            LHeatEncounter(route);
        }

        static void BuildLabyrinthUpperChoice(EscapePursuerRoute route)
        {
            // Exposed spans trade cooperative machinery for balance risk. Each remains below
            // the existing shared course's 28 m finish distance; no special traversal rule.
            LOpen(route, new(159,4.5f,64), new(8,9,25));
            LOpen(route, new(202,4.5f,54), new(86,9,8));
            LOpen(route, new(244,4.5f,64), new(8,9,25));
            LCutNamedBox("압축 출발부 남측 기초",new Bounds(new(159,3,67),new(6.3f,6,8)));
            LFloor("06 western transfer apron", new(159,0,61.5f), 6, 19);
            LRope("Labyrinth 06 transfer cable west", new(162,0,54), new(187,0,54));
            LFloor("06 isolated transfer pier", new(190,0,54), 6, 8);
            LPole("Labyrinth Transfer swing 01",new(196,1.5f,54),Vector3.right);
            LPole("Labyrinth Transfer swing 02",new(202,1.5f,54),Vector3.right);
            LPole("Labyrinth Transfer swing 03",new(208,1.5f,54),Vector3.right);
            LFloor("06 second transfer pier", new(214,0,54),6,8);
            LRope("Labyrinth 06 transfer cable east", new(217,0,54), new(241,0,54));
            LFloor("06 eastern transfer apron", new(244,0,61.5f), 6, 19);
            LClearTransferApproach();
            LOpen(route,new(177,3.25f,64),new(9,5.5f,14));
            // Keep the two live nozzles on each side. The middle maintenance apertures
            // reveal the real public pursuit deck without exposing its hidden service route.
            foreach(float x in new[]{190f,205f,230f})
            {
                LCutNamedBox("벽체 매립 대형 분사함",new Bounds(new(x,3.25f,57.05f),new(6.2f,5.5f,1)));
                LOpen(route,new(x,3.25f,64),new(6.2f,5.5f,14));
                foreach(float dx in new[]{-3.1f,3.1f})LBox("Transfer machine inspection jamb",new(x+dx,3.25f,57.05f),new(.15f,5.5f,.3f),wornSteel,false);
                foreach(float y in new[]{.5f,6f})LBox("Transfer machine inspection lintel",new(x,y,57.05f),new(6.2f,.15f,.3f),wornSteel,false);
            }
            LGuide("06 transfer departure", new(159,0,78), new(159,0,54), 48, 49);
            LGuide("06 transfer west", new(159,0,54), new(190,0,54), 49, 53);
            LGuide("06 transfer middle",new(190,0,54),new(214,0,54),53,55);
            LGuide("06 transfer east", new(214,0,54), new(244,0,54), 55, 57);
            LGuide("06 transfer rejoin", new(244,0,54), new(244,0,78), 57, 58);
            LGuard("Transfer west edge", new(156,0,52), new(156,0,71));
            LGuard("Transfer east edge", new(247,0,52), new(247,0,71));
            foreach(float x in new[]{190f,214f})
            {
                LBox("Transfer anchored pier shaft "+x, new(x,-34,54), new(5,67.2f,6), bConcrete);
                LLamp("Transfer island worklight "+x, new(x,5,58), new(x,0,54), 95);
                LBox("Transfer island light support "+x, new(x,2.5f,58), new(.4f,5,.4f), wornSteel);
            }
            LLamp("Transfer west entrance worklight",new(156,5,64),new(159,0,56),130);
            LLamp("Transfer east entrance worklight",new(247,5,64),new(244,0,56),130);
        }

        static void WidenLabyrinthConveyorBay(EscapePursuerRoute route)
        {
            // Keep the original shared cannon and its timing protection. Add room only behind its mouth.
            LOpen(route, new(72,-15.5f,9), new(24,13,16));
            LFloor("03 conveyor eastern decision apron", new(76,-22,9.25f), 10, 17.5f);
            LGuide("03 conveyor observation apron", new(67,-22,9), new(79,-22,9), 8, 8);
            LGuard("Conveyor side apron end rail", new(81,-22,.5f), new(81,-22,18));
            LGuard("Conveyor side apron rear rail", new(71,-22,18), new(81,-22,18));
            LLamp("Conveyor observation worklight", new(80,-16.5f,16), new(76,-22,9), 100);
            var chase = labyrinthFrame.GetComponentInChildren<EscapeUnderstageChase>(true);
            var so = new SerializedObject(chase);
            var waiting = so.FindProperty("conveyorWaitDeck").objectReferenceValue as BoxCollider;
            if (waiting) { waiting.transform.SetPositionAndRotation(LW(new(72,-21.35f,9.25f)),labyrinthFrame.rotation); waiting.center=Vector3.zero; waiting.size=new(17.8f,2,17.3f); }
        }

        static void BuildLabyrinthNarrative()
        {
            LStory("01 human intake", new(25,0,33), 90, EscapeLabyrinthStory.StoryStage.Intake);
            LStory("02 inspection restraints", new(87,-6,93), 0, EscapeLabyrinthStory.StoryStage.Intake);
            LStory("04 conversion works", new(85.5f,-5,34), 0, EscapeLabyrinthStory.StoryStage.Processing);
            LStory("06 unknown production", new(249,0,87), 0, EscapeLabyrinthStory.StoryStage.Output);
            LStory("07 rejected shipment", new(350,0,92), 0, EscapeLabyrinthStory.StoryStage.Output);
        }
        static void LStory(string name, Vector3 at, float yaw, EscapeLabyrinthStory.StoryStage stage)
        {
            var root=LAnchor(name,at); root.rotation=labyrinthFrame.rotation*Quaternion.Euler(0,yaw,0);
            EscapeLabyrinthStory.Build(root,bConcrete,wornSteel,maintenancePaint,stage);
            var slab = GameObject.CreatePrimitive(PrimitiveType.Cube); slab.name=name+" sealed machinery shelf";
            slab.transform.SetParent(root,false); slab.transform.localPosition=new(0,-.5f,2.8f); slab.transform.localScale=new(15,1,5.6f);
            slab.GetComponent<MeshRenderer>().sharedMaterial=bConcrete;
            // Cosmetic machinery is visibly enclosed, including its sides and rear.
            // A front railing alone leaves a walk-in route around the display's ends.
            var sill=GameObject.CreatePrimitive(PrimitiveType.Cube); sill.name=name+" inaccessible machine sill";
            sill.transform.SetParent(root,false); sill.transform.localPosition=new(0,.45f,.3f); sill.transform.localScale=new(15,.9f,.5f);
            sill.GetComponent<MeshRenderer>().sharedMaterial=bRecess;
            for(int i=0;i<=25;i++)
            {
                var bar=GameObject.CreatePrimitive(PrimitiveType.Cube);bar.name=name+" machine guard bar "+i;bar.transform.SetParent(root,false);
                bar.transform.localPosition=new(-7.5f+i*.6f,4.9f,.3f);bar.transform.localScale=new(.1f,8f,.13f);
                bar.GetComponent<MeshRenderer>().sharedMaterial=bSteel;
            }
            var rail=GameObject.CreatePrimitive(PrimitiveType.Cube);rail.name=name+" machine guard lintel";rail.transform.SetParent(root,false);
            rail.transform.localPosition=new(0,9,.3f);rail.transform.localScale=new(15.6f,.2f,.4f);rail.GetComponent<MeshRenderer>().sharedMaterial=bSteel;
            void Shell(string label,Vector3 position,Vector3 size)
            {
                var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.name=name+" sealed machinery "+label;
                wall.transform.SetParent(root,false);wall.transform.localPosition=position;wall.transform.localScale=size;
                wall.GetComponent<MeshRenderer>().sharedMaterial=bConcrete;
            }
            Shell("left return",new(-7.65f,4.55f,2.8f),new(.3f,9.1f,5.6f));
            Shell("right return",new(7.65f,4.55f,2.8f),new(.3f,9.1f,5.6f));
            Shell("rear wall",new(0,4.55f,5.75f),new(15.6f,9.1f,.3f));
            Shell("roof",new(0,9.25f,3),new(15.6f,.3f,6.2f));
            foreach(float x in new[]{-3.5f,3.5f})
            {
                var housing=GameObject.CreatePrimitive(PrimitiveType.Cube);housing.name=name+" gantry task lamp";
                Object.DestroyImmediate(housing.GetComponent<Collider>());housing.transform.SetParent(root,false);
                housing.transform.localPosition=new(x,5.32f,2.5f);housing.transform.localScale=new(.7f,.16f,.35f);housing.GetComponent<MeshRenderer>().sharedMaterial=bSteel;
                var clamp=GameObject.CreatePrimitive(PrimitiveType.Cube);clamp.name=name+" task lamp rail clamp";
                Object.DestroyImmediate(clamp.GetComponent<Collider>());clamp.transform.SetParent(root,false);
                clamp.transform.localPosition=new(x,5.44f,2.5f);clamp.transform.localScale=new(.12f,.16f,.16f);clamp.GetComponent<MeshRenderer>().sharedMaterial=bSteel;
                var lamp=new GameObject("Gantry mounted task light").AddComponent<Light>();lamp.transform.SetParent(root,false);
                lamp.transform.localPosition=new(x,5.22f,2.5f);lamp.transform.localRotation=Quaternion.Euler(90,0,0);
                lamp.type=LightType.Spot;lamp.spotAngle=95;lamp.innerSpotAngle=65;lamp.range=8;lamp.intensity=45;
                lamp.color=new Color(.86f,.88f,.8f);lamp.shadows=LightShadows.None;
            }
        }
        static void RegisterLabyrinthDevices(EscapePursuerRoute route)
        {
            var course=labyrinthFrame.GetComponentInChildren<TightropeCourse>(true);
            var data=new SerializedObject(course); var ropes=data.FindProperty("sceneRopes");
            int original=ropes.arraySize; ropes.arraySize+=labyrinthRopes.Count;
            for(int i=0;i<labyrinthRopes.Count;i++) ropes.GetArrayElementAtIndex(original+i).objectReferenceValue=labyrinthRopes[i];
            data.FindProperty("independentSceneRopes").boolValue=true;
            data.FindProperty("sceneShape").FindPropertyRelative("laneCount").intValue=ropes.arraySize;
            var ends=data.FindProperty("sceneShape").FindPropertyRelative("laneEndDistances"); ends.arraySize=ropes.arraySize;
            for(int i=0;i<labyrinthRopes.Count;i++) ends.GetArrayElementAtIndex(original+i).floatValue=labyrinthRopes[i].size.z;
            data.ApplyModifiedPropertiesWithoutUndo(); course.Rebuild();
            var cannon=labyrinthFrame.GetComponentInChildren<HumanCannonSystem>(true);
            data=new SerializedObject(cannon); var stations=data.FindProperty("stations");
            var additions=labyrinthRoot.GetComponentsInChildren<HumanCannonStation>().OrderBy(s=>s.name,StringComparer.Ordinal).ToArray();
            int old=stations.arraySize; stations.arraySize+=additions.Length;
            for(int i=0;i<additions.Length;i++) stations.GetArrayElementAtIndex(old+i).objectReferenceValue=additions[i];
            data.ApplyModifiedPropertiesWithoutUndo();
            data=new SerializedObject(route); var guides=data.FindProperty("playerGuides"); guides.arraySize=labyrinthGuides.Count;
            for(int i=0;i<labyrinthGuides.Count;i++)
            {
                var row=guides.GetArrayElementAtIndex(i); var g=labyrinthGuides[i];
                row.FindPropertyRelative("from").objectReferenceValue=g.from; row.FindPropertyRelative("to").objectReferenceValue=g.to;
                row.FindPropertyRelative("fromProgress").floatValue=g.fromProgress; row.FindPropertyRelative("toProgress").floatValue=g.toProgress;
            }
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        static void LRemoveDuplicateBoxes()
        {
            Physics.SyncTransforms();
            var seen=new HashSet<string>();
            foreach(var box in labyrinthRoot.GetComponentsInChildren<BoxCollider>().Where(c=>!c.isTrigger).ToArray())
            {
                string key=box.bounds.center.ToString("F3")+box.bounds.size.ToString("F3")+box.transform.rotation.ToString("F3");
                if(!seen.Add(key) && box.GetComponents<Component>().All(c=>c is Transform || c is MeshFilter || c is MeshRenderer || c is BoxCollider))
                    Object.DestroyImmediate(box.gameObject);
            }
        }
        static void LClearTransferApproach()
        {
            // These are decorative instances, not the live falling-load hazards. Preserve the
            // source scene and remove whole connected scenery roots in the new public doorway.
            var volume=new Bounds(LW(new(244,2,65)),new(5,5,24));
            foreach(var collider in labyrinthFrame.GetComponentsInChildren<Collider>().Where(c=>!c.isTrigger&&volume.Intersects(c.bounds)).ToArray())
            {
                if(!collider)continue;
                if(collider.name=="이탈한 무거운 콘크리트 조각"||collider.name=="잘게 깨진 바닥 가장자리")
                    Object.DestroyImmediate(collider.gameObject);
                else if(collider.name=="SM_Machinery_06_LOD0")
                {
                    var assembly=collider.GetComponentsInParent<Transform>().FirstOrDefault(t=>t.name=="하부 집진 설비");
                    if(assembly&&assembly.GetComponentsInChildren<MonoBehaviour>(true).Length==0)Object.DestroyImmediate(assembly.gameObject);
                }
            }
        }
        static void LCarveNewDrops()
        {
            foreach(var rope in labyrinthRopes)
            {
                var a=labyrinthFrame.InverseTransformPoint(rope.transform.TransformPoint(rope.center+new Vector3(0,rope.size.y*.5f,-rope.size.z*.5f)));
                var b=labyrinthFrame.InverseTransformPoint(rope.transform.TransformPoint(rope.center+new Vector3(0,rope.size.y*.5f,rope.size.z*.5f)));
                var direction=(b-a).normalized;a+=direction*1.55f;b-=direction*1.55f;
                var clear=new Bounds((a+b)*.5f-Vector3.up*14.95f,new Vector3(Mathf.Abs(b.x-a.x)+2.4f,30,Mathf.Abs(b.z-a.z)+2.4f));
                // Only trim static wall remnants produced by the room openings. Gameplay floors,
                // existing cooperative islands, hazards and shared mechanism references are retained.
                LCutNamedBox("Concrete remainder",clear);
            }
        }
        static void LTrimFloorSeams()
        {
            Physics.SyncTransforms();
            foreach(var floor in labyrinthRoot.GetComponentsInChildren<HumanCannonLandingZone>())
            {
                var box=floor.GetComponent<BoxCollider>();
                if(!box||!box.GetComponent<MeshRenderer>())continue;
                var bounds=new Bounds(labyrinthFrame.InverseTransformPoint(box.bounds.center),box.bounds.size);
                LCutNamedBox("Concrete remainder",bounds,bounds.max.y);
            }
        }
        static void LCarvePoleDrops()
        {
            // Opening the heat bay leaves wall tops at Y0 between the isolated props.
            // Remove the whole obsolete shelf, not just the space below each grip;
            // otherwise walking around the poles is a shorter, unintended route.
            LCutNamedBox("Concrete remainder",new Bounds(new(145.25f,-16,57),new(16.5f,32.2f,26)));
            LCutNamedBox("타설 콘크리트 벽",new Bounds(new(140,-16,69),new(5.2f,32.2f,3)));
            foreach (var pole in labyrinthRoot.GetComponentsInChildren<HumanCannonPole>())
            {
                var center = labyrinthFrame.InverseTransformPoint(pole.Grip.position);
                var forward = labyrinthFrame.InverseTransformDirection(pole.Forward);
                float swing = CurtainCall.Settings.GameSettings.Poles.SwingLength;
                var size = new Vector3(Mathf.Abs(forward.x) * swing * 2 + 1.2f, 32,
                    Mathf.Abs(forward.z) * swing * 2 + 1.2f);
                LCutNamedBox("Concrete remainder", new Bounds(center - Vector3.up * 16.5f, size));
            }
        }
        static void LCutNamedBox(string name, Bounds opening, float? top = null)
        {
            Physics.SyncTransforms();
            // Some authored cuboid wall panels were migrated to MeshCollider. These explicitly
            // named structural boxes still need the same fitted opening as primitive walls.
            foreach(var box in labyrinthFrame.GetComponentsInChildren<Collider>().Where(c=>(c is BoxCollider||c is MeshCollider)
                &&(c.name==name||c.name==name+" fitted return")&&!c.isTrigger&&c.GetComponent<MeshRenderer>()).ToArray())
            {
                // Migrated panels can carry both box and mesh colliders on one object.
                if (!box) continue;
                var bounds=new Bounds(labyrinthFrame.InverseTransformPoint(box.bounds.center),box.bounds.size);
                if(top.HasValue&&Mathf.Abs(bounds.max.y-top.Value)>.01f)continue;
                if(!bounds.Intersects(opening))continue;
                var lo=Vector3.Max(bounds.min,opening.min);var hi=Vector3.Min(bounds.max,opening.max);
                if((hi-lo).x<.001f||(hi-lo).y<.001f||(hi-lo).z<.001f)continue;
                var material=box.GetComponent<MeshRenderer>().sharedMaterial;
                void Piece(Vector3 a,Vector3 b){var size=b-a;if(size.x>.01f&&size.y>.01f&&size.z>.01f)LBox(name+" fitted return",(a+b)*.5f,size,material);}
                Piece(bounds.min,new(lo.x,bounds.max.y,bounds.max.z)); Piece(new(hi.x,bounds.min.y,bounds.min.z),bounds.max);
                Piece(new(lo.x,bounds.min.y,bounds.min.z),new(hi.x,lo.y,bounds.max.z));Piece(new(lo.x,hi.y,bounds.min.z),new(hi.x,bounds.max.y,bounds.max.z));
                Piece(new(lo.x,lo.y,bounds.min.z),new(hi.x,hi.y,lo.z));Piece(new(lo.x,lo.y,hi.z),new(hi.x,hi.y,bounds.max.z));
                Object.DestroyImmediate(box.gameObject);
            }
        }
    }
}
