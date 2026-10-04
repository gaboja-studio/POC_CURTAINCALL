using System.Collections.Generic;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Player;
using CurtainCall.Settings;
using UnityEngine;

namespace CurtainCall.Damage
{
    /// <summary>
    /// 겉모습 연출: 잃은 팔·다리를 모델에서 떼어 날려 보낸다. 떨어진 부위만 래그돌이 되고 몸은 그대로 움직인다.
    /// 몸 전체 래그돌은 추락(네 팔다리 모두 손실 포함) 때 <see cref="PlayerRagdoll"/>이 한다.
    /// 모든 화면이 공유된 <see cref="PlayerCondition"/>을 보고 각자 연출한다(날아간 조각의 물리 결과는 맞추지 않음).
    /// 모델이 바뀌면(교체·래그돌 복구·늦게 접속) 이미 잃은 부위는 날리지 않고 바로 없앤다. 부위가 돌아오면 모델을 새로 끼운다.
    /// 부위 위치는 모델의 <see cref="ModelLimbs"/> 또는 휴머노이드 뼈에서 찾는다. 날아가는 세기는 게임 기본 세팅(신체 손상).
    /// 두 다리를 다 잃으면 남은 몸이 바닥에 닿게 모델을 내리고, 애니메이터에는 잃은 팔·다리 개수(LostArms·LostLegs)를 넘긴다.
    /// </summary>
    [RequireComponent(typeof(PlayerCondition))]
    [RequireComponent(typeof(PlayerModelSlot))]
    public sealed class LimbDismemberment : MonoBehaviour
    {
        static readonly BodyPart[] Limbs = { BodyPart.LeftArm, BodyPart.RightArm, BodyPart.LeftLeg, BodyPart.RightLeg };

        // 스킨 메시에서 잘린 뼈는 0으로 줄이면 계산 경고가 날 수 있어 아주 작게만 줄인다
        const float CollapsedScale = 0.0001f;

        static readonly int LostArmsHash = Animator.StringToHash("LostArms");
        static readonly int LostLegsHash = Animator.StringToHash("LostLegs");

        PlayerCondition condition;
        PlayerModelSlot modelSlot;
        CharacterController body;

        GameObject appliedModel;
        BodyPart appliedParts;
        bool settled; // 두 다리를 잃어 모델을 내렸는지(모델마다)
        readonly List<Transform> collapsedBones = new();
        readonly List<GameObject> pieces = new();
        readonly List<(GameObject piece, Mesh mesh)> createdMeshes = new();
        BodyPart warnedMissing;

        static BaseGameSettings.BodyDamageSettings Settings => GameSettings.Base.BodyDamage;

        /// <summary>지금 날아가 있는 조각 수.</summary>
        public int PieceCount
        {
            get
            {
                pieces.RemoveAll(p => p == null);
                return pieces.Count;
            }
        }

        /// <summary>날아간 조각을 모두 지운다. 묘기 재시작 때 자동으로 불린다.</summary>
        public void ClearPieces()
        {
            foreach (var piece in pieces)
                if (piece != null) Destroy(piece);
            pieces.Clear();
            ReleaseMeshes(true);
        }

        void Awake()
        {
            condition = GetComponent<PlayerCondition>();
            modelSlot = GetComponent<PlayerModelSlot>();
            body = GetComponent<CharacterController>();
            condition.Changed += HandleConditionChanged;
            NetworkPlayer.Restarted += HandleRestarted;
        }

        void OnDestroy()
        {
            if (condition != null) condition.Changed -= HandleConditionChanged;
            NetworkPlayer.Restarted -= HandleRestarted;
            ClearPieces();
        }

        // 이번에 새로 잃은 부위만 날리고, 전부터 잃어 있던 부위는 조용히 없앤다
        void HandleConditionChanged(BodyPart previous, BodyPart current) => Sync(previous);

        void HandleRestarted(NetworkPlayer player)
        {
            if (player.gameObject == gameObject) ClearPieces();
        }

