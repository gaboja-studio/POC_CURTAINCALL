using System.Collections.Generic;
using UnityEngine;

namespace CurtainCall.WaitingRoom
{
    /// <summary>
    /// 탐색 발견품 자리(Task-079). 세대마다 한 번 이 자리 바닥에 기물을 놓는다(<see cref="ExplorationState"/>가 판정 화면에서).
    /// 놓인 뒤에는 공통 바닥 기물이라 F로 주워 슬롯에 든다. 탐색 발견품 내구도(<see cref="ItemCatalog.FoundUses"/>)로 시작한다.
    /// </summary>
    public sealed class FoundItemSpot : MonoBehaviour
    {
        static readonly List<FoundItemSpot> all = new();
        public static IReadOnlyList<FoundItemSpot> All => all;

        [Tooltip("자리 이름(세대 안에서 한 번만 놓기 위한 구분).")]
        [SerializeField] string spotId = "workshop-crowbar";

        [Tooltip("UID 구분 번호(1~99, 자리마다 다르게). 발견품 UID = 기준 + 세대×100 + 이 값.")]
        [SerializeField, Range(1, 99)] int uidOffset = 1;

        [SerializeField] string itemId = ItemUses.StakePuller;
        [SerializeField] string itemName = "천막 말뚝 뽑개";

        [Tooltip("이 대상이 열린 뒤에만 바닥 기물 생성. 비우면 기존처럼 처음부터 생성.")]
        [SerializeField] string availableAfter = "";
        [Tooltip("디버그 보급품: Editor 또는 Development Build에서만 생성.")]
        [SerializeField] bool debugOnly;
        public bool CanSeed => (!debugOnly || Application.isEditor || Debug.isDebugBuild)
            && (string.IsNullOrEmpty(availableAfter)
            || (ExplorationState.Instance != null && ExplorationState.Instance.IsOpen(availableAfter)));
        public void UnlockAfter(string targetId) => availableAfter = targetId ?? "";

        public string SpotId => spotId;
        public Vector3 FloorPoint => transform.position;

        // 탐색 발견품은 기물 목록의 발견품 내구도(초안: 새 제품보다 적다)로 시작한다.
        // 디버그 보급품은 내구도 차감을 여러 번 시험할 수 있게 새 제품 내구도(2026-10-30 사용자 결정).
        public PropItem MakeItem(int generation) =>
            new(ItemUses.FoundUidBase + generation * 100 + uidOffset, itemId, ItemCatalog.Get(itemId)?.displayName ?? itemName,
                debugOnly ? ItemCatalog.NewUses(itemId) : ItemCatalog.FoundUses(itemId));

        void OnEnable() { if (!all.Contains(this)) all.Add(this); }
        void OnDisable() => all.Remove(this);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => all.Clear();
    }
}
