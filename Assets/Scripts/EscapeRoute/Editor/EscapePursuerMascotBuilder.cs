using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace CurtainCall.EscapeRoute.EditorTools
{
    public static class EscapePursuerMascotBuilder
    {
        const string Output = "Assets/_ThirdParty/Task100/Mascot";
        const string Brute = "Assets/_ThirdParty/ExplosiveLLC/Brute Warrior Mecanim Animation Pack/Animations/";
        static readonly HumanBodyBones[] Bases = { HumanBodyBones.LeftFoot, HumanBodyBones.RightFoot,
            HumanBodyBones.LeftHand, HumanBodyBones.RightHand, HumanBodyBones.LeftUpperLeg, HumanBodyBones.RightUpperLeg,
            HumanBodyBones.LeftUpperArm, HumanBodyBones.RightUpperArm };
        static readonly HumanBodyBones[] Tips = { HumanBodyBones.LeftToes, HumanBodyBones.RightToes,
            HumanBodyBones.LeftMiddleProximal, HumanBodyBones.RightMiddleProximal, HumanBodyBones.LeftFoot, HumanBodyBones.RightFoot,
            HumanBodyBones.LeftHand, HumanBodyBones.RightHand };

        [MenuItem("Tools/CurtainCall/탈출로/Mascot 추격자 에셋 적용")]
        public static void Apply()
        {
            var scene = SceneManager.GetActiveScene();
            if (Application.isPlaying || scene.path != EscapePursuerLayoutReview.SourceScene)
                throw new InvalidOperationException("운영 A를 편집 모드로 여세요.");
            var route = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<EscapePursuerRoute>(true)).Single();
            var chase = route.GetComponentInChildren<EscapeUnderstageChase>(true);
            if (chase.GetComponentInChildren<EscapePursuerMascot>(true)) throw new InvalidOperationException("이미 Mascot이 연결되어 있습니다.");
            var actor = new SerializedObject(chase);
            var gait = (Animation)actor.FindProperty("gait").objectReferenceValue;
            var boss = chase.GetComponent<EscapePursuerBoss>();
            var oldMeshes = chase.GetComponentsInChildren<Renderer>(true).Where(r => r is MeshRenderer || r is SkinnedMeshRenderer).ToArray();
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(Output + "/Mascot3Humanoid.fbx");
            var model = (GameObject)PrefabUtility.InstantiatePrefab(source);
            model.name = "Mascot 5m";
            PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.OutermostRoot, InteractionMode.AutomatedAction);
            var animator = model.GetComponent<Animator>();
            if (!animator || !animator.isHuman || !animator.avatar.isValid) throw new InvalidOperationException("유효한 Humanoid 복사본이 필요합니다.");
            var sourceSkins = model.GetComponentsInChildren<SkinnedMeshRenderer>();
            Bounds initial = sourceSkins[0].bounds;
            foreach (var skin in sourceSkins.Skip(1)) initial.Encapsulate(skin.bounds);
            model.transform.localScale = Vector3.one * (5f / initial.size.y);
            Bounds scaled = sourceSkins[0].bounds;
            foreach (var skin in sourceSkins.Skip(1)) scaled.Encapsulate(skin.bounds);
            model.transform.position -= Vector3.up * scaled.min.y;

            var material = BuildSurface(sourceSkins[0].sharedMaterial);
            foreach (var skin in sourceSkins) skin.sharedMaterials = Enumerable.Repeat(material, skin.sharedMaterials.Length).ToArray();
            var body = sourceSkins.Single(s => s.name == "Body");
            var parts = Split(body, animator);
            var appearance = model.AddComponent<EscapePursuerMascot>();
            appearance.chase = chase;
            appearance.animator = animator;
            appearance.boss = boss;
            appearance.legacyMeshes = oldMeshes;
            appearance.clips = new[] { Clip("Idle"), Clip("Walk"), Clip("Run"), Crouch(), Clip("SpecialAttack1"), Clip("Attack1"),
                Clip("RangeAttack1"), Clip("Jump", "Jump"), Clip("Jump", "Land"), Clip("LightHit") };
            var bruteModel = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_ThirdParty/ExplosiveLLC/Brute Warrior Mecanim Animation Pack/Characters/Brute.FBX");
            float ratio = animator.humanScale * model.transform.lossyScale.y / bruteModel.GetComponent<Animator>().humanScale;
            appearance.walkCycleDistance = Mathf.Max(1f, appearance.clips[1].averageSpeed.magnitude * appearance.clips[1].length * ratio);
            appearance.runCycleDistance = Mathf.Max(1f, appearance.clips[2].averageSpeed.magnitude * appearance.clips[2].length * ratio);
            appearance.hitboxes = new BoxCollider[8];
            appearance.starts = new Transform[8]; appearance.ends = new Transform[8]; appearance.stumps = new GameObject[8];
            var wounds = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Mascot torn stuffing", color = new Color(.19f,.025f,.02f) };
            AssetDatabase.CreateAsset(wounds, Output + "/TornStuffing.mat");
            var serializedBoss = new SerializedObject(boss);
            var slots = serializedBoss.FindProperty("parts");
            for (int i = 0; i < 8; i++)
            {
                var slot = slots.GetArrayElementAtIndex(i);
                var old = slot.FindPropertyRelative("hitbox").objectReferenceValue as BoxCollider;
                if (old) old.enabled = false;
                var hit = new GameObject("Mascot cut target " + i).AddComponent<BoxCollider>();
                hit.transform.SetParent(model.transform, false); hit.isTrigger = true;
                appearance.hitboxes[i] = hit;
                appearance.starts[i] = animator.GetBoneTransform(Bases[i]);
                appearance.ends[i] = animator.GetBoneTransform(Tips[i]) ?? appearance.starts[i];
                var stump = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                stump.name = "Torn joint " + i;
                Object.DestroyImmediate(stump.GetComponent<Collider>());
                stump.transform.SetParent(chase.transform, false);
                stump.transform.localScale = new Vector3(i < 4 ? .4f : .65f, .035f, i < 4 ? .4f : .65f);
                stump.GetComponent<MeshRenderer>().sharedMaterial = wounds;
                stump.SetActive(false); appearance.stumps[i] = stump;
                slot.FindPropertyRelative("hitbox").objectReferenceValue = hit;
                var meshes = slot.FindPropertyRelative("meshes"); meshes.arraySize = 1;
                meshes.GetArrayElementAtIndex(0).objectReferenceValue = parts[i + 1];
            }
            serializedBoss.ApplyModifiedPropertiesWithoutUndo();
            foreach (var mesh in oldMeshes) mesh.enabled = false;
            model.transform.SetParent(gait.transform, false);
            actor.FindProperty("mascot").objectReferenceValue = appearance;
            var rock = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rock.name = "Pursuer thrown concrete";
            Object.DestroyImmediate(rock.GetComponent<Collider>());
            rock.transform.SetParent(chase.transform, false);
            rock.transform.localScale = new Vector3(.75f,.6f,.7f);
            rock.GetComponent<MeshRenderer>().sharedMaterial = route.GetComponentsInChildren<MeshRenderer>(true)
                .First(r => r.name == "Final leap concrete breach").sharedMaterial;
            rock.SetActive(false);
            actor.FindProperty("thrownDebris").objectReferenceValue = rock.transform;
            var audio = new GameObject("Pursuer threats").AddComponent<AudioSource>();
            audio.transform.SetParent(chase.transform, false); audio.transform.localPosition = Vector3.up * 3.8f;
            audio.playOnAwake = false; audio.spatialBlend = 1; audio.minDistance = 10; audio.maxDistance = 70; audio.volume = .85f; audio.pitch = .75f;
            actor.FindProperty("threatAudio").objectReferenceValue = audio;
            actor.FindProperty("chargeVoice").objectReferenceValue = Audio("SFX-Growl-Fast-01_wav");
            actor.FindProperty("throwVoice").objectReferenceValue = Audio("Golem_Attack_v1_variation_02_wav");
            actor.ApplyModifiedPropertiesWithoutUndo();
            var settings = AssetDatabase.LoadAssetAtPath<CurtainCall.Settings.EscapeRouteSettings>("Assets/Resources/Settings/GameSettings/EscapeRouteSettings.asset");
            settings.Pursuer.severSeconds = 1.6f; settings.Pursuer.holdSeconds = 4.5f;
            EditorUtility.SetDirty(settings); AssetDatabase.SaveAssetIfDirty(settings);
            Save(route.transform, scene);
        }

        static AnimationClip Clip(string file, string name = null) => AssetDatabase.LoadAllAssetsAtPath(Brute + "Brute@" + file + ".FBX")
            .OfType<AnimationClip>().First(c => !c.name.StartsWith("__preview__") && (name == null || c.name == name));

        [MenuItem("Tools/CurtainCall/탈출로/기존 추격자 외형으로 복원")]
        public static void RestoreGreybox()
        {
            var scene = SceneManager.GetActiveScene();
            if (Application.isPlaying || scene.path != "Assets/Scenes/Levels/escape_route.unity")
                throw new InvalidOperationException("Play를 종료하고 탈출로 A 원본 씬에서 실행하세요.");
            var mascot = Object.FindFirstObjectByType<EscapePursuerMascot>(FindObjectsInactive.Include);
            if (!mascot) return;
            var chase = mascot.chase;
            var boss = new SerializedObject(mascot.boss);
            var parts = boss.FindProperty("parts");
            for (int i = 0; i < 8; i++)
            {
                var hit = chase.GetComponentsInChildren<BoxCollider>(true).Single(c => c.name == "Saw cut target " + i);
                hit.enabled = true;
                var slot = parts.GetArrayElementAtIndex(i);
                slot.FindPropertyRelative("hitbox").objectReferenceValue = hit;
                var renderers = i < 4 ? hit.transform.parent.GetComponents<Renderer>()
                    : hit.transform.parent.GetComponentsInChildren<Renderer>(true).Where(r => r.name != "Hand" && r.name != "Foot").ToArray();
                var meshes = slot.FindPropertyRelative("meshes"); meshes.arraySize = renderers.Length;
                for (int j = 0; j < renderers.Length; j++) meshes.GetArrayElementAtIndex(j).objectReferenceValue = renderers[j];
            }
            boss.ApplyModifiedPropertiesWithoutUndo();
            foreach (var mesh in mascot.legacyMeshes) if (mesh) mesh.enabled = true;
            foreach (var stump in mascot.stumps) if (stump) stump.SetActive(false);
            mascot.gameObject.SetActive(false);
            var actor = new SerializedObject(chase); actor.FindProperty("mascot").objectReferenceValue = null;
            actor.ApplyModifiedPropertiesWithoutUndo();
            Save(chase.GetComponentInParent<EscapePursuerRoute>().transform, scene);
        }
        static AnimationClip Crouch()
        {
            string path = AssetDatabase.FindAssets("HumanM@Crouch01_Walk t:Model", new[] { "Assets/_ThirdParty/Kevin Iglesias" })
                .Select(AssetDatabase.GUIDToAssetPath).FirstOrDefault(p => !p.Contains("[RM]") && p.EndsWith("HumanM@Crouch01_Walk.fbx"));
            return path != null ? AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().First(c => !c.name.StartsWith("__preview__")) : Clip("WalkSlow");
        }
        static AudioClip Audio(string name) => AssetDatabase.FindAssets(name + " t:AudioClip", new[] { "Assets/_ThirdParty/MonstersSFX" })
            .Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<AudioClip>).First();
        static Material BuildSurface(Material source)
        {
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Mascot3 URP" };
            material.SetTexture("_BaseMap", source.GetTexture("_MainTex"));
            material.SetColor("_BaseColor", Color.white);
            material.SetTexture("_BumpMap", source.GetTexture("_BumpMap"));
            if (material.GetTexture("_BumpMap")) material.EnableKeyword("_NORMALMAP");
            material.SetFloat("_Smoothness", .22f);
            AssetDatabase.CreateAsset(material, Output + "/Mascot3URP.mat");
            return material;
        }

        static SkinnedMeshRenderer[] Split(SkinnedMeshRenderer source, Animator animator)
        {
            var mesh = source.sharedMesh;
            var roots = Bases.Select(animator.GetBoneTransform).ToArray();
            int Group(int bone)
            {
                var t = source.bones[bone];
                // Distal parts must win over their containing arm/leg subtree.
                for (int i = 0; i < roots.Length; i++) if (t == roots[i] || t.IsChildOf(roots[i])) return i + 1;
                return 0;
            }
            var weights = mesh.boneWeights;
            var lists = Enumerable.Range(0, 9).Select(_ => Enumerable.Range(0, mesh.subMeshCount).Select(__ => new List<int>()).ToArray()).ToArray();
            int total = 0;
            for (int sub = 0; sub < mesh.subMeshCount; sub++)
            {
                var triangles = mesh.GetTriangles(sub);
                for (int i = 0; i < triangles.Length; i += 3)
                {
                    var influence = new float[9];
                    for (int corner = 0; corner < 3; corner++)
                    {
                        var w = weights[triangles[i + corner]];
                        influence[Group(w.boneIndex0)] += w.weight0; influence[Group(w.boneIndex1)] += w.weight1;
                        influence[Group(w.boneIndex2)] += w.weight2; influence[Group(w.boneIndex3)] += w.weight3;
                    }
                    int group = Array.IndexOf(influence, influence.Max());
                    lists[group][sub].AddRange(new[] { triangles[i], triangles[i + 1], triangles[i + 2] });
                    total++;
                }
            }
            var skins = new SkinnedMeshRenderer[9];
            for (int i = 0; i < 9; i++)
            {
                int count = lists[i].Sum(a => a.Count);
                if (count == 0) throw new InvalidOperationException("빈 절단 부위 " + i);
                var copy = Object.Instantiate(mesh); copy.name = "Mascot part " + i;
                for (int sub = 0; sub < mesh.subMeshCount; sub++) copy.SetTriangles(lists[i][sub], sub, false);
                // Keep source skinning/UVs intact: every source triangle belongs to exactly one part.
                AssetDatabase.CreateAsset(copy, Output + "/Part" + i + ".asset");
                var skin = new GameObject(copy.name).AddComponent<SkinnedMeshRenderer>();
                skin.transform.SetParent(source.transform, false); skin.sharedMesh = copy;
                skin.sharedMaterials = source.sharedMaterials; skin.bones = source.bones; skin.rootBone = source.rootBone;
                skin.localBounds = source.localBounds; skin.updateWhenOffscreen = true;
                skins[i] = skin;
            }
            if (lists.Sum(a => a.Sum(b => b.Count)) != total * 3) throw new InvalidOperationException("메시 분할 보존 검사 실패");
            source.enabled = false;
            return skins;
        }

        static void Save(Transform module, Scene scene)
        {
            var external = new List<(Component component, string path, Object value)>();
            foreach (var component in module.GetComponentsInChildren<Component>(true))
            {
                if (!component) continue;
                var so = new SerializedObject(component); var p = so.GetIterator();
                while (p.Next(true))
                {
                    if (p.propertyType != SerializedPropertyType.ObjectReference) continue;
                    var value = p.objectReferenceValue;
                    Transform t = value is Component c ? c.transform : value is GameObject g ? g.transform : null;
                    if (t && !EditorUtility.IsPersistent(t) && !t.IsChildOf(module)) external.Add((component,p.propertyPath,value));
                }
            }
            PrefabUtility.SaveAsPrefabAssetAndConnect(module.gameObject,
                "Assets/Resources/Prefabs/Characters/Enemies/UnderstagePursuer/EscapeAPursuer.prefab", InteractionMode.AutomatedAction);
            foreach (var item in external)
            {
                var so = new SerializedObject(item.component); so.FindProperty(item.path).objectReferenceValue = item.value; so.ApplyModifiedPropertiesWithoutUndo();
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            EditorSceneManager.SaveScene(scene, EscapeMppmReviewConfig.PursuerScene, true);
            AssetDatabase.SaveAssets();
        }
    }
}
