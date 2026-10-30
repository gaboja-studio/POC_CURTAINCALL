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
        public static void TuneCurrentEscapeFlow()
        {
            var scene = SceneManager.GetActiveScene();
            if (Application.isPlaying || !EscapeDevelopmentSetup.IsEscapeScene(scene)) throw new InvalidOperationException("Escape edit scene required");
            CompletionMaterials();
            var background = GameObject.Find(RootName).transform;
            var group = background.Find("31_FlowRefinement");
            if (group != null) throw new InvalidOperationException("Flow pass already applied; edit saved objects");
            group = VerticalChild(background, "31_FlowRefinement");
            group.localScale = new Vector3(1/background.lossyScale.x,1/background.lossyScale.y,1/background.lossyScale.z);
            EscapeDevelopmentSetup.ConfigureCurrent();
            var poles = UnityEngine.Object.FindObjectsByType<HumanCannonPole>(FindObjectsSortMode.None);
            foreach (var pole in poles)
            {
                bool descent = pole.name.StartsWith("Descent_", StringComparison.Ordinal);
                Vector3 at = pole.Grip.position;
                if (descent)
                {
                    var bits = pole.name.Split('_'); int stage = int.Parse(bits[1]), step = int.Parse(bits[2]);
                    float y = 2 - stage * 8, z = 60.5f - stage * 19;
                    at = new Vector3(67, y - (step==1?.4f:2.3f), z - (step==1?7.1f:11.5f));
                }
                else if (at.x > 270)
                {
                    int index = at.x < 284 ? 0 : at.x < 292 ? 1 : 2;
                    at = new Vector3(280 + index*7, 3.2f-index*.2f,78);
                }
                TuneFlowPole(group,pole,at,descent?4.2f:5f,descent?32:38,descent?5.2f:7.2f);
            }
            for (int stage=0;stage<3;stage++)
            {
                float z=60.5f-stage*19, y=2-stage*8;
                // Extend the safe ends into each gap; keep the middle a visible traversal gap.
                CompletionDeck(group,"Descent_"+stage+"_TakeoffLip",63,71,z-6.7f,z-6.2f,y-2.4f);
                CompletionDeck(group,"Descent_"+stage+"_RecoveryLip",62.5f,71.5f,z-14.2f,z-13.3f,y-6.4f);
            }
            int offset=0;
            foreach(var hazard in UnityEngine.Object.FindObjectsByType<HumanCannonHazard>(FindObjectsSortMode.None))
            {
                var data=new SerializedObject(hazard);
                data.FindProperty("rotationSpeed").floatValue=105;
                data.FindProperty("periodSeconds").floatValue=4.2f;
                data.ApplyModifiedProperties();
                var cue=hazard.GetComponent<HumanCannonHazardCue>(); if(cue==null)continue;
                Undo.RecordObject(cue,"Faster readable hazard rhythm");
                if(hazard.Kind==CannonHazardKind.FireJet)
                {
                    // All left vents share one window, all right vents exactly the other half-cycle.
                    bool north=cue.origin.x>119 && cue.origin.x<126 && cue.origin.y<0;
                    bool left=north?cue.origin.x<122.5f:cue.origin.z<78;
                    cue.warningSeconds=.65f; cue.activeSeconds=1.6f; cue.restSeconds=2.25f; cue.phase=left?0:2.25f;
                }
                else
                {
                    cue.warningSeconds=cue.falling?.9f:.75f;
                    cue.activeSeconds=cue.falling?.85f:1.05f;
                    cue.restSeconds=cue.falling?2.8f:1.8f;
                    cue.phase=(offset++%3)*(cue.warningSeconds+cue.activeSeconds+cue.restSeconds)/3;
                }
                EditorUtility.SetDirty(cue);
            }
            foreach(var station in UnityEngine.Object.FindObjectsByType<HumanCannonStation>(FindObjectsSortMode.None))
            {
                var data=new SerializedObject(station); data.FindProperty("boardingDistance").floatValue=2.75f;
                data.FindProperty("airControlSpeed").floatValue=6; data.ApplyModifiedProperties();
                var target=station.transform.Find("AuthoredLanding");
                if(target!=null) AimCompletionCannon(station,target.position,station.name=="04_ReturnCannon"?3.05f:1.25f);
            }
            if(scene.path==ScenePath) AimCompletionCannon(UnityEngine.Object.FindObjectsByType<HumanCannonStation>(FindObjectsSortMode.None).Single(s=>s.name=="03_Cannon"),new Vector3(48,-6,75),1.45f);
            var trampoline=UnityEngine.Object.FindAnyObjectByType<HumanCannonTrampoline>();
            if(trampoline!=null)
            {
                var data=new SerializedObject(trampoline);data.FindProperty("groundBounceAirControlSpeed").floatValue=6;
                data.ApplyModifiedProperties();
            }
            RegisterAllPoles(); Physics.SyncTransforms(); EditorSceneManager.MarkSceneDirty(scene);
        }

        static void TuneFlowPole(Transform group, HumanCannonPole pole, Vector3 at,float length,float angle,float upwardCap)
        {
            var data=new SerializedObject(pole); var delta=at-pole.Grip.position;
            Undo.RecordObject(pole.transform,"Align swing to route");pole.transform.position+=delta;
            Vector3 side=Vector3.Cross(Vector3.up,pole.Forward);
            foreach(var prefix in new[]{"left","right"})
            {
                var anchor=data.FindProperty(prefix+"Anchor").objectReferenceValue as Transform;
                if(anchor==null)throw new InvalidOperationException("Missing swing anchor: "+pole.name);
                Undo.RecordObject(anchor,"Short controlled swing suspension");
                anchor.position=at+Vector3.up*length+side*(prefix=="left"?-.8f:.8f);
            }
            data.FindProperty("maxSwingAngle").floatValue=angle;
            data.FindProperty("maxSwingSpeed").floatValue=100;
            data.FindProperty("swingAcceleration").floatValue=150;
            data.FindProperty("swingDamping").floatValue=.65f;
            data.FindProperty("releaseUpwardSpeed").floatValue=2.4f;
            data.FindProperty("maxReleaseUpwardSpeed").floatValue=upwardCap;
            data.FindProperty("airControlSpeed").floatValue=6;
            data.FindProperty("grabDistance").floatValue=2.6f;
            data.ApplyModifiedProperties();pole.PresentSwing(0);
            var cross=Vector3.Cross(Vector3.up,pole.Forward);
            var originalMount=GameObject.Find(pole.name+" 천장 매립 하중보");
            var upper=originalMount!=null?originalMount.transform.position-Vector3.up*.6f:at+Vector3.up*(length+9);
            BMass(group,pole.name+"_LowerSuspensionBeam",at+Vector3.up*(length+.25f),
                new Vector3(Mathf.Abs(cross.x)*3.2f+.4f,.5f,Mathf.Abs(cross.z)*3.2f+.4f),wornSteel,false);
            foreach(float sign in new[]{-1f,1f})
                VerticalCable(group,pole.name+"_FixedDropRod",at+Vector3.up*(length+.5f)+cross*sign*1.2f,
                    upper+cross*sign*1.2f,.1f,wornSteel);
            PrefabUtility.RecordPrefabInstancePropertyModifications(pole);
        }
    }
}
