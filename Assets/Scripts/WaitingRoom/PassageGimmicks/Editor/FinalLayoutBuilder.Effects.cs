using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CurtainCall.WaitingRoom.PassageGimmicks
{
    // 9단계: 분위기 입자(2026-11-12 사용자). 다시 실행할 수 있다. 결과물은 FinalLayout/Effects.
    // - 천장 흙먼지: 실내 곳곳의 천장 바로 아래에서 천천히 떨어진다.
    // - 녹 가루: 녹슨 벽·금 간 벽 앞에서, 가까이 갔을 때만 흩날린다.
    // 최적화: 한 곳당 입자 수를 작게 잡고(먼지 24개, 녹 20개), 그림자·충돌 없이, 카메라가 가까울 때만 방출한다(ProximityParticles).
    public static partial class FinalLayoutBuilder
    {
        const string DustMaterial = "Assets/Scenes/Levels/EscapeFacilityMotion/pendant-dust.mat";
        // 천장 먼지를 둘 곳(바닥 위 한 점). 천장 높이는 위로 쏴서 찾는다.
        static readonly (string name, float x, float y, float z)[] DustSpots =
        {
            ("로비 서쪽", -12f, 0f, 76f), ("로비 동쪽", -1f, 0f, 74f), ("숙소", 16f, 0f, 76f), ("숙소 화장실", 15f, 0f, 64.5f), ("휴게 공간", 3f, 0f, 80f),
            ("작업실", -14f, 0f, 56.5f), ("소품실", 4f, 0f, 56.5f), ("배관 구역", 20f, 0f, 55f), ("폐기물 보관소", 28f, 0f, 38f),
            ("사무실 복도 서", -7f, 0f, 48.4f), ("사무실 복도 동", 6f, 0f, 48.4f), ("생활 장소 서", -14f, 0f, 41f), ("생활 장소 동", -4f, 0f, 41f),
            ("화물 적재장", -40f, 0f, 68f), ("무대 장치 보관실", -40f, 0f, 42f), ("연습실", -45f, 0f, 108f), ("계단실", 5f, 0f, 104.5f),
            ("기계실 입구", 26.6f, -5f, 77.3f), ("기계실 북", 8f, -10f, 92f), ("기계실 남", 6f, -10f, 70f), ("관리실", -9f, -5f, 105f), ("탈출로 진입", -11.5f, -10f, 93.4f),
        };

        [MenuItem("CurtainCall/Waiting Room/Final Layout/9. Add Dust And Rust Effects")]
        static void EffectsMenu() => Debug.Log(BuildEffects());

        public static string BuildEffects()
        {
            var layout = GameObject.Find(LayoutPath)?.transform;
            if (layout == null) return "WaitingRoom 씬을 여세요.";
            var material = AssetDatabase.LoadAssetAtPath<Material>(DustMaterial);
            if (material == null) return "먼지 재질을 찾지 못했습니다.";
            int undo = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("Final layout: effects");
            var old = layout.Find(RootName + "/Effects"); if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
            var root = Child(Child(layout, RootName), "Effects");
            Undo.RegisterCreatedObjectUndo(root.gameObject, "Effects");
            Physics.SyncTransforms();

            int dust = 0, skipped = 0;
            var dustRoot = Child(root, "CeilingDust");
            foreach (var (name, x, y, z) in DustSpots)
            {
                // 천장: 사람 머리 높이에서 위로 쏴서 처음 닿는 면. 너무 높으면(12m 초과) 먼지가 보이지 않으니 12m로 자른다.
                var from = new Vector3(x, y + 1.6f, z);
                if (!Physics.Raycast(from, Vector3.up, out var hit, 30f, ~0, QueryTriggerInteraction.Ignore)) { skipped++; continue; }
                float ceiling = Mathf.Min(hit.point.y, y + 12f);
                var ps = Particles(dustRoot, "Dust_" + name, new Vector3(x, ceiling - .12f, z), material, 14f);
                var main = ps.main; main.startLifetime = new ParticleSystem.MinMaxCurve(5f, 8f); main.startSpeed = new ParticleSystem.MinMaxCurve(.05f, .2f);
                main.startSize = new ParticleSystem.MinMaxCurve(.015f, .05f); main.startColor = new Color(.78f, .7f, .58f, .55f); main.gravityModifier = .012f; main.maxParticles = 24;
                var emission = ps.emission; emission.rateOverTime = 3f;
                var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Box; shape.scale = new Vector3(4.5f, .05f, 4.5f); shape.rotation = new Vector3(90f, 0, 0);
                Drift(ps, .06f); dust++;
            }

            // 녹 가루: 부술 수 있는 벽(금 간 벽·녹슨 벽) 앞면.
            int rustCount = 0; var rustRoot = Child(root, "RustFlakes");
            foreach (var wall in layout.GetComponentsInChildren<BreakableWall>(false))
            {
                var b = Bounds(wall.transform); if (b.size == Vector3.zero) continue;
                var ps = Particles(rustRoot, "Rust_" + wall.name, new Vector3(b.center.x, b.max.y - .1f, b.center.z), material, 4.5f);
                var main = ps.main; main.startLifetime = new ParticleSystem.MinMaxCurve(1.8f, 3f); main.startSpeed = new ParticleSystem.MinMaxCurve(.02f, .12f);
                main.startSize = new ParticleSystem.MinMaxCurve(.012f, .035f); main.startColor = new Color(.5f, .22f, .1f, .8f); main.gravityModifier = .12f; main.maxParticles = 20;
                var emission = ps.emission; emission.rateOverTime = 7f;
                var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Box; shape.scale = new Vector3(Mathf.Max(b.size.x, .3f) + .3f, .1f, Mathf.Max(b.size.z, .3f) + .3f); shape.rotation = new Vector3(90f, 0, 0);
                Drift(ps, .1f); rustCount++;
            }
            Undo.CollapseUndoOperations(undo); EditorSceneManager.MarkSceneDirty(layout.gameObject.scene);
            return $"천장 먼지 {dust}곳(천장을 못 찾아 건너뜀 {skipped}) / 녹 가루 {rustCount}곳. 한 곳당 최대 입자 24·20개, 가까울 때만 방출";
        }

        static ParticleSystem Particles(Transform parent, string name, Vector3 position, Material material, float radius)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false); go.transform.position = position;
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main; main.loop = true; main.playOnAwake = true; main.simulationSpace = ParticleSystemSimulationSpace.World; main.scalingMode = ParticleSystemScalingMode.Local;
            main.cullingMode = ParticleSystemCullingMode.Pause;
            var renderer = go.GetComponent<ParticleSystemRenderer>(); renderer.sharedMaterial = material; renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; renderer.receiveShadows = false;
            var so = new SerializedObject(go.AddComponent<ProximityParticles>()); so.FindProperty("radius").floatValue = radius; so.ApplyModifiedPropertiesWithoutUndo();
            return ps;
        }

        // 옆으로 살짝 흩날리게 하고, 나타날 때·사라질 때 흐려지게 한다.
        static void Drift(ParticleSystem ps, float amount)
        {
            var velocity = ps.velocityOverLifetime; velocity.enabled = true; velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(-amount, amount); velocity.y = new ParticleSystem.MinMaxCurve(0f, 0f); velocity.z = new ParticleSystem.MinMaxCurve(-amount, amount);
            var color = ps.colorOverLifetime; color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, .15f), new GradientAlphaKey(1f, .75f), new GradientAlphaKey(0f, 1f) });
            color.color = gradient;
        }
    }
}
