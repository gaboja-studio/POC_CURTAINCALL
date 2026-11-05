using System.Reflection;
using CurtainCall.Cameras;
using CurtainCall.Damage;
using CurtainCall.Player;
using NUnit.Framework;
using UnityEngine;

namespace CurtainCall.Tightrope.EditorTools.Tests
{
    public sealed partial class TightropeSurfaceControlTests
    {
        [TestCase(true, 0)] [TestCase(true, 1)] [TestCase(true, 2)] [TestCase(true, 3)]
        [TestCase(false, 0)] [TestCase(false, 1)] [TestCase(false, 2)] [TestCase(false, 3)]
        public void CameraOrbitWithoutMovementDoesNotTurnInjuredRopePlayer(bool authored, int lostCount)
        {
            float side = authored ? 0 : course.Layout.GetLaneSide(0);
            if (authored) AuthoredRopes(); else AddRope(0, side);
            var condition = player.GetComponent<PlayerCondition>() ?? player.gameObject.AddComponent<PlayerCondition>();
            var damage = player.gameObject.AddComponent<BodyDamageEffects>();
            Invoke(damage, "Awake");
            var cameraObject = MakeObject("Injured orbit camera"); cameraObject.SetActive(false);
            var view = cameraObject.AddComponent<CameraViewDirector>();
            var currentField = typeof(CameraViewDirector).GetField("<Current>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic);
            var previousView = currentField.GetValue(null);
            try
            {
                const BindingFlags field = BindingFlags.Instance | BindingFlags.NonPublic;
                currentField.SetValue(null, view);
                typeof(CameraViewDirector).GetField("local", field).SetValue(view, player);
                typeof(CameraViewDirector).GetField("freeThirdPerson", field).SetValue(view, true);
                MovePlayer(new Vector3(side, 0, 4));
                Invoke(switcher, "Bind", player); Invoke(switcher, "UpdateSurfaceMode");
                condition.SetLostParts(PlayerCondition.PartsForLostCount(lostCount));
                Invoke(damage, "Update");
                Quaternion facing = player.transform.rotation;
                Vector3 axis = mover.CourseForward;
                for (int yaw = 0; yaw <= 360; yaw += 15)
                {
                    typeof(CameraViewDirector).GetField("yaw", field).SetValue(view, (float)yaw);
                    Assert.That(view.UsesMouseLook, Is.True);
                    mover.ClearPendingInput();
                    Invoke(switcher, "UpdateSurfaceMode");
                    Invoke(mover, "Update");
                    Physics.SyncTransforms();
                    Assert.That(mover.FreeMovement, Is.False, "Injury must retain rope controls.");
                    Assert.That(mover.CourseForward, Is.EqualTo(axis), $"Camera yaw {yaw}");
                    Assert.That(Quaternion.Angle(player.transform.rotation, facing), Is.LessThan(.01f), $"Idle rotation at yaw {yaw}, injury {lostCount}");
                }
            }
            finally { currentField.SetValue(null, previousView); }
        }
    }
}
