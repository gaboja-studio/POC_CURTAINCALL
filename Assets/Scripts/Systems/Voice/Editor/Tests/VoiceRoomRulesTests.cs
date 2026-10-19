using CurtainCall.Network.PlayerSync;
using NUnit.Framework;

namespace CurtainCall.Voice.EditorTools.Tests
{
    /// <summary>Task-063 음성 무리 규칙: 사망 판정(추락·리타이어), 사용 씬(Start 흐름 확정 씬만), 원하는 무리, 채널 이름(같은 방·무리 = 같은 이름, 무리끼리 다름).</summary>
    public class VoiceRoomRulesTests
    {
        const string Key = "3f2a9c1e-7b4d-4e11-9a0b-1234567890ab";

        [TestCase(PlayerState.Normal, false)]
        [TestCase(PlayerState.Arrived, false)]
        [TestCase(PlayerState.Fallen, true)]
        [TestCase(PlayerState.Retired, true)]
        public void IsDead_FallenAndRetiredOnly(PlayerState state, bool dead)
        {
            Assert.AreEqual(dead, VoiceRoomRules.IsDead(state));
        }

        static readonly string[] FlowScenes = { "Start", "Lobby", "WaitingRoomBase", "Tightrope", "HumanCannon" };

        [TestCase("Start")]
        [TestCase("Lobby")]
        [TestCase("WaitingRoomBase")]
        [TestCase("Tightrope")]
        [TestCase("HumanCannon")]
        public void IsVoiceScene_StartFlowConfirmedScenes_True(string scene)
        {
            Assert.IsTrue(VoiceRoomRules.IsVoiceScene(true, scene, FlowScenes));
        }

        [Test]
        public void IsVoiceScene_NotStartFlowOrUnlistedScene_False()
        {
            Assert.IsFalse(VoiceRoomRules.IsVoiceScene(false, "Tightrope", FlowScenes), "테스트 씬을 직접 연 경우(Start 흐름 아님)");
            Assert.IsFalse(VoiceRoomRules.IsVoiceScene(true, "DummyExploration", FlowScenes), "아직 확정 안 된 공간");
            Assert.IsFalse(VoiceRoomRules.IsVoiceScene(true, "tightrope", FlowScenes), "씬 이름은 대소문자까지 같아야 함");
            Assert.IsFalse(VoiceRoomRules.IsVoiceScene(true, "Lobby", null));
            Assert.IsTrue(VoiceRoomRules.IsVoiceScene(true, "Lobby", new[] { " Lobby " }), "세팅 값 앞뒤 공백 무시");
        }

        [Test]
        public void Desired_AliveStates_Alive()
        {
            Assert.AreEqual(VoiceRoom.Alive, VoiceRoomRules.Desired(true, true, Key, true, PlayerState.Normal));
            Assert.AreEqual(VoiceRoom.Alive, VoiceRoomRules.Desired(true, true, Key, true, PlayerState.Arrived));
        }

        [Test]
        public void Desired_DeadStates_Dead()
        {
            Assert.AreEqual(VoiceRoom.Dead, VoiceRoomRules.Desired(true, true, Key, true, PlayerState.Fallen));
            Assert.AreEqual(VoiceRoom.Dead, VoiceRoomRules.Desired(true, true, Key, true, PlayerState.Retired));
        }

        [Test]
        public void Desired_MissingConditions_None()
        {
            Assert.AreEqual(VoiceRoom.None, VoiceRoomRules.Desired(false, true, Key, true, PlayerState.Normal), "꺼짐");
            Assert.AreEqual(VoiceRoom.None, VoiceRoomRules.Desired(true, false, Key, true, PlayerState.Normal), "음성 씬 아님");
            Assert.AreEqual(VoiceRoom.None, VoiceRoomRules.Desired(true, false, Key, true, PlayerState.Fallen), "음성 씬 아님(사망)");
            Assert.AreEqual(VoiceRoom.None, VoiceRoomRules.Desired(true, true, null, true, PlayerState.Normal), "방 키 없음");
            Assert.AreEqual(VoiceRoom.None, VoiceRoomRules.Desired(true, true, "---", true, PlayerState.Normal), "쓸 수 있는 글자 없음");
            Assert.AreEqual(VoiceRoom.None, VoiceRoomRules.Desired(true, true, Key, false, PlayerState.Fallen), "내 캐릭터 없음");
        }

        [Test]
        public void ChannelName_SameRoomSameName_DifferentPerRoom()
        {
            string alive = VoiceRoomRules.ChannelName(Key, VoiceRoom.Alive);
            string dead = VoiceRoomRules.ChannelName(Key, VoiceRoom.Dead);

            Assert.AreEqual("cc-3f2a9c1e7b4d4e119a0b1234567890ab-alive", alive);
            Assert.AreEqual("cc-3f2a9c1e7b4d4e119a0b1234567890ab-dead", dead);
            Assert.AreNotEqual(alive, dead, "생존·사망 채널이 같으면 서로 들린다");
            Assert.AreEqual(alive, VoiceRoomRules.ChannelName(Key.ToUpperInvariant(), VoiceRoom.Alive), "대소문자 무관");
            Assert.AreNotEqual(alive, VoiceRoomRules.ChannelName("otherroom", VoiceRoom.Alive), "다른 방과 섞이지 않음");
        }

        [Test]
        public void ChannelName_NoneOrBadKey_Null()
        {
            Assert.IsNull(VoiceRoomRules.ChannelName(Key, VoiceRoom.None));
            Assert.IsNull(VoiceRoomRules.ChannelName(null, VoiceRoom.Alive));
            Assert.IsNull(VoiceRoomRules.ChannelName("!@#", VoiceRoom.Dead));
        }

        [Test]
        public void Sanitize_LimitsLengthAndCharacters()
        {
            string longKey = new string('a', 100) + "!";
            Assert.AreEqual(VoiceRoomRules.MaxKeyLength, VoiceRoomRules.Sanitize(longKey).Length);
            Assert.AreEqual("abc123", VoiceRoomRules.Sanitize("A-b_C 1.2/3"));
        }
    }
}