        void LateUpdate()
        {
            if (modelSlot.CurrentModel != appliedModel) Sync(PlayerCondition.AllLimbs);
            if (createdMeshes.Count > 0) ReleaseMeshes(false); // 수명이 끝나 사라진 조각

            // 애니메이션이 뼈 크기를 되돌려도 잘린 부위는 계속 숨긴다
            foreach (var bone in collapsedBones)
                if (bone != null) bone.localScale = Vector3.one * CollapsedScale;
        }

        /// <summary>지금 모델을 잃은 부위에 맞춘다. <paramref name="silentParts"/>에 든 부위는 날리지 않고 없앤다(이미 잃어 있던 부위).</summary>
        void Sync(BodyPart silentParts)
        {
            var model = modelSlot.CurrentModel;
            if (model != appliedModel)
            {
                // 모델이 바뀌었다(교체·래그돌 복구·처음): 다시 처음부터 맞춘다
                appliedModel = model;
                appliedParts = BodyPart.None;
                settled = false;
                collapsedBones.Clear();
            }
            if (model == null) return; // 래그돌 중이거나 모델 없음. 모델이 돌아오면 다시 맞춘다

            BodyPart target = condition.LostParts & PlayerCondition.AllLimbs;
            if ((appliedParts & ~target) != 0)
            {
                // 부위가 돌아왔다(재시작 복구 등): 모델을 새로 끼우면 다음 줄에서 남은 손실만 다시 적용한다
                if (modelSlot.CurrentPrefab == null)
                {
                    Debug.LogWarning("[LimbDismemberment] 프리팹으로 끼운 모델이 아니라 잘린 부위를 되돌릴 수 없습니다. PlayerModelSlot의 Model Prefab을 지정하세요.", this);
                    return;
                }
                modelSlot.SetModel(modelSlot.CurrentPrefab);
                Sync(PlayerCondition.AllLimbs);
                return;
            }

            foreach (var part in Limbs)
            {
                if ((target & part) == 0 || (appliedParts & part) != 0) continue;
                Remove(model, part, (silentParts & part) == 0);
                appliedParts |= part;
            }

            SettleBody(model);
            UpdateAnimator(model);
        }

        /// <summary>
        /// 두 다리를 다 잃으면 남은 몸의 가장 낮은 곳(몸통 아래)이 바닥(슬롯 원점 = 발 위치)에 닿도록 모델을 내린다. 모델마다 한 번.
        /// 다리가 돌아오면 모델을 새로 끼우므로 원래 높이로 돌아간다. 충돌 몸통(캡슐) 높이는 바꾸지 않는다(기획 미정).
        /// </summary>
        void SettleBody(GameObject model)
        {
            if (settled || (appliedParts & PlayerCondition.Legs) != PlayerCondition.Legs) return;
            settled = true;
            if (TryGetBodyBottom(model, out float bottom))
                model.transform.localPosition += Vector3.down * bottom; // 모델의 부모는 슬롯이라 슬롯 기준 높이만큼 내린다
        }

