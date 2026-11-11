using UnityEditor;
using UnityEngine;

namespace alpha.ingame.enemy.boss.editor
{
    /// <summary>경로에 필요한 설정만 표시하고 Scene 뷰에서 곡선을 조정한다.</summary>
    [CustomEditor(typeof(Projectile))]
    public sealed class ProjectileEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.LabelField("Flight", EditorStyles.boldLabel);
            Draw("_pathType", "Path Type");
            Draw("_flightSpeed", "Flight Speed");
            Draw("_maxFlightDuration", "Max Flight Duration");

            if (IsCurve())
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Curve", EditorStyles.boldLabel);
                Draw("_curveStartControl", "Start Control");
                Draw("_curveEndControl", "End Control");
                Draw("_previewTargetOffset", "Preview Target Offset");
                EditorGUILayout.HelpBox(
                    "Scene 뷰에서 미리보기 목표점과 두 제어점을 움직여 경로를 조정합니다. " +
                    "로컬 +Z가 미사일의 발사 방향입니다. " +
                    "미리보기 목표점은 실제 발사 목표에는 영향을 주지 않습니다.",
                    MessageType.Info);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Impact", EditorStyles.boldLabel);
            Draw("_blastRadius", "Blast Radius");
            Draw("_impactLayers", "Impact Layers");
            Draw("_hit", "Hit");
            Draw("_explosionPrefab", "Explosion Prefab");
            Draw("_explosionLifetime", "Explosion Lifetime");
            serializedObject.ApplyModifiedProperties();
        }

        private void OnSceneGUI()
        {
            serializedObject.Update();
            if (!IsCurve()) return;

            Projectile projectile = (Projectile)target;
            Transform root = projectile.transform;
            SerializedProperty preview = serializedObject.FindProperty("_previewTargetOffset");
            SerializedProperty startControl = serializedObject.FindProperty("_curveStartControl");
            SerializedProperty endControl = serializedObject.FindProperty("_curveEndControl");
            Vector3 start = root.position;
            Quaternion launchRotation = root.rotation;
            Quaternion inverseRotation = Quaternion.Inverse(launchRotation);
            Vector3 end = start + launchRotation * preview.vector3Value;

            Handles.color = Color.yellow;
            EditorGUI.BeginChangeCheck();
            Vector3 movedEnd = Handles.PositionHandle(end, launchRotation);
            if (EditorGUI.EndChangeCheck())
            {
                preview.vector3Value = inverseRotation * (movedEnd - start);
                end = movedEnd;
            }

            Vector3 first = start + launchRotation * startControl.vector3Value;
            Vector3 second = end + launchRotation * endControl.vector3Value;

            Handles.DrawBezier(start, end, first, second, Color.cyan, null, 3f);
            Handles.color = Color.gray;
            Handles.DrawDottedLine(start, first, 4f);
            Handles.DrawDottedLine(second, end, 4f);
            Handles.Label(end, "Preview Target");

            Handles.color = Color.green;
            EditorGUI.BeginChangeCheck();
            Vector3 movedFirst = Handles.PositionHandle(first, launchRotation);
            if (EditorGUI.EndChangeCheck())
                startControl.vector3Value = inverseRotation * (movedFirst - start);
            Handles.Label(movedFirst, "Start Control");

            Handles.color = Color.magenta;
            EditorGUI.BeginChangeCheck();
            Vector3 movedSecond = Handles.PositionHandle(second, launchRotation);
            if (EditorGUI.EndChangeCheck())
                endControl.vector3Value = inverseRotation * (movedSecond - end);
            Handles.Label(movedSecond, "End Control");

            serializedObject.ApplyModifiedProperties();
        }

        private bool IsCurve() => serializedObject.FindProperty("_pathType")
            .enumValueIndex == (int)EProjectilePathType.Curve;

        private void Draw(string name, string label) =>
            EditorGUILayout.PropertyField(serializedObject.FindProperty(name),
                new GUIContent(label), true);

    }
}
