using UnityEditor;
using UnityEngine;

namespace alpha.ingame.player
{
    [CustomEditor(typeof(PlayerHitModule))]
    public sealed class PlayerHitModuleEditor : Editor
    {
        public override bool RequiresConstantRepaint() => Application.isPlaying;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.LabelField("Normal Hit", EditorStyles.boldLabel);
            DrawTiming("_normal", "Reaction Duration");
            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Stagger Hit", EditorStyles.boldLabel);
            DrawTiming("_stagger", "Reaction Duration");
            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Knockback Hit", EditorStyles.boldLabel);
            DrawTiming("_knockback", "Reaction Duration");
            DrawField("_knockbackDistance", "Move Distance");
            DrawField("_knockbackMoveDuration", "Move Duration");
            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Knockdown Hit", EditorStyles.boldLabel);
            DrawTiming("_knockdown", "Reaction Duration");
            DrawField("_knockdownDistance", "Move Distance");
            DrawField("_knockdownMoveDuration", "Move Duration");
            DrawField("_knockdownLyingDuration", "Lying Duration");
            DrawField("_standUpDuration", "Stand Up Duration");
            DrawField("_maximumKnockbackSpeed", "Maximum Knockback Speed");

            serializedObject.ApplyModifiedProperties();

            if (!Application.isPlaying) return;

            PlayerHitModule hitModule = (PlayerHitModule)target;
            PlayerRoot core = hitModule.GetComponentInParent<PlayerRoot>(true);
            PlayerHealthModule health = core?.GetComponentInChildren<PlayerHealthModule>(true);
            PlayerActionStateFlow action = core?.GetComponentInChildren<PlayerActionStateFlow>(true);
            if (health != null)
                EditorGUILayout.LabelField("Current Health", $"{health.CurrentHealth:0.##} / {health.MaxHealth:0.##}");
            if (action != null)
                EditorGUILayout.LabelField("Hit Phase", action.CurrentPhase.ToString());
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Trigger Test Hit", EditorStyles.boldLabel);
            DrawHitButton(hitModule, "Normal Hit", EPlayerHitType.Normal);
            DrawHitButton(hitModule, "Stagger Hit", EPlayerHitType.Stagger);
            DrawHitButton(hitModule, "Knockback Hit", EPlayerHitType.Knockback);
            DrawHitButton(hitModule, "Knockdown Hit", EPlayerHitType.Knockdown);
        }

        private void DrawTiming(string propertyName, string reactionLabel)
        {
            SerializedProperty timing = serializedObject.FindProperty(propertyName);
            if (timing == null) return;
            DrawField(timing.FindPropertyRelative("_reactionDuration"), reactionLabel);
            DrawField(timing.FindPropertyRelative("_damageInvulnerabilityDuration"),
                "Damage Invulnerability Duration");
            DrawField(timing.FindPropertyRelative("_reactionImmunityDuration"),
                "Reaction Immunity Duration");
        }

        private void DrawField(string propertyName, string label) =>
            DrawField(serializedObject.FindProperty(propertyName), label);

        private static void DrawField(SerializedProperty property, string label)
        {
            if (property == null) return;
            EditorGUILayout.PropertyField(property, new GUIContent(label, property.tooltip));
        }

        private static void DrawHitButton(PlayerHitModule hitModule, string label,
            EPlayerHitType type)
        {
            if (!GUILayout.Button(label)) return;

            // 버튼을 누를 때 해당 타입의 공격을 한 번 전달한다.
            if (!hitModule.TryReceiveTestHit(type))
                Debug.Log($"{label} 거부: 사망 또는 피해 무적 상태를 확인하세요.", hitModule);
        }
    }
}
