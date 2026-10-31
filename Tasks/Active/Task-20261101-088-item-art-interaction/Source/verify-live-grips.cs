var p=UnityEngine.Object.FindFirstObjectByType<CurtainCall.ItemArt.ItemGameplayPlayground>();
var slot=p.Player.GetComponent<CurtainCall.Player.PlayerModelSlot>();
var condition=p.Player.GetComponent<CurtainCall.Player.PlayerCondition>();
var presentation=p.Player.GetComponent<CurtainCall.Player.HumanoidPlayerPresentation>();
var records=new System.Collections.Generic.List<object>();
int sample=-1,phase=0,frame=UnityEngine.Time.frameCount;
double next=UnityEditor.EditorApplication.timeSinceStartup;
UnityEditor.EditorApplication.CallbackFunction tick=null;
tick=()=>
{
    if(!UnityEditor.EditorApplication.isPlaying){UnityEditor.EditorApplication.update-=tick;return;}
    if(UnityEditor.EditorApplication.timeSinceStartup<next||UnityEngine.Time.frameCount<frame+5)return;
    try
    {
        if(phase==0)
        {
            sample++;
            if(sample==16)
            {
                condition.SetLostParts(CurtainCall.Player.PlayerCondition.PartsForLostCount(0));slot.SetModel(p.CharacterPrefabs[0]);
                UnityEditor.EditorApplication.update-=tick;
                System.IO.File.WriteAllText("Tasks/Active/Task-20261101-088-item-art-interaction/Art/live-grip-validation.json",Newtonsoft.Json.JsonConvert.SerializeObject(records,Newtonsoft.Json.Formatting.Indented));return;
            }
            condition.SetLostParts(CurtainCall.Player.PlayerCondition.PartsForLostCount(sample%4));slot.SetModel(p.CharacterPrefabs[sample/4]);
            phase=1;next=UnityEditor.EditorApplication.timeSinceStartup+1.35;frame=UnityEngine.Time.frameCount;return;
        }
        var model=slot.CurrentModel.GetComponent<CurtainCall.Player.HumanoidModel>();var a=model.Animator;
        var prop=presentation.HeldItemVisual;
        var skin=model.Stage==0?model.Original:model.Bodies[model.Stage-1];var mesh=new UnityEngine.Mesh();skin.BakeMesh(mesh,true);
        var clearance=CurtainCall.ItemArt.EditorTools.ItemArtValidation.HandClearance(skin,mesh,prop,"stake-puller");UnityEngine.Object.DestroyImmediate(mesh);
        int layer=model.Stage>=2||phase==2?0:a.GetLayerIndex("ItemRightHand");
        string expected=CurtainCall.ItemArt.ItemVisualAssets.State(phase==1?"ItemHold":"ItemStrike",sample%4);
        string clip=string.Join(",",a.GetCurrentAnimatorClipInfo(layer).Select(x=>x.clip.name));
        records.Add(new{character=model.name,stage=model.Stage,motion=expected,clip,frame=UnityEngine.Time.frameCount,palmClearance=clearance.x,fingerClearance=clearance.y,pass=clip.Contains(expected)&&clearance.x>=-.002f&&clearance.y>=-.002f&&prop.activeInHierarchy});
        if(phase==1)
        {presentation.RequestItemMotion(0,p.Player.transform.position+p.Player.transform.forward+UnityEngine.Vector3.up);phase=2;next=UnityEditor.EditorApplication.timeSinceStartup+.5;}
        else{phase=0;next=UnityEditor.EditorApplication.timeSinceStartup;}
        frame=UnityEngine.Time.frameCount;
    }
    catch(System.Exception ex)
    {
        UnityEditor.EditorApplication.update-=tick;
        System.IO.File.WriteAllText("Tasks/Active/Task-20261101-088-item-art-interaction/Art/live-grip-validation-error.txt",ex.ToString());
    }
};
UnityEditor.EditorApplication.update+=tick;
return "Live grip validation started: 4 characters x 4 stages x hold/strike, waits for actual advancing frames.";
