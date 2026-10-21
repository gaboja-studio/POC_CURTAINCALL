using System;
using System.Collections;
using System.Reflection;
using System.Threading.Tasks;
using CurtainCall.Network.Session;
using CurtainCall.StartLobby;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace CurtainCall.UI.SceneIntegration.EditorTools.Tests
{
    public sealed class StartConnectionTests
    {
        StartMenu menu;
        NetworkSessionManager session;
        const string Failure = "방이 없거나 네트워크 연결이 불안정 합니다";
        Button[] Buttons => new[] { menu.host, menu.join, menu.lan, menu.settings, menu.quit,
            menu.joinConfirm, menu.joinBack, menu.lanHost, menu.lanJoin, menu.lanBack };

        [UnitySetUp]
        public IEnumerator Open()
        {
            yield return new EnterPlayMode();
            Assert.IsTrue(Application.isPlaying);
            yield return SceneManager.LoadSceneAsync("Start");
            session = NetworkSessionManager.EnsureExists();
            yield return null;
            yield return null;
            menu = UnityEngine.Object.FindFirstObjectByType<StartMenu>();
            Assert.NotNull(menu);
            Assert.NotNull(session);
        }

        [UnityTearDown]
        public IEnumerator Close()
        {
            if (session != null)
            {
                var leave = session.LeaveAsync();
                float until = Time.realtimeSinceStartup + 5;
                while ((!leave.IsCompleted || session.NetworkManager.ShutdownInProgress) && Time.realtimeSinceStartup < until)
                    yield return null;
            }
            yield return new ExitPlayMode();
        }

        void Run(Func<Task<bool>> action) => typeof(StartMenu).GetMethod("Run", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(menu, new object[] { action, false, false });

        void AssertLocked()
        {
            foreach (var button in Buttons) Assert.IsFalse(button.interactable, button.name);
            Assert.IsFalse(menu.code.interactable);
            Assert.IsFalse(menu.address.interactable);
        }

        [UnityTest]
        public IEnumerator EmptyLanTimesOutAndRestoresControls()
        {
            menu.Show(StartMenu.Page.Lan);
            menu.address.text = "127.0.0.1:17998";
            float began = Time.realtimeSinceStartup;
            menu.lanJoin.onClick.Invoke();
            Assert.IsTrue(menu.IsBusy);
            AssertLocked();
            var connector = session.ActiveConnector;
            menu.lanJoin.onClick.Invoke();
            menu.lanBack.onClick.Invoke();
            menu.settings.onClick.Invoke();
            Assert.AreSame(connector, session.ActiveConnector);
            Assert.AreEqual(StartMenu.Page.Lan, menu.Current);
            while (menu.IsBusy && Time.realtimeSinceStartup - began < 5) yield return null;
            Assert.IsFalse(menu.IsBusy);
            Assert.That(Time.realtimeSinceStartup - began, Is.InRange(2.8f, 5f));
            Assert.AreEqual(SessionConnectionState.Offline, session.ConnectionState);
            Assert.AreEqual(Failure, menu.status.text);
            foreach (var button in Buttons) Assert.IsTrue(button.interactable, button.name);
            Assert.IsTrue(menu.address.interactable);
        }

        [UnityTest]
        public IEnumerator PendingFailureAndSuccessKeepCorrectButtonLock()
        {
            menu.Show(StartMenu.Page.Join);
            menu.code.text = "";
            menu.joinConfirm.onClick.Invoke();
            Assert.IsFalse(menu.IsBusy);
            Assert.AreEqual("방 코드를 입력하세요.", menu.status.text);
            var pending = new TaskCompletionSource<bool>();
            menu.join.interactable = false;
            Run(() => pending.Task);
            AssertLocked();
            menu.Show(StartMenu.Page.Settings);
            Assert.AreEqual(StartMenu.Page.Join, menu.Current);
            pending.SetResult(false);
            yield return null;
            yield return null;
            Assert.IsFalse(menu.IsBusy);
            Assert.IsFalse(menu.join.interactable, "기존 비활성 상태를 보존해야 한다.");
            Assert.IsTrue(menu.quit.interactable);
            Run(() => session.HostLanAsync(17997));
            yield return null;
            Assert.AreEqual(SessionConnectionState.Host, session.ConnectionState);
            Assert.IsTrue(menu.IsBusy, "성공 후 씬 전환 전에도 잠금을 유지해야 한다.");
            AssertLocked();
            var leave = session.LeaveAsync();
            while (!leave.IsCompleted || session.ConnectionState != SessionConnectionState.Offline) yield return null;
            yield return null;
            Assert.IsFalse(menu.IsBusy);
            Assert.IsTrue(menu.quit.interactable);
        }

        sealed class DelayedConnector : ISessionConnector
        {
            public readonly TaskCompletionSource<bool> Result = new();
            public bool Left;
            public string JoinKey => null;
            public Task<bool> HostAsync() => Task.FromResult(false);
            public Task<bool> JoinAsync(string code) => Result.Task;
            public Task LeaveAsync() { Left = true; return Task.CompletedTask; }
        }

        [UnityTest]
        public IEnumerator LateFailureCannotCancelNewHostAndCancelledServicesCannotStart()
        {
            var old = new DelayedConnector();
            var join = session.JoinAsync(old, "INVALID");
            float until = Time.realtimeSinceStartup + 5;
            while (!join.IsCompleted && Time.realtimeSinceStartup < until) yield return null;
            Assert.IsTrue(join.IsCompleted);
            Assert.IsFalse(join.Result);
            Assert.IsTrue(old.Left);
            Assert.AreEqual(Failure, session.LastDisconnectReason);
            var host = session.HostLanAsync(17996);
            while (!host.IsCompleted) yield return null;
            Assert.IsTrue(host.Result);
            old.Result.SetException(new InvalidOperationException("이전 요청의 늦은 실패"));
            yield return null;
            yield return null;
            Assert.AreEqual(SessionConnectionState.Host, session.ConnectionState);
            var cancelled = new ServicesSessionConnector(session.NetworkManager);
            var leave = cancelled.LeaveAsync();
            while (!leave.IsCompleted) yield return null;
            var field = typeof(ServicesSessionConnector).GetField("handler", BindingFlags.Instance | BindingFlags.NonPublic);
            var handler = (Unity.Services.Multiplayer.INetworkHandler)field.GetValue(cancelled);
            var start = handler.StartAsync(null);
            yield return null;
            Assert.IsTrue(start.IsCanceled, "취소된 SDK 요청은 네트워크 설정을 만지기 전에 중단되어야 한다.");
            Assert.AreEqual(SessionConnectionState.Host, session.ConnectionState);
        }
    }
}
