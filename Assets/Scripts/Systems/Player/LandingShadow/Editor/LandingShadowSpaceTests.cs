using CurtainCall.Settings;
using NUnit.Framework;
using UnityEngine;
using Space = CurtainCall.Settings.BaseGameSettings.LandingMarkerSpace;

namespace CurtainCall.Player.LandingShadowTests
{
    public sealed class LandingShadowSpaceTests
    {
        [Test]
        public void SpaceOf_MapsConfiguredAndTestScenes()
        {
            var transition = GameSettings.Base.SpaceTransition;
            Assert.AreEqual(Space.Performance, LandingShadowMarkers.SpaceOf(transition.TightropeScene, ""));
            Assert.AreEqual(Space.Performance, LandingShadowMarkers.SpaceOf(transition.HumanCannonScene, ""));
            Assert.AreEqual(Space.EscapeRoute, LandingShadowMarkers.SpaceOf(transition.EscapeRouteScene, ""));
            Assert.AreEqual(Space.WaitingRoom, LandingShadowMarkers.SpaceOf(transition.WaitingRoomScene, ""));
            Assert.AreEqual(Space.Lobby, LandingShadowMarkers.SpaceOf(transition.HubScene, ""));
            Assert.AreEqual(Space.Lobby, LandingShadowMarkers.SpaceOf("Start", "Assets/Scenes/InGame/Start.unity"));
            Assert.AreEqual(Space.WaitingRoom, LandingShadowMarkers.SpaceOf("WaitingRoom", "Assets/Scenes/InGame/WaitingRoom/WaitingRoom.unity"));
            Assert.AreEqual(Space.WaitingRoom, LandingShadowMarkers.SpaceOf("WaitingRoomBase", "Assets/Scenes/Tests/WaitingRoom/Base/WaitingRoomBase.unity"));
            Assert.AreEqual(Space.EscapeRoute, LandingShadowMarkers.SpaceOf("escape_route_b", "Assets/Scenes/Levels/escape_route_b.unity"));
            Assert.AreEqual(Space.EscapeRoute, LandingShadowMarkers.SpaceOf("UnderstagePursuerPlaytest", "Assets/Scenes/Tests/EscapeRoute/Pursuer/UnderstagePursuerPlaytest.unity"));
            Assert.AreEqual(Space.Other, LandingShadowMarkers.SpaceOf("SampleScene", "Assets/Scenes/Levels/SampleScene.unity"));
        }

        [Test]
        public void DefaultSpaces_OutlineInPerformanceEscapeRehearsal_HideInWaitingRoomAndLobby()
        {
            var settings = new BaseGameSettings.LandingMarkerSettings();
            Assert.IsTrue(settings.ShowIn(Space.Performance));
            Assert.IsTrue(settings.ShowIn(Space.EscapeRoute));
            Assert.IsTrue(settings.ShowIn(Space.Rehearsal), "리허설은 대기실과 별도로 외곽선을 켠다");
            Assert.IsFalse(settings.ShowIn(Space.WaitingRoom));
            Assert.IsFalse(settings.ShowIn(Space.Lobby));
            JsonUtility.FromJsonOverwrite("{\"showInRehearsal\":false}", settings);
            Assert.IsFalse(settings.ShowIn(Space.Rehearsal));
        }

        [Test]
        public void RehearsalAware_OnlyTurnsActiveWaitingRoomIntoRehearsal()
        {
            Assert.AreEqual(Space.Rehearsal, LandingShadowMarkers.RehearsalAware(Space.WaitingRoom, true));
            Assert.AreEqual(Space.WaitingRoom, LandingShadowMarkers.RehearsalAware(Space.WaitingRoom, false));
            Assert.AreEqual(Space.Performance, LandingShadowMarkers.RehearsalAware(Space.Performance, true));
        }

