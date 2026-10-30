var parent="Assets/Resources/Models";
foreach(var name in new[]{"Characters","CannonMotion"}) { var next=parent+"/"+name; if(!UnityEditor.AssetDatabase.IsValidFolder(next)) UnityEditor.AssetDatabase.CreateFolder(parent,name); parent=next; }
return parent;
