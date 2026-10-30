using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CurtainCall.UI.SceneIntegration.Tests
{
    public sealed partial class DevStartLauncherPlayTests
    {
        /// <summary>호스트가 오늘 밤 고칠 일차를 외줄타기로 채워 확정한다(밤 종료 조건).</summary>
        void ConfirmTonight()
        {
            var board = Find("CurtainCall.WaitingRoom.ScheduleBoard");
            int day = (int)Field(State, "Day");
            var days = (int[])T("CurtainCall.WaitingRoom.ScheduleBook").GetMethod("EditableDays").Invoke(null, new object[] { day });
            foreach (int d in days) for (int slot = 0; slot < 2; slot++) Call(board, "Set", d, slot, "Tightrope", 1);
            Assert.AreEqual("None", Call(board, "Confirm").ToString());
        }

        [UnityTest]
        public IEnumerator W3_BedRestCamera_KeepsYawWithoutRollThroughMorning()
        {
            // 시선과 침대 방향이 달라도 눕기·일어나기 카메라는 위아래로만 움직인다(밤 → 아침 전환 때 화면이 돌던 문제).
            yield return Launch(1, "Night", 0, "", "WaitingRoomBase");
            yield return new WaitForSecondsRealtime(.5f);
            ConfirmTonight();
            var bed = Find("CurtainCall.WaitingRoom.NightBed");
            Camera.main.transform.rotation = Quaternion.Euler(17f, bed.transform.eulerAngles.y + 150f, 0f);
            float yaw = Camera.main.transform.eulerAngles.y;
            Call(bed, "Interact");
            var view = bed.transform.Find("BedRestCamera");
            Assert.IsNotNull(view, "눕기 카메라가 생겨야 한다");
            float lowest = 90f, until = Time.realtimeSinceStartup + 8f;
            bool morning = false;
            while (Time.realtimeSinceStartup < until && view != null)
            {
                var e = view.eulerAngles;
                Assert.AreEqual(0f, Mathf.DeltaAngle(0, e.z), .5f, "화면이 옆으로 기울면 안 된다");
                Assert.AreEqual(0f, Mathf.DeltaAngle(yaw, e.y), .5f, "좌우 시선 방향을 유지한다");
                lowest = Mathf.Min(lowest, Mathf.DeltaAngle(0, e.x));
                morning |= Field(State, "Phase").ToString() == "RestTime";
                yield return null;
            }
            Assert.Less(lowest, -70f, "누우면 천장을 본다");
            Assert.IsTrue(morning, "아침 쉬는 시간으로 넘어간다");
            Assert.IsTrue(view == null, "일어나면 눕기 카메라를 정리한다");
        }
    }
}
