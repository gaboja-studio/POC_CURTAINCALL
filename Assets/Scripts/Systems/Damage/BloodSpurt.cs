using UnityEngine;

namespace CurtainCall.Damage
{
    /// <summary>
    /// 혈흔 한 번의 수명: 정해진 시간 동안 뿜고 멈춘 뒤, 마지막 방울이 사라지면 지운다.
    /// 따라갈 대상이 있으면 그 위치를 따라가고 방향은 기준(캐릭터) 회전을 따른다. 대상이 사라지면 그 자리에 남는다.
    /// </summary>
    public sealed class BloodSpurt : MonoBehaviour
    {
        ParticleSystem[] systems;
        Transform anchor, basis;
        Vector3 offset, localDirection;
        float stopAt, destroyAt;
        bool following, emitting;

        public bool IsEmitting => emitting;

        /// <summary>재생을 시작한다. <paramref name="amount"/>는 방출 수 배율, <paramref name="duration"/>은 뿜는 시간(초).</summary>
        public void Begin(bool worldSpace, float amount, float duration)
        {
            systems = GetComponentsInChildren<ParticleSystem>(true);
            float longest = 0f;
            foreach (var system in systems)
            {
                system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                var main = system.main;
                main.simulationSpace = worldSpace ? ParticleSystemSimulationSpace.World : ParticleSystemSimulationSpace.Local;
                main.loop = true;
                var emission = system.emission;
                emission.rateOverTimeMultiplier *= amount;
                var bursts = new ParticleSystem.Burst[emission.burstCount];
                emission.GetBursts(bursts);
                for (int i = 0; i < bursts.Length; i++) bursts[i].count = new ParticleSystem.MinMaxCurve(Mathf.Max(1f, bursts[i].count.constant * amount));
                emission.SetBursts(bursts);
                longest = Mathf.Max(longest, main.startLifetime.constantMax);
            }
            foreach (var system in systems) system.Play(false);
            emitting = true;
            stopAt = Time.time + Mathf.Max(.05f, duration);
            destroyAt = stopAt + longest + .25f;
        }

        public void Follow(Transform target, Vector3 targetOffset, Transform directionBasis, Vector3 direction)
        {
            anchor = target;
            offset = targetOffset;
            basis = directionBasis;
            localDirection = direction;
            following = target != null;
        }

        void LateUpdate()
        {
            if (following)
            {
                if (anchor == null) following = false;
                else
                {
                    transform.position = anchor.TransformPoint(offset);
                    if (basis != null && localDirection.sqrMagnitude > .0001f)
                        transform.rotation = Quaternion.LookRotation(basis.TransformDirection(localDirection));
                }
            }
            if (emitting && Time.time >= stopAt)
            {
                emitting = false;
                foreach (var system in systems) if (system != null) system.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
            if (Time.time >= destroyAt) Destroy(gameObject);
        }
    }
}
