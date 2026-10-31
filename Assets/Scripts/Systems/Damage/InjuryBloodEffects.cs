using System;
using System.Collections.Generic;
using CurtainCall.Cameras;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Player;
using CurtainCall.Settings;
using CurtainCall.StartLobby;
using UnityEngine;

namespace CurtainCall.Damage
{
    /// <summary>
    /// 절단 혈흔 연출(Task-090). 팔·다리를 새로 잃는 순간 잘린 부위에서 피가 튄다. 판정과 무관한 겉모습이다.
    /// 모든 화면이 공유된 <see cref="PlayerCondition.Changed"/>를 보고 각자 재생하므로 네트워크 전송이 없다.
    /// 접속 직후 기존 부상 적용(<see cref="NetworkPlayer.IsApplyingInitialCondition"/>)·부위 복구에서는 재생하지 않는다.
    /// <list type="bullet">
    /// <item>팀원·내 3인칭: 몸에 남은 절단 부위(휴머노이드는 위팔·허벅지 뼈)에서 바깥·위로 튀고, 이펙트는 몸을 따라가며 방울은 월드에서 떨어진다.</item>
    /// <item>내 1인칭: 몸이 숨겨지므로 카메라 앞 잘린 쪽에서 화면 안쪽으로 튄다.</item>
    /// </list>
    /// 보이는지: 게임 설정(신체 손상 · 혈흔 연출)이 켜져 있고 이 PC 설정(<see cref="ClientSettings.ShowBloodEffect"/>)이 켜져 있을 때만.
    /// 모양은 <c>Resources/Effects/Blood/BloodSpurt</c> 프리팹(메뉴 Tools/CurtainCall/Effects로 생성)이며, 없으면 코드 기본값으로 만든다.
    /// </summary>
    public sealed class InjuryBloodEffects : MonoBehaviour
    {
        public const string PrefabPath = "Effects/Blood/BloodSpurt";
        static readonly BodyPart[] Limbs = { BodyPart.LeftArm, BodyPart.RightArm, BodyPart.LeftLeg, BodyPart.RightLeg };

        static InjuryBloodEffects instance;
        readonly Dictionary<NetworkPlayer, (PlayerCondition condition, Action<BodyPart, BodyPart> handler)> bound = new();
        readonly List<NetworkPlayer> released = new();
        GameObject prefab;
        bool prefabLoaded;
        Material fallbackMaterial;

        /// <summary>게임 설정에서 혈흔 연출을 쓰는지. 꺼져 있으면 설정 화면에 개인 토글도 표시하지 않는다.</summary>
        public static bool IsEnabledByGame => GameSettings.Base.BodyDamage.BloodEffectEnabled;

        /// <summary>이 화면에서 혈흔이 보이는지(게임 설정 && 이 PC 설정).</summary>
        public static bool IsVisible => IsEnabledByGame && ClientSettings.ShowBloodEffect;

        /// <summary>재생했을 때(플레이어, 부위, 1인칭 화면 연출인지). 검사·다른 연출 연결용.</summary>
        public static event Action<NetworkPlayer, BodyPart, bool> Played;

