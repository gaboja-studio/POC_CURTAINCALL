var source=(UnityEditor.ModelImporter)UnityEditor.AssetImporter.GetAtPath("Assets/_ThirdParty/CharacterArt/Rabbit/RabbitV14.fbx");
var m=(UnityEditor.ModelImporter)UnityEditor.AssetImporter.GetAtPath("Assets/Resources/Models/Characters/CannonMotion/CannonLoaded.fbx");
return new{original=source.humanDescription.skeleton.Where(b=>b.name=="Hips"||b.name=="LeftUpperArm"||b.name=="CC_MotionRig"||b.name=="RabbitRig_v14").Select(b=>new{b.name,r=b.rotation.ToString(),p=b.position.ToString()}).ToArray(),import=m.humanDescription.skeleton.Where(b=>b.name=="Hips"||b.name=="LeftUpperArm"||b.name=="CC_MotionRig"||b.name=="RabbitRig_v14").Select(b=>new{b.name,r=b.rotation.ToString(),p=b.position.ToString()}).ToArray()};