        [Test]
        public void SurfaceClip_MapsRotatedRopeTopToItsLengthAndWidth()
        {
            var rope = new GameObject("ClipRope");
            try
            {
                rope.transform.SetPositionAndRotation(new Vector3(10, 3, -2), Quaternion.Euler(0, 30, 0));
                rope.transform.localScale = new Vector3(2, 1, 1);
                var box = rope.AddComponent<BoxCollider>();
                box.size = new Vector3(0.05f, 0.04f, 8);
                box.center = new Vector3(0, -0.02f, 1);
                Assert.IsTrue(LandingShadowMarkers.TryGetSurfaceClip(box, Vector3.up, out var toSurface, out var half));
                Assert.That(half.x, Is.EqualTo(4).Within(1e-4f), "길이 방향 반폭(m)");
                Assert.That(half.y, Is.EqualTo(0.05f).Within(1e-4f), "폭 방향 반폭(m, 축척 포함)");
                Vector3 Local(float width, float length) => toSurface.MultiplyPoint3x4(rope.transform.TransformPoint(new Vector3(width, 0, 1 + length)));
                var center = Local(0, 0);
                Assert.That(Mathf.Abs(center.x), Is.LessThan(1e-3f)); Assert.That(Mathf.Abs(center.z), Is.LessThan(1e-3f));
                Assert.That(center.y, Is.EqualTo(1).Within(1e-3f), "윗면");
                Assert.That(Mathf.Abs(Local(0, 3.9f).x), Is.LessThan(1), "줄 위 끝 근처는 안");
                Assert.That(Mathf.Abs(Local(0, 4.1f).x), Is.GreaterThan(1), "줄 끝 밖");
                Assert.That(Mathf.Abs(Local(0.02f, 0).z), Is.LessThan(1), "줄 폭 안");
                Assert.That(Mathf.Abs(Local(0.03f, 0).z), Is.GreaterThan(1), "줄 폭 밖은 그리지 않는다");
                Assert.IsFalse(LandingShadowMarkers.TryGetSurfaceClip(null, Vector3.up, out _, out _));
                var sphere = rope.AddComponent<SphereCollider>();
                Assert.IsFalse(LandingShadowMarkers.TryGetSurfaceClip(sphere, Vector3.up, out _, out _), "박스가 아니면 자르지 않는다");
            }
            finally { Object.DestroyImmediate(rope); }
        }

        [Test]
        public void SurfaceShape_NarrowClipsWithThinOutline_WideFadesOutsideByOption()
        {
            var settings = new BaseGameSettings.LandingMarkerSettings();
            var rope = new Vector2(4, 0.06f);
            var deck = new Vector2(2, 1.5f);
            Assert.That(settings.NarrowSurfaceWidth, Is.GreaterThan(0.12f).And.LessThan(3f));
            Assert.AreEqual(BaseGameSettings.LandingSurfaceShape.Clip, settings.ShapeFor(rope), "좁은 줄은 자른다");
            Assert.That(settings.ClippedOutlineWidth(rope), Is.LessThan(settings.OutlineWidth), "좁은 줄의 외곽선은 얇아진다");
            Assert.That(settings.ClippedOutlineWidth(rope), Is.LessThan(0.06f), "띠 가운데에 그림자 바탕이 남는다");
            Assert.AreEqual(settings.OutlineWidth, settings.ClippedOutlineWidth(deck), "넓은 면은 외곽선 두께 그대로");
            Assert.IsTrue(settings.FadeOutsideOnWideSurface);
            Assert.AreEqual(BaseGameSettings.LandingSurfaceShape.FadeOutside, settings.ShapeFor(deck), "넓은 면은 원을 유지하고 바깥만 옅게");
            Assert.That(settings.OutsideOpacity, Is.GreaterThan(0).And.LessThan(1));
            JsonUtility.FromJsonOverwrite("{\"fadeOutsideOnWideSurface\":false}", settings);
            Assert.AreEqual(BaseGameSettings.LandingSurfaceShape.Clip, settings.ShapeFor(deck), "옵션을 끄면 넓은 면도 자른다");
        }

        [Test]
        public void CommonMaterial_IsLoadableFromResources()
        {
            var material = Resources.Load<Material>("Materials/LandingShadow/LandingShadow");
            Assert.IsNotNull(material, "공통 착지 그림자는 Resources 머티리얼로 빌드에 포함된다");
            Assert.AreEqual("CurtainCall/HumanCannon/GroundMarker", material.shader.name);
        }
    }
}
