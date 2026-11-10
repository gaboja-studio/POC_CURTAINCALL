using CurtainCall.Damage;
using CurtainCall.Player;
using CurtainCall.Settings;
using UnityEngine;

namespace CurtainCall.Network.PlayerSync
{
    public sealed partial class NetworkPlayer
    {
        double lastHazardHit = double.NegativeInfinity;
        int hazardRestart = -1, hazardRevive = -1;

        /// <summary>절단 위험물은 종류/씬/보고 주체와 무관하게 플레이어별 피해 유예를 공유한다.
        /// 접촉 시작/종료와 접촉 위치의 검증은 해당 위험물이 담당한다.</summary>
        public BodyPart ServerHazardCut(string obstacle = null, DeathOrigin origin = DeathOrigin.Hazard)
        {
            if (!IsServer || !ServerCanTakeDamage()) return BodyPart.None;
            if (hazardRestart != restartCount.Value || hazardRevive != reviveCount.Value)
            {
                hazardRestart = restartCount.Value; hazardRevive = reviveCount.Value;
                lastHazardHit = double.NegativeInfinity;
            }
            double now = Time.unscaledTimeAsDouble;
            if (!CommonDamageRules.CanHit(now, lastHazardHit, GameSettings.Base.BodyDamage.HazardHitCooldown)) return BodyPart.None;
            var cut = ServerCut(1, obstacle, origin);
            if (cut != BodyPart.None) lastHazardHit = now;
            return cut;
        }

        public bool ServerHazardContact(HazardDamageKind kind, string obstacle = null, DeathOrigin origin = DeathOrigin.Hazard)
        {
            // 추격 화염벽/압사 같은 즉사형은 직전 절단의 무적 시간으로 무효화하지 않는다.
            return CommonDamageRules.IsFatal(kind) ? ServerKill(origin, obstacle)
                : ServerHazardCut(obstacle, origin) != BodyPart.None;
        }
    }
}
