using alpha.system;
using UnityEditor;
using UnityEngine;

namespace alpha.ingame.enemy.boss.editor
{
    /// <summary>한 패턴의 이펙트 이벤트와 사운드 재생 구간을 편집한다.</summary>
    [CustomEditor(typeof(BossAttackEffectView))]
    public sealed class BossAttackEffectViewEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_spawnOrigin"),
                new GUIContent("Spawn Origin"));
            SerializedProperty bindings = serializedObject.FindProperty("_bindings");
            for (int i = 0; i < bindings.arraySize; i++)
            {
                if (!DrawEffectBinding(bindings, i)) continue;
                serializedObject.ApplyModifiedProperties();
                return;
            }

            if (GUILayout.Button("Add Effect")) AddBinding(bindings);
            serializedObject.ApplyModifiedProperties();
        }

        private static bool DrawEffectBinding(SerializedProperty bindings, int index)
        {
            SerializedProperty binding = bindings.GetArrayElementAtIndex(index);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.PropertyField(binding.FindPropertyRelative("_eventKey"),
                        new GUIContent("Event Key"));
                    EditorGUILayout.PropertyField(binding.FindPropertyRelative("_mode"),
                        GUIContent.none, GUILayout.Width(72f));
                    if (GUILayout.Button("Remove", GUILayout.Width(60f)))
                    {
                        bindings.DeleteArrayElementAtIndex(index);
                        return true;
                    }
                }

                EditorGUILayout.PropertyField(binding.FindPropertyRelative("_effectObject"),
                    new GUIContent("Effect / Prefab"));
                EditorGUILayout.PropertyField(binding.FindPropertyRelative("_soundClip"),
                    new GUIContent("Sound Clip", "비워 두면 이 이벤트의 소리 재생을 건너뜁니다."));
                EditorGUILayout.PropertyField(binding.FindPropertyRelative("_soundSource"),
                    new GUIContent("Sound Source"));
                EditorGUILayout.PropertyField(binding.FindPropertyRelative("_soundVolume"),
                    new GUIContent("Sound Volume"));
                EditorGUILayout.PropertyField(binding.FindPropertyRelative("_soundStartDelay"),
                    new GUIContent("Start Delay (s)", "이펙트 시작 후 소리 재생까지의 시간"));
                EditorGUILayout.PropertyField(binding.FindPropertyRelative("_soundClipOffset"),
                    new GUIContent("Clip Offset (s)", "클립 앞부분을 건너뛸 시간"));
                EditorGUILayout.PropertyField(binding.FindPropertyRelative("_soundDuration"),
                    new GUIContent("Duration (s)", "0이면 클립의 남은 길이를 모두 재생"));
                EditorGUILayout.PropertyField(binding.FindPropertyRelative("_soundFadeOut"),
                    new GUIContent("Fade Out (s)", "재생 종료 직전 소리가 줄어드는 시간"));
                if (binding.FindPropertyRelative("_mode").enumValueIndex != 1) return false;

                SerializedProperty positionMode = binding.FindPropertyRelative("_spawnPositionMode");
                EditorGUILayout.PropertyField(positionMode,
                    new GUIContent("Spawn Position",
                        "Targeting: 타겟 위치에 1개, Current: 현재 위치에 1개, " +
                        "Specified: 지정한 위치마다 1개, RandomTargetArea: 타겟 주변에 여러 개"));
                if (positionMode.enumValueIndex == 2)
                    EditorGUILayout.PropertyField(binding.FindPropertyRelative("_spawnPoints"),
                        new GUIContent("Spawn Points"), true);
                if (!OwnsLifetime(binding))
                    EditorGUILayout.PropertyField(binding.FindPropertyRelative("_spawnLifetime"),
                        new GUIContent("Lifetime (s)"));
                EditorGUILayout.PropertyField(binding.FindPropertyRelative("_preserveInactiveChildren"),
                    new GUIContent("Preserve Inactive Children"));
                EditorGUILayout.PropertyField(binding.FindPropertyRelative("_randomYaw"),
                    new GUIContent("Random Y Rotation"));
            }
            return false;
        }

        private static void AddBinding(SerializedProperty bindings)
        {
            int index = bindings.arraySize;
            bindings.InsertArrayElementAtIndex(index);
            SerializedProperty binding = bindings.GetArrayElementAtIndex(index);
            binding.FindPropertyRelative("_eventKey").stringValue = string.Empty;
            binding.FindPropertyRelative("_effectObject").objectReferenceValue = null;
            binding.FindPropertyRelative("_mode").enumValueIndex = 0;
            binding.FindPropertyRelative("_spawnPositionMode").enumValueIndex = 0;
            binding.FindPropertyRelative("_spawnPoints").ClearArray();
            binding.FindPropertyRelative("_spawnLifetime").floatValue = 2f;
            binding.FindPropertyRelative("_preserveInactiveChildren").boolValue = false;
            binding.FindPropertyRelative("_randomYaw").boolValue = false;
            binding.FindPropertyRelative("_soundClip").objectReferenceValue = null;
            binding.FindPropertyRelative("_soundSource").objectReferenceValue = null;
            binding.FindPropertyRelative("_soundVolume").floatValue = 1f;
            binding.FindPropertyRelative("_soundStartDelay").floatValue = 0f;
            binding.FindPropertyRelative("_soundClipOffset").floatValue = 0f;
            binding.FindPropertyRelative("_soundDuration").floatValue = 0f;
            binding.FindPropertyRelative("_soundFadeOut").floatValue = 0f;
        }

        private static bool OwnsLifetime(SerializedProperty binding)
        {
            GameObject effect = binding.FindPropertyRelative("_effectObject").objectReferenceValue as GameObject;
            return effect != null &&
                   (effect.GetComponent<ObjectMove>() != null ||
                    effect.GetComponent<Projectile>() != null ||
                    effect.GetComponent<BossAttackInstance>() != null);
        }
    }
}
