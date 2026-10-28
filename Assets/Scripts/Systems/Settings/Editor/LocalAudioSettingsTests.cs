using CurtainCall.StartLobby;
using CurtainCall.Voice;
using NUnit.Framework;
using UnityEngine;

namespace CurtainCall.Settings.EditorTests
{
    public sealed class LocalAudioSettingsTests
    {
        static readonly string[] Keys = { "CurtainCall.Settings.Volume", "CurtainCall.Settings.BgmVolume", "CurtainCall.Settings.SfxVolume" };
        readonly bool[] existed = new bool[3];
        readonly float[] values = new float[3];
        float listener;

        [SetUp]
        public void SetUp()
        {
            listener = AudioListener.volume;
            for (int i = 0; i < Keys.Length; i++)
            {
                existed[i] = PlayerPrefs.HasKey(Keys[i]);
                values[i] = PlayerPrefs.GetFloat(Keys[i]);
                PlayerPrefs.DeleteKey(Keys[i]);
            }
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < Keys.Length; i++)
                if (existed[i]) PlayerPrefs.SetFloat(Keys[i], values[i]); else PlayerPrefs.DeleteKey(Keys[i]);
            AudioListener.volume = listener;
        }

        [Test]
        public void Defaults_PreserveMasterContract_AndUnchangedCategoryGain()
        {
            Assert.AreEqual(.8f, ClientSettings.Volume);
            Assert.AreEqual(1f, ClientSettings.BgmVolume);
            Assert.AreEqual(1f, ClientSettings.SfxVolume);
        }

        [Test]
        public void CategoryChanges_DoNotApplyMasterAgain()
        {
            ClientSettings.Volume = .23f;
            ClientSettings.BgmVolume = .4f;
            ClientSettings.SfxVolume = .6f;
            Assert.AreEqual(.23f, AudioListener.volume);
            Assert.AreEqual(.4f, PlayerPrefs.GetFloat(Keys[1]));
            Assert.AreEqual(.6f, PlayerPrefs.GetFloat(Keys[2]));
        }

        [Test]
        public void Categories_ClampAndNotify()
        {
            int changes = 0;
            void Changed() => changes++;
            ClientSettings.AudioChanged += Changed;
            try
            {
                ClientSettings.BgmVolume = -1f;
                ClientSettings.SfxVolume = 2f;
                ClientSettings.Volume = .5f;
                Assert.AreEqual(0f, ClientSettings.BgmVolume);
                Assert.AreEqual(1f, ClientSettings.SfxVolume);
                Assert.AreEqual(3, changes);
            }
            finally { ClientSettings.AudioChanged -= Changed; }
        }

        [TestCase(0f, -50)]
        [TestCase(.5f, -25)]
        [TestCase(1f, 0)]
        [TestCase(2f, 0)]
        public void ExistingVivoxGainContract_IsPreserved(float normalized, int expected) =>
            Assert.AreEqual(expected, VoiceChat.ToVivoxVolume(normalized));
    }
}
