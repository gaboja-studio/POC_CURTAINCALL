using System;
using CurtainCall.Network.PlayerSync;
using UnityEngine;

namespace CurtainCall.HumanCannon
{
    public sealed partial class HumanCannonSystem
    {
        // 부상 조회는 Base 공통값만 읽으며 씬에 별도 설정을 저장하지 않는다.
        public HumanCannonInjurySettings Injuries { get; } = new();
        internal bool DelayInjuredInteraction(NetworkPlayer target, int device, uint revision, Func<bool> isValid, Action complete) => false;
        internal bool HasPendingTrampolineGrab(NetworkPlayer target, uint revision) => false;
    }
}
