using UnityEditor;
using UnityEngine;
namespace CurtainCall.UI.SceneIntegration.Editor
{
    [CustomPropertyDrawer(typeof(PerformanceHudProfile.Cue))]
    public sealed class PerformanceCueDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty p,GUIContent label)
        {
            float h=EditorGUIUtility.singleLineHeight+4;
            if(!p.isExpanded)return h;
            foreach(string k in new[]{"title","body","badge"}) h+=EditorGUI.GetPropertyHeight(p.FindPropertyRelative(k))+3;
            return h+PreviewHeight(p);
        }
        static string Preview(SerializedProperty p)
        {
            string Value(string k)=>PerformanceHudProfile.Format(p.FindPropertyRelative(k).stringValue,null);
            string badge=Value("badge");return "기본 키 미리보기\n"+(badge.Length>0?"["+badge+"]  ":"")+Value("title")+"\n"+Value("body");
        }
        static float PreviewHeight(SerializedProperty p)=>EditorStyles.helpBox.CalcHeight(new GUIContent(Preview(p)),Mathf.Max(100,EditorGUIUtility.currentViewWidth-75))+6;
        public override void OnGUI(Rect r,SerializedProperty p,GUIContent label)
        {
            var name=(InspectorNameAttribute)System.Attribute.GetCustomAttribute(fieldInfo,typeof(InspectorNameAttribute));
            var trigger=(TooltipAttribute)System.Attribute.GetCustomAttribute(fieldInfo,typeof(TooltipAttribute));
            label=new GUIContent(name?.displayName ?? label.text,trigger?.tooltip ?? label.tooltip);
            EditorGUI.BeginProperty(r,label,p);
            r.height=EditorGUIUtility.singleLineHeight;
            p.isExpanded=EditorGUI.Foldout(r,p.isExpanded,label,true);
            if(p.isExpanded)
            {
                EditorGUI.indentLevel++;r.y+=r.height+4;
                foreach(string k in new[]{"title","body","badge"}) {
                    var v=p.FindPropertyRelative(k);r.height=EditorGUI.GetPropertyHeight(v);EditorGUI.PropertyField(r,v);r.y+=r.height+3;
                }
                r.height=PreviewHeight(p)-3;EditorGUI.HelpBox(r,Preview(p),MessageType.None);EditorGUI.indentLevel--;
            }
            EditorGUI.EndProperty();
        }
    }
    public static class PerformanceHudMenu
    {
        [MenuItem("CurtainCall/UI/공연 HUD 문구")]
        public static void Open()
        {
            const string path="Assets/Resources/"+PerformanceHudProfile.ResourcePath+".asset";
            var asset=AssetDatabase.LoadAssetAtPath<PerformanceHudProfile>(path);
            if(!asset){asset=ScriptableObject.CreateInstance<PerformanceHudProfile>();AssetDatabase.CreateAsset(asset,path);}
            Selection.activeObject=asset;
        }
    }
}
