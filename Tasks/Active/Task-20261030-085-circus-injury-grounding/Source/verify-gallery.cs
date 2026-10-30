var g=UnityEngine.Object.FindAnyObjectByType<CurtainCall.HumanCannon.CircusMotionGallery>();
if(g==null||!Application.isPlaying)throw new System.Exception("Gallery must be playing");
g.SetPaused(true);int samples=0;
foreach(int motion in new[]{5,6,9}){
g.SelectMotion(motion);
foreach(float t in new[]{0f,.21f,.5f,.8f,.99f}){
g.Seek(t);
for(int actor=0;actor<16;actor++){
var m=g.Models[actor];var a=m.Animator;int stage=actor%4;
if(m.Stage!=stage||!a.GetCurrentAnimatorStateInfo(0).IsName(g.StateName(stage)))throw new System.Exception("Wrong stage/state");
if(!float.IsFinite(a.GetBoneTransform(UnityEngine.HumanBodyBones.Hips).position.y))throw new System.Exception("Invalid pelvis");
if(!(stage==0?m.Original:m.Bodies[stage-1]).enabled)throw new System.Exception("Wrong visible surface");samples++;
}}}
g.Focus(0,2,false);g.SelectMotion(5);g.Seek(.21f);
if(g.Models.Count(m=>m.gameObject.activeInHierarchy)!=1)throw new System.Exception("Focus failed");
g.Focus(0,0,true);g.SetPaused(false);
return new{samples,actors=g.Models.Length,frame=UnityEngine.Time.frameCount,time=UnityEngine.Time.time};
