using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace CurtainCall.Tests.HumanoidValidation
{
    public sealed class AnimationClipPreviewTests
    {
        const string Folder = "Assets/_ThirdParty/Task062/Mixamo/";
        GameObject instance;
        bool previousBackground;
        bool changedBackground;
        Type type;
        Component view;

        T Field<T>(string name) => (T)type.GetField(name).GetValue(view);
        T Property<T>(string name) => (T)type.GetProperty(name).GetValue(view);
        void Call(string name, params object[] args) => type.GetMethod(name).Invoke(view, args);

        void InstantiatePreview()
        {
            type = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("CurtainCall.Player.Validation.AnimationClipPreview")).First(t => t != null);
            instance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "AnimationPreview.prefab"));
            instance.transform.position = new Vector3(30, 0, 0);
            view = instance.GetComponent(type);
            Assert.That(view, Is.Not.Null);
            previousBackground = Application.runInBackground;
            Application.runInBackground = true;
            changedBackground = true;
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (instance != null) Object.DestroyImmediate(instance);
            if (changedBackground) Application.runInBackground = previousBackground;
            changedBackground = false;
            if (Application.isPlaying) yield return new ExitPlayMode();
        }

        [Test]
        public void EightSourceAvatarsClipsAndSavedReferencesAreHumanoid()
        {
            string[] names = { "army_crawl", "carrying", "crawling", "fall", "flying", "pickup", "reach hand", "tightrope_walk" };
            float[] lengths = { 2.333333f, 10, 5.133334f, 3.583333f, 2.633333f, 7.2f, 6.166667f, 4.583333f };
            for (int i = 0; i < names.Length; i++)
            {
                string path = Folder + names[i] + ".fbx";
                var importer = (ModelImporter)AssetImporter.GetAtPath(path);
                Assert.That(importer.animationType, Is.EqualTo(ModelImporterAnimationType.Human), names[i]);
                var mapping = importer.humanDescription.human;
                Assert.That(mapping.Single(h => h.humanName == "Neck").boneName, Is.EqualTo("spine.004"));
                Assert.That(mapping.Single(h => h.humanName == "Head").boneName, Is.EqualTo("spine.005"));
                Assert.That(mapping.Any(h => h.humanName == "LeftEye" || h.humanName == "RightEye"), Is.False);
                Assert.That(mapping.Where(h => h.boneName.StartsWith("thumb.")).All(h => h.humanName.Contains(" Thumb ")), Is.True);
                var animator = AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponent<Animator>();
                Assert.That(animator.avatar.isValid && animator.avatar.isHuman, Is.True, names[i]);
                var clip = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Single(c => !c.name.StartsWith("__preview__"));
                Assert.That(clip.humanMotion, Is.True, names[i]);
                Assert.That(clip.length, Is.EqualTo(lengths[i]).Within(.00001f));
                Assert.That(clip.frameRate, Is.EqualTo(60));
            }
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "AnimationPreview.prefab");
            Assert.That(prefab, Is.Not.Null);
            var previewType = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("CurtainCall.Player.Validation.AnimationClipPreview")).First(t => t != null);
            var clips = (AnimationClip[])previewType.GetField("Clips").GetValue(prefab.GetComponent(previewType));
            Assert.That(clips.Length, Is.EqualTo(8));
            Assert.That(clips.All(c => c != null && c.humanMotion), Is.True);
            Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Scenes/Tests/Systems/HumanoidCharacters/MixamoAnimationPreview.unity"), Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator EveryClipAdvancesChangesPosePausesRestartsAndKeepsFiniteStationaryRoot()
        {
            yield return new EnterPlayMode();
            InstantiatePreview();
            var animator = Field<Animator>("Animator");
            var bones = animator.GetComponentsInChildren<Transform>();
            var root = instance.transform.position;
            for (int i = 0; i < 8; i++)
            {
                Call("SelectClip", i);
                Call("SeekNormalized", .1f);
                var rotations = bones.Select(b => b.localRotation).ToArray();
                double start = Property<double>("PlaybackTime");
                yield return new WaitForSecondsRealtime(.2f);
                Assert.That(Property<double>("PlaybackTime"), Is.GreaterThan(start + .01), "시간 진행 " + i);
                Call("SeekNormalized", .55f);
                Assert.That(bones.Select((b, j) => Quaternion.Angle(b.localRotation, rotations[j])).Sum(), Is.GreaterThan(.001f), "본 자세 변화 " + i);
                Call("SetPaused", true);
                double paused = Property<double>("PlaybackTime");
                yield return new WaitForSecondsRealtime(.15f);
                Assert.That(Property<double>("PlaybackTime"), Is.EqualTo(paused).Within(.000001), "실제 graph 정지 " + i);
                foreach (float sample in new[] { 0f, .5f, 1f })
                {
                    Call("SeekNormalized", sample);
                    foreach (var bone in bones)
                        Assert.That(IsFinite(bone.position) && IsFinite(bone.localScale), Is.True, "유한한 본 좌표 " + i);
                    var mesh = new Mesh();
                    try
                    {
                        Field<SkinnedMeshRenderer>("Original").BakeMesh(mesh);
                        Assert.That(mesh.vertices.All(IsFinite), Is.True, "유한한 스킨 메시 " + i);
                    }
                    finally { Object.DestroyImmediate(mesh); }
                }
                Assert.That(Vector3.Distance(instance.transform.position, root), Is.LessThan(.00001f));
                Assert.That(animator.applyRootMotion, Is.False);
                Call("SelectClip", i);
                Assert.That(Property<double>("PlaybackTime"), Is.LessThan(.001), "처음부터 재시작 " + i);
                Call("SeekNormalized", 1f);
                yield return null;
                Assert.That(Property<bool>("IsPaused"), Is.True, "원본 끝에서 정지 " + i);
            }
        }

        [UnityTest]
        public IEnumerator CrawlComparisonsAndStopDisableReenableReleaseGraphs()
        {
            yield return new EnterPlayMode();
            InstantiatePreview();
            foreach (int clip in new[] { 0, 2 })
            foreach (int stage in new[] { 2, 3 })
            {
                Call("SelectClip", clip);
                Call("SelectBody", stage);
                Call("SeekNormalized", .5f);
                Assert.That(Field<SkinnedMeshRenderer>("Original").enabled, Is.False);
                var bodies = Field<SkinnedMeshRenderer[]>("Bodies");
                for (int i = 0; i < bodies.Length; i++) Assert.That(bodies[i].enabled, Is.EqualTo(i == stage - 1));
                yield return null;
            }
            Call("StopPreview");
            Assert.That(Property<bool>("HasGraph"), Is.False);
            // 활성 객체에 사후 추가한 경우에도 최초 재생 시점의 Animator 설정을 복원한다.
            var animator = Field<Animator>("Animator");
            var clips = Field<AnimationClip[]>("Clips");
            Object.DestroyImmediate(view);
            view = instance.AddComponent(type);
            type.GetField("Animator").SetValue(view, animator);
            type.GetField("Clips").SetValue(view, clips);
            animator.speed = .37f;
            animator.applyRootMotion = true;
            Call("SelectClip", 0);
            Call("StopPreview");
            Assert.That(animator.speed, Is.EqualTo(.37f));
            Assert.That(animator.applyRootMotion, Is.True);
            animator.speed = 1;
            animator.applyRootMotion = false;
            Call("SelectClip", 7);
            ((Behaviour)view).enabled = false;
            Assert.That(Property<bool>("HasGraph"), Is.False);
            ((Behaviour)view).enabled = true;
            Assert.That(Property<bool>("HasGraph"), Is.True);
            Call("SetPaused", true);
            Call("SetPaused", false);
            double time = Property<double>("PlaybackTime");
            yield return new WaitForSecondsRealtime(.15f);
            Assert.That(Property<double>("PlaybackTime"), Is.GreaterThan(time));
        }

        static bool IsFinite(Vector3 value) => !float.IsNaN(value.x) && !float.IsInfinity(value.x) && !float.IsNaN(value.y) && !float.IsInfinity(value.y) && !float.IsNaN(value.z) && !float.IsInfinity(value.z);
    }
}
