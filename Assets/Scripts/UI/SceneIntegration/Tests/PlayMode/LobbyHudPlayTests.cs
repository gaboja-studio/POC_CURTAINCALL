using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CurtainCall.UI.SceneIntegration.Tests
{
    public sealed partial class DevStartLauncherPlayTests
    {
        [UnityTest]
        public IEnumerator LobbyHud_RealSessionReadyAndStartPreservePosition()
        {
            yield return SceneManager.LoadSceneAsync("Start",LoadSceneMode.Single);
            yield return MenuReady();
            var session=Find("CurtainCall.Network.Session.NetworkSessionManager");
            var host=(System.Threading.Tasks.Task<bool>)Call(session,"HostLanAsync",(ushort)17793);
            while(!host.IsCompleted)yield return null;
            Assert.IsTrue(host.Result);
            Assert.IsTrue((bool)Call(Find("CurtainCall.StartLobby.StartLobbyFlow"),"OpenLobbyAsHost"));
            float until=Time.realtimeSinceStartup+30;
            while((!SceneManager.GetSceneByName("Lobby").isLoaded || (bool)Static("CurtainCall.UI.SceneIntegration.SceneCurtainTransition","IsActive")) && Time.realtimeSinceStartup<until)yield return null;
            yield return new WaitForSecondsRealtime(.5f);
            var binding=Find("CurtainCall.UI.SceneIntegration.LobbyHudBinding");Assert.IsNotNull(binding);
            var info=Field(binding,"info");
            Assert.AreEqual(false,Get(info,"IsCodeVisible"));
            Call(info,"ToggleCodeVisibility");Assert.AreEqual(true,Get(info,"IsCodeVisible"));
            string oldClipboard=GUIUtility.systemCopyBuffer;
            try{Call(info,"RequestCopy");Assert.AreEqual(Get(session,"JoinKey"),GUIUtility.systemCopyBuffer);}
            finally{GUIUtility.systemCopyBuffer=oldClipboard;}
            var board=Field(binding,"board");Call(board,"Use");yield return null;
            Assert.AreEqual(true,Get(board,"LocalReady"));
            var room=Field(binding,"room");room.GetType().GetField("introSeconds").SetValue(room,30f);
            var player=(Component)Static("CurtainCall.Network.PlayerSync.NetworkPlayer","Local");
            Assert.IsNotNull(player);
            Assert.AreEqual(0,Get(player,"AppearanceOutfit"));
            var select=GameObject.Find("Outfit");Assert.IsNotNull(select);
            select.GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            yield return null;
            Assert.AreEqual(1,Get(player,"AppearanceOutfit"),"로비에서 선택한 의상은 호스트가 확정한다.");
            var catalog=T("CurtainCall.CharacterArt.PlayerAppearanceCatalog");
            var free=(int)catalog.GetMethod("FindAvailableOutfit").Invoke(null,new object[]{1,4,new[]{1}});
            Assert.AreEqual(2,free,"다음 참가자의 기본 의상이 이미 사용 중이면 남은 의상을 배정한다.");
            var lever=(Component)Field(binding,"lever");
            var target=lever.transform.position+Vector3.right*.5f;target.y=player.transform.position.y;
            Call(player.GetComponent(T("CurtainCall.Player.PlayerMover")),"SetStartPose",target,Quaternion.identity);
            yield return new WaitForSecondsRealtime(.1f);
            var before=player.transform.position;
            Call(lever,"Use");
            yield return new WaitForSecondsRealtime(.5f);
            Assert.AreEqual("Playing",Get(session,"GameState").ToString());
            var delta=player.transform.position-before;delta.y=0;
            Assert.Less(delta.magnitude,.25f,"Starting the intro must not send the host back to lobby spawn.");
            Assert.IsNull(GameObject.Find("Room Info"));
            Assert.IsTrue(((Component)info).gameObject.activeInHierarchy);
        }
    }
}