        /// <summary>몸통(팔·다리를 뺀 모델)의 가장 낮은 점(슬롯 기준 높이). 팔이 몸통 아래로 늘어진 모델도 몸통 아래를 기준으로 한다.</summary>
        bool TryGetBodyBottom(GameObject model, out float bottom)
        {
            var slot = modelSlot.Slot;
            var limbRoots = new List<Transform>();
            foreach (var part in Limbs)
            {
                var root = ModelLimbs.Find(model, part);
                if (root != null) limbRoots.Add(root);
            }
            bottom = float.MaxValue;
            bool found = false;

            foreach (var filter in model.GetComponentsInChildren<MeshFilter>())
            {
                var renderer = filter.GetComponent<MeshRenderer>();
                if (renderer == null || !renderer.enabled || filter.sharedMesh == null || IsUnder(filter.transform, limbRoots)) continue;
                Bounds local = filter.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = local.center + Vector3.Scale(local.extents,
                        new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
                    bottom = Mathf.Min(bottom, slot.InverseTransformPoint(filter.transform.TransformPoint(corner)).y);
                }
                found = true;
            }

            foreach (var skin in model.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                var source = skin.sharedMesh;
                if (!skin.enabled || source == null) continue;
                var weights = source.boneWeights;
                if (weights.Length != source.vertexCount) continue;

                var bones = skin.bones;
                var hidden = new bool[bones.Length];
                for (int i = 0; i < bones.Length; i++) hidden[i] = bones[i] != null && IsUnder(bones[i], limbRoots);

                var baked = new Mesh();
                skin.BakeMesh(baked, false);
                var vertices = baked.vertices;
                for (int v = 0; v < vertices.Length; v++)
                {
                    if (InLimb(weights[v], hidden)) continue;
                    bottom = Mathf.Min(bottom, slot.InverseTransformPoint(skin.transform.TransformPoint(vertices[v])).y);
                    found = true;
                }
                Destroy(baked);
            }
            return found;
        }

        static bool IsUnder(Transform target, List<Transform> roots)
        {
            foreach (var root in roots)
                if (target == root || target.IsChildOf(root)) return true;
            return false;
        }

        /// <summary>
        /// 모델에 애니메이션이 있으면 잃은 개수를 넘긴다. 애니메이터에 Int 파라미터 "LostArms"·"LostLegs"가 있을 때만 쓴다(0~2).
        /// 다리 없는 이동 동작 등은 모델 작업에서 이 값으로 고른다.
        /// </summary>
        void UpdateAnimator(GameObject model)
        {
            var animator = model.GetComponentInChildren<Animator>();
            if (animator == null || animator.runtimeAnimatorController == null) return;
            foreach (var parameter in animator.parameters)
            {
                if (parameter.type != AnimatorControllerParameterType.Int) continue;
                if (parameter.nameHash == LostArmsHash) animator.SetInteger(LostArmsHash, condition.LostArmCount);
                else if (parameter.nameHash == LostLegsHash) animator.SetInteger(LostLegsHash, condition.LostLegCount);
            }
        }

        void Remove(GameObject model, BodyPart part, bool fling)
        {
            var root = ModelLimbs.Find(model, part);
            if (root == null)
            {
                if ((warnedMissing & part) == 0)
                    Debug.LogWarning($"[LimbDismemberment] 모델 '{model.name}'에서 {part} 위치를 찾지 못했습니다. 모델에 ModelLimbs를 붙여 칸을 채우거나 휴머노이드 Animator를 쓰세요.", this);
                warnedMissing |= part;
                return;
            }

            var skins = FindSkinsUsing(model, root);
            if (skins.Count > 0)
            {
                if (fling) SpawnSkinnedPiece(part, skins, root);
                collapsedBones.Add(root);
                root.localScale = Vector3.one * CollapsedScale;
                return;
            }

            if (fling) DetachPiece(root.gameObject);
            else root.gameObject.SetActive(false); // 꺼 두면 몸 래그돌(PlayerRagdoll)도 이 부위를 쓰지 않는다
        }

        // ── 부위마다 렌더러가 따로 있는 모델(BlockDoll 등) ───────────────

        /// <summary>부위 오브젝트를 모델에서 떼어 래그돌 조각으로 만든다. 몸과 이어 주던 관절은 지운다.</summary>
        void DetachPiece(GameObject piece)
        {
            piece.transform.SetParent(null, true);
            foreach (var joint in piece.GetComponents<Joint>())
                Destroy(joint);

            var colliders = piece.GetComponentsInChildren<Collider>(true);
            if (colliders.Length == 0) colliders = new Collider[] { AddFittedBox(piece) };

            var bodies = piece.GetComponentsInChildren<Rigidbody>(true);
            if (bodies.Length == 0)
            {
                var single = piece.AddComponent<Rigidbody>();
                single.mass = Settings.LimbMass;
                bodies = new[] { single };
            }

            Launch(piece, colliders, bodies);
        }

