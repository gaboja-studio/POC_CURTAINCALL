using UnityEngine;

namespace CurtainCall.Player
{
    /// <summary>
    /// 조작 규칙(전략). 공통 역할 입력(<see cref="PlayerInputFrame"/>)이 이 콘텐츠에서 무슨 뜻인지 정하고 동작 부품을 부른다.
    /// 콘텐츠마다 규칙을 하나씩 만들고, <see cref="PlayerController.SetScheme"/>로 갈아 끼운다.
    /// 규칙 에셋은 여러 플레이어가 함께 쓰므로 플레이어별 상태는 <see cref="PlayerControlContext"/>에 둔다.
    /// 신체 부위 손실 같은 공통 상태의 디메리트도 콘텐츠마다 다르므로 각 규칙이 <see cref="PlayerControlContext.Condition"/>을 읽어 처리한다.
    /// </summary>
    public abstract class PlayerControlScheme : ScriptableObject
    {
        /// <summary>규칙이 적용될 때 한 번 부른다.</summary>
        public virtual void Enter(PlayerControlContext context) { }

        /// <summary>규칙이 빠질 때 한 번 부른다.</summary>
        public virtual void Exit(PlayerControlContext context) { }

        /// <summary>매 프레임 입력을 해석해 동작 부품에 전달한다.</summary>
        public abstract void Tick(PlayerControlContext context, in PlayerInputFrame input);
    }

    /// <summary>조작 규칙이 쓰는 플레이어의 동작 부품 묶음. 없는 부품은 null.</summary>
    public class PlayerControlContext
    {
        public PlayerControlContext(GameObject player)
        {
            Player = player;
            Mover = player.GetComponent<PlayerMover>();
            Balance = player.GetComponent<PlayerBalance>();
            Interaction = player.GetComponent<PlayerInteraction>();
            Condition = player.GetComponent<PlayerCondition>();
        }

        public GameObject Player { get; }
        public PlayerMover Mover { get; }
        public PlayerBalance Balance { get; }
        public PlayerInteraction Interaction { get; }

        /// <summary>신체 상태(잃은 부위). 콘텐츠별 조작 규칙이 읽어 자기 디메리트를 적용한다. 프리팹에 없으면 null.</summary>
        public PlayerCondition Condition { get; }

        /// <summary>조작 규칙이 지금 지정해 둔 방향(-1 왼쪽, 0 없음, +1 오른쪽). 외줄에서는 옆줄 이동 방향. 디버그 표시용으로도 읽는다.</summary>
        public int HeldDirection { get; set; }
    }
}
