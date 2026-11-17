using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CurtainCall.UI.SceneIntegration.Tests
{
    public sealed partial class DevStartLauncherPlayTests
    {
        [UnityTest] public IEnumerator AudienceBed_Tightrope() => CheckAudienceBed("Tightrope");
        [UnityTest] public IEnumerator AudienceBed_HumanCannon() => CheckAudienceBed("HumanCannon");

        [UnityTest]
        public IEnumerator AudienceSoundSettings_SaveCancelResetAndIndependentGain()
        {
            var restore = CaptureSettingsExtras();
            var gos = new System.Collections.Generic.List<GameObject>();
            try
            {
                yield return UnityEngine.SceneManagement.SceneManager.LoadSceneAsync("Start"); yield return MenuReady();
                var start = Find("CurtainCall.StartLobby.StartMenu");
                var owner = Find("CurtainCall.UI.SceneIntegration.CommonMenuUI");
                Call(start, "Show", System.Enum.Parse(T("CurtainCall.StartLobby.StartMenu+Page"), "Settings")); yield return null;
                var view = Get(Get(owner, "Settings"), "Main");
                Call(view, "ShowPage", 3); yield return null;
                string[] rows = { "audienceCheerVolume", "audienceBooVolume", "audienceMurmurVolume" };
                string[] properties = { "AudienceCheerVolume", "AudienceBooVolume", "AudienceMurmurVolume" };
                float[] original = properties.Select(p => (float)Static("CurtainCall.StartLobby.ClientSettings", p)).ToArray();
                for (int i=0;i<3;i++) SettingsSlider(view,rows[i]).value = .11f * (i+1);
                yield return MenuEscape();
                for (int i=0;i<3;i++) Assert.AreEqual(original[i], Static("CurtainCall.StartLobby.ClientSettings",properties[i]), "Cancel must preserve saved audio.");
                Call(start, "Show", System.Enum.Parse(T("CurtainCall.StartLobby.StartMenu+Page"), "Settings")); yield return null;
                Call(view,"ShowPage",3); yield return null;
                for (int i=0;i<3;i++) SettingsSlider(view,rows[i]).value = .11f * (i+1);
                SettingsSlider(view,"sfxVolume").value=.5f;
                Call(view,"RequestApply"); yield return null;
                var client=T("CurtainCall.StartLobby.ClientSettings");
                var mixer=T("CurtainCall.Settings.LocalAudioController");
                var category=T("CurtainCall.Settings.LocalAudioCategory");
                for (int i=0;i<3;i++)
                {
                    Assert.AreEqual(.11f*(i+1), (float)Static(client.FullName,properties[i]), .001f);
                    var go=new GameObject("Audience gain verification"); gos.Add(go); var source=go.AddComponent<AudioSource>();
                    mixer.GetMethod("Register").Invoke(null,new object[]{source,System.Enum.Parse(category,new[]{"AudienceCheer","AudienceBoo","AudienceMurmur"}[i])});
                    mixer.GetMethod("SetSourceVolume").Invoke(null,new object[]{source,.8f});
                    Assert.AreEqual(.8f*.5f*.11f*(i+1),source.volume,.001f);
                    client.GetProperty(properties[i]).SetValue(null,0f);
                    Assert.AreEqual(0,source.volume,.001f,"Live channel mute updates an existing source.");
                    client.GetProperty(properties[i]).SetValue(null,.11f*(i+1));
                    Assert.AreEqual(.8f*.5f*.11f*(i+1),source.volume,.001f,"Unmute restores base gain, without multiplying it twice.");
                }
                Call(start,"Show",System.Enum.Parse(T("CurtainCall.StartLobby.StartMenu+Page"),"Settings")); yield return null;
                Call(view,"ShowPage",3); yield return null;
                TabReset(view,"audio").onClick.Invoke(); yield return null;
                for(int i=0;i<3;i++) { Assert.AreEqual(1,SettingsSlider(view,rows[i]).value); Assert.AreEqual(.11f*(i+1),(float)Static(client.FullName,properties[i]),.001f); }
                Call(view,"RequestApply"); yield return null;
                for(int i=0;i<3;i++) Assert.AreEqual(1,Static(client.FullName,properties[i]));
            }
            finally { foreach(var go in gos) Object.Destroy(go); restore(); }
        }

        IEnumerator CheckAudienceBed(string trick)
        {
            var session = T("CurtainCall.Network.Session.NetworkSessionManager").GetMethod("EnsureExists").Invoke(null, null);
            if (!(bool)Get(session, "IsHost"))
            {
                var hosting = (System.Threading.Tasks.Task<bool>)Call(session, "HostLanAsync", (ushort)17944);
                while (!hosting.IsCompleted) yield return null;
                Assert.IsTrue(hosting.Result);
            }
            yield return Launch(2, "Performance", 0, trick, trick);
            yield return new WaitForSecondsRealtime(.35f);
            var rewards = Find("CurtainCall.Reward.PerformanceRewards");
            var common = Find("CurtainCall.UI.SceneIntegration.CommonPerformanceUI");
            var feedback = Get(common, "Feedback"); var view = Get(feedback, "View");
            var audio = Get(feedback, "ReactionAudio");
            Assert.IsNull(Get(feedback, "Numbers"), "The rejected numeric panel is disconnected.");
            Assert.IsNotNull(view); Assert.IsNotNull(audio);
            var sources = ((Component)audio).GetComponentsInChildren<AudioSource>();
            Assert.AreEqual(3, sources.Length);
            var bed = sources.Single(s=>s.loop); var shots = sources.Where(s=>!s.loop).ToArray();
            Assert.IsTrue(bed.isPlaying); Assert.AreEqual(30f, bed.clip.length, .01f);
            Assert.AreEqual(1, (int)Get(audio, "AmbienceStartCount"));
            Assert.AreEqual(0, (int)Get(audio, "PlayCount"), "Opening a venue must not fabricate a reaction.");
            Assert.IsTrue(shots.All(s=>s.spatialBlend > 0 && s.dopplerLevel == 0));
            var labels = ((Component)view).GetComponentsInChildren<TMPro.TMP_Text>(true);
            var person = ((IEnumerable)Get(Get(rewards, "State"), "Segments")).Cast<object>().First();
            string id = (string)Get(person, "ParticipantId");
            Assert.IsTrue((bool)Call(rewards, "AddAudienceReaction", id, 6));
            var updated = ((IEnumerable)Get(Get(rewards, "State"), "Segments")).Cast<object>().First(p=>(string)Get(p,"ParticipantId")==id);
            int actual = (int)Get(updated, "ReactionAmount");
            yield return new WaitForSecondsRealtime(.2f);
            Assert.IsTrue((bool)Get(view, "IsVisible"), (string)Get(view, "LastIssue"));
            Assert.IsTrue(labels.Any(t=>t.text=="환호")); Assert.IsTrue(labels.Any(t=>t.text=="+"+actual));
            Assert.AreEqual(1, (int)Get(audio, "PlayCount"));
            Assert.IsTrue((bool)Call(rewards, "AddAudienceReaction", id, -5));
            Assert.AreEqual(2, (int)Get(audio, "PlayCount"), "One-shot audio is immediate even while the original popup is occupied.");
            yield return new WaitForSecondsRealtime(.2f);
            Assert.IsTrue(labels.Any(t=>t.text=="야유")); Assert.IsTrue(labels.Any(t=>t.text=="−5"));
            int starts = (int)Get(audio, "AmbienceStartCount");
            float phase = bed.time;
            Call(feedback, "SetContext", false); Call(feedback, "Clear");
            Assert.IsFalse((bool)Get(view, "HasRuntimePresentation"));
            Assert.IsFalse(shots.Any(s=>s.isPlaying)); Assert.IsTrue(bed.isPlaying, "Personal focus/menu changes must not cut the crowd bed.");
            yield return new WaitForSecondsRealtime(.15f);
            Assert.AreEqual(starts, (int)Get(audio, "AmbienceStartCount"));
            Assert.Greater((bed.time - phase + bed.clip.length) % bed.clip.length, .02f);
            Assert.IsFalse(shots.Any(s=>s.isPlaying), "Old reactions cannot replay.");
            ((Behaviour)Find("CurtainCall.UI.SceneIntegration.PerformanceSceneBinding")).enabled = false;
            Call(feedback, "SetVenueContext", false);
            yield return new WaitForSecondsRealtime(1.4f);
            Assert.IsTrue(bed == null || !bed.isPlaying, "Leaving the performance stops or unloads the continuous bed.");
        }
    }
}
