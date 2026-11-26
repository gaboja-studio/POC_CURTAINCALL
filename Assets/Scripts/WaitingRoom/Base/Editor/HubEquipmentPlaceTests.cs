using CurtainCall.Settings;
using CurtainCall.SpaceLink;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CurtainCall.WaitingRoom.EditorTools
{
    /// <summary>
    /// 2026-11-26 Task-205(M3 피드백): 런 성공 뒤 로비(시작 씬)로 돌아가면 공연 기물이 발동됐다.
    /// 시작 씬에서는 막고, 장소가 지정되지 않은 개발 확인 씬은 기존처럼 정상 사용으로 둔다.
    /// </summary>
    [Category("CommonProgressionRegression")]
    public sealed class HubEquipmentPlaceTests
    {
        Scene scene;
        string previousName;

        [SetUp]
        public void SetUp()
        {
            scene = SceneManager.GetActiveScene();
            previousName = scene.name;
            Assume.That(SpaceLinkDirector.Instance, Is.Null, "에디터 검사에서는 장소 전환기가 없어야 한다");
        }

        [TearDown]
        public void TearDown() => scene.name = previousName;

        [Test]
        public void HubScene_BlocksPerformanceEquipment()
        {
            scene.name = SpaceRoute.SceneName(SpaceId.Hub, GameSettings.Base.SpaceTransition);
            Assert.That(PropCarry.PlaceAt(Vector3.zero), Is.EqualTo(PropCarry.EquipmentPlace.Blocked));
            Assert.That(PropCarry.PlaceOf(1), Is.EqualTo(PropCarry.EquipmentPlace.Blocked));
        }

        [Test]
        public void UnmappedDevScene_KeepsNormalUse()
        {
            scene.name = "HubEquipmentPlaceDevCheck";
            Assert.That(PropCarry.PlaceAt(Vector3.zero), Is.EqualTo(PropCarry.EquipmentPlace.Normal));
            Assert.That(PropCarry.PlaceOf(1), Is.EqualTo(PropCarry.EquipmentPlace.Normal));
        }

        [Test]
        public void PerformanceScene_KeepsNormalUse()
        {
            scene.name = GameSettings.Base.SpaceTransition.HumanCannonScene;
            Assert.That(PropCarry.PlaceAt(Vector3.zero), Is.EqualTo(PropCarry.EquipmentPlace.Normal));
        }
    }
}
