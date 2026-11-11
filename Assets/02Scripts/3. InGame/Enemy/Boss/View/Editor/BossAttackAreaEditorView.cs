using UnityEditor;
using UnityEngine;

namespace alpha.ingame.enemy.boss.editor
{
    [InitializeOnLoad]
    [CustomEditor(typeof(BossAttackAreaModule))]
    public sealed class BossAttackAreaEditorView : Editor
    {
        static BossAttackAreaEditorView()
        {
            // 비활성 GameObject에는 OnDrawGizmos가 호출되지 않아 SceneView에서 직접 그린다.
            SceneView.duringSceneGui += DrawSceneAreas;
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            Draw("_targetLayers", "Target Layers");
            SerializedProperty shape = serializedObject.FindProperty("_shape");
            EditorGUILayout.PropertyField(shape, new GUIContent("Area Shape"));
            Draw("_localOffset", "Local Offset");
            Draw("_yaw", "Yaw");
            if (shape.enumValueIndex == (int)EBossAttackAreaShape.Box)
            {
                Draw("_width", "Width");
                Draw("_length", "Length");
            }
            else
            {
                Draw("_radius", "Radius");
                Draw("_angle", "Angle (360 = Circle)");
            }
            Draw("_height", "Height");
            Draw("_showArea", "Show Area");
            if (serializedObject.FindProperty("_showArea").boolValue)
                Draw("_areaColor", "Area Color");
            if (serializedObject.ApplyModifiedProperties()) SceneView.RepaintAll();
        }

        private void Draw(string propertyName, string label) =>
            EditorGUILayout.PropertyField(serializedObject.FindProperty(propertyName),
                new GUIContent(label));

        private static void DrawSceneAreas(SceneView sceneView)
        {
            if (Event.current.type != EventType.Repaint) return;
            foreach (BossAttackAreaModule area in Resources.FindObjectsOfTypeAll<BossAttackAreaModule>())
            {
                if (area == null || EditorUtility.IsPersistent(area) ||
                    !area.gameObject.scene.IsValid() || !area.gameObject.scene.isLoaded)
                    continue;
                DrawAreaPreview(area);
            }
        }

        // Inspector에서 수정한 판정 설정을 읽어 Scene 뷰에만 표현한다.
        private static void DrawAreaPreview(BossAttackAreaModule area)
        {
            if (!area.ShowArea || !area.IsValid) return;
            Transform source = Application.isPlaying ? area.Source :
                area.GetComponentInParent<BossRoot>(true)?.transform ?? area.transform;
            area.GetAreaPose(source, out Vector3 center, out Quaternion rotation);

            Color previousColor = Handles.color;
            Matrix4x4 previousMatrix = Handles.matrix;
            UnityEngine.Rendering.CompareFunction previousDepthTest = Handles.zTest;
            Handles.color = area.AreaColor;
            Handles.matrix = Matrix4x4.identity;
            Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;
            if (area.Shape == EBossAttackAreaShape.Box)
            {
                Handles.matrix = Matrix4x4.TRS(center + rotation * Vector3.forward *
                    (area.Length * 0.5f), rotation, Vector3.one);
                Handles.DrawWireCube(Vector3.zero,
                    new Vector3(area.Width, area.Height, area.Length));
            }
            else
            {
                Vector3 forward = rotation * Vector3.forward;
                Vector3 top = center + Vector3.up * area.Height * 0.5f;
                Vector3 bottom = center - Vector3.up * area.Height * 0.5f;
                if (area.Angle >= 360f)
                {
                    Handles.DrawWireDisc(top, Vector3.up, area.Radius);
                    Handles.DrawWireDisc(bottom, Vector3.up, area.Radius);
                }
                else
                {
                    Vector3 left = Quaternion.AngleAxis(-area.Angle * 0.5f, Vector3.up) * forward;
                    Vector3 right = Quaternion.AngleAxis(area.Angle * 0.5f, Vector3.up) * forward;
                    Handles.DrawWireArc(top, Vector3.up, left, area.Angle, area.Radius);
                    Handles.DrawWireArc(bottom, Vector3.up, left, area.Angle, area.Radius);
                    Handles.DrawLine(top, top + left * area.Radius);
                    Handles.DrawLine(top, top + right * area.Radius);
                    Handles.DrawLine(bottom, bottom + left * area.Radius);
                    Handles.DrawLine(bottom, bottom + right * area.Radius);
                    Handles.DrawLine(top, bottom);
                    Handles.DrawLine(top + left * area.Radius, bottom + left * area.Radius);
                    Handles.DrawLine(top + right * area.Radius, bottom + right * area.Radius);
                }
            }
            Handles.matrix = previousMatrix;
            Handles.Label(center + Vector3.up * area.Height * 0.5f, $"{area.name} Hit");
            Handles.color = previousColor;
            Handles.zTest = previousDepthTest;
        }
    }
}
