using System;
using System.Collections.Generic;
using System.Linq;
using MagicaCloth2;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 백업 프리팹의 MagicaCloth 설정 수치를 현재 캐릭터에 다시 적용한다.
/// 본/렌더러/콜라이더 참조와 빌드 데이터는 현재 프리팹의 것을 유지한다.
/// </summary>
public sealed class MagicaClothScaleBackup : EditorWindow
{
    private const string BackupFolder = "Assets/00Arts/InGame/Character/ClothScaleBackups/";
    private const float OriginalBoneScale = 100f;

    private float visibleSizeRatio = 1f;

    [MenuItem("Tools/Character/Restore MagicaCloth Settings")]
    private static void Open() => GetWindow<MagicaClothScaleBackup>("Cloth Settings");

    private void OnGUI()
    {
        EditorGUILayout.HelpBox(
            "Silyon/Herna 백업 프리팹의 클로스 수치를 복원합니다. 보이는 캐릭터 크기가 같으면 비율은 1입니다. " +
            "FBX 단위만 바꾼 경우 콜라이더의 기존 월드 크기도 보정합니다. 적용 후 클로스를 다시 빌드하세요.",
            MessageType.Info);
        visibleSizeRatio = EditorGUILayout.FloatField("New / old visible size", visibleSizeRatio);
        using (new EditorGUI.DisabledScope(visibleSizeRatio <= 0f))
        {
            if (GUILayout.Button("Restore Silyon settings"))
            {
                try { RestoreSilyon(visibleSizeRatio); }
                catch (Exception exception) { Debug.LogException(exception); }
            }
            if (GUILayout.Button("Restore Herna settings"))
            {
                try { RestoreHerna(visibleSizeRatio); }
                catch (Exception exception) { Debug.LogException(exception); }
            }
        }
    }

    public static void RestoreSilyon(float visibleSizeRatio)
    {
        if (visibleSizeRatio <= 0f) throw new ArgumentOutOfRangeException(nameof(visibleSizeRatio));
        RestoreOne(BackupFolder + "SilyonBaseline.prefab", "Assets/03Prefabs/InGame/Player/Silyon.prefab", visibleSizeRatio);
        AssetDatabase.SaveAssets();
        Debug.Log("Silyon MagicaCloth settings restored. Rebuild cloth data after changing the rig.");
    }

    public static void RestoreHerna(float visibleSizeRatio)
    {
        if (visibleSizeRatio <= 0f) throw new ArgumentOutOfRangeException(nameof(visibleSizeRatio));
        RestoreOne(BackupFolder + "HernaBaseline.prefab", "Assets/03Prefabs/InGame/Player/Herna.prefab", visibleSizeRatio);
        AssetDatabase.SaveAssets();
        Debug.Log("Herna MagicaCloth settings restored. Rebuild cloth data after changing the rig.");
    }

    private static void RestoreOne(string backupPath, string targetPath, float ratio)
    {
        GameObject backup = PrefabUtility.LoadPrefabContents(backupPath);
        if (backup == null) throw new InvalidOperationException("Could not load " + backupPath);
        GameObject target = null;
        try
        {
            target = PrefabUtility.LoadPrefabContents(targetPath);
            if (target == null) throw new InvalidOperationException("Could not load " + targetPath);
            MagicaCloth[] sourceCloths = backup.GetComponentsInChildren<MagicaCloth>(true);
            MagicaCloth[] targetCloths = target.GetComponentsInChildren<MagicaCloth>(true);
            ColliderComponent[] sourceColliders = backup.GetComponentsInChildren<ColliderComponent>(true);
            ColliderComponent[] targetColliders = target.GetComponentsInChildren<ColliderComponent>(true);
            if (sourceCloths.Length != targetCloths.Length || sourceColliders.Length != targetColliders.Length)
                throw new InvalidOperationException("Cloth/collider count changed in " + targetPath);

            // 먼저 모든 대응 컴포넌트를 확인한다. 누락된 부품이 있으면 저장하지 않는다.
            foreach (MagicaCloth source in sourceCloths) FindUnique(targetCloths, source);
            foreach (ColliderComponent source in sourceColliders) FindUnique(targetColliders, source);

            foreach (MagicaCloth source in sourceCloths)
            {
                MagicaCloth destination = FindUnique(targetCloths, source);
                CopyNumericSettings(source, destination, "serializeData.", ratio);
                EditorUtility.SetDirty(destination);
            }

            foreach (ColliderComponent source in sourceColliders)
            {
                ColliderComponent destination = FindUnique(targetColliders, source);
                CopyNumericSettings(source, destination, null, 1f);

                // 원래 본은 100배이고 콜라이더는 약 0.01배였다. 새 본 배율에 맞춰 역보정한다.
                Transform sourceTransform = source.transform;
                Transform destinationTransform = destination.transform;
                if (destinationTransform.parent == null)
                    throw new InvalidOperationException("Collider has no parent: " + source.name);
                Vector3 factor = Divide(Vector3.one * (OriginalBoneScale * ratio), destinationTransform.parent.lossyScale);
                destinationTransform.localPosition = Vector3.Scale(sourceTransform.localPosition, factor);
                destinationTransform.localRotation = sourceTransform.localRotation;
                destinationTransform.localScale = Vector3.Scale(sourceTransform.localScale, factor);
                EditorUtility.SetDirty(destinationTransform);
                EditorUtility.SetDirty(destination);
            }

            if (PrefabUtility.SaveAsPrefabAsset(target, targetPath) == null)
                throw new InvalidOperationException("Could not save " + targetPath);
        }
        finally
        {
            if (target != null) PrefabUtility.UnloadPrefabContents(target);
            PrefabUtility.UnloadPrefabContents(backup);
        }
    }

