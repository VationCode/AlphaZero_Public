using System;
using UnityEngine;

namespace alpha.ingame.enemy.boss
{
    /// <summary>한 공격의 선택 범위와 시전 시작 조건을 보관한다.</summary>
    [Serializable]
    public sealed class BossAttackCastConditions
    {
        [Tooltip("테스트 중 이 공격을 선택하고 시전할지 결정한다.")]
        [SerializeField] private bool _enabled = true;
        [SerializeField] private string _attackId;
        [Tooltip("보스 중심과 타깃 중심의 수평 거리가 이 값보다 가까우면 공격 후보에서 제외한다.")]
        [SerializeField, Min(0f)] private float _minSelectionDistance;
        [Tooltip("보스 중심과 타깃 중심의 수평 거리가 이 값보다 멀면 공격 후보에서 제외한다.")]
        [SerializeField, Min(0f)] private float _maxSelectionDistance;
        [Tooltip("보스 중심을 기준으로 선택 거리를 Scene 뷰에 표시한다.")]
        [SerializeField] private bool _showSelectionDistance;
        [SerializeField] private Color _selectionDistanceColor = new(1f, 1f, 1f, 0.8f);

        [Tooltip("보스 중심과 타깃 중심 사이에서 공격을 시작할 수 있는 최대 수평 거리다.")]
        [SerializeField, Min(0f)] private float _castDistance;
        [Tooltip("보스 중심을 기준으로 시전 거리를 Scene 뷰에 표시한다.")]
        [SerializeField] private bool _showCastDistance;
        [SerializeField] private Color _castDistanceColor = new(1f, 1f, 1f, 0.8f);

        [Tooltip("이 공격의 애니메이션과 이벤트를 반복할 캐스팅 횟수입니다.")]
        [SerializeField, Min(1)] private int _castCount = 1;
        [Tooltip("공격 종료 후 다시 후보가 되기까지 기다리는 시간이다.")]
        [SerializeField, Min(0f)] private float _cooldown;

        public bool IsEnabled => _enabled;
        public string AttackId => _attackId;
        public float MinSelectionDistance => _minSelectionDistance;
        public float MaxSelectionDistance => _maxSelectionDistance;
        public bool ShowSelectionDistance => _showSelectionDistance;
        public Color SelectionDistanceColor => _selectionDistanceColor;
        public float CastDistance => _castDistance;
        public bool ShowCastDistance => _showCastDistance;
        public Color CastDistanceColor => _castDistanceColor;
        public int CastCount => Mathf.Max(1, _castCount);
        public float Cooldown => _cooldown;

        public void Validate()
        {
            _minSelectionDistance = Mathf.Max(0f, _minSelectionDistance);
            _maxSelectionDistance = Mathf.Max(_minSelectionDistance, _maxSelectionDistance);
            _castDistance = Mathf.Clamp(_castDistance, 0f, _maxSelectionDistance);
            _castCount = Mathf.Max(1, _castCount);
            _cooldown = Mathf.Max(0f, _cooldown);
        }
    }
}
