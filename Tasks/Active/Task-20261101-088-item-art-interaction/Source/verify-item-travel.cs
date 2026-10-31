var p=UnityEngine.Object.FindFirstObjectByType<CurtainCall.ItemArt.ItemGameplayPlayground>();
var slot=p.Player.GetComponent<CurtainCall.Player.PlayerModelSlot>();var condition=p.Player.GetComponent<CurtainCall.Player.PlayerCondition>();var mover=p.Player.GetComponent<CurtainCall.Player.PlayerMover>();var presentation=p.Player.GetComponent<CurtainCall.Player.HumanoidPlayerPresentation>();
var previousSettings=UnityEngine.InputSystem.InputSystem.settings;var previousFlags=previousSettings.hideFlags;previousSettings.hideFlags=UnityEngine.HideFlags.DontSave;var workingSettings=UnityEngine.Object.Instantiate(previousSettings);workingSettings.hideFlags=UnityEngine.HideFlags.DontSave;UnityEngine.InputSystem.InputSystem.settings=workingSettings;workingSettings.backgroundBehavior=UnityEngine.InputSystem.InputSettings.BackgroundBehavior.IgnoreFocus;workingSettings.editorInputBehaviorInPlayMode=UnityEngine.InputSystem.InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;var previousKeyboard=UnityEngine.InputSystem.Keyboard.current;var previousMouse=UnityEngine.InputSystem.Mouse.current;int previousRate=UnityEngine.Application.targetFrameRate;UnityEngine.Application.targetFrameRate=60;bool previousBackground=UnityEngine.Application.runInBackground;
var keyboard=UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Keyboard>("ItemTravelKeyboard");
var mouse=UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Mouse>("ItemTravelMouse");
keyboard.MakeCurrent();mouse.MakeCurrent();
var records=new System.Collections.Generic.List<object>();var failures=new System.Collections.Generic.List<string>();
var ids=new[]{"stake-puller","magician-hairpin","uv-lamp"};
int sample=-1,phase=0,lastFrame=UnityEngine.Time.frameCount;double next=UnityEditor.EditorApplication.timeSinceStartup;
UnityEngine.Vector3 start=default,rightStart=default,leftStart=default;
System.Action<bool,bool> input=(move,uv)=>{UnityEngine.InputSystem.InputSystem.QueueStateEvent(keyboard,move?new UnityEngine.InputSystem.LowLevel.KeyboardState(UnityEngine.InputSystem.Key.W):new UnityEngine.InputSystem.LowLevel.KeyboardState());UnityEngine.InputSystem.InputSystem.QueueStateEvent(mouse,new UnityEngine.InputSystem.LowLevel.MouseState().WithButton(UnityEngine.InputSystem.LowLevel.MouseButton.Right,uv));};
System.Action<string> capture=label=>
{
    var model=slot.CurrentModel.GetComponent<CurtainCall.Player.HumanoidModel>();
    var go=new UnityEngine.GameObject("Travel review camera");var camera=go.AddComponent<UnityEngine.Camera>();camera.CopyFrom(p.ViewCamera);camera.orthographic=true;camera.orthographicSize=model.Stage>=2?.95f:1.3f;camera.nearClipPlane=.01f;
    var target=p.Player.transform.position+(model.Stage>=2?new UnityEngine.Vector3(0,.3f,.4f):UnityEngine.Vector3.up);
    camera.transform.position=target+new UnityEngine.Vector3(-2.8f,1.7f,4);camera.transform.LookAt(target);
    var rt=new UnityEngine.RenderTexture(1000,1000,24);camera.targetTexture=rt;camera.Render();var old=UnityEngine.RenderTexture.active;UnityEngine.RenderTexture.active=rt;var tex=new UnityEngine.Texture2D(1000,1000,UnityEngine.TextureFormat.RGB24,false);tex.ReadPixels(new UnityEngine.Rect(0,0,1000,1000),0,0);tex.Apply();System.IO.File.WriteAllBytes("Tasks/Active/Task-20261101-088-item-art-interaction/Art/travel-"+label+".png",tex.EncodeToPNG());UnityEngine.RenderTexture.active=old;camera.targetTexture=null;UnityEngine.Object.DestroyImmediate(tex);UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(go);
};
UnityEditor.EditorApplication.CallbackFunction tick=null;
System.Action finish=()=>{System.IO.File.WriteAllText("Tasks/Active/Task-20261101-088-item-art-interaction/Art/travel-validation.json",Newtonsoft.Json.JsonConvert.SerializeObject(new{records,failures},Newtonsoft.Json.Formatting.Indented));UnityEditor.EditorApplication.update-=tick;UnityEngine.InputSystem.InputSystem.RemoveDevice(keyboard);UnityEngine.InputSystem.InputSystem.RemoveDevice(mouse);UnityEngine.InputSystem.InputSystem.settings=previousSettings;previousSettings.hideFlags=previousFlags;UnityEngine.Object.DestroyImmediate(workingSettings);UnityEngine.Application.targetFrameRate=previousRate;UnityEngine.Application.runInBackground=previousBackground;previousKeyboard?.MakeCurrent();previousMouse?.MakeCurrent();System.IO.File.WriteAllText("Tasks/Active/Task-20261101-088-item-art-interaction/Art/travel-validation.json",Newtonsoft.Json.JsonConvert.SerializeObject(new{records,failures},Newtonsoft.Json.Formatting.Indented));};
tick=()=>
{
    if(!UnityEditor.EditorApplication.isPlaying){failures.Add("Play mode stopped");finish();return;}
    if(UnityEditor.EditorApplication.timeSinceStartup<next||UnityEngine.Time.frameCount<lastFrame+5)return;
    try
    {
        if(phase==0)
        {
            sample++;
            if(sample==48)
            {
                condition.SetLostParts(CurtainCall.Player.PlayerCondition.PartsForLostCount(0));slot.SetModel(p.CharacterPrefabs[0]);mover.SetStartPose(new UnityEngine.Vector3(0,.05f,-1.2f),UnityEngine.Quaternion.identity);
                p.Give("magician-hairpin");CurtainCall.WaitingRoom.PropCarry.Instance.RequestDrop();p.Give("stake-puller");finish();return;
            }
            int stage=sample%4,character=(sample%16)/4;string item=ids[sample/16];input(false,item=="uv-lamp");p.Give(item);
            condition.SetLostParts(CurtainCall.Player.PlayerCondition.PartsForLostCount(stage));slot.SetModel(p.CharacterPrefabs[character]);mover.SetStartPose(new UnityEngine.Vector3(6,.05f,-6),UnityEngine.Quaternion.identity);
            phase=1;next=UnityEditor.EditorApplication.timeSinceStartup+1.15;lastFrame=UnityEngine.Time.frameCount;return;
        }
        var model=slot.CurrentModel.GetComponent<CurtainCall.Player.HumanoidModel>();var a=model.Animator;string id=ids[sample/16];var prop=presentation.HeldItemVisual;
        var hips=a.GetBoneTransform(UnityEngine.HumanBodyBones.Hips);
        var right=a.GetBoneTransform(UnityEngine.HumanBodyBones.RightHand).position-hips.position;
        var left=a.GetBoneTransform(UnityEngine.HumanBodyBones.LeftHand).position-hips.position;
        if(phase==1)
        {
            if(prop==null||prop.name!="RightHand_"+id)throw new System.Exception("Missing held item "+sample+" "+id);
            if(sample%16==0)capture(model.name.ToLowerInvariant()+"-0-"+id+"-hold");
            start=p.Player.transform.position;input(true,id=="uv-lamp");phase=2;next=UnityEditor.EditorApplication.timeSinceStartup+.40;lastFrame=UnityEngine.Time.frameCount;return;
        }
        if(phase==2){rightStart=right;leftStart=left;phase=3;next=UnityEditor.EditorApplication.timeSinceStartup+.45;lastFrame=UnityEngine.Time.frameCount;return;}
        float travel=UnityEngine.Vector3.Distance(start,p.Player.transform.position),rightTravel=UnityEngine.Vector3.Distance(rightStart,right),leftTravel=UnityEngine.Vector3.Distance(leftStart,left);
        int layer=a.GetLayerIndex("ItemRightHand");string baseClip=string.Join(",",a.GetCurrentAnimatorClipInfo(0).Select(x=>x.clip.name));string armClip=string.Join(",",a.GetCurrentAnimatorClipInfo(layer).Select(x=>x.clip.name));
        var mesh=new UnityEngine.Mesh();var skin=model.Stage==0?model.Original:model.Bodies[model.Stage-1];skin.BakeMesh(mesh,true);var clearance=CurtainCall.ItemArt.EditorTools.ItemArtValidation.HandClearance(skin,mesh,prop,id);UnityEngine.Object.DestroyImmediate(mesh);
        bool crawl=model.Stage>=2;bool pose=crawl?baseClip.Contains("ItemCrawl")&&a.GetLayerWeight(layer)<.01f:baseClip.Contains("Walk")&&armClip.Contains(id=="uv-lamp"?"ItemAim":"ItemHold");
        var light=UnityEngine.Object.FindFirstObjectByType<CurtainCall.WaitingRoom.UvFlashlight>();float beamError=id=="uv-lamp"?UnityEngine.Vector3.Distance(light.LastOrigin,prop.transform.TransformPoint(new UnityEngine.Vector3(0,.14f,0))):0;
        bool pass=mover.IsMoving&&travel>.1f&&pose&&prop.activeInHierarchy&&clearance.x>=-.002f&&clearance.y>=-.002f&&beamError<.025f&&(!crawl||rightTravel>.025f&&rightTravel>leftTravel*1.3f);
        records.Add(new{character=model.name,stage=model.Stage,item=id,frame=UnityEngine.Time.frameCount,travel,baseClip,armClip,rightTravel,leftTravel,palmClearance=clearance.x,fingerClearance=clearance.y,beamError,pass});if(!pass)failures.Add(model.name+" stage"+model.Stage+" "+id);
        if(sample%16==3)capture(model.name.ToLowerInvariant()+"-3-"+id+"-crawl");
        input(false,false);phase=0;next=UnityEditor.EditorApplication.timeSinceStartup+.1;lastFrame=UnityEngine.Time.frameCount;
    }
    catch(System.Exception error){failures.Add(error.ToString());finish();}
};
UnityEngine.Application.runInBackground=true;UnityEditor.EditorApplication.update+=tick;return "Travel validation started (48 live movement cases, virtual input devices removed on completion).";



