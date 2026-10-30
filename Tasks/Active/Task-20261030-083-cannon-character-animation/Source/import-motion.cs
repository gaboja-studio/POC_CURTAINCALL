var root="Assets/Resources/Models/Characters/CannonMotion";
AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
var source=(ModelImporter)AssetImporter.GetAtPath("Assets/_ThirdParty/CharacterArt/Rabbit/RabbitV14.fbx");
var names=System.IO.Directory.GetFiles(root,"*.fbx").Select(System.IO.Path.GetFileNameWithoutExtension).OrderBy(n=>n).ToArray();
var rows=new System.Collections.Generic.List<object>();
foreach(var name in names){
var path=root+"/"+name+".fbx";
var m=(ModelImporter)AssetImporter.GetAtPath(path);
m.animationType=ModelImporterAnimationType.Human;m.avatarSetup=ModelImporterAvatarSetup.CopyFromOther; m.sourceAvatar=AssetDatabase.LoadAllAssetsAtPath("Assets/_ThirdParty/CharacterArt/Rabbit/RabbitV14.fbx").OfType<Avatar>().First();
m.importAnimation=true;m.animationCompression=ModelImporterAnimationCompression.Off;
var clips=m.defaultClipAnimations;
foreach(var c in clips){c.name=name;c.loopTime=!new[]{"FlightApex","FlightLand","RopePull","RopeLift"}.Any(n=>name.StartsWith(n));c.loopPose=c.loopTime;c.lockRootRotation=true;c.lockRootHeightY=true;c.lockRootPositionXZ=true;c.keepOriginalOrientation=true;c.keepOriginalPositionY=true;c.keepOriginalPositionXZ=true;}
m.clipAnimations=clips;m.SaveAndReimport();
var a=m.sourceAvatar ?? AssetDatabase.LoadAllAssetsAtPath(path).OfType<Avatar>().FirstOrDefault();
var clip=AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().First(c=>!c.name.StartsWith("__preview__"));
rows.Add(new{name,valid=a!=null&&a.isValid,human=clip.humanMotion,clip.length,curves=AnimationUtility.GetCurveBindings(clip).Length});
}
return rows;


