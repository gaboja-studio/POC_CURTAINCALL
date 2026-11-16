using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using CurtainCall.UI.SceneIntegration.Editor;
public static class ScheduleKitExport
{
 public static object Run()
 {
  string kit=ScheduleTicketKitBuilder.Kit;
  if(!AssetDatabase.IsValidFolder(kit+"/Exports")) AssetDatabase.CreateFolder(kit,"Exports");
  var sprites=Directory.GetFiles(kit+"/Sprites","*.png").SelectMany(p=>AssetDatabase.LoadAllAssetsAtPath(p.Replace('\\','/')).OfType<Sprite>()).Select(s=>new {name=s.name,source=AssetDatabase.GetAssetPath(s),rect=new {x=s.rect.x,y=s.rect.y,width=s.rect.width,height=s.rect.height},ppu=s.pixelsPerUnit}).ToArray();
  File.WriteAllText(kit+"/sprite-catalog.json",Newtonsoft.Json.JsonConvert.SerializeObject(sprites,Newtonsoft.Json.Formatting.Indented));
  AssetDatabase.ImportAsset(kit+"/sprite-catalog.json");
  string output=kit+"/Exports/schedule-ticket-v2.unitypackage";
  AssetDatabase.ExportPackage(new [] {kit+"/Sprites",kit+"/readme.md",kit+"/approved-concept.png",kit+"/sprite-catalog.json",ScheduleTicketKitBuilder.PrefabPath,"Assets/Resources/Fonts/ScheduleUi/ScheduleTicket-Bold SDF.asset","Assets/Scripts/UI/SceneIntegration/ScheduleTicketCard.cs","Assets/Scripts/UI/SceneIntegration/Editor/ScheduleTicketKitBuilder.cs","Assets/Scripts/UI/SceneIntegration/WaitingRoomScheduleUI.cs","Assets/Scripts/UI/SceneIntegration/WaitingRoomScheduleUI.Board.cs","Assets/Scripts/UI/SceneIntegration/PanelHoverTooltip.cs","Assets/Scripts/WaitingRoom/NightSchedule/ScheduleScreenHold.cs","Assets/Scripts/Systems/Voice/VoiceChatDevOverlay.cs"},output,ExportPackageOptions.Recurse);
  return new {output,spriteCount=sprites.Length,bytes=new FileInfo(output).Length};
 }
}
