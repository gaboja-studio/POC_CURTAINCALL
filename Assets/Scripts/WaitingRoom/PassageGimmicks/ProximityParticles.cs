using UnityEngine;

namespace CurtainCall.WaitingRoom
{
    /// <summary>
    /// 가까이 있을 때만 켜지는 분위기 입자(녹 가루, 천장 흙먼지. 2026-11-12 사용자: 렉이 걸리지 않게).
    /// 화면의 카메라가 반경 안에 들어오면 방출을 켜고, 벗어나면 끈다. 판정은 0.5초에 한 번만 한다.
    /// 각 화면에서 따로 돌고 게임 판정과 무관하다.
    /// </summary>
    public sealed class ProximityParticles : MonoBehaviour
    {
        [SerializeField, Min(.5f)] float radius = 14f;
        ParticleSystem[] systems;
        float next;
        bool on = true;

        void Awake()
        {
            systems = GetComponentsInChildren<ParticleSystem>(true);
            next = Time.unscaledTime + Random.value * .5f; // 여러 개가 같은 프레임에 판정하지 않게 흩는다.
            Set(false);
        }

        void Update()
        {
            if (Time.unscaledTime < next) return;
            next = Time.unscaledTime + .5f;
            var cam = Camera.main; if (cam == null) return;
            bool near = (cam.transform.position - transform.position).sqrMagnitude < radius * radius;
            if (near != on) Set(near);
        }

        void Set(bool value)
        {
            on = value;
            foreach (var system in systems)
            {
                if (system == null) continue;
                var emission = system.emission; emission.enabled = value;
                if (value && !system.isPlaying) system.Play();
            }
        }
    }
}
