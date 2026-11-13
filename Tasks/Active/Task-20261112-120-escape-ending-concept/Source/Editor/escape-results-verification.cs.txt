#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;

namespace CurtainCall.UI.EscapeResultsKit.Editor
{
    [InitializeOnLoad]
    public static class EscapeResultsVerification
    {
        const string Key="EscapeResultsKit.ValidationPending";
        static int ticks;
        static EscapeResultsVerification()
        {
            if(SessionState.GetBool(Key,false)) EditorApplication.update+=Poll;
        }
        // Use a dedicated validation project; this runner exits its editor when finished.
        public static void Run()
        {
            if(!Application.isBatchMode)throw new InvalidOperationException("Batch validation only.");
            SessionState.SetBool(Key,true);
            EditorSceneManager.OpenScene("Assets/Scenes/Tests/EscapeResultsKit/EscapeResultsPreview.unity");
            EditorApplication.EnterPlaymode();
        }
        static void Poll()
        {
            if(!EditorApplication.isPlaying || EditorApplication.isCompiling || ticks++<8)return;
            EditorApplication.update-=Poll;SessionState.SetBool(Key,false);
            var errors=new List<string>();int checks=0;
            Action<bool,string> check=(ok,message)=>{checks++;if(!ok)errors.Add(message);};
            try
            {
                var view=UnityEngine.Object.FindFirstObjectByType<EscapeResultsView>();
                check(view!=null,"View missing");
                check(view.Visible,"Preview did not open");
                check(UnityEngine.Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Length==1,"EventSystem count");
                check(EventSystem.current.GetComponents<BaseInputModule>().Length==1,"Input module count");
                view.ReturnRequested.RemoveAllListeners();
                PointerClick(view.StatisticsTab.gameObject);
                check(view.StatisticsSelected && !view.PlayersContent.activeSelf,"Pointer statistics tab");
                PointerClick(view.PlayersTab.gameObject);
                check(!view.StatisticsSelected && view.PlayersContent.activeSelf,"Pointer players tab");
                var input=new PointerEventData(EventSystem.current){position=new Vector2(Screen.width*.8f,Screen.height*.5f)};
                var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(input,hits);
                check(!hits.Any(h=>h.gameObject.transform.IsChildOf(view.transform)),"Right side blocks background");
                var snapshot=view.PlayerRows.Select((r,i)=>new EscapePlayerData{Name="단원 "+(i+1),Portrait=r.Portrait.sprite,SlotColor=r.SlotColor.color,Escaped=i<3,Items=r.Items.Select(x=>x.sprite).ToArray()}).ToArray();
                for(int count=1;count<=4;count++)
                {
                    view.SetPlayers(snapshot.Take(count).ToArray());
                    check(view.PlayerRows.Count(r=>r.Root.activeSelf)==count,"Participant count "+count);
                }
                snapshot[0].Items=Array.Empty<Sprite>();view.SetPlayers(snapshot);
                check(!view.PlayerRows[0].Items[0].enabled && view.PlayerRows[0].EmptyItems[0].enabled,"Empty inventory slot");
                snapshot[3].Escaped=false;snapshot[3].Items=new[]{snapshot[1].Items[0]};view.SetPlayers(snapshot);
                check(view.PlayerRows[3].Items[0].enabled,"Non-escaped player's inventory was erased");
                int requests=0;view.ReturnRequested.AddListener(()=>requests++);
                view.SetReturnAllowed(false);PointerClick(view.ReturnButton.gameObject);check(requests==0,"Non-authority return");
                view.SetReturnAllowed(true);view.SetVisible(false);view.RequestReturn();check(requests==0,"Hidden return");
                view.SetVisible(true);PointerClick(view.ReturnButton.gameObject);PointerClick(view.ReturnButton.gameObject);check(requests==1,"One-shot return request");
                check(view.Visible,"Return request automatically hid the result");
                check(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name=="EscapeResultsPreview","Return request changed scene");
                view.SetReturnAllowed(true);
                view.SelectTab(true);Canvas.ForceUpdateCanvases();
                check(view.GetComponentsInChildren<TMPro.TMP_Text>().All(t=>!t.isTextTruncated),"Visible labels truncated");
            }
            catch(Exception e){errors.Add(e.ToString());}
            Directory.CreateDirectory("Artifacts");File.WriteAllText("Artifacts/playmode-verification.json",JsonUtility.ToJson(new Report{checks=checks,errors=errors.ToArray()},true));
            Debug.Log("ESCAPE_KIT_PLAYMODE "+(errors.Count==0?"PASS":"FAIL")+" checks="+checks);
            EditorApplication.Exit(errors.Count==0?0:1);
        }
        static void PointerClick(GameObject target)
        {
            var button=target.GetComponent<UnityEngine.UI.Button>();
            var center=RectTransformUtility.WorldToScreenPoint(null,button.targetGraphic.rectTransform.TransformPoint(button.targetGraphic.rectTransform.rect.center));
            var data=new PointerEventData(EventSystem.current){position=center,button=PointerEventData.InputButton.Left,eligibleForClick=true};
            var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(data,hits);
            var hit=hits.FirstOrDefault(h=>h.gameObject.transform.IsChildOf(target.transform));
            if(hit.gameObject==null)throw new InvalidOperationException("Button is not hit by UI raycast: "+target.name);
            ExecuteEvents.ExecuteHierarchy(hit.gameObject,data,ExecuteEvents.pointerClickHandler);
        }
        [Serializable]sealed class Report{public int checks;public string[] errors;}
        public static void Export()
        {
            if(!Application.isBatchMode)throw new InvalidOperationException("Batch export only.");
            var roots=new[]{"Assets/_ThirdParty/EscapeResultsKit","Assets/Scripts/EscapeResultsKit", "Assets/Resources/Prefabs/UIs/EscapeResultsKit","Assets/Scenes/Tests/EscapeResultsKit"};
            var files=AssetDatabase.FindAssets("",roots).Select(AssetDatabase.GUIDToAssetPath).ToArray();
            // Registry package scripts stay in UPM; include only Assets dependencies such as TMP shaders.
            var dependencies=AssetDatabase.GetDependencies(files,true).Where(p=>p.StartsWith("Assets/",StringComparison.Ordinal)).ToArray();
            AssetDatabase.ExportPackage(dependencies,"Artifacts/curtaincall-escape-results-v1.unitypackage",ExportPackageOptions.Default);
            Debug.Log("ESCAPE_KIT_PACKAGE_OK");
        }
    }
}
#endif
