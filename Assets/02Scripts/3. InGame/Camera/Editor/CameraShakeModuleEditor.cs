using UnityEditor;
using UnityEngine;

namespace alpha.ingame.camera.editor
{
    /// <summary>플레이 모드에서 타입별 카메라 흔들림을 직접 시험한다.</summary>
    [CustomEditor(typeof(CameraShakeModule))]
    public sealed class CameraShakeModuleEditor : Editor
    {
        // 인스펙터에서 재생할 쉐이크 타입을 표시 순서대로 나열한다.
        private static readonly ECameraShakeType[] TestTypes =
        {
            ECameraShakeType.LightHit,
            ECameraShakeType.HeavyHit,
            ECameraShakeType.StrongHit,
            ECameraShakeType.CriticalHit,
            ECameraShakeType.Recoil,
            ECameraShakeType.Explosion,
            ECameraShakeType.GroundImpact
        };

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Shake Test", EditorStyles.boldLabel);
            // 현재 선택된 씬의 쉐이크 모듈이다.
            CameraShakeModule shake = (CameraShakeModule)target;
            using (new EditorGUI.DisabledScope(!Application.isPlaying || !shake.isActiveAndEnabled))
            {
                foreach (ECameraShakeType type in TestTypes)
                    if (GUILayout.Button($"Play {type}")) shake.Play(type);
            }

            if (!Application.isPlaying)
                EditorGUILayout.HelpBox("플레이 모드에서 버튼을 사용할 수 있습니다.", MessageType.Info);
            else
                EditorGUILayout.HelpBox("월드 충격도 거리 감쇠 없이 최대 강도로 재생합니다.", MessageType.Info);
        }
    }
}
