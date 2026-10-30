var lines=new System.Collections.Generic.List<string>();
foreach(var path in new[]{"Assets/Scenes/Levels/escape_route.unity","Assets/Scenes/Levels/escape_route_b.unity","Assets/Scenes/Levels/escape_route_c.unity"})
{
 var scene=UnityEditor.SceneManagement.EditorSceneManager.OpenPreviewScene(path);
 try {var stations=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<CurtainCall.HumanCannon.HumanCannonStation>(true)).ToArray();
  int expected=path.EndsWith("escape_route.unity")?4:path.EndsWith("escape_route_b.unity")?5:3;
  if(stations.Length!=expected)throw new System.Exception(path+" station count="+stations.Length);
  foreach(var s in stations)
  { var source=PrefabUtility.GetCorrespondingObjectFromSource(s.gameObject);var view=s.GetComponentInChildren<CurtainCall.HumanCannon.CannonBarrelPresentation>(true);
    if(view==null || s.transform.Find("CannonVisual").GetComponentsInChildren<Renderer>(true).Any(r=>r.enabled))throw new System.Exception(path+" "+s.name+" missing animated art or old art visible");
    lines.Add(path+" | "+s.name+" | source="+(source?AssetDatabase.GetAssetPath(source):"none")+" | art="+(view!=null)+" | renderers="+string.Join(",",s.GetComponentsInChildren<Renderer>(true).Select(r=>r.name+":"+r.enabled))); }}
 finally{UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);}
}
System.IO.File.WriteAllLines("Tasks/Active/Task-20261030-084-cannon-barrel-state-animation/Source/escape-cannon-audit.txt",lines);return string.Join("\n",lines);
