using System;
using System.Linq;
using System.Text;
using CurtainCall.HumanCannon;
using CurtainCall.Player;
using CurtainCall.Settings;
using CurtainCall.Tightrope;
using CurtainCall.UI.SceneIntegration;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CurtainCall.EscapeRoute.EditorTools
{
    public static partial class EscapeBackgroundBuilder
    {
        [MenuItem("Tools/CurtainCall/탈출로/현재 맵 연결·접합부 검사")]
        public static void CheckCompletedEscapeMenu()
        {
            string path=SceneManager.GetActiveScene().path;
            if(path!=ScenePath&&path!=VariantBPath&&path!=VariantCPath)throw new InvalidOperationException("Open an escape route scene first");
            Debug.Log(ValidateCompletedEscape()+"\n"+ValidateEscapeJoints()+"\n"+
                (path==ScenePath?ValidateDepthExpansion():ValidateVariantTraversal()));
        }

        public static string ValidateCompletedEscape()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Static edit checks only");
            Physics.SyncTransforms(); var scene = SceneManager.GetActiveScene();
            int passed = 0, failed = 0; var report = new StringBuilder();
            void Check(string name, bool value) { if (value) passed++; else { failed++; if(failed < 40) report.AppendLine("FAIL " + name); } }
            T[] All<T>() where T : Component => scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<T>()).ToArray();
            var system = All<HumanCannonSystem>().Single(); var poleSystem = All<HumanCannonPoleSystem>().Single();
            var poles = All<HumanCannonPole>(); var hazards = All<HumanCannonHazard>();
            Check("original interaction binding", All<InteractionSceneBinding>().Length == 1);
            Check("original interaction UI enabled in build", EditorBuildSettings.scenes.Any(s => s.enabled && s.path == CommonInteractionUI.ScenePath));
            Check("one trampoline", All<HumanCannonTrampoline>().Length == 1);
            Check("one rope course", All<TightropeCourse>().Length == 1);
            var registered = new SerializedObject(poleSystem).FindProperty("poles");
            Check("all poles registered", poles.Length == registered.arraySize && poles.All(p => Enumerable.Range(0,registered.arraySize).Any(i=>registered.GetArrayElementAtIndex(i).objectReferenceValue==p)));
            foreach (var pole in poles)
            {
                var data = new SerializedObject(pole);
                foreach (string field in new[]{"grip","visualBar","leftCable","rightCable","leftAnchor","rightAnchor"})
                {
                    var reference = data.FindProperty(field).objectReferenceValue as Component;
                    Check(pole.name + " local " + field, reference != null && reference.gameObject.scene == scene);
                }
                Check(pole.name + " original art", pole.transform.Find("SwingVisual/AppliedPoleBar") != null);
                Check(pole.name + " authored dynamics", pole.HoldSeconds == 0 && data.FindProperty("maxSwingAngle").floatValue == 50);
                Check(pole.name + " finite bar", Vector3.Distance(pole.Grip.position, pole.BarPosition(0)) < .01f);
            }
            foreach (var hazard in hazards)
            {
                var cue = hazard.GetComponent<HumanCannonHazardCue>();
                Check(hazard.name + " cue", cue != null && cue.visual != null && cue.warning != null);
                if (cue == null) continue;
                if (hazard.Kind == CannonHazardKind.GroundSaw)
                {
                    var forward = (cue.destination - cue.origin).normalized;
                    Check(hazard.transform.parent.name + " edge-first contact", Mathf.Abs(Vector3.Dot(hazard.BladeNormal, forward)) < .01f);
                    var mesh = cue.visual.GetComponentsInChildren<MeshFilter>().First(f => f.sharedMesh != null);
                    var thin = mesh.transform.TransformDirection(Vector3.up);
                    Check(hazard.transform.parent.name + " art and contact plane", Mathf.Abs(Vector3.Dot(thin, hazard.BladeNormal)) > .99f);
                }
                if (hazard.Kind == CannonHazardKind.FireJet)
                {
                    var particles = cue.visual.GetComponentsInChildren<ParticleSystem>(true);
                    Check(hazard.transform.parent.name + " real fire", particles.Length == 6);
                    Check(hazard.transform.parent.name + " no sphere placeholder", cue.visual.GetComponentsInChildren<MeshFilter>(true).Length == 0);
                    Check(hazard.transform.parent.name + " materials", particles.All(p => p.GetComponent<ParticleSystemRenderer>().sharedMaterial != null));
                }
            }
            var descent = GameObject.Find(RootName).transform.Find(CompletionGroup + "/04_StairSwingDescent");
            Check("expanded descent", descent != null);
            if (descent != null) for (int n = 0; n < 3; n++)
            {
                float z = 60.5f - n * 19, y = 2 - n * 8;
                foreach (var p in new[]{new Vector3(67,y,z),new Vector3(67,y-2.4f,z-5.2f),new Vector3(67,y-6.4f,z-15.2f)})
                    foreach(float dx in new[]{-2.5f,0,2.5f})
                        Check("descent support " + (p+Vector3.right*dx), Physics.Raycast(p+Vector3.right*dx+Vector3.up*.26f,Vector3.down,.55f,~0,QueryTriggerInteraction.Ignore));
                var first = poles.Single(p=>p.name=="Descent_"+n+"_1"); var second = poles.Single(p=>p.name=="Descent_"+n+"_2");
                Check("entry grab " + n, Vector3.Distance(first.Grip.position,new Vector3(67,y-1.65f,z-6.1f))<3.5f);
                Check("swing transfer " + n, StaticSwingReach(first, second.Grip.position, false, out var proof));
                report.AppendLine("Descent " + n + " transfer " + proof);
                Check("swing landing " + n, StaticSwingReach(second, new Vector3(67,y-6.4f,z-15.2f), true, out proof));
                report.AppendLine("Descent " + n + " recovery " + proof);
                Check("intentional gap " + n, !Physics.Raycast(new Vector3(67,y-2,z-10),Vector3.down,5,~0,QueryTriggerInteraction.Ignore));
            }
            Check("missing scripts", scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Transform>(true)).Sum(t=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject))==0);
            report.AppendLine("Scene=" + scene.name + "; stations=" + system.StationCount + "; poles="+poles.Length+"; hazards="+hazards.Length);
            report.AppendLine("Passed="+passed+"; Failed="+failed+". Static geometry/ballistics only; no input or player movement.");
            return report.ToString();
        }

        // A feasibility envelope, not a playtest or input sequence. Tests original release velocities against the real colliders.
        static bool StaticSwingReach(HumanCannonPole pole, Vector3 target, bool landing, out string proof)
        {
            float radius = GameSettings.Base.Character.CapsuleRadius, height = GameSettings.Base.Character.CapsuleHeight;
            for (float angle = 0; angle <= 30; angle += 5) for (float angularSpeed = 10; angularSpeed <= 80; angularSpeed += 5)
            {
                Vector3 position = pole.HangingPosition(angle), velocity = pole.ReleaseVelocity(angle, angularSpeed);
                velocity = CannonAirControl.LimitPoleHorizontalSpeed(velocity, GameSettings.HumanCannon.PoleJumpHorizontalSpeedLimit);
                for (int i=0;i<240;i++)
                {
                    if (!landing && Vector3.Distance(position+Vector3.up*height*.5f,target)<2.6f)
                    { proof=$"angle {angle}, speed {angularSpeed}, t {i/60f:F2}s"; return true; }
                    var step = BallisticMotion.Step(ref velocity, Vector3.zero, GameSettings.Base.Character.Gravity, 1f/60);
                    if (Physics.CapsuleCast(position+Vector3.up*radius,position+Vector3.up*(height-radius),radius,step.normalized,out var hit,step.magnitude+.01f,~0,QueryTriggerInteraction.Ignore))
                    {
                        if(landing && hit.normal.y>.9f && Mathf.Abs(hit.point.y-target.y)<.15f && Vector2.Distance(new Vector2(hit.point.x,hit.point.z),new Vector2(target.x,target.z))<2)
                        { proof=$"angle {angle}, speed {angularSpeed}, first contact {hit.point}"; return true; }
                        break;
                    }
                    position += step;
                }
            }
            proof="no unobstructed original release found"; return false;
        }
    }
}
