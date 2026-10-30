using System.Collections.Generic;
using UnityEngine;

namespace CurtainCall.WaitingRoom
{
    /// <summary>
    /// 구조 A의 확정된 열쇠 경로. 참조만 연결하며 오브젝트/좌표/아이템/텍스트를 생성하지 않는다.
    /// 미지정 참조는 건너뛰어 구역별로 연결할 수 있다. 모든 화면에 같은 참조를 지정한다.
    /// </summary>
    public sealed class StructureAKeyRoute : MonoBehaviour
    {
        public const string Key1 = "structure-a:key-1";
        public const string SteelBox = "structure-a:steel-box";
        public const string ManagementKey = "structure-a:management-key";
        public const string LockedBox1 = "structure-a:locked-box-1";
        public const string StairsKey = "structure-a:stairs-key";
        public const string StairsDoor = "structure-a:stairs-door";
        public const string Cabinet3 = "structure-a:cabinet-3";
        public const string ManagementDrawer = "structure-a:management-drawer";
        public const string EscapeKey1 = "structure-a:escape-key-1";
        public const string EscapeKey2 = "structure-a:escape-key-2";
        public const string EscapeDoor = "structure-a:escape-door";

        [SerializeField] ExplorationGate key1, steelBox, managementKey;
        [SerializeField] ToolTarget lockedBox1;
        [SerializeField] ExplorationGate stairsKey, stairsDoor;
        [SerializeField] ToolTarget cabinet3, managementDrawer;
        [SerializeField] ExplorationGate escapeKey1, escapeKey2, escapeDoor;

        void Awake()
        {
            if (!TryApply(out var error)) Debug.LogError("[Exploration] " + error, this);
        }

        /// <summary>중복 참조를 먼저 검사해 일부만 잘못 덮어쓰는 일을 막는다.</summary>
        public bool TryApply(out string error)
        {
            var unique = new HashSet<MonoBehaviour>();
            foreach (var target in new MonoBehaviour[] { key1, steelBox, managementKey, lockedBox1, stairsKey,
                stairsDoor, cabinet3, managementDrawer, escapeKey1, escapeKey2, escapeDoor })
                if (target != null && !unique.Add(target))
                { error = "서로 다른 경로 대상에 같은 컴포넌트가 지정되었습니다."; return false; }

            key1?.ConfigureRoute(Key1);
            steelBox?.ConfigureRoute(SteelBox, new[] { Key1 });
            managementKey?.ConfigureRoute(ManagementKey, new[] { SteelBox }, SteelBox);
            lockedBox1?.ConfigureRoute(LockedBox1);
            stairsKey?.ConfigureRoute(StairsKey, new[] { LockedBox1 }, LockedBox1);
            stairsDoor?.ConfigureRoute(StairsDoor, new[] { StairsKey });
            cabinet3?.ConfigureRoute(Cabinet3);
            managementDrawer?.ConfigureRoute(ManagementDrawer);
            escapeKey1?.ConfigureRoute(EscapeKey1, new[] { Cabinet3 }, Cabinet3);
            escapeKey2?.ConfigureRoute(EscapeKey2, new[] { ManagementDrawer }, ManagementDrawer);
            escapeDoor?.ConfigureRoute(EscapeDoor, new[] { EscapeKey1, EscapeKey2 });
            error = "";
            return true;
        }
    }
}
