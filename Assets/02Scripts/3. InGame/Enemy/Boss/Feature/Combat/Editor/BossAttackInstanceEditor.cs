using UnityEditor;
using UnityEngine;

namespace alpha.ingame.enemy.boss.editor
{
    [CustomEditor(typeof(BossAttackInstance))]
    public sealed class BossAttackInstanceEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            Draw("_detectionMode", "Detection Mode");
            SerializedProperty timingMode = serializedObject.FindProperty("_timingMode");
            EditorGUILayout.PropertyField(timingMode, new GUIContent("Timing Mode"));
            if (timingMode.enumValueIndex == (int)EBossAttackTimingMode.Duration)
            {
                Draw("_startTime", "Start Time");
                Draw("_duration", "Duration");
            }
            else
            {
                // 클립 이벤트가 시작과 종료를 전달하므로 시간 필드는 표시하지 않는다.
                EditorGUILayout.HelpBox(
                    "판정 시작과 종료는 애니메이션 클립 이벤트에서 전달됩니다. " +
                    "이벤트 연결은 공격을 사용하는 BossCombatFlow에서 설정합니다.",
                    MessageType.Info);
            }

            Draw("_hitMode", "Hit Mode");
            Draw("_hitInterval", "Hit Interval");
            Draw("_hit", "Hit");
            Draw("_shieldBlockable", "Shield Blockable");
            Draw("_endAction", "End Action");
            if (timingMode.enumValueIndex == (int)EBossAttackTimingMode.Duration)
                Draw("_endAfterParticles", "End After Particles");

            serializedObject.ApplyModifiedProperties();
        }

        private void Draw(string propertyName, string label) =>
            EditorGUILayout.PropertyField(serializedObject.FindProperty(propertyName),
                new GUIContent(label), true);
    }
}
