using System;
using System.Linq;
using CurtainCall.UI.SceneIntegration;
using UnityEditor;
using UnityEngine;

namespace CurtainCall.WaitingRoom.Editor
{
    public static partial class WaitingRoomLayoutA
    {
        static void CompactLobbyElevator(Transform target)
        {
            var hall=target.Find("S08_ElevatorHall");if(hall==null)return;
            MuteFacility(target.Find("S01_Lobby/FutureConnections/Portal_08/DestinationSign"));
            var lift=hall.Find("FreightLift");if(lift==null)return;
            var view=hall.GetComponentInChildren<ExpertRoomView>(true);
            if(view==null)throw new InvalidOperationException("Missing expert service view");
            // Retain the service instance referenced by the UI while removing the hall and booth.
            if(view.transform.parent!=lift)Undo.SetTransformParent(view.transform,lift,"Attach expert service to lift");
            foreach(Transform t in hall)if(t!=lift)MuteFacility(t);
            foreach(Transform t in view.transform)MuteFacility(t);
            Undo.RecordObject(lift,"Place lift directly against lobby");lift.position=W(LobbyW+.12f,HallDoorZ);
            foreach(Transform t in lift)if(t.name=="GateBar"||t.name=="GateLattice"||t.name=="LiftSign")MuteFacility(t);
            var old=lift.Find("LobbyServiceFittings");if(old!=null)Undo.DestroyObjectImmediate(old.gameObject);
            var g=Group(lift,"LobbyServiceFittings",W(LobbyW+.12f,HallDoorZ));
            Box(g,"Threshold",W(LobbyW+.1f,HallDoorZ,-.03f),new Vector3(.7f,.08f,3.5f),Mat("LA_SteelPlate"),true);
            Box(g,"UpperInfill",W(LobbyW+.12f,HallDoorZ,3.95f),new Vector3(.2f,.7f,3.65f),Mat("V4_Iron_Dark"),true);
            foreach(var z in new[]{HallDoorZ-1.72f,HallDoorZ+1.72f})Box(g,"LiftJamb",W(LobbyW+.12f,z,1.85f),new Vector3(.23f,3.7f,.22f),Mat("V4_RustyMetal"),true);
            Undo.RecordObject(view.transform,"Set lift service origin");view.transform.SetPositionAndRotation(W(LobbyW+.04f,HallDoorZ),Quaternion.Euler(0,90,0));
            var frame=Group(g,"DoorMotionFrame",view.transform.position);frame.rotation=view.transform.rotation;
            var left=Box(frame,"ServiceDoorLeft",frame.TransformPoint(new Vector3(-.8f,1.8f,0)),new Vector3(.11f,3.5f,1.56f),Mat("LA_SteelPlate"));
            var right=Box(frame,"ServiceDoorRight",frame.TransformPoint(new Vector3(.8f,1.8f,0)),new Vector3(.11f,3.5f,1.56f),Mat("LA_SteelPlate"));
            foreach(var door in new[]{left,right})
            {
                var hardware=Group(door,"DoorHardware",door.position);var scale=door.lossyScale;
                hardware.localScale=new Vector3(1/scale.x,1/scale.y,1/scale.z);
                foreach(var y in new[]{.55f,2.75f})Box(hardware,"DoorStiffener",new Vector3(LobbyW-.035f,y,door.position.z),new Vector3(.045f,.07f,1.45f),Mat("V4_Iron_Dark"));
                Box(hardware,"DoorPull",new Vector3(LobbyW-.07f,1.5f,door.position.z+(door==left?-.6f:.6f)),new Vector3(.09f,.32f,.045f),Mat("LA_Bronze"));
            }
            var inside=Group(g,"ServiceInside",W(LobbyW+1.65f,HallDoorZ));
            var outside=Group(g,"ServiceOutside",W(LobbyW-1.4f,HallDoorZ));
            Point(g,"ServiceGlow",W(LobbyW+1.6f,HallDoorZ,2.8f),new Color(1,.67f,.35f),0,4);
            FacilityLabel(g,"⑧  전문가",W(LobbyW-.08f,HallDoorZ,4.05f),Quaternion.Euler(0,90,0),.055f);
            var so=new SerializedObject(view);
            so.FindProperty("curtainLeft").objectReferenceValue=left;so.FindProperty("curtainRight").objectReferenceValue=right;
            so.FindProperty("inside").objectReferenceValue=inside;so.FindProperty("outside").objectReferenceValue=outside;
            so.FindProperty("glow").objectReferenceValue=g.Find("ServiceGlow").GetComponent<Light>();
            so.FindProperty("sign").objectReferenceValue=g.GetComponentsInChildren<TextMesh>().Single();
            so.FindProperty("bunched").floatValue=1;so.FindProperty("slide").floatValue=1.5f;so.ApplyModifiedProperties();
            Connect<WaitingRoomWorldLabels>(UnityEngine.SceneManagement.SceneManager.GetActiveScene(),"expert",view);
        }