        /// <summary>렌더러 범위에 맞춘 상자 충돌체를 붙인다.</summary>
        static Collider AddFittedBox(GameObject piece)
        {
            var box = piece.AddComponent<BoxCollider>();
            var renderers = piece.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return box;

            Bounds world = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) world.Encapsulate(renderers[i].bounds);
            Vector3 scale = piece.transform.lossyScale;
            box.center = piece.transform.InverseTransformPoint(world.center);
            box.size = new Vector3(
                world.size.x / Mathf.Max(Mathf.Abs(scale.x), 0.0001f),
                world.size.y / Mathf.Max(Mathf.Abs(scale.y), 0.0001f),
                world.size.z / Mathf.Max(Mathf.Abs(scale.z), 0.0001f));
            return box;
        }

        // ── 스킨 메시 모델(휴머노이드 아트 모델) ───────────────

        static List<SkinnedMeshRenderer> FindSkinsUsing(GameObject model, Transform root)
        {
            var result = new List<SkinnedMeshRenderer>();
            foreach (var skin in model.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                if (skin.sharedMesh == null) continue;
                foreach (var bone in skin.bones)
                {
                    if (bone != null && (bone == root || bone.IsChildOf(root)))
                    {
                        result.Add(skin);
                        break;
                    }
                }
            }
            return result;
        }

        /// <summary>
        /// 스킨 메시에서 그 부위 뼈(와 아래 뼈)에 가장 많이 묶인 면만 지금 자세 그대로 잘라 새 조각을 만든다.
        /// 몸쪽은 뼈를 아주 작게 줄여 숨긴다(<see cref="collapsedBones"/>).
        /// </summary>
        void SpawnSkinnedPiece(BodyPart part, List<SkinnedMeshRenderer> skins, Transform root)
        {
            var piece = new GameObject($"{part} (잘린 부위)");
            piece.transform.SetPositionAndRotation(root.position, root.rotation);
            piece.transform.localScale = Vector3.one;

            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var submeshes = new List<List<int>>();
            var materials = new List<Material>();

            foreach (var skin in skins)
                AppendLimbTriangles(skin, root, piece.transform, vertices, normals, uvs, submeshes, materials);

            if (vertices.Count == 0)
            {
                Destroy(piece);
                return;
            }

            var mesh = new Mesh { name = piece.name };
            if (vertices.Count > 65535) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(vertices);
            if (normals.Count == vertices.Count) mesh.SetNormals(normals);
            if (uvs.Count == vertices.Count) mesh.SetUVs(0, uvs);
            mesh.subMeshCount = submeshes.Count;
            for (int i = 0; i < submeshes.Count; i++) mesh.SetTriangles(submeshes[i], i);
            mesh.RecalculateBounds();
            if (normals.Count != vertices.Count) mesh.RecalculateNormals();

            piece.AddComponent<MeshFilter>().sharedMesh = mesh;
            piece.AddComponent<MeshRenderer>().sharedMaterials = materials.ToArray();
            var collider = piece.AddComponent<BoxCollider>(); // 메시 범위에 자동으로 맞춰진다
            var rigidbody = piece.AddComponent<Rigidbody>();
            rigidbody.mass = Settings.LimbMass;
            createdMeshes.Add((piece, mesh));

            Launch(piece, new Collider[] { collider }, new[] { rigidbody });
        }

