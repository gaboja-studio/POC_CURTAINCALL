if (!UnityEditor.AssetDatabase.IsValidFolder("Assets/Resources/UI")) UnityEditor.AssetDatabase.CreateFolder("Assets/Resources", "UI");
if (!UnityEditor.AssetDatabase.IsValidFolder("Assets/Resources/UI/Inventory")) UnityEditor.AssetDatabase.CreateFolder("Assets/Resources/UI", "Inventory");
var border = new UnityEngine.Texture2D(128,128,UnityEngine.TextureFormat.RGBA32,false);
var hand = new UnityEngine.Texture2D(64,64,UnityEngine.TextureFormat.RGBA32,false);
var clear = new UnityEngine.Color(0,0,0,0);
for(int y=0;y<128;y++) for(int x=0;x<128;x++) {
 float dx=UnityEngine.Mathf.Max(UnityEngine.Mathf.Abs(x-63.5f)-52f,0f),dy=UnityEngine.Mathf.Max(UnityEngine.Mathf.Abs(y-63.5f)-52f,0f);
 float d=UnityEngine.Mathf.Sqrt(dx*dx+dy*dy); bool outside=d<=8f; bool inside=UnityEngine.Mathf.Abs(x-63.5f)<53f && UnityEngine.Mathf.Abs(y-63.5f)<53f;
 var col=outside&&!inside?new UnityEngine.Color(.96f,.78f,.39f,1f):clear;
 if(outside&&!inside&&d>5f)col=new UnityEngine.Color(.55f,.36f,.15f,1f);
 border.SetPixel(x,y,col);
}
System.Func<float,float,float,float,float,float,bool> seg=(x,y,ax,ay,bx,by)=>{float vx=bx-ax,vy=by-ay,t=UnityEngine.Mathf.Clamp01(((x-ax)*vx+(y-ay)*vy)/(vx*vx+vy*vy));float ex=x-(ax+t*vx),ey=y-(ay+t*vy);return ex*ex+ey*ey<7f;};
for(int y=0;y<64;y++)for(int x=0;x<64;x++){
 bool shape=seg(x,y,20,15,39,15)||seg(x,y,20,15,15,28)||seg(x,y,15,28,14,36)||seg(x,y,14,36,18,38)||seg(x,y,18,38,23,31)||seg(x,y,23,31,23,49)||seg(x,y,23,49,27,51)||seg(x,y,27,51,29,36)||seg(x,y,29,36,30,54)||seg(x,y,30,54,34,54)||seg(x,y,34,54,35,36)||seg(x,y,35,36,37,51)||seg(x,y,37,51,41,50)||seg(x,y,41,50,42,35)||seg(x,y,42,35,44,45)||seg(x,y,44,45,48,43)||seg(x,y,48,43,48,26)||seg(x,y,48,26,39,15);
 hand.SetPixel(x,y,shape?new UnityEngine.Color(.98f,.84f,.57f,1):clear);
}
border.Apply();hand.Apply();
System.IO.File.WriteAllBytes("Assets/Resources/UI/Inventory/inventory-selection.png",border.EncodeToPNG());
System.IO.File.WriteAllBytes("Assets/Resources/UI/Inventory/inventory-held.png",hand.EncodeToPNG());
UnityEngine.Object.DestroyImmediate(border);UnityEngine.Object.DestroyImmediate(hand);
foreach(var path in new[]{"Assets/Resources/UI/Inventory/inventory-selection.png","Assets/Resources/UI/Inventory/inventory-held.png"}){
 UnityEditor.AssetDatabase.ImportAsset(path,UnityEditor.ImportAssetOptions.ForceSynchronousImport);
 var importer=(UnityEditor.TextureImporter)UnityEditor.AssetImporter.GetAtPath(path); importer.textureType=UnityEditor.TextureImporterType.Sprite; importer.alphaIsTransparency=true; importer.mipmapEnabled=false; importer.textureCompression=UnityEditor.TextureImporterCompression.Uncompressed; importer.SaveAndReimport();
}
return "created selection and held sprites";