        public static InjuryBloodEffects EnsureExists()
        {
            if (instance != null) return instance;
            var go = new GameObject(nameof(InjuryBloodEffects));
            DontDestroyOnLoad(go);
            return go.AddComponent<InjuryBloodEffects>();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap() => EnsureExists();

        void Awake()
        {
            if (instance != null && instance != this) { Destroy(gameObject); return; }
            instance = this;
        }

        void OnDestroy()
        {
            foreach (var pair in bound)
                if (pair.Value.condition != null) pair.Value.condition.Changed -= pair.Value.handler;
            bound.Clear();
            if (fallbackMaterial != null) Destroy(fallbackMaterial);
            if (instance == this) instance = null;
        }

        // 새로 생긴 플레이어의 상태 변화를 듣는다. 생성 프레임의 변화는 초기 상태 적용이라 놓쳐도 된다.
        void Update()
        {
            foreach (var player in NetworkPlayer.All)
            {
                if (player == null || bound.ContainsKey(player) || !player.TryGetComponent(out PlayerCondition condition)) continue;
                var target = player;
                Action<BodyPart, BodyPart> handler = (previous, current) => HandleChanged(target, previous, current);
                condition.Changed += handler;
                bound[player] = (condition, handler);
            }
            released.Clear();
            foreach (var player in bound.Keys) if (player == null) released.Add(player);
            foreach (var player in released) bound.Remove(player);
        }

        void HandleChanged(NetworkPlayer player, BodyPart previous, BodyPart current)
        {
            if (player == null || player.IsApplyingInitialCondition || !IsVisible) return;
            var added = current & ~previous & PlayerCondition.AllLimbs;
            if (added == BodyPart.None) return;
            foreach (var part in Limbs)
                if ((added & part) != 0) Play(player, part);
        }

        /// <summary>그 플레이어의 잘린 부위에서 혈흔을 재생한다. 보이지 않는 설정이면 아무것도 하지 않는다.</summary>
        public void Play(NetworkPlayer player, BodyPart part)
        {
            if (player == null || !IsVisible) return;
            var director = CameraViewDirector.Current;
            var camera = Camera.main;
            bool firstPerson = director != null && director.LocalPlayer == player && director.IsFirstPerson && camera != null;
            if (firstPerson) PlayFirstPerson(camera.transform, part);
            else PlayWorld(player, part);
            Played?.Invoke(player, part, firstPerson);
        }

        void PlayWorld(NetworkPlayer player, BodyPart part)
        {
            ResolveStump(player, part, out var anchor, out var offset);
            Vector3 position = anchor.TransformPoint(offset);
            Vector3 outward = position - player.transform.position;
            outward.y = 0f;
            if (outward.sqrMagnitude < .0001f) outward = player.transform.right * Side(part);
            bool arm = IsArm(part);
            Vector3 direction = (outward.normalized + Vector3.up * (arm ? .7f : .35f)).normalized;
            var spurt = Spawn(position, Quaternion.LookRotation(direction), null, true, 1f);
            // 이펙트는 몸을 따라가되 방향은 캐릭터 기준을 유지한다(뼈 회전에 흔들리지 않게).
            spurt.Follow(anchor, offset, player.transform, player.transform.InverseTransformDirection(direction));
        }

        void PlayFirstPerson(Transform view, BodyPart part)
        {
            float side = Side(part);
            // 잘린 쪽 화면 아래 가장자리에서 화면 안쪽·위로 튄다. 카메라와 함께 움직인다.
            var local = new Vector3(side * .24f, IsArm(part) ? -.14f : -.3f, .6f);
            var direction = new Vector3(-side * .55f, .8f, .2f).normalized;
            Spawn(view.TransformPoint(local), view.rotation * Quaternion.LookRotation(direction), view, false, .45f);
        }

        BloodSpurt Spawn(Vector3 position, Quaternion rotation, Transform parent, bool worldSpace, float scale)
        {
            if (!prefabLoaded) { prefab = Resources.Load<GameObject>(PrefabPath); prefabLoaded = true; }
            var go = prefab != null ? Instantiate(prefab, position, rotation) : BuildDefault(FallbackMaterial(), position, rotation);
            go.name = "BloodSpurt";
            if (parent != null) go.transform.SetParent(parent, true);
            go.transform.localScale *= scale;
            if (!go.TryGetComponent(out BloodSpurt spurt)) spurt = go.AddComponent<BloodSpurt>();
            var settings = GameSettings.Base.BodyDamage;
            spurt.Begin(worldSpace, settings.BloodAmount, settings.BloodSpurtDuration);
            return spurt;
        }

        Material FallbackMaterial() => fallbackMaterial != null ? fallbackMaterial : fallbackMaterial = CreateMaterial(CreateDropTexture());

        /// <summary>몸에 남은 절단 부위. 휴머노이드는 위팔·허벅지 뼈, 그 외 모델은 몸 기준 어깨·엉덩이 높이.</summary>
        static void ResolveStump(NetworkPlayer player, BodyPart part, out Transform anchor, out Vector3 offset)
        {
            var slot = player.GetComponent<PlayerModelSlot>();
            var model = slot != null ? slot.CurrentModel : null;
            if (model != null && model.TryGetComponent(out HumanoidModel humanoid) && humanoid.Animator != null && humanoid.Animator.isHuman)
            {
                var bone = humanoid.Animator.GetBoneTransform(part switch
                {
                    BodyPart.LeftArm => HumanBodyBones.LeftUpperArm,
                    BodyPart.RightArm => HumanBodyBones.RightUpperArm,
                    BodyPart.LeftLeg => HumanBodyBones.LeftUpperLeg,
                    _ => HumanBodyBones.RightUpperLeg,
                });
                if (bone != null) { anchor = bone; offset = Vector3.zero; return; }
            }
            float height = player.TryGetComponent(out PlayerMover mover) ? mover.BodyHeight : 1.5f;
            anchor = player.transform;
            offset = IsArm(part)
                ? new Vector3(Side(part) * .28f, height * .78f, 0f)
                : new Vector3(Side(part) * .12f, height * .42f, 0f);
        }

        static bool IsArm(BodyPart part) => part == BodyPart.LeftArm || part == BodyPart.RightArm;
        static float Side(BodyPart part) => part == BodyPart.LeftArm || part == BodyPart.LeftLeg ? -1f : 1f;

        // ── 기본 모양 (프리팹이 없을 때·에디터 생성 메뉴 공용) ───────────────

        /// <summary>
        /// 기본 혈흔 파티클: 처음 크게 한 번 튀고 짧게 이어 뿜는다. 속도 방향으로 늘린 방울이 중력으로 떨어지며 작아지고 흐려진다.
        /// 방향은 오브젝트 정면(+Z)이다.
        /// </summary>
        public static GameObject BuildDefault(Material material, Vector3 position, Quaternion rotation)
        {
            var go = new GameObject("BloodSpurt");
            go.transform.SetPositionAndRotation(position, rotation);
            var system = go.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = system.main;
            main.duration = 1f;
            main.loop = true; // 뿜는 시간은 BloodSpurt가 정한다
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(.45f, .9f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.6f, 4.6f);
            main.startSize = new ParticleSystem.MinMaxCurve(.025f, .07f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(.62f, .02f, .03f), new Color(.32f, 0f, .01f));
            main.gravityModifier = 1.4f;
            main.maxParticles = 300;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = system.emission;
            emission.rateOverTime = 45f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 32), new ParticleSystem.Burst(.12f, 14) });

