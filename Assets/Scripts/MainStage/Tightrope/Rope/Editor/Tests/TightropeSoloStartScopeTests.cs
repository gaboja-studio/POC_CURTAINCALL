using System;
using System.Collections.Generic;
using System.Reflection;
using CurtainCall.Dev;
using CurtainCall.Network.Session;
using CurtainCall.Settings;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CurtainCall.Tightrope.EditorTools.Tests
{
    /// <summary>1인 시작 요청은 세션·콘텐츠 수명 안에서만 유효하며 정식 인원 규칙을 바꾸지 않는다.</summary>
    public sealed class TightropeSoloStartScopeTests
    {
        readonly List<GameObject> objects = new();
        readonly List<IDisposable> requests = new();
        FieldInfo devModeCache;
        object previousDevMode;

        [SetUp]
        public void SetUp()
        {
            devModeCache = typeof(DevMode).GetField("_enabled", BindingFlags.Static | BindingFlags.NonPublic);
            previousDevMode = devModeCache.GetValue(null);
            devModeCache.SetValue(null, false);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var request in requests) request.Dispose();
            requests.Clear();
            foreach (var go in objects) if (go != null) Object.DestroyImmediate(go);
            objects.Clear();
            devModeCache?.SetValue(null, previousDevMode);
        }

        [Test]
        public void SoloRequestAllowsOneWaitingHostAndRestoresOriginalRulesOnRelease()
        {
            var session = Session();
            int official = GameSettings.Base.Session.MinPlayers;
            int capacity = session.RequiredPlayers;
            Assert.That(official, Is.GreaterThan(1), "테스트는 개발자 모드를 끈 정식 최소 인원에서 시작한다.");
            Set(session, "ConnectionState", SessionConnectionState.Host);
            Set(session, "PlayerCount", 1);
            Assert.That(session.CanStartGame, Is.False);
            var request = Request(session, 1);
            Assert.That(session.CanStartGame, Is.True);
            Assert.That(session.MinPlayers, Is.EqualTo(1));
            Assert.That(session.GameState, Is.EqualTo(GameSessionState.Waiting));
            Assert.That(session.RequiredPlayers, Is.EqualTo(capacity));
            Assert.That(GameSettings.Base.Session.MinPlayers, Is.EqualTo(official));
            Assert.That(DevMode.Enabled, Is.False);
            request.Dispose();
            Assert.That(session.MinPlayers, Is.EqualTo(official));
            Assert.That(session.CanStartGame, Is.False);
        }

        [Test]
        public void IndependentRequestsAreReleasedIndividuallyAndCannotAffectAnotherSession()
        {
            var first = Session();
            var second = Session();
            int official = second.MinPlayers;
            var one = Request(first, 1);
            var two = Request(first, 1);
            Request(first, official + 1); // 완화 요청이 기본 규칙을 더 엄격하게 만들지는 않는다.
            Assert.That(second.MinPlayers, Is.EqualTo(official));
            one.Dispose();
            one.Dispose();
            Assert.That(first.MinPlayers, Is.EqualTo(1));
            two.Dispose();
            Assert.That(first.MinPlayers, Is.EqualTo(official));
        }

        [Test]
        public void SoloRequestDoesNotGrantClientAuthorityOrAllowStartingAnAlreadyPlayingGame()
        {
            var session = Session();
            Request(session, 1);
            Set(session, "PlayerCount", 1);
            Set(session, "ConnectionState", SessionConnectionState.Client);
            Assert.That(session.CanStartGame, Is.False);
            Set(session, "ConnectionState", SessionConnectionState.Host);
            Set(session, "GameState", GameSessionState.Playing);
            Assert.That(session.CanStartGame, Is.False);
        }

        [Test]
        public void InvalidRequestCannotAllowAnEmptyRoomToStart()
        {
            var session = Session();
            Assert.Throws<ArgumentOutOfRangeException>(() => session.RequestMinimumStartPlayers(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => session.RequestMinimumStartPlayers(-1));
            Request(session, 1);
            Set(session, "ConnectionState", SessionConnectionState.Host);
            Assert.That(session.CanStartGame, Is.False);
        }

        NetworkSessionManager Session()
        {
            // EditMode에서 네트워크 접속이나 Awake를 시작하지 않고 정책·권한 경계만 검사한다.
            var go = new GameObject("Minimum start player scope test");
            objects.Add(go);
            return go.AddComponent<NetworkSessionManager>();
        }

        IDisposable Request(NetworkSessionManager session, int minimum)
        {
            var request = session.RequestMinimumStartPlayers(minimum);
            requests.Add(request);
            return request;
        }

        static void Set(NetworkSessionManager session, string property, object value) =>
            typeof(NetworkSessionManager).GetProperty(property).SetValue(session, value);
    }
}
