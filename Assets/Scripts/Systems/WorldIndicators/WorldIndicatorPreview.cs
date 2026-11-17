using System.Collections.Generic;
using CurtainCall.HumanCannon;
using CurtainCall.Player;
using CurtainCall.Settings;
using UnityEngine;

namespace CurtainCall.WorldIndicators
{
    // Deliberately synthetic timing in an isolated review scene, using the production views.
    public sealed class WorldIndicatorPreview : MonoBehaviour
    {
        public AreaIndicatorView circle, rectangle;
        public SweptVolumeIndicatorView grab;
        public PathIndicatorView path;
        public TrajectoryIndicatorView trajectory;
        public TargetIndicatorView target;
        public CurtainCall.WaitingRoom.Pursuit.PursuitEdgeGraphic threatEdges;
        public Transform hand, runner, targetRunner, cannonMuzzle;
        public bool previewDangerEdge=true;
        public float frozenProgress = -1;
        public int cameraPreset;
        public Camera reviewCamera;
        readonly List<Vector3> points=new(128);
        // The review capsule is 1.7m tall with its pivot at the body centre.
        const float ReviewAimOffset = 1.7f * (.68f - .5f);
        readonly Vector3 grabStart=new(-5,2.5f,7),grabEnd=new(4.8f,3+ReviewAimOffset,7);
        public float Progress { get; private set; }
        void Update()
        {
            float t=frozenProgress>=0?frozenProgress:Mathf.Repeat(Time.time,6)/6;
            Progress=Mathf.Clamp01(t/.75f);
            circle.Show(new Vector3(-5,0,0),Quaternion.identity,Vector2.one*3.3f,Progress);
            rectangle.Show(new Vector3(1,0,0),Quaternion.identity,new Vector2(4.8f,2.4f),Progress);
            bool warning=t<.75f;
            bool preparing=t<.25f;
            float strike=Mathf.InverseLerp(.75f,.9f,t);
            Vector3 tip=Vector3.Lerp(grabStart,grabEnd,strike);
            if(!preparing && t<.9f && GameSettings.WorldUi.showGrabPath) grab.Show(warning?grabStart:tip,grabEnd,.48f,Mathf.InverseLerp(.25f,.75f,t),warning); else grab.Hide();
            if(targetRunner) targetRunner.position=new Vector3(4+Mathf.Sin(t*Mathf.PI*2)*.8f,3,7);
            if(target && t<.9f)
                target.Show(preparing?targetRunner.position+Vector3.up*ReviewAimOffset:grabEnd,
                    preparing?t/.25f:Mathf.InverseLerp(.25f,.75f,t),!preparing,1.7f*.68f+.25f);
            else if(target) target.Hide();
            if(threatEdges) threatEdges.SetThreat(previewDangerEdge && t<.9f?GameSettings.WaitingRoom.Pursuit.EdgeIntensity:0,GameSettings.WaitingRoom.Pursuit.EdgeWidth);
            if(hand) hand.position=warning?grabStart:tip;
            if(runner) runner.position=new Vector3(-.5f,3.1f,7+Mathf.Sin(t*Mathf.PI*2)*1.1f);
            path.Show(new Vector3(-5,0,12),new Vector3(5,0,12),1.8f,Progress);
            points.Clear(); Vector3 p=cannonMuzzle?cannonMuzzle.position:new Vector3(5,1,-4),v=new Vector3(0,7,9);
            points.Add(p); float length=0;
            for(int i=0;i<128 && length<GameSettings.WorldUi.cannonArcLength;i++)
            {
                Vector3 step=BallisticMotion.Step(ref v,Vector3.zero,GameSettings.Base.Character.Gravity,.035f);
                float use=Mathf.Min(step.magnitude,GameSettings.WorldUi.cannonArcLength-length);
                p+=step.normalized*use; length+=use; points.Add(p);
            }
            trajectory.Show(points);
            if(reviewCamera)
            {
                Vector3 position=cameraPreset==1?new Vector3(1,6,-8):cameraPreset==2?new Vector3(12,7,1):cameraPreset==3?new Vector3(0,10,-8):new Vector3(13,16,-17);
                Vector3 target=cameraPreset==1?new Vector3(0,2.8f,7):cameraPreset==2?new Vector3(1,2,4):cameraPreset==3?new Vector3(-1,0,0):new Vector3(0,1.5f,5);
                reviewCamera.transform.SetPositionAndRotation(position,Quaternion.LookRotation(target-position));
            }
        }
        void OnGUI()
        {
            var style=new GUIStyle(GUI.skin.label) { fontSize=22,normal={textColor=new Color(.94f,.93f,.86f)} };
            GUI.Label(new Rect(24,85,1100,36),"CURTAIN CALL  /  TARGET → LOCK → GRAB",style);
            style.fontSize=15;
            GUI.Label(new Rect(24,123,1100,28),"Iconless areas / optional locked path / shared danger edge / short cannon arc",style);
            GUI.Label(new Rect(24,Screen.height-65,1200,28),"Isolated review scene / production prefabs / scripted timing and danger intensity (not network gameplay)",style);
        }
    }
}
