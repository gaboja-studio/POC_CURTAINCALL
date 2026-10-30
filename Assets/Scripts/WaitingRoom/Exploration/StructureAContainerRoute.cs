using System.Collections.Generic;
using UnityEngine;

namespace CurtainCall.WaitingRoom
{
    /// <summary>구조A 상자8개와 캐비넷/미정 상자의 연결. 배치·타격 수·드랍 내용은 생성하지 않는다.</summary>
    public sealed class StructureAContainerRoute : MonoBehaviour
    {
        [SerializeField] ExplorationGate dormitoryBox1, dormitoryBox2, pipeBox1;
        [SerializeField] ExplorationGate cargoBox1, cargoBox2, cargoBox3, loungeBox1, loungeBox2;
        [SerializeField] ToolTarget emptyCabinet, mapCabinet, lockedBox2;
        [SerializeField] ClueInspectable mapClue;
        public const string EmptyCabinet = "structure-a:cabinet-1";
        public const string MapCabinet = "structure-a:cabinet-2";
        public const string MapClue = "structure-a:map";
        public const string LockedBox2 = "structure-a:locked-box-2";
        static readonly string[] boxIds = { "structure-a:dormitory-box-1", "structure-a:dormitory-box-2", "structure-a:pipe-box-1",
            "structure-a:cargo-box-1", "structure-a:cargo-box-2", "structure-a:cargo-box-3", "structure-a:lounge-box-1", "structure-a:lounge-box-2" };
        void Awake() { if (!TryApply(out var error)) Debug.LogError("[Exploration] " + error, this); }
        public bool TryApply(out string error)
        {
            var boxes = new[] { dormitoryBox1, dormitoryBox2, pipeBox1, cargoBox1, cargoBox2, cargoBox3, loungeBox1, loungeBox2 };
            var unique = new HashSet<MonoBehaviour>();
            foreach (var target in new MonoBehaviour[] { dormitoryBox1, dormitoryBox2, pipeBox1, cargoBox1, cargoBox2,
                cargoBox3, loungeBox1, loungeBox2, emptyCabinet, mapCabinet, lockedBox2, mapClue })
                if (target != null && !unique.Add(target))
                { error = "서로 다른 상자/캐비넷에 같은 컴포넌트가 지정되었습니다."; return false; }
            foreach (var box in boxes)
                if (box != null && !box.IsBreakable)
                { error = "파괴 상자는 Breakable 대상으로 지정해야 합니다."; return false; }
            if (mapClue != null && (mapCabinet == null || mapCabinet.transform.IsChildOf(mapClue.transform)))
            { error = "지도 공개에 사용할 캐비넷 참조/계층을 확인하세요."; return false; }
            // 검사 후 적용: 기존 타격 수·도구·드랍 목록은 그대로 보존한다.
            for (int i = 0; i < boxes.Length; i++) if (boxes[i] != null) boxes[i].ConfigureRoute(boxIds[i]);
            if (emptyCabinet != null)
            {
                emptyCabinet.ConfigureRoute(EmptyCabinet);
                Drops(emptyCabinet).ConfigureEmpty();
            }
            if (mapCabinet != null)
            {
                mapCabinet.ConfigureRoute(MapCabinet);
                Drops(mapCabinet).ConfigureEmpty();
            }
            if (mapClue != null)
            {
                mapClue.ConfigureRoute(MapClue, MapCabinet);
                mapCabinet.RevealWhenOpened(mapClue.gameObject);
            }
            if (lockedBox2 != null)
            {
                lockedBox2.ConfigureRoute(LockedBox2);
                // 명시적인 기존 보상 설정은 존중하고, 미지정이면 미정 상태로 둔다.
                if (lockedBox2.GetComponent<ExplorationDrops>() == null) Drops(lockedBox2).ConfigurePending();
            }
            error = ""; return true;
        }
        static ExplorationDrops Drops(Component target) => target.GetComponent<ExplorationDrops>()
            ?? target.gameObject.AddComponent<ExplorationDrops>();
    }
}
