using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CurtainCall.HumanCannon.Tests
{
    public sealed partial class FinalCannonPlayTests
    {
        [UnityTest]
        public IEnumerator OrdinaryJumpBuffer_RejumpsAfterLandingAndClearsPendingInput()
        {
            float previousCapture = Time.captureDeltaTime;
            Time.captureDeltaTime = 1f/60f;
            try
            {
            DisableEnhancementThreats();
            Call(mover,"SetStartPose",new Vector3(0,.01f,57),Quaternion.identity,true);
            yield return WaitUntil(()=>((CharacterController)Get(mover,"Body")).isGrounded,3,"바닥 접촉");
            float floor=player.transform.position.y;
            Call(mover,"RequestJump");
            yield return WaitUntil(()=>(bool)Get(mover,"IsAirborne"),2,"첫 점프");
            yield return WaitUntil(()=>player.transform.position.y>floor+.4f,2,"정점 전");
            yield return WaitUntil(()=>player.transform.position.y<floor+.22f,3,"착지 직전");
            Assert.IsTrue((bool)Get(mover,"IsAirborne"));
            Call(mover,"RequestJump");
            yield return WaitUntil(()=>player.transform.position.y>floor+.35f,2,"입력 유예로 두 번째 점프");
            Assert.IsTrue((bool)Get(mover,"IsAirborne"));
            Call(mover,"RequestJump"); Call(mover,"ClearPendingInput");
            Assert.That((double)Get(mover,"jumpBufferedUntil"),Is.EqualTo(double.NegativeInfinity));
            yield return WaitUntil(()=>!(bool)Get(mover,"IsAirborne"),3,"두 번째 착지");
            yield return new WaitForSeconds(.2f);
            Assert.IsFalse((bool)Get(mover,"IsAirborne"),"소비/취소한 입력으로 세 번째 점프하지 않는다.");
            }
            finally { Time.captureDeltaTime = previousCapture; }
        }
    }
}
