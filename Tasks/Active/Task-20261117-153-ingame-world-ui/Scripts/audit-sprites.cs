var sprites = UnityEditor.AssetDatabase.FindAssets("t:Sprite",new[]{"Assets/_ThirdParty/curtaincall_world_ui_v1/Sprites"});
var report = new System.Collections.Generic.List<object>();
foreach(var guid in sprites) {
 var path=UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
 var texture=new UnityEngine.Texture2D(2,2);
 UnityEngine.ImageConversion.LoadImage(texture,System.IO.File.ReadAllBytes(path));
 int clear=0,partial=0,white=0,opaque=0;
 foreach(var p in texture.GetPixels32()) { if(p.a==0)clear++; else { if(p.a==255)opaque++;else partial++; if(p.r==255 && p.g==255 && p.b==255)white++; } }
 report.Add(new{path,width=texture.width,height=texture.height,clear,partial,opaque,white});
 UnityEngine.Object.DestroyImmediate(texture);
}
return report;