        static void RepairPracticeCollision(Transform target)
        {
            var arena=target.Find("S09_PracticeRoom/Arena");if(arena==null)return;
            RaisedPracticeRing(arena);
            foreach(var ladder in arena.Find("PracticeLanes").Cast<Transform>().Where(t=>t.name=="SM_Ladder1"))
                foreach(var c in ladder.GetComponentsInChildren<Collider>(true)){Undo.RecordObject(c,"Remove oversized ladder collider");c.enabled=false;}
            Remove(arena,"CollisionRepairs");var g=Group(arena,"CollisionRepairs",arena.position);
            foreach(var x in new[]{TowerWestX,TowerEastX})Box(g,"LadderBackstop",W(x,PracRing.y-1.04f,1.8f),new Vector3(.82f,3.6f,.12f),Mat("V4_Iron_Dark"),true);
        }

        static void ConcealedMachinePlant(Transform target)
        {
            var room=target.Find("B1_MachineRoom");if(room==null)return;
            foreach(var n in new[]{"PitLevel","CatwalkLevel","FloorCollider"})MuteFacility(room.Find(n));
            foreach(var text in room.GetComponentsInChildren<TextMesh>(true))MuteFacility(text.transform);
            foreach(var t in room.GetComponentsInChildren<Transform>(true).Where(t=>t.name=="ManifoldSignBoard"))MuteFacility(t);
            Remove(room,"DeepPlant");var g=Group(room,"DeepPlant",room.position);float keep=LevelY;
            try
            {
                const float ex=10,nz=28;
                LevelY=PitLevel;
                RoomShell(Group(g,"LowerShell",room.position),"LA_DeepPlantLower",MachineX0,ex,MachineZ0,nz,5,2.4f,Mat("LA_FloorConcrete"),Mat("LA_WallConcrete"),Mat("LA_WallRust"),new[]{(3,EscapeDoorX,2f,.5f)},withCeiling:false);
                FloorCollider(g,MachineX0,ex,MachineZ0,nz);
                LevelY=B1Level;
                RoomShell(Group(g,"UpperShell",room.position),"LA_DeepPlantUpper",MachineX0,ex,MachineZ0,nz,B1Height,2.2f,Mat("LA_FloorConcrete"),Mat("LA_WallConcrete"),Mat("LA_WallRust"),new[]{(0,MachineDoorZ,2.4f,.5f),(3,LinkWestX,2f,.5f)},withFloor:false);
                // Reach the unmarked exit from the lower plant, behind an offset equipment screen.
                foreach(var n in new[]{"B1_EscapeCorridor","B1_EscapeEntrance"})
                {
                    var t=target.Find(n);Undo.RecordObject(t,"Lower hidden escape approach");var at=t.position;at.y=PitLevel;t.position=at;
                    foreach(var text in t.GetComponentsInChildren<TextMesh>(true))MuteFacility(text.transform);
                }
                LevelY=PitLevel;var plant=Group(g,"EquipmentBays",room.position);
                Box(plant,"HiddenPassageThreshold",W(EscapeDoorX,.65f,-.06f),new Vector3(2,.12f,1),Mat("LA_FloorConcrete"),true);
                foreach(var v in new[]{(-19f,21f,0f,"SM_Machinery_04"),(-9f,22.5f,90f,"SM_Machinery_03"),(3.5f,21.5f,0f,"SM_Machinery_04"),(5.2f,11f,90f,"SM_Machinery_03")})
                    PlaceProp(LeartesProps+v.Item4,plant,W(v.Item1,v.Item2),v.Item3);
                foreach(var v in new[]{(-20f,3.6f,5.7f),(-13.3f,3.6f,4f),(1.4f,4.4f,3.2f)})
                {
                    var skid=Group(plant,"ServiceSkid",W(v.Item1,v.Item2));
                    Box(skid,"MachineBase",W(v.Item1,v.Item2,.2f),new Vector3(v.Item3,.4f,1.5f),Mat("V4_Iron_Dark"),true);
                    for(int i=0;i<3;i++)
                    {
                        float x=v.Item1+(i-1)*v.Item3*.27f;
                        Prim(skid,PrimitiveType.Cylinder,"PumpBody",W(x,v.Item2,1.4f),new Vector3(.9f,1.1f,.9f),Mat("V4_RustyMetal"),true);
                        PlaceProp(LeartesProps+"SM_Pipe_Small_Valve_01",skid,W(x,v.Item2-.5f,1.7f),0,false);
                        PipeRun(skid,new(x,v.Item2),new(x,v.Item2+1.2f),2.6f,.14f);
                    }
                    Box(skid,"ServiceBack",W(v.Item1,v.Item2+.65f,2f),new Vector3(v.Item3,4,.13f),Mat("V4_Partition_RustGreen"),true);
                    PipeRun(skid,new(v.Item1-v.Item3*.5f,v.Item2+1.2f),new(v.Item1+v.Item3*.5f,v.Item2+1.2f),2.6f,.19f);
                }
                for(int i=0;i<7;i++)
                {
                    float x=-22f+i*1.15f;Pipe(plant,W(x,5.2f,3.6f),Vector3.up,7.2f,.16f).name="ServiceRiser";
                    PipeRun(plant,new(x,5.2f),new(x,18f),7.2f,.16f);
                }
                foreach(var z in new[]{18f,25.5f})
                {
                    PipeRun(plant,new(MachineX0,z),new(ex,z),7.6f,.32f);
                    foreach(var x in new[]{-23f,-14f,-4f,6f})Box(plant,"PipeTrestle",W(x,z,6.8f),new Vector3(.2f,1.8f,1.3f),Mat("V4_Iron_Dark"));
                }
                foreach(var x in new[]{-15f,-2f,6.5f})
                {
                    Prim(plant,PrimitiveType.Cylinder,"PressureTank",W(x,26f,2.1f),new Vector3(2.4f,2.1f,2.4f),Mat("V4_Partition_RustGreen"),true);
                    Pipe(plant,W(x,26f,5.9f),Vector3.up,3.5f,.18f);PipeRun(plant,new(x,26f),new(x,nz),7.6f,.18f);
                }
                foreach(var v in new[]{(-25.5f,23f),(-24f,24.2f),(-.3f,17.6f),(7.7f,6.3f),(-10f,18f)})
                {
                    PlaceFactory("SM_Barrel",plant,W(v.Item1,v.Item2),v.Item1*17);Decal(plant,"OilAndRust",v.Item1,v.Item2,2.5f,1.8f,Mat("LA_RustDecal"));
                }
                foreach(var v in new[]{(-20f,20f),(-7f,24f),(5f,16f)})
                {
                    Bulb(plant,W(v.Item1,v.Item2,5.2f));Point(plant,"MaintenancePool",W(v.Item1,v.Item2,4.8f),new Color(1,.63f,.31f),4,11);
                }
                Point(plant,"ReturnAisleLamp",W(-19f,1.8f,2.5f),new Color(1,.6f,.3f),1.5f,4);
                foreach(var l in target.Find("B1_EscapeEntrance").GetComponentsInChildren<Light>(true)){Undo.RecordObject(l,"Subdue hidden escape light");l.intensity=l.name=="WarningBeacon"?.15f:.5f;}
            }
            finally{LevelY=keep;}
        }
    }
}
