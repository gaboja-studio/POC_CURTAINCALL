using System.Collections.Generic;
using System.Reflection;
using CurtainCall.Player;
using CurtainCall.Settings;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace CurtainCall.WorldIndicators.EditorTools
{
    public sealed class ActorShadowPresenterTests
    {
        const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        static readonly FieldInfo CurrentSettings = typeof(GameSettings).GetField("current", BindingFlags.Static | BindingFlags.NonPublic);
        readonly List<ActorShadowPresenter> presenters = new();
        Scene scene;
        GameSettings previousSettings, isolatedSettings;
        WorldUiSettings settings;
        PlayerModelSlot slot;
        HumanoidModel model;
        MeshRenderer prop;
        static readonly ShadowCastingMode[] BodyModes = {
            ShadowCastingMode.TwoSided, ShadowCastingMode.ShadowsOnly, ShadowCastingMode.Off, ShadowCastingMode.On
        };

        [SetUp]
        public void SetUp()
        {
            // Do not edit or dirty the user's settings asset; swap only the runtime cache.
            previousSettings = (GameSettings)CurrentSettings.GetValue(null);
            isolatedSettings = ScriptableObject.CreateInstance<GameSettings>();
            settings = ScriptableObject.CreateInstance<WorldUiSettings>();
            typeof(GameSettings).GetField("worldUi", PrivateInstance).SetValue(isolatedSettings, settings);
            CurrentSettings.SetValue(null, isolatedSettings);
            settings.disablePlayerRealtimeShadows = true;
            settings.pursuerBlobShadows = false;
            scene = EditorSceneManager.NewPreviewScene();
            slot = NewObject("Shadow lifecycle fixture").AddComponent<PlayerModelSlot>();
        }

        [TearDown]
        public void TearDown()
        {
            try
            {
                // Explicit lifecycle calls also clean up if an assertion interrupted the test.
                // Plain MonoBehaviours do not tick automatically in these EditMode fixtures.
                foreach (var presenter in presenters) if (presenter) Call(presenter, "OnDestroy");
                presenters.Clear();
                if (scene.IsValid()) EditorSceneManager.ClosePreviewScene(scene);
            }
            finally
            {
                CurrentSettings.SetValue(null, previousSettings);
                Object.DestroyImmediate(isolatedSettings);
                Object.DestroyImmediate(settings);
            }
        }

        GameObject NewObject(string name, Transform parent = null)
        {
            var go = new GameObject(name);
            SceneManager.MoveGameObjectToScene(go, scene);
            if (parent) go.transform.SetParent(parent, false);
            return go;
        }

        SkinnedMeshRenderer Skin(string name, Transform parent, ShadowCastingMode mode, bool receive)
        {
            var renderer = NewObject(name, parent).AddComponent<SkinnedMeshRenderer>();
            renderer.shadowCastingMode = mode;
            renderer.receiveShadows = receive;
            return renderer;
        }

        ActorShadowPresenter CreatePlayer(bool instantiate = false)
        {
            var source = NewObject("Authored player", instantiate ? null : slot.Slot);
            var humanoid = source.AddComponent<HumanoidModel>();
            humanoid.Original = Skin("Original body", source.transform, ShadowCastingMode.On, true);
            humanoid.Bodies = new SkinnedMeshRenderer[4];
            humanoid.Limbs = new SkinnedMeshRenderer[4];
            for (int i = 0; i < 4; i++)
            {
                humanoid.Bodies[i] = Skin("Injury body " + i, source.transform, BodyModes[i], i % 2 == 0);
                humanoid.Bodies[i].gameObject.SetActive(false);
                humanoid.Limbs[i] = Skin("Detached limb template " + i, source.transform, ShadowCastingMode.TwoSided, false);
                humanoid.Limbs[i].enabled = false;
            }
            var held = NewObject("Held prop", source.transform).AddComponent<MeshRenderer>();
            held.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
            held.receiveShadows = false;
            if (instantiate)
            {
                typeof(PlayerModelSlot).GetField("fitToHeight", PrivateInstance).SetValue(slot, false);
                slot.SetModel(source);
            }
            else Call(slot, "Awake"); // Exercise the existing-child hookup, not just AttachPlayer.
            model = slot.CurrentModel.GetComponent<HumanoidModel>();
            prop = model.GetComponentInChildren<MeshRenderer>(true);
            var presenter = model.GetComponent<ActorShadowPresenter>();
            Assert.That(presenter, Is.Not.Null, "Both PlayerModelSlot hookup paths must attach the presenter.");
            presenters.Add(presenter);
            return presenter;
        }

        void AssertBodies(bool suppressed)
        {
            Assert.That(model.Original.shadowCastingMode, Is.EqualTo(suppressed ? ShadowCastingMode.Off : ShadowCastingMode.On));
            Assert.That(model.Original.receiveShadows, Is.True);
            for (int i = 0; i < 4; i++)
            {
                Assert.That(model.Bodies[i].shadowCastingMode, Is.EqualTo(suppressed ? ShadowCastingMode.Off : BodyModes[i]), "Injury body " + i);
                Assert.That(model.Bodies[i].receiveShadows, Is.EqualTo(i % 2 == 0));
                Assert.That(model.Limbs[i].shadowCastingMode, Is.EqualTo(ShadowCastingMode.TwoSided));
                Assert.That(model.Limbs[i].receiveShadows, Is.False);
            }
            Assert.That(prop.shadowCastingMode, Is.EqualTo(ShadowCastingMode.ShadowsOnly));
            Assert.That(prop.receiveShadows, Is.False);
        }

        [TestCase(false), TestCase(true)]
        public void SlotHookupSuppressesInactiveInjuryBodiesButPreservesPropsAndLimbTemplates(bool instantiate)
        {
            var presenter = CreatePlayer(instantiate);
            AssertBodies(true);
            foreach (var body in model.Bodies) Assert.That(body.gameObject.activeSelf, Is.False);
            model.Original.enabled = false;
            model.Bodies[0].gameObject.SetActive(true);
            Call(presenter, "LateUpdate");
            AssertBodies(true);
            Assert.That(model.Original.enabled, Is.False, "Shadow presentation must not select the injury body.");
        }

        [Test]
        public void SettingsToggleRestoresExactModesWithoutCapturingLaterProps()
        {
            settings.disablePlayerRealtimeShadows = false;
            var presenter = CreatePlayer();
            AssertBodies(false);
            var laterProp = NewObject("Runtime prop", model.transform).AddComponent<MeshRenderer>();
            laterProp.shadowCastingMode = ShadowCastingMode.TwoSided;
            foreach (bool suppress in new[] { true, false, true, false })
            {
                settings.disablePlayerRealtimeShadows = suppress;
                Call(presenter, "LateUpdate");
                AssertBodies(suppress);
                Assert.That(laterProp.shadowCastingMode, Is.EqualTo(ShadowCastingMode.TwoSided));
            }
        }

        [Test]
        public void DetachRestoresImmediatelyAndReattachmentReusesOriginalCastingSnapshot()
        {
            var presenter = CreatePlayer();
            var detached = slot.DetachModel();
            Assert.That(detached, Is.EqualTo(model.gameObject));
            Assert.That(slot.CurrentModel, Is.Null);
            Assert.That(detached.transform.parent, Is.Null);
            Assert.That(presenter.enabled, Is.False);
            AssertBodies(false);
            Call(presenter, "LateUpdate");
            AssertBodies(false);

            detached.transform.SetParent(slot.Slot, false);
            Call(slot, "Awake");
            Assert.That(detached.GetComponent<ActorShadowPresenter>(), Is.SameAs(presenter));
            Assert.That(detached.GetComponents<ActorShadowPresenter>(), Has.Length.EqualTo(1));
            AssertBodies(true);
            presenter.ReleasePlayer();
            AssertBodies(false);
        }

        [Test]
        public void DisableAndDestructionRestoreCastingWhileLeavingBodiesAlive()
        {
            var presenter = CreatePlayer();
            Call(presenter, "OnDisable");
            AssertBodies(false);
            Call(presenter, "LateUpdate");
            AssertBodies(true);
            Call(presenter, "OnDestroy");
            Object.DestroyImmediate(presenter);
            AssertBodies(false);
        }

        [Test]
        public void PursuerBlobIsOptInRestoresCastingAndDisposesOnlyItsOwnedObjects()
        {
            var authored = NewObject("Authored doll", slot.transform);
            var skin = Skin("Body", authored.transform, ShadowCastingMode.TwoSided, true);
            // Hidden presentation avoids scene cameras/physics; allocation and lifetime still run.
            var presenter = ActorShadowPresenter.AttachPursuer(slot, authored.transform, true, () => false);
            presenters.Add(presenter);
            var lateProp = NewObject("Runtime held prop", authored.transform).AddComponent<MeshRenderer>();
            lateProp.shadowCastingMode = ShadowCastingMode.On;
            var material = Resources.Load<Material>("Materials/LandingShadow/LandingShadow");
            Assert.That(material, Is.Not.Null, "The existing shared landing shadow resource is required.");
            string originalMaterial = EditorJsonUtility.ToJson(material);

            Call(presenter, "LateUpdate");
            Assert.That(Field<MeshRenderer>(presenter, "blob"), Is.Null);
            Assert.That(Field<Mesh>(presenter, "quad"), Is.Null);
            Assert.That(skin.shadowCastingMode, Is.EqualTo(ShadowCastingMode.TwoSided));

            settings.pursuerBlobShadows = true;
            Call(presenter, "LateUpdate");
            var blob = Field<MeshRenderer>(presenter, "blob");
            var quad = Field<Mesh>(presenter, "quad");
            Assert.That(blob, Is.Not.Null);
            Assert.That(quad, Is.Not.Null);
            var blobObject = blob.gameObject;
            Assert.That(blob.sharedMaterial, Is.SameAs(material));
            Assert.That(blob.enabled, Is.False, "A hidden pursuer must not display a stale blob.");
            Assert.That(skin.shadowCastingMode, Is.EqualTo(ShadowCastingMode.Off));
            Assert.That(skin.receiveShadows, Is.True);
            Assert.That(lateProp.shadowCastingMode, Is.EqualTo(ShadowCastingMode.On));
            var properties = Field<MaterialPropertyBlock>(presenter, "properties");
            Assert.That(properties.GetFloat("_OutlineWidth"), Is.Zero);
            Assert.That(properties.GetColor("_RimColor").a, Is.Zero);

            settings.pursuerBlobShadows = false;
            Call(presenter, "LateUpdate");
            Assert.That(skin.shadowCastingMode, Is.EqualTo(ShadowCastingMode.TwoSided));
            Assert.That(blob.enabled, Is.False);
            settings.pursuerBlobShadows = true;
            Call(presenter, "LateUpdate");
            Assert.That(Field<MeshRenderer>(presenter, "blob"), Is.SameAs(blob));
            Assert.That(Field<Mesh>(presenter, "quad"), Is.SameAs(quad));
            Call(presenter, "OnDisable");
            Assert.That(skin.shadowCastingMode, Is.EqualTo(ShadowCastingMode.TwoSided));
            Call(presenter, "LateUpdate");
            Assert.That(skin.shadowCastingMode, Is.EqualTo(ShadowCastingMode.Off));

            // In EditMode both objects must be gone synchronously, without Destroy errors.
            Call(presenter, "OnDestroy");
            Assert.That(blobObject == null, Is.True);
            Assert.That(quad == null, Is.True);
            Assert.That(skin.shadowCastingMode, Is.EqualTo(ShadowCastingMode.TwoSided));
            Assert.That(lateProp.shadowCastingMode, Is.EqualTo(ShadowCastingMode.On));
            Assert.That(material != null, Is.True, "The shared asset is not owned by this presenter.");
            Assert.That(EditorJsonUtility.ToJson(material), Is.EqualTo(originalMaterial));
            Object.DestroyImmediate(presenter); // Repeated cleanup must also be harmless.
        }

        static void Call(object target, string method) => target.GetType().GetMethod(method, PrivateInstance).Invoke(target, null);
        static T Field<T>(object target, string field) => (T)target.GetType().GetField(field, PrivateInstance).GetValue(target);
    }
}
