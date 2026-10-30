using System;
using System.Linq;
using CurtainCall.HumanCannon;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
namespace CurtainCall.EscapeRoute.EditorTools
{
    public static partial class EscapeBackgroundBuilder
    {
        public static string PolishAReveal()
        {
            var bg=GameObject.Find(RootName).transform;
            if(Application.isPlaying||bg.Find("42_AInternalMachineryVista")!=null)throw new InvalidOperationException("Already polished or playing");
            CompletionMaterials();midWarning=RunnerMaterial("solo-danger-red");
            var root=VerticalChild(bg,"42_AInternalMachineryVista");root.localScale=new Vector3(1/bg.lossyScale.x,1/bg.lossyScale.y,1/bg.lossyScale.z);
            var floor=bg.Find("17_BrutalistRoute/01_Understage/무대 하부 바닥");floor.gameObject.SetActive(false);
            AFloor(root,"시작홀 연속 마감 바닥",0,30,0,22.5f,0);
            // Working equipment across the shaft previews the exact original saw/press motions.
            AFloor(root,"심부 설비 정비 선반",41,50,9,31,-2);
            BMass(root,"설비 선반 심부 구조",new Vector3(47,-50,20),new Vector3(4,96,18),bRecess);
            MidHazard(root,"앞으로 만날 횡단 톱", "01_LeftSaw",new Vector3(43,1.2f,12),new Vector3(43,1.2f,25),new Vector3(1.2f,1.2f,.25f),new Vector3(43,-1.97f,18.5f),new Vector3(1.4f,.02f,13),1.5f,3,2,0);
            MidHazard(root,"원거리 가동 압착 설비", "05_DropLeft",new Vector3(47,10,27),new Vector3(47,-.7f,27),new Vector3(4,2.2f,3),new Vector3(47,-1.97f,27),new Vector3(4,.02f,3),1.8f,1.2f,3,2);
            foreach(float z in new[]{24.8f,29.2f})BMass(root,"압착 설비 정착 가이드",new Vector3(47,4,z),new Vector3(.6f,12,.6f),wornSteel);
            BMass(root,"가동 설비 상부 보",new Vector3(47,11,27),new Vector3(5,1,5.5f),bRecess);
            BLight(root,new Vector3(38,7,13),new Vector3(44,0,17),1000,28);
            BLight(root,new Vector3(40,9,29),new Vector3(47,1,27),900,25);
            var hd=new SerializedObject(UnityEngine.Object.FindFirstObjectByType<HumanCannonHazardCourse>());
            SetFinaleArray(hd,"hazards",UnityEngine.Object.FindObjectsByType<HumanCannonHazard>(FindObjectsSortMode.None).Where(h=>h.isActiveAndEnabled).OrderBy(h=>h.name,StringComparer.Ordinal).ToArray());hd.ApplyModifiedProperties();
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());Physics.SyncTransforms();return "Start floor closed, actual moving machinery visible across enclosed shaft";
        }
    }
}
