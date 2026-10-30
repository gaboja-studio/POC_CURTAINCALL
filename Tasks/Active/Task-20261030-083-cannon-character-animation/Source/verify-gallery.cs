var g=UnityEngine.Object.FindAnyObjectByType<CurtainCall.HumanCannon.CircusMotionGallery>();
if(g==null||!Application.isPlaying)throw new System.Exception("Gallery must be playing");
g.SetPaused(true);int samples=0;
for(int clip=0;clip<11;clip++)
{
    g.SelectMotion(clip);g.Seek(.35f);
    for(int actor=0;actor<16;actor++)
    {
        var m=g.Models[actor];int stage=actor%4;
        if(m.Stage!=stage||!m.Animator.GetCurrentAnimatorStateInfo(0).IsName(g.StateName(stage)))throw new System.Exception("Wrong stage/state: "+actor+" "+clip);
        if(!float.IsFinite(m.Animator.GetBoneTransform(HumanBodyBones.Head).position.y))throw new System.Exception("Invalid pose");
        if((stage==0?m.Original:m.Bodies[stage-1]).enabled!=true)throw new System.Exception("Wrong cut surface");samples++;
    }
}
var buttons=UnityEngine.Object.FindObjectsByType<UnityEngine.UI.Button>(FindObjectsSortMode.None);
var focus=buttons.First(b=>b.GetComponentInChildren<UnityEngine.UI.Text>().text=="Focus selected actor");focus.onClick.Invoke();
if(g.Models.Count(m=>m.gameObject.activeInHierarchy)!=1)throw new System.Exception("Focus must show one actor");
g.Focus(3,3,false);g.Seek(.75f);
if(!g.Models[15].Animator.GetCurrentAnimatorStateInfo(0).IsName("RopeLift_Cut3"))throw new System.Exception("Focused cut clip missing");
g.Focus(0,0,true);g.SelectMotion(3);g.SetPaused(false);
return new{samples,uniqueClips=44,actors=g.Models.Length,buttons=buttons.Length,focusAndSeek=true,time=Time.time};
