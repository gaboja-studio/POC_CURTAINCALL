var source="Assets/Tasks/Active/Task-20261117-153-ingame-world-ui/Evidence";
var destination="Tasks/Active/Task-20261117-153-ingame-world-ui/Evidence";
System.IO.Directory.CreateDirectory(destination);
foreach(var file in System.IO.Directory.GetFiles(source,"*.png"))
 System.IO.File.Copy(file,System.IO.Path.Combine(destination,System.IO.Path.GetFileName(file)),true);
return UnityEditor.AssetDatabase.DeleteAsset("Assets/Tasks");