    private static T FindUnique<T>(IEnumerable<T> components, T source) where T : Component
    {
        T[] matches = components.Where(x => x.name == source.name && x.GetType() == source.GetType()).ToArray();
        if (matches.Length != 1)
            throw new InvalidOperationException("Missing or ambiguous MagicaCloth component: " + source.name);
        return matches[0];
    }

    private static void CopyNumericSettings(Component source, Component destination, string prefix, float ratio)
    {
        var sourceObject = new SerializedObject(source);
        var destinationObject = new SerializedObject(destination);
        SerializedProperty property = sourceObject.GetIterator();
        while (property.NextVisible(true))
        {
            if (prefix != null && !property.propertyPath.StartsWith(prefix, StringComparison.Ordinal)) continue;
            SerializedProperty targetProperty = destinationObject.FindProperty(property.propertyPath);
            if (targetProperty == null || targetProperty.propertyType != property.propertyType) continue;
            float scale = IsDistance(property.propertyPath) ? ratio : 1f;
            switch (property.propertyType)
            {
                case SerializedPropertyType.Integer: targetProperty.longValue = property.longValue; break;
                case SerializedPropertyType.Enum: targetProperty.intValue = property.intValue; break;
                case SerializedPropertyType.Boolean: targetProperty.boolValue = property.boolValue; break;
                case SerializedPropertyType.Float: targetProperty.floatValue = property.floatValue * scale; break;
                case SerializedPropertyType.Vector2: targetProperty.vector2Value = property.vector2Value * scale; break;
                case SerializedPropertyType.Vector3: targetProperty.vector3Value = property.vector3Value * scale; break;
                case SerializedPropertyType.Vector4: targetProperty.vector4Value = property.vector4Value * scale; break;
                case SerializedPropertyType.AnimationCurve: targetProperty.animationCurveValue = property.animationCurveValue; break;
            }
        }
        destinationObject.ApplyModifiedPropertiesWithoutUndo();
    }

    // 길이 단위만 보이는 크기 비율에 맞춘다. 강성, 감쇠, 마찰 등의 비율은 유지한다.
    private static bool IsDistance(string path)
    {
        switch (path)
        {
            case "serializeData.reductionSetting.simpleDistance":
            case "serializeData.reductionSetting.shapeDistance":
            case "serializeData.radius.value":
            case "serializeData.motionConstraint.maxDistance.value":
            case "serializeData.motionConstraint.backstopDistance.value":
            case "serializeData.colliderCollisionConstraint.limitDistance.value":
            case "serializeData.selfCollisionConstraint.surfaceThickness.value":
            case "serializeData.springConstraint.limitDistance":
                return true;
            default: return false;
        }
    }

    private static Vector3 Divide(Vector3 value, Vector3 divisor)
    {
        if (Mathf.Abs(divisor.x * divisor.y * divisor.z) < 1e-9f)
            throw new InvalidOperationException("Collider parent has a zero scale.");
        return new Vector3(value.x / divisor.x, value.y / divisor.y, value.z / divisor.z);
    }
}
