using UnityEngine;

namespace alpha.ingame.enemy
{
    /// <summary>적의 피해 결과를 공유 Canvas의 피해 텍스트에 전달한다.</summary>
    [DisallowMultipleComponent]
    public sealed class EnemyHealthRevealSpawnerView : MonoBehaviour
    {
        // 체력 변경 이벤트를 제공하는 적의 Health다.
        private EnemyHealth _health;

        private void OnEnable()
        {
            _health = GetComponentInChildren<EnemyHealth>(true);
            if (_health != null) _health.Damaged += HandleDamaged;
        }

        private void OnDisable()
        {
            if (_health != null) _health.Damaged -= HandleDamaged;
            _health = null;
        }

        private void HandleDamaged(EnemyDamageResult result)
        {
            EnemyHealthRevealCanvasView.Instance?.Show(result);
        }
    }
}
