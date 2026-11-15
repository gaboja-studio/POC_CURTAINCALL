using System.Collections.Generic;
using System.Linq;
using CurtainCall.Player;
using BodyPart = CurtainCall.Player.BodyPart;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CurtainCall.WaitingRoom.PassageGimmicks
{
    // 13단계: 폐기물 보관소 전시대의 인형 옷 4벌(곰·토끼·삐에로·인형)을 플레이어 캐릭터 모델로 바꾼다(2026-11-13 사용자). 11단계 뒤에 실행. 다시 실행할 수 있다.
    // - 기본 도형으로 만든 옛 인형 옷은 끄고, 같은 자리에 캐릭터 모델을 자세 잡아 고정 메시로 구워 둔다(움직이지 않는 전시물).
    // - 누워 있지 않다: 앉아 있거나 받침대에 받쳐져 서 있다.
    // - 없는 팔다리는 새로 만들지 않고 신체 절단 기능의 메시(단계별 몸통 HumanoidModel.Bodies, 잘린 부위 Limbs)를 그대로 쓴다.
    //   절단 순서와 다른 조합(팔 하나만 없음)은 "팔다리가 모두 잘린 몸통 + 남아 있는 부위"를 함께 세워 만든다.
    // - 플레이어와 구분되게 바랜 색 재질 사본을 쓴다.
    // - 네 벌 모두 살펴볼 수 있다(설명은 문구 파일 target.final:old-costume*.desc).
    public static partial class FinalLayoutBuilder
    {
        // 캐릭터 그림·메시에서 만든 사본이라 원본과 같은 비공개 외부 에셋 저장소에 둔다.
        const string CostumeFolder = "Assets/_ThirdParty/CharacterArt/OldCostumes";
        const string CostumeChild = "CharacterCostume", StandChild = "DisplayStand";
        // 플레이어 캐릭터가 게임에서 보이는 크기에 맞춘 배율(모델 원본 키 약 2m).
        const float CostumeScale = .62f;
        // 바랜 색(차갑게 빠진 회청색). 원래 재질 색에 곱한다. 캐릭터 전용 재질이면 때 묻은 정도(_DirtAmount)도 올린다.
        static readonly Color CostumeFade = new(.82f, .84f, .86f);
        const float CostumeDirt = .55f;


        // 전시물, 캐릭터, 없는 부위, 자세(동작 이름·시점 0~1), 널브러진 기울기(뒤로 기댄 각도·돌린 각도·옆으로 쓰러진 각도),
        // 색조 돌리기(0~1, 플레이어 캐릭터와 다른 색), 조사 대상 ID·이름(설명은 문구 파일).
        // 2026-11-15 사용자: 모두 같은 앉은 자세로 굳어 보였다 → 벌마다 다르게 벽에 기댄다(자세는 아래 CostumePoses에서 직접 만든다). 눕히지 않는다.
        static readonly (string exhibit, string model, BodyPart lost, string clip, float at, Vector3 slump, float hue, string id, string title)[] Costumes =
        {
            // 2026-11-15 사용자: 곰이 (방 안에서 볼 때) 맨 왼쪽에 와야 한다 → 곰과 삐에로의 자리(옛 전시물 칸)를 맞바꿨다.
            // 곰: 설명에 없는 부위가 적혀 있지 않아 온전한 채로 둔다.
            ("04_Clown_Slumped_MissingLowerLeg", "Bear", BodyPart.None, "Idle", 0f, new Vector3(14, 18, -4), .55f, "final:old-costume", "낡은 곰인형 옷"),
            // 토끼: 팔이 잘려 있다(어느 팔인지는 적혀 있지 않아 인형과 겹치지 않게 오른팔).
            ("02_Rabbit_Prone_MissingRightLeg", "Rabbit", BodyPart.RightArm, "Idle", 0f, new Vector3(16, -25, 10), .5f, "final:old-costume-rabbit", "낡은 토끼 인형 옷"),
            // 인형: 왼팔이 없다.
            ("03_Ringmaster_Side_MissingHead", "PinkGirl", BodyPart.LeftArm, "Idle", 0f, new Vector3(10, 28, -12), .33f, "final:old-costume-doll", "낡은 인형 옷"),
            // 삐에로: 두 다리가 없다 → 몸통째 벽에 기대 앉아 있다.
            ("01_Bear_Seated_MissingLeftArm", "BluePierrot", BodyPart.LeftLeg | BodyPart.RightLeg, "Idle", 0f, new Vector3(22, -18, 10), .45f, "final:old-costume-pierrot", "낡은 삐에로 인형 옷"),
        };

        // 2026-11-15 사용자: 캐릭터에 있는 동작을 그대로 쓰지 않고 자세를 직접 만든다. 기준 동작(Idle)을 잡은 뒤 뼈를 차례로 돌린다(부모 → 자식 순서).
        // 각도는 캐릭터가 +z를 보고 선 상태의 월드 축 기준: x 음수 = 다리·팔을 앞으로 들기, x 양수 = 허리·목을 앞으로 숙이기 / 무릎 굽히기, y = 좌우로 돌리기, z = 옆으로 기울이기.
        static readonly Dictionary<string, (HumanBodyBones bone, Vector3 turn)[]> CostumePoses = new()
        {
            // 곰: 두 다리를 앞으로 뻗고 벽에 등을 대고 앉아 고개를 떨군다.
            ["Bear"] = new[]
            {
                (HumanBodyBones.LeftUpperLeg, new Vector3(-85, -14, 0)), (HumanBodyBones.RightUpperLeg, new Vector3(-85, 14, 0)),
                (HumanBodyBones.LeftLowerLeg, new Vector3(8, 0, 0)), (HumanBodyBones.RightLowerLeg, new Vector3(8, 0, 0)),
                (HumanBodyBones.Spine, new Vector3(6, 0, 0)), (HumanBodyBones.Head, new Vector3(26, 0, 8)),
                (HumanBodyBones.LeftUpperArm, new Vector3(-18, 0, 0)), (HumanBodyBones.RightUpperArm, new Vector3(-18, 0, 0)),
            },
            // 토끼: 한쪽 무릎을 세우고 앉아 어깨를 벽에 기대고 고개를 옆으로 기울인다.
            ["Rabbit"] = new[]
            {
                (HumanBodyBones.LeftUpperLeg, new Vector3(-85, -10, 0)), (HumanBodyBones.RightUpperLeg, new Vector3(-125, 8, 0)),
                (HumanBodyBones.RightLowerLeg, new Vector3(110, 0, 0)),
                (HumanBodyBones.Spine, new Vector3(4, 0, -8)), (HumanBodyBones.Head, new Vector3(14, 0, -18)),
                (HumanBodyBones.LeftUpperArm, new Vector3(-12, 0, 0)),
            },
            // 인형: 무릎을 꿇고 주저앉아 고개를 떨군다(2026-11-15 사용자: 다리를 앞으로 뻗으면 치마를 뚫고 나온다).
            ["PinkGirl"] = new[]
            {
                (HumanBodyBones.LeftUpperLeg, new Vector3(-50, -8, 0)), (HumanBodyBones.RightUpperLeg, new Vector3(-50, 8, 0)),
                (HumanBodyBones.LeftLowerLeg, new Vector3(140, 0, 0)), (HumanBodyBones.RightLowerLeg, new Vector3(140, 0, 0)),
                (HumanBodyBones.Spine, new Vector3(10, 0, 6)), (HumanBodyBones.Head, new Vector3(22, -14, 0)),
                (HumanBodyBones.RightUpperArm, new Vector3(-14, 0, 0)),
            },
            // 삐에로(두 다리 없음): 몸통째 벽에 기대 앞으로 처지고 고개를 떨군다.
            ["BluePierrot"] = new[]
            {
                (HumanBodyBones.Spine, new Vector3(12, 0, 0)), (HumanBodyBones.Head, new Vector3(30, 0, -12)),
                (HumanBodyBones.LeftUpperArm, new Vector3(-24, 0, 0)), (HumanBodyBones.RightUpperArm, new Vector3(-10, 0, 0)),
            },
        };

        [MenuItem("CurtainCall/Waiting Room/Final Layout/13. Replace Display Costumes")]
        static void CostumesMenu() => Debug.Log(ReplaceDisplayCostumes());

        public static string ReplaceDisplayCostumes()
        {
            var layout = GameObject.Find(LayoutPath)?.transform;
            var display = layout != null ? layout.Find("S05_JunkStorage/AbandonedResearchStorage/RetiredTroupeDisplay") : null;
            if (display == null) return "WaitingRoom 씬의 전시대를 찾지 못했습니다.";
            int undo = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("Final layout: display costumes");
            foreach (var folder in new[] { CostumeFolder })
                if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder(System.IO.Path.GetDirectoryName(folder).Replace('\\', '/'), System.IO.Path.GetFileName(folder));
            var iron = display.Find("DisplayBoundaryPost")?.GetComponentInChildren<Renderer>()?.sharedMaterial;
            var clueRoot = Child(Child(layout, RootName), "Objects");
            // 옛 조사 대상(광대 자리 하나)과 지난 실행 결과를 지운다.
            foreach (var holderName in new[] { "OldCostume" }.Concat(Costumes.Select(c => "OldCostume_" + c.model)))
                if (clueRoot.Find(holderName) is Transform stale) Undo.DestroyObjectImmediate(stale.gameObject);
            var notes = new List<string>(); costumeDone.Clear();
            foreach (var entry in Costumes)
            {
                var exhibit = display.Find(entry.exhibit);
                var prefab = Resources.Load<GameObject>($"Prefabs/Characters/Players/Models/{entry.model}/{entry.model}");
                if (exhibit == null || prefab == null) { notes.Add(entry.exhibit + ": 찾지 못함"); continue; }
                foreach (var childName in new[] { CostumeChild, StandChild })
                    if (exhibit.Find(childName) is Transform old) Undo.DestroyObjectImmediate(old.gameObject);
                // 옛 도형 인형 옷이 차지하던 자리(꺼 둔 것도 메시로 잰다).
                Bounds? spot = null;
                foreach (var r in exhibit.GetComponentsInChildren<MeshRenderer>(true))
                {
                    if (r.GetComponent<MeshFilter>()?.sharedMesh == null) continue;
                    var b = MeshBounds(r); if (spot is Bounds s) { s.Encapsulate(b); spot = s; } else spot = b;
                }
                if (spot == null) { notes.Add(entry.exhibit + ": 자리 없음"); continue; }

                var costume = new GameObject(CostumeChild).transform;
                Undo.RegisterCreatedObjectUndo(costume.gameObject, "Costume");
                // 임시로 캐릭터를 세워 자세를 잡고, 보여 줄 부위마다 고정 메시로 굽는다.
                var temp = (GameObject)PrefabUtility.InstantiatePrefab(prefab); temp.hideFlags = HideFlags.HideAndDontSave;
                int parts = 0;
                try
                {
                    temp.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                    var model = temp.GetComponentInChildren<HumanoidModel>(true);
                    var clip = model.Animator.runtimeAnimatorController.animationClips.FirstOrDefault(c => c.name == entry.clip);
                    if (clip == null) { notes.Add($"{entry.exhibit}: 동작 {entry.clip} 없음"); Object.DestroyImmediate(costume.gameObject); continue; }
                    AnimationMode.StartAnimationMode();
                    try
                    {
                        AnimationMode.BeginSampling(); AnimationMode.SampleAnimationClip(model.Animator.gameObject, clip, clip.length * Mathf.Clamp01(entry.at)); AnimationMode.EndSampling();
                        if (CostumePoses.TryGetValue(entry.model, out var pose))
                            foreach (var (bone, turn) in pose)
                                if (model.Animator.GetBoneTransform(bone) is Transform joint) joint.rotation = Quaternion.Euler(turn) * joint.rotation;
                        foreach (var (source, label) in CostumeParts(model, entry.lost))
                        {
                            var mesh = new Mesh { name = $"OldCostume_{entry.model}_{label}" };
                            source.BakeMesh(mesh, true);
                            string meshPath = $"{CostumeFolder}/{mesh.name}.asset";
                            var saved = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                            if (saved != null) { EditorUtility.CopySerialized(mesh, saved); Object.DestroyImmediate(mesh); mesh = saved; mesh.vertices = mesh.vertices; mesh.RecalculateBounds(); mesh.UploadMeshData(false); EditorUtility.SetDirty(mesh); } // 복사로 덮으면 화면용 메시가 옛 자세로 남는다 → 다시 올린다
                            else AssetDatabase.CreateAsset(mesh, meshPath);
                            var part = new GameObject(label, typeof(MeshFilter), typeof(MeshRenderer)).transform;
                            // 구운 메시는 그 렌더러 기준 좌표다. 캐릭터 원점(costume) 아래에 같은 자리로 둔다.
                            part.SetPositionAndRotation(source.transform.position, source.transform.rotation); part.SetParent(costume, true);
                            part.GetComponent<MeshFilter>().sharedMesh = mesh;
                            part.GetComponent<MeshRenderer>().sharedMaterials = source.sharedMaterials.Select(m => FadedMaterial(m, entry.hue)).ToArray();
                            GameObjectUtility.SetStaticEditorFlags(part.gameObject, StaticEditorFlags.OccludeeStatic);
                            parts++;
                        }
                    }
                    finally { AnimationMode.StopAnimationMode(); }
                }
                finally { Object.DestroyImmediate(temp); }

                // 크기를 주고, 방 안쪽(북쪽)을 보게 한 뒤 제각각 기울인다: 옆으로 쓰러지고(roll) 돌아앉고(yaw) 뒤로 기댄다(lean).
                costume.localScale = Vector3.one * CostumeScale;
                costume.rotation = Quaternion.AngleAxis(-entry.slump.x, Vector3.right) * Quaternion.Euler(0, entry.slump.y, 0) * Quaternion.AngleAxis(entry.slump.z, Vector3.forward);
                costume.SetParent(exhibit, true);
                var target = spot.Value; var now = CostumeBounds(costume); float floorY = target.min.y;
                // 뒷벽(남쪽)에 등이 닿게 붙이고 가장 낮은 곳이 전시대 바닥에 닿게 한다(떠 있지 않게).
                float wallZ = Physics.Raycast(new Vector3(target.center.x, floorY + .6f, target.center.z + .3f), Vector3.back, out var wall, 3f, ~0, QueryTriggerInteraction.Ignore) ? wall.point.z : target.min.z;
                costume.position += new Vector3(target.center.x - now.center.x, floorY - now.min.y, wallZ + .01f - now.min.z);
                now = CostumeBounds(costume);

                // 옛 도형 인형 옷은 끈다(지우지 않는다).
                foreach (Transform child in exhibit)
                    if (child != costume && child.name != StandChild && child.gameObject.activeSelf) { Undo.RecordObject(child.gameObject, "Hide dummy costume"); child.gameObject.SetActive(false); }

                // 살펴보기 대상.
                var holder = Child(clueRoot, entry.id == "final:old-costume" ? "OldCostume" : "OldCostume_" + entry.model);
                Undo.RegisterCreatedObjectUndo(holder.gameObject, "Costume clue"); holder.position = now.center;
                var clue = Clue(holder, entry.id, entry.title, "", costume, null, .55f, 1.6f);
                // 옷의 원점이 받침대 속에 묻히면 손이 닿는 길이 받침대에 막혀 살펴볼 수 없다. 옷 한가운데를 닿는 점으로 쓴다(2026-11-15 사용자: 인형 옷 하나만 살펴봐짐).
                var reach = new SerializedObject(clue); reach.FindProperty("reachPoint").objectReferenceValue = holder; reach.ApplyModifiedPropertiesWithoutUndo();
                notes.Add($"{entry.title}({entry.model}, 없는 부위 {(entry.lost == BodyPart.None ? "없음" : entry.lost.ToString())}) 높이 {now.size.y:F2}m 조각 {parts}개");
            }
            AssetDatabase.SaveAssets();
            Undo.CollapseUndoOperations(undo); EditorSceneManager.MarkSceneDirty(layout.gameObject.scene);
            return string.Join(" / ", notes);
        }

        // 보여 줄 메시: 절단 순서대로 잃은 경우는 그 단계 몸통 하나, 아니면 팔다리가 모두 잘린 몸통 + 남은 부위.
        static IEnumerable<(SkinnedMeshRenderer source, string label)> CostumeParts(HumanoidModel model, BodyPart lost)
        {
            for (int stage = 0; stage <= 4; stage++)
                if (PlayerCondition.PartsForLostCount(stage) == lost)
                { yield return (stage == 0 ? model.Original : model.Bodies[stage - 1], "Cut" + stage); yield break; }
            yield return (model.Bodies[3], "Torso");
            for (int i = 0; i < 4; i++)
                if ((lost & PlayerCondition.CutOrder[i]) == 0) yield return (model.Limbs[i], PlayerCondition.CutOrder[i].ToString());
        }

        // 기울인 옷의 실제 크기: 상자 어림이 아니라 꼭짓점으로 잰다(어림으로 재면 바닥·벽에서 떠 보인다).
        static Bounds CostumeBounds(Transform costume)
        {
            Bounds? all = null;
            foreach (var filter in costume.GetComponentsInChildren<MeshFilter>(true))
            {
                var matrix = filter.transform.localToWorldMatrix;
                foreach (var vertex in filter.sharedMesh.vertices)
                {
                    var p = matrix.MultiplyPoint3x4(vertex);
                    if (all is Bounds b) { b.Encapsulate(p); all = b; } else all = new Bounds(p, Vector3.zero);
                }
            }
            return all ?? new Bounds(costume.position, Vector3.zero);
        }

        // 색을 바꾼 재질 사본(원본 재질·그림은 건드리지 않는다). 그림의 색조를 돌린 사본을 만들어 플레이어 캐릭터와 다른 색으로 보이게 한다.
        static readonly Dictionary<string, Object> costumeDone = new();
        static Material FadedMaterial(Material source, float hue)
        {
            if (source == null) return null;
            string path = $"{CostumeFolder}/Old_{source.name}.mat";
            // 한 번 실행하는 동안 같은 재질·그림은 한 번만 만든다(부위마다 다시 만들면 매우 느리다).
            if (costumeDone.TryGetValue(path, out var done) && done is Material ready) return ready;
            var faded = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (faded == null) { faded = new Material(source) { name = "Old_" + source.name }; AssetDatabase.CreateAsset(faded, path); }
            else { faded.shader = source.shader; faded.CopyPropertiesFromMaterial(source); }
            foreach (var property in new[] { "_BaseMap", "_MainTex" })
                if (faded.HasProperty(property) && source.GetTexture(property) is Texture2D picture) faded.SetTexture(property, Recolored(picture, hue));
            // 캐릭터 전용 재질의 옷 염색 색도 같은 만큼 돌린다.
            if (faded.HasProperty("_VariantColor")) faded.SetColor("_VariantColor", Shift(source.GetColor("_VariantColor"), hue));
            foreach (var property in new[] { "_BaseColor", "_Color" })
                if (faded.HasProperty(property)) faded.SetColor(property, source.GetColor(property) * CostumeFade);
            if (faded.HasProperty("_DirtAmount")) faded.SetFloat("_DirtAmount", CostumeDirt);
            EditorUtility.SetDirty(faded);
            costumeDone[path] = faded;
            return faded;
        }

        static Color Shift(Color c, float hue)
        {
            Color.RGBToHSV(c, out float h, out float s, out float v);
            var shifted = Color.HSVToRGB(Mathf.Repeat(h + hue, 1f), s * .8f, v); shifted.a = c.a;
            return shifted;
        }

        // 그림 사본: 색조를 돌리고 채도를 조금 뺀다. 원본이 읽기 금지여도 되게 그려서 읽는다.
        static Texture2D Recolored(Texture2D source, float hue)
        {
            string path = $"{CostumeFolder}/Old_{source.name}.png";
            if (costumeDone.TryGetValue(path, out var done) && done is Texture2D ready) return ready;
            // 전시물은 가까이서 오래 보지 않으므로 긴 변 1024까지만 쓴다.
            float shrink = Mathf.Min(1f, 1024f / Mathf.Max(source.width, source.height));
            int w = Mathf.Max(1, Mathf.RoundToInt(source.width * shrink)), h = Mathf.Max(1, Mathf.RoundToInt(source.height * shrink));
            var rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Graphics.Blit(source, rt); var before = RenderTexture.active; RenderTexture.active = rt;
            var copy = new Texture2D(w, h, TextureFormat.RGBA32, false);
            copy.ReadPixels(new Rect(0, 0, w, h), 0, 0); RenderTexture.active = before; RenderTexture.ReleaseTemporary(rt);
            var pixels = copy.GetPixels();
            for (int i = 0; i < pixels.Length; i++) pixels[i] = Shift(pixels[i], hue);
            copy.SetPixels(pixels); copy.Apply();
            System.IO.File.WriteAllBytes(path, copy.EncodeToPNG()); Object.DestroyImmediate(copy);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var result = AssetDatabase.LoadAssetAtPath<Texture2D>(path); costumeDone[path] = result;
            return result;
        }
    }
}
