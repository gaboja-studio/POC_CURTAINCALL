using System.Collections.Generic;
using UnityEngine;

namespace CurtainCall.Player
{
    /// <summary>
    /// 이 씬(공간)의 이동 규칙. 씬에 두고 규칙 에셋을 넣으면, 켜져 있는 동안 규칙을 직접 끼우지 않은 플레이어가 이 규칙으로 움직인다.
    /// 각 공간은 자기 폴더에 <see cref="PlayerControlScheme"/>을 상속한 규칙 에셋을 만들어 여기에 넣는다(공용 코드 수정 없음).
    /// 여러 개가 켜져 있으면 마지막에 켜진 것을 쓴다. 꺼지면 기본 이동 규칙(<see cref="ControlSettings"/>)으로 돌아간다.
    /// </summary>
    public sealed class SceneControlScheme : MonoBehaviour
    {
        [Tooltip("이 씬에서 쓸 이동 규칙. 비우면 기본 이동 규칙(조작 설정)을 쓴다.")]
        [SerializeField] PlayerControlScheme scheme;

        static readonly List<SceneControlScheme> active = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => active.Clear();

        /// <summary>지금 켜진 씬 규칙(없으면 null).</summary>
        public static PlayerControlScheme Current
        {
            get
            {
                for (int i = active.Count - 1; i >= 0; i--)
                    if (active[i] != null && active[i].scheme != null) return active[i].scheme;
                return null;
            }
        }

        /// <summary>이 컴포넌트의 규칙. 바꾸면 다음 프레임부터 반영된다.</summary>
        public PlayerControlScheme Scheme
        {
            get => scheme;
            set => scheme = value;
        }

        void OnEnable() => active.Add(this);
        void OnDisable() => active.Remove(this);
    }
}
