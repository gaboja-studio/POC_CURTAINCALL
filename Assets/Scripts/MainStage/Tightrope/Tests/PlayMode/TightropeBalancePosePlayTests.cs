using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace CurtainCall.Tightrope.SystemIntegration.Tests
{
    public sealed partial class TightropeCampaignPlayTests
    {
        [UnityTest]
        public IEnumerator BalancePoseMatchesBothGaugeDirectionsInTheSameFrameWithoutMovingRoot()
        {
            yield return BeginPerformance();
            yield return StandOnRopeForSawTest(1, 4f);
            var pose = (Behaviour)course.GetComponent(GameType("CurtainCall.Tightrope.TightropeBalancePoseView"));
            Assert.IsNotNull(pose, "저장된 코스에 외줄 몸 기울기 연출이 연결되어야 한다.");
            var commonView = (Behaviour)player.GetComponent(GameType("CurtainCall.Player.BalanceTiltView"));
            var probeObject = new GameObject("Tightrope balance pose probe");
            var probe = probeObject.AddComponent<TightropeBalancePoseProbe>();
            probe.Initialize(player);
            float maximum = (float)Get(Get(Static("CurtainCall.Settings.GameSettings", "Base"), "Presentation"), "MaxTiltAngle");
            try
            {
                foreach (float value in new[] { 30f, -30f, 50f, -50f, 100f, -100f, 0f })
                {
                    int changedFrame = Time.frameCount;
                    probe.RequestedValue = value;
                    yield return Until(() => probe.SampleFrame > changedFrame && Mathf.Approximately(probe.SampleValue, value),
                        2f, "현재 게이지값의 같은 프레임 몸 기울기");
                    Assert.IsFalse(commonView.enabled, "공용 연출과 외줄 연출이 슬롯을 중복 갱신하면 안 된다.");
                    Assert.That(Quaternion.Angle(probe.SampleRotation, Quaternion.Euler(0f, 0f, -value / 100f * maximum)),
                        Is.LessThan(.01f), "처음 반영하는 프레임부터 게이지 부호와 크기를 따라야 한다.");
                    Assert.That(probe.RootPositionChange, Is.LessThan(.00001f));
                    Assert.That(probe.RootRotationChange, Is.LessThan(.001f));
                    Assert.That(probe.BodyCenterChange, Is.LessThan(.00001f));
                    Assert.That(probe.BodyHeightChange, Is.LessThan(.00001f));
                    Assert.That(Mathf.Abs(Camera.main.transform.right.y), Is.LessThan(.001f), "몸을 기울여도 카메라는 롤하지 않는다.");
                }

                probe.RequestedValue = 80f;
                probe.RequestedActive = false;
                int inactiveFrame = Time.frameCount;
                yield return Until(() => probe.SampleFrame > inactiveFrame && !probe.SampleActive, 2f, "균형 비활성 직립");
                Assert.That(Quaternion.Angle(probe.SampleRotation, Quaternion.identity), Is.LessThan(.01f));
            }
            finally { Object.Destroy(probeObject); }
        }

        [UnityTest]
        public IEnumerator BalancePoseIncludesRemoteReplicaAndRestoresPreviousViewStates()
        {
            yield return BeginPerformance();
            yield return StandOnRopeForSawTest(1, 4f);
            var pose = (Behaviour)course.GetComponent(GameType("CurtainCall.Tightrope.TightropeBalancePoseView"));
            Assert.IsNotNull(pose);
            var localView = (Behaviour)player.GetComponent(GameType("CurtainCall.Player.BalanceTiltView"));
            Component remoteObject = null;
            GameObject probeObject = null;
            bool courseEnabled = ((Behaviour)course).enabled;
            try
            {
                pose.enabled = false;
                Assert.IsTrue(localView.enabled, "외줄 연출을 끄면 원래 켜져 있던 공용 연출을 복원한다.");

                // 한 LAN 호스트의 합성 원격 소유 복제본이다. AI나 두 번째 클라이언트를 실행하지 않는다.
                var prefab = (GameObject)Get(Get(network, "NetworkConfig"), "PlayerPrefab");
                var instance = Object.Instantiate(prefab);
                remoteObject = instance.GetComponent(GameType("Unity.Netcode.NetworkObject"));
                Call(remoteObject, "SpawnWithOwnership", 778UL, true);
                var remote = instance.GetComponent(GameType("CurtainCall.Network.PlayerSync.NetworkPlayer"));
                Assert.IsFalse((bool)Get(remote, "IsLocal"));
                Assert.IsTrue((bool)Get(remote, "IsSpawned"));
                var remoteView = (Behaviour)instance.GetComponent(GameType("CurtainCall.Player.BalanceTiltView"));
                remoteView.enabled = false; // 원래 꺼진 컴포넌트도 복원 과정에서 켜면 안 된다.
                probeObject = new GameObject("Tightrope remote balance pose probe");
                var probe = probeObject.AddComponent<TightropeBalancePoseProbe>();
                probe.Initialize(remote);
                probe.RequestedValue = -80f;
                int enableFrame = Time.frameCount;
                pose.enabled = true;
                yield return Until(() => probe.SampleFrame > enableFrame && Mathf.Approximately(probe.SampleValue, -80f),
                    2f, "원격 표시용 균형을 같은 프레임에 반영");
                float maximum = (float)Get(Get(Static("CurtainCall.Settings.GameSettings", "Base"), "Presentation"), "MaxTiltAngle");
                Assert.That(Quaternion.Angle(probe.SampleRotation, Quaternion.Euler(0f, 0f, .8f * maximum)), Is.LessThan(.01f));
                Assert.That(probe.RootRotationChange, Is.LessThan(.001f));
                Assert.IsFalse(localView.enabled);
                Assert.IsFalse(remoteView.enabled);

                pose.enabled = false;
                Assert.IsTrue(localView.enabled);
                Assert.IsFalse(remoteView.enabled);

                pose.enabled = true;
                yield return Until(() => !localView.enabled, 2f, "외줄 기울기 연출 재활성");
                ((Behaviour)course).enabled = false;
                yield return Until(() => localView.enabled, 2f, "코스 비활성 시 공용 연출 복원");
                Assert.IsFalse(remoteView.enabled);
            }
            finally
            {
                pose.enabled = false;
                ((Behaviour)course).enabled = courseEnabled;
                if (probeObject != null) Object.Destroy(probeObject);
                if (remoteObject != null)
                {
                    if ((bool)Get(remoteObject, "IsSpawned")) Call(remoteObject, "Despawn", true);
                    else Object.Destroy(remoteObject.gameObject);
                }
            }
        }
    }

    // NetworkPlayer.Update가 받은 표시용 상태를 넣은 뒤처럼 샘플을 주입한다.
    // 실제 LateUpdate가 끝난 뒤 관찰하며 모델·루트·카메라를 직접 돌리지 않는다.
    [DefaultExecutionOrder(32000)]
    public sealed class TightropeBalancePoseProbe : MonoBehaviour
    {
        Component balance;
        Transform root, slot;
        CharacterController body;
        MethodInfo setDisplayedState;
        PropertyInfo valueProperty, activeProperty;
        Vector3 rootPosition, bodyCenter;
        Quaternion rootRotation;
        float bodyHeight;
        public float RequestedValue;
        public bool RequestedActive = true;
        public int SampleFrame { get; private set; }
        public float SampleValue { get; private set; }
        public bool SampleActive { get; private set; }
        public Quaternion SampleRotation { get; private set; }
        public float RootPositionChange, RootRotationChange, BodyCenterChange, BodyHeightChange;

        public void Initialize(Component subject)
        {
            root = subject.transform;
            body = subject.GetComponent<CharacterController>();
            foreach (var component in subject.GetComponents<Component>())
            {
                if (component.GetType().FullName == "CurtainCall.Player.PlayerBalance") balance = component;
                if (component.GetType().FullName == "CurtainCall.Player.PlayerModelSlot")
                    slot = (Transform)component.GetType().GetProperty("Slot").GetValue(component);
            }
            setDisplayedState = balance.GetType().GetMethod("SetDisplayedState");
            valueProperty = balance.GetType().GetProperty("Value");
            activeProperty = balance.GetType().GetProperty("IsActive");
        }

        void Update()
        {
            if (balance == null) return;
            setDisplayedState.Invoke(balance, new object[] { RequestedActive, RequestedValue });
            rootPosition = root.position;
            rootRotation = root.rotation;
            bodyCenter = body.center;
            bodyHeight = body.height;
        }

        void LateUpdate()
        {
            if (balance == null) return;
            SampleFrame = Time.frameCount;
            SampleValue = (float)valueProperty.GetValue(balance);
            SampleActive = (bool)activeProperty.GetValue(balance);
            SampleRotation = slot.localRotation;
            RootPositionChange = Vector3.Distance(rootPosition, root.position);
            RootRotationChange = Quaternion.Angle(rootRotation, root.rotation);
            BodyCenterChange = Vector3.Distance(bodyCenter, body.center);
            BodyHeightChange = Mathf.Abs(bodyHeight - body.height);
        }
    }
}
