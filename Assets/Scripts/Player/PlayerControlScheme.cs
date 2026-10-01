using UnityEngine;

namespace CurtainCall.Player
{
    /// <summary>
    /// 조작 규칙(전략). 공통 역할 입력(<see cref="PlayerInputFrame"/>)이 이 콘텐츠에서 무슨 뜻인지 정하고 동작 부품을 부른다.
    /// 콘텐츠마다 규칙을 하나씩 만들고, <see cref="PlayerController.SetScheme"/>로 갈아 끼운다.
    /// 규칙 에셋은 여러 플레이어가 함께 쓰므로 플레이어별 상태는 <see cref="PlayerControlContext"/>에 둔다.
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
        }

        public GameObject Player { get; }
        public PlayerMover Mover { get; }
        public PlayerBalance Balance { get; }
    }
}
