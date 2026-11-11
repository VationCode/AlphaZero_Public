using UnityEngine;

namespace alpha.ingame.enemy
{
    public enum EEnemyKind
    {
        Normal,
        Boss
    }

    public enum EEnemyAttackStyle
    {
        None = 0,
        Melee = 1,
        Ranged = 2,
        ChargeMelee = 3
    }

    /// <summary>프리팹별 적 식별 정보와 일반 적의 공격 분류를 보관한다.</summary>
    public sealed class EnemyInfo : MonoBehaviour
    {
        [Header("Identity")]
        [SerializeField] private string _enemyId = "enemy-base";
        [SerializeField] private string _displayName = "Enemy Base";
        [SerializeField] private EEnemyKind _kind = EEnemyKind.Normal;

        [Header("Attack Style")]
        [SerializeField] private EEnemyAttackStyle _attackStyle = EEnemyAttackStyle.Melee;

        public string EnemyId => _enemyId;
        public string DisplayName => _displayName;
        public EEnemyKind Kind => _kind;
        // 보스는 여러 공격 패턴을 조합하므로 단일 공격 스타일을 사용하지 않는다.
        public EEnemyAttackStyle AttackStyle => _kind == EEnemyKind.Boss
            ? EEnemyAttackStyle.None : _attackStyle;
    }
}
