using System;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CurtainCall.EscapeRoute.EditorTools
{
    public static partial class EscapeBackgroundBuilder
    {
        public static string ValidateASoloReview()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetActiveScene().path != ScenePath)
                throw new InvalidOperationException("A 편집 씬에서 실행하세요.");
            var p = UnityEngine.Object.FindAnyObjectByType<EscapeSoloReview>();
            if (p == null) throw new InvalidOperationException("현재 씬 검토 연결 없음");
            var output = new StringBuilder(); int pass=0,fail=0;
            void Check(string name,bool ok) { if(ok)pass++; else {fail++;output.AppendLine("FAIL "+name);} }
            Check("one driver",UnityEngine.Object.FindObjectsByType<EscapeSoloReview>(FindObjectsSortMode.None).Length==1);
            Check("camera",p.view!=null && p.view.enabled);
            Check("old controls suspended",p.suspendBehaviours.All(b=>b!=null&&!b.enabled));
            Check("cannon and rebound references",p.cannonMuzzle!=null&&p.cannonBoarding!=null&&p.bounceSurface!=null);
            Check("rope references",p.ropeStarts.Length==2&&p.ropeEnds.Length==2&&p.ropeStarts.All(t=>t!=null)&&p.ropeEnds.All(t=>t!=null));
            Check("six swing references",p.grips.Length==6&&p.pivots.Length==6&&p.swingVisuals.Length==6&&p.grips.All(t=>t!=null)&&p.pivots.All(t=>t!=null)&&p.swingVisuals.All(t=>t!=null&&t.childCount>0));
            Check("fourteen hazard waves",p.hazards.Length==14&&p.hazards.All(h=>h!=null));
            foreach(EscapeSoloHazard.Kind kind in Enum.GetValues(typeof(EscapeSoloHazard.Kind))) Check("hazard family "+kind,p.hazards.Any(h=>h.kind==kind));
            Check("both side directions",p.hazards.Any(h=>h.kind==EscapeSoloHazard.Kind.SideSaw&&h.destination.z>h.origin.z)&&p.hazards.Any(h=>h.kind==EscapeSoloHazard.Kind.SideSaw&&h.destination.z<h.origin.z));
            Check("front saw approaches player",p.hazards.Where(h=>h.kind==EscapeSoloHazard.Kind.FrontSaw).All(h=>h.destination.x<h.origin.x));
            foreach(var h in p.hazards)
            {
                Check(h.name+" refs/timing",h.player==p&&h.visual!=null&&h.warning!=null&&h.warningSeconds>=1&&h.travelSeconds>0&&h.restSeconds>0);
                Check(h.name+" no accidental solid collider",h.GetComponentsInChildren<Collider>(true).Length==0);
                Check(h.name+" warning within corridor",h.warning.position.x>=164&&h.warning.position.x<=366&&h.warning.localScale.x<30&&h.warning.localScale.z<=20);
                Check(h.name+" known visuals",h.visual.GetComponentsInChildren<Renderer>(true).All(r=>r.sharedMaterials.All(m=>m!=null&&m.shader!=null)));
            }
            Physics.SyncTransforms();
            foreach(var feet in new[]{p.startFeet,new Vector3(27,0,75),new Vector3(86,0,28),new Vector3(123,-8,48),new Vector3(150,0,78),new Vector3(228,0,78),new Vector3(305,0,78),new Vector3(370,0,78)})
                Check("restart floor "+feet,Physics.Raycast(feet+Vector3.up*.2f,Vector3.down,out var hit,.4f)&&Mathf.Abs(hit.point.y-feet.y)<.03f);
            Check("07 landing recovery",p.hazards.All(h=>h.origin.x<298||h.origin.x>=316));
            Check("final exit respite",p.hazards.All(h=>h.origin.x<366));
            Check("missing scripts",SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).Sum(t=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject))==0);
            output.AppendLine($"Passed={pass}; Failed={fail}. Wiring/static checks only; manual traversal and production multiplayer remain unverified.");
            return output.ToString();
        }
    }
}
