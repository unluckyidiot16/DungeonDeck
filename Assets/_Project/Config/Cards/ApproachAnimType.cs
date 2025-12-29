// Assets/_Project/Config/Cards/ApproachAnimType.cs
namespace DungeonDeck.Config.Cards
{
    /// <summary>
    /// 플레이어가 적에게 접근할 때 사용할 애니메이션 타입.
    /// Animator의 Bool 파라미터와 매핑됨.
    /// </summary>
    public enum ApproachAnimType
    {
        /// <summary>
        /// 접근 없이 제자리에서 시전 (원거리 공격, 마법 등)
        /// Bool: 없음 (이동하지 않음)
        /// </summary>
        None = 0,
        
        /// <summary>
        /// 기본 달리기 (RunBegin → Running → RunEnd)
        /// Bool: "Run"
        /// </summary>
        Run = 1,
        
        /// <summary>
        /// 대시 접근 (빠른 돌진)
        /// Bool: "Dash"
        /// </summary>
        DashB = 2,
        
        /// <summary>
        /// 전력 질주 접근
        /// Bool: "Sprint"
        /// </summary>
        Sprint = 3,
        
        /// <summary>
        /// 슬라이드 접근 (낮은 자세)
        /// Bool: "Slide"
        /// </summary>
        Slide = 4,
        
        /// <summary>
        /// 블로킹 자세로 접근 (방어 자세 유지)
        /// Bool: "RunBlocking"
        /// </summary>
        RunBlocking = 5,
    }
}