            var shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 24f;
            shape.radius = .03f;

            var size = system.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, .35f)));

            var color = system.colorOverLifetime;
            color.enabled = true;
            var fade = new Gradient();
            fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(.7f, .7f, .7f), 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, .7f), new GradientAlphaKey(0f, 1f) });
            color.color = fade;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = .035f;
            renderer.lengthScale = 1.4f;
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            go.AddComponent<BloodSpurt>();
            return go;
        }

        /// <summary>부드러운 원형 방울 텍스처(가장자리 투명).</summary>
        public static Texture2D CreateDropTexture(int size = 32)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "BloodDrop", wrapMode = TextureWrapMode.Clamp };
            float radius = (size - 1) * .5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float distance = new Vector2(x - radius, y - radius).magnitude / radius;
                    float alpha = Mathf.Clamp01((1f - distance) * 3f);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            texture.Apply();
            return texture;
        }

        /// <summary>URP 파티클 무광 반투명 재질. 색은 파티클 색(어두운 빨강)을 쓴다.</summary>
        public static Material CreateMaterial(Texture2D texture)
        {
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            var material = new Material(shader) { name = "BloodParticle" };
            if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
            if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", texture);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Color.white);
            if (material.HasProperty("_Surface"))
            {
                material.SetFloat("_Surface", 1f);
                material.SetFloat("_Blend", 0f);
                material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                material.SetFloat("_ZWrite", 0f);
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            }
            return material;
        }
    }
}