        static void AppendLimbTriangles(SkinnedMeshRenderer skin, Transform root, Transform piece,
            List<Vector3> vertices, List<Vector3> normals, List<Vector2> uvs, List<List<int>> submeshes, List<Material> materials)
        {
            var source = skin.sharedMesh;
            var weights = source.boneWeights;
            if (weights.Length != source.vertexCount) return;

            var bones = skin.bones;
            var inLimb = new bool[bones.Length];
            for (int i = 0; i < bones.Length; i++)
                inLimb[i] = bones[i] != null && (bones[i] == root || bones[i].IsChildOf(root));

            var baked = new Mesh();
            skin.BakeMesh(baked, false);
            var bakedVertices = baked.vertices;
            var bakedNormals = baked.normals;
            var sourceUvs = source.uv;
            Matrix4x4 toPiece = piece.worldToLocalMatrix * skin.transform.localToWorldMatrix;

            var sharedMaterials = skin.sharedMaterials;
            var remap = new Dictionary<int, int>();
            for (int sub = 0; sub < source.subMeshCount; sub++)
            {
                var triangles = source.GetTriangles(sub);
                List<int> output = null;
                for (int t = 0; t + 2 < triangles.Length; t += 3)
                {
                    int a = triangles[t], b = triangles[t + 1], c = triangles[t + 2];
                    if (!InLimb(weights[a], inLimb) || !InLimb(weights[b], inLimb) || !InLimb(weights[c], inLimb)) continue;
                    if (output == null)
                    {
                        output = new List<int>();
                        submeshes.Add(output);
                        materials.Add(sharedMaterials.Length > 0 ? sharedMaterials[Mathf.Min(sub, sharedMaterials.Length - 1)] : null);
                    }
                    output.Add(Remap(a));
                    output.Add(Remap(b));
                    output.Add(Remap(c));
                }
            }
            Object.Destroy(baked);

            int Remap(int index)
            {
                if (remap.TryGetValue(index, out int mapped)) return mapped;
                mapped = vertices.Count;
                vertices.Add(toPiece.MultiplyPoint3x4(bakedVertices[index]));
                if (bakedNormals.Length == bakedVertices.Length) normals.Add(toPiece.MultiplyVector(bakedNormals[index]).normalized);
                if (sourceUvs.Length == bakedVertices.Length) uvs.Add(sourceUvs[index]);
                remap[index] = mapped;
                return mapped;
            }
        }

        /// <summary>가장 많이 묶인 뼈가 그 부위 뼈인지.</summary>
        static bool InLimb(BoneWeight weight, bool[] inLimb)
        {
            int bone = weight.boneIndex0;
            float best = weight.weight0;
            if (weight.weight1 > best) { best = weight.weight1; bone = weight.boneIndex1; }
            if (weight.weight2 > best) { best = weight.weight2; bone = weight.boneIndex2; }
            if (weight.weight3 > best) bone = weight.boneIndex3;
            return bone >= 0 && bone < inLimb.Length && inLimb[bone];
        }

        // ── 공통 ───────────────

        /// <summary>조각을 래그돌로 켜고 몸 바깥쪽·위로 날린다. 자기 몸통과는 부딪히지 않는다.</summary>
        void Launch(GameObject piece, Collider[] colliders, Rigidbody[] bodies)
        {
            foreach (var c in colliders)
            {
                c.enabled = true;
                if (body != null) Physics.IgnoreCollision(c, body);
            }

            var settings = Settings;
            Vector3 outward = piece.transform.position - transform.position;
            outward.y = 0f;
            outward = outward.sqrMagnitude > 0.0001f ? outward.normalized : transform.right;
            Vector3 carry = body != null ? body.velocity : Vector3.zero;
            Vector3 velocity = carry + outward * settings.LimbFlingSpeed + Vector3.up * settings.LimbFlingUpSpeed;

            foreach (var rb in bodies)
            {
                rb.isKinematic = false;
                rb.linearVelocity = velocity;
                rb.angularVelocity = Random.onUnitSphere * settings.LimbSpin;
            }

            pieces.Add(piece);
            if (settings.LimbLifetime > 0f) Destroy(piece, settings.LimbLifetime);
        }

        /// <summary>조각이 사라졌으면 코드로 만든 메시도 지운다.</summary>
        void ReleaseMeshes(bool all)
        {
            for (int i = createdMeshes.Count - 1; i >= 0; i--)
            {
                var (piece, mesh) = createdMeshes[i];
                if (!all && piece != null) continue;
                if (mesh != null) Destroy(mesh);
                createdMeshes.RemoveAt(i);
            }
        }
    }
}
