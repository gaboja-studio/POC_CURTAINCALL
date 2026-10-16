using UnityEngine;

namespace CurtainCall.HumanCannon
{
    public sealed partial class HumanCannonPilot
    {
        /// <summary>1인 테스트의 쿠션 낙하 준비. 호스트가 이미 발사한 식별자를 그대로 사용한다.</summary>
        internal void PracticeDrop(Vector3 position)
        {
            if (!Application.isEditor || !IsFlying || system == null
                || system.GetComponentInParent<HumanCannonSoloPractice>() == null) return;
            Launch(flightStationIndex, launchRevision, position, Quaternion.identity, Vector3.down * 2);
        }
    }
}
