using UnityEditor;
using UnityEngine;

namespace alpha.ingame.enemy.editor
{
    /// <summary>일반·치명타 피해량 텍스트를 플레이 모드에서 시험한다.</summary>
    [CustomEditor(typeof(EnemyHealthRevealView))]
    public sealed class EnemyHealthRevealViewEditor : Editor
    {
        // 미리보기에서 표시할 피해량과 색 구간 계산용 남은 체력이다.
        private float _damage = 15f;
        private float _currentHealth = 72f;
        private float _maxHealth = 100f;
        private bool _isCritical;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);
            _damage = Mathf.Max(0f, EditorGUILayout.FloatField("Damage", _damage));
            _currentHealth = EditorGUILayout.FloatField("Current Health", _currentHealth);
            _maxHealth = Mathf.Max(1f, EditorGUILayout.FloatField("Max Health", _maxHealth));
            _isCritical = EditorGUILayout.Toggle("Critical", _isCritical);

            using (new EditorGUI.DisabledScope(!Application.isPlaying))
            {
                if (!GUILayout.Button("Play in Front of Camera")) return;
                Camera camera = Camera.main;
                if (camera == null)
                {
                    Debug.LogWarning("피해량 텍스트 미리보기에 Main Camera가 필요합니다.");
                    return;
                }

                EnemyHealthRevealCanvasView canvas = EnemyHealthRevealCanvasView.Instance;
                if (canvas == null)
                {
                    Debug.LogWarning("피해량 텍스트 미리보기에 EnemyHealthRevealCanvasView가 필요합니다.");
                    return;
                }
                Vector3 point = camera.transform.position + camera.transform.forward * 10f;
                canvas.Preview(_damage, _currentHealth, _maxHealth, _isCritical, point);
            }
        }
    }
}
