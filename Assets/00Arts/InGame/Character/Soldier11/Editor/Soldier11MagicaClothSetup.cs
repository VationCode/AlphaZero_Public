using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MagicaCloth2;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Configures MagicaCloth2 on the Soldier11 instance in the active scene.
public static class Soldier11MagicaClothSetup
{
    private const string ModelName = "Soldier11_UnityReady";
    private const string HolderName = "Soldier11_MagicaCloth";
    private const string PresetPath = "Assets/MagicaCloth2/Res/Preset/MC2_Preset_";

    [MenuItem("Tools/Soldier11/Apply Hair and Skirt Cloth")]
    public static void Apply()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (EditorApplication.isPlaying || !scene.IsValid())
            throw new InvalidOperationException("Open the InGame scene in Edit mode first.");

        GameObject model = scene.GetRootGameObjects().Single(root => root.name == ModelName);
        Transform[] transforms = model.GetComponentsInChildren<Transform>(true);
        string[] shortHairNames = { "Bone_042", "Bone_047", "Bone_052", "Bone_057", "Bone_111" };
        string[] longHairNames = { "Bone_063" };
        string[] skirtNames = Enumerable.Range(0, 15).Select(index => $"Skirt_0_{index}").ToArray();
        string[] colliderBoneNames = {
            "Head", "LeftShoulder", "RightShoulder", "Hips",
            "LeftUpperLeg", "LeftLowerLeg", "RightUpperLeg", "RightLowerLeg",
        };
        var names = shortHairNames.Concat(longHairNames).Concat(skirtNames).Concat(colliderBoneNames);
        var bones = names.Distinct().ToDictionary(
            name => name,
            name => transforms.Single(item => item.name == name));
        foreach (string presetName in new[] { "ShortHair", "LongHair", "Skirt" })
            if (AssetDatabase.LoadAssetAtPath<TextAsset>(PresetPath + presetName + ".json") == null)
                throw new InvalidOperationException("Missing MagicaCloth2 preset: " + presetName);

        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Set up Soldier11 MagicaCloth2");
        Transform holder = FindOrCreateChild(model.transform, HolderName);

        // Colliders follow the imported rig while their child scale cancels its 100x bone scale.
        MagicaSphereCollider head = Sphere(bones["Head"], "MC2_Head", bones["Head"].position + Vector3.up * 0.07f, 0.105f);
        MagicaSphereCollider leftShoulder = Sphere(bones["LeftShoulder"], "MC2_LeftShoulder", bones["LeftShoulder"].position + new Vector3(-0.10f, -0.015f, 0), 0.065f);
        MagicaSphereCollider rightShoulder = Sphere(bones["RightShoulder"], "MC2_RightShoulder", bones["RightShoulder"].position + new Vector3(0.10f, -0.015f, 0), 0.065f);
        MagicaSphereCollider pelvis = Sphere(bones["Hips"], "MC2_Pelvis", bones["Hips"].position + Vector3.down * 0.02f, 0.115f);
        MagicaCapsuleCollider leftThigh = Thigh(bones["LeftUpperLeg"], bones["LeftLowerLeg"], "MC2_LeftThigh");
        MagicaCapsuleCollider rightThigh = Thigh(bones["RightUpperLeg"], bones["RightLowerLeg"], "MC2_RightThigh");

        // Short strands, long back strands and skirt need different physics presets.
        MagicaCloth front = Cloth(holder, scene, "Soldier11_HairShort_Cloth", "ShortHair",
            shortHairNames.Select(name => bones[name]), RenderSetupData.BoneConnectionMode.Line,
            head, leftShoulder, rightShoulder);
        MagicaCloth back = Cloth(holder, scene, "Soldier11_HairBack_Cloth", "LongHair",
            longHairNames.Select(name => bones[name]), RenderSetupData.BoneConnectionMode.Line,
            head, leftShoulder, rightShoulder);
        MagicaCloth skirt = Cloth(holder, scene, "Soldier11_Skirt_Cloth", "Skirt",
            skirtNames.Select(name => bones[name]), RenderSetupData.BoneConnectionMode.SequentialLoopMesh,
            pelvis, leftThigh, rightThigh);

        // Preserve the earlier draft, but stop an unfinished cloth from failing at runtime.
        MagicaCloth draft = model.transform.Find("Magica Cloth")?.GetComponent<MagicaCloth>();
        if (draft != null && draft.SerializeData.rootBones.Count == 0 && draft.enabled)
        {
            Undo.RecordObject(draft, "Disable unfinished Magica Cloth draft");
            draft.enabled = false;
            EditorUtility.SetDirty(draft);
        }

        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject = holder.gameObject;
        Debug.Log($"Soldier11 MagicaCloth2 ready: short hair {front.SerializeData.rootBones.Count} roots, " +
            $"back hair {back.SerializeData.rootBones.Count} root, skirt {skirt.SerializeData.rootBones.Count} roots, " +
            "6 colliders. Scene saved.");
    }

    private static Transform FindOrCreateChild(Transform parent, string name)
    {
        Transform child = parent.Find(name);
        if (child != null)
            return child;
        var gameObject = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(gameObject, "Create " + name);
        gameObject.transform.SetParent(parent, false);
        return gameObject.transform;
    }

    private static MagicaSphereCollider Sphere(Transform bone, string name, Vector3 center, float radius)
    {
        Transform anchor = bone.Find(name);
        if (anchor == null)
        {
            anchor = FindOrCreateChild(bone, name);
            anchor.position = center;
            anchor.rotation = Quaternion.identity;
            // FBX bones have a 100x scene scale; colliders use metre-sized dimensions.
            anchor.localScale = new Vector3(
                1f / bone.lossyScale.x, 1f / bone.lossyScale.y, 1f / bone.lossyScale.z);
        }
        MagicaSphereCollider collider = anchor.GetComponent<MagicaSphereCollider>();
        if (collider == null)
        {
            collider = Undo.AddComponent<MagicaSphereCollider>(anchor.gameObject);
            collider.SetSize(radius);
        }
        return collider;
    }

    private static MagicaCapsuleCollider Thigh(Transform upper, Transform lower, string name)
    {
        Transform anchor = upper.Find(name);
        if (anchor == null)
        {
            anchor = FindOrCreateChild(upper, name);
            Vector3 direction = lower.position - upper.position;
            anchor.position = (upper.position + lower.position) * 0.5f;
            anchor.rotation = Quaternion.FromToRotation(Vector3.up, direction.normalized);
            anchor.localScale = new Vector3(
                1f / upper.lossyScale.x, 1f / upper.lossyScale.y, 1f / upper.lossyScale.z);
        }
        MagicaCapsuleCollider collider = anchor.GetComponent<MagicaCapsuleCollider>();
        if (collider == null)
        {
            collider = Undo.AddComponent<MagicaCapsuleCollider>(anchor.gameObject);
            collider.direction = MagicaCapsuleCollider.Direction.Y;
            collider.alignedOnCenter = true;
            collider.SetSize(0.065f, 0.065f, Vector3.Distance(upper.position, lower.position) * 0.85f);
        }
        return collider;
    }

    private static MagicaCloth Cloth(Transform holder, Scene scene, string name, string preset,
        System.Collections.Generic.IEnumerable<Transform> roots,
        RenderSetupData.BoneConnectionMode connection,
        params ColliderComponent[] colliders)
    {
        Transform child = holder.Find(name);
        if (child == null && name == "Soldier11_HairShort_Cloth")
        {
            GameObject model = holder.parent.gameObject;
            GameObject unused = model.transform.Find("Magica Cloth")?.gameObject;
            if (!IsEmptyCloth(unused))
                unused = scene.GetRootGameObjects().FirstOrDefault(root =>
                    root.name == "Magica Cloth" && IsEmptyCloth(root));
            if (unused != null)
            {
                Undo.RecordObject(unused, "Reuse empty Magica Cloth");
                unused.name = name;
                Undo.SetTransformParent(unused.transform, holder, "Attach Soldier11 hair cloth");
                child = unused.transform;
            }
        }
        child = child != null ? child : FindOrCreateChild(holder, name);
        MagicaCloth component = child.GetComponent<MagicaCloth>();
        if (component == null)
            component = Undo.AddComponent<MagicaCloth>(child.gameObject);

        Undo.RecordObject(component, "Configure " + name);
        ClothSerializeData data = component.SerializeData;
        bool needsPreset = data.rootBones.Count == 0;
        data.clothType = ClothProcess.ClothType.BoneCloth;
        if (needsPreset)
        {
            TextAsset text = AssetDatabase.LoadAssetAtPath<TextAsset>(PresetPath + preset + ".json");
            if (!data.ImportJson(text.text))
                throw new InvalidOperationException("Cannot load MagicaCloth2 preset: " + preset);
        }
        data.rootBones.Clear();
        data.rootBones.AddRange(roots);
        data.connectionMode = connection;
        foreach (ColliderComponent collider in colliders)
            if (!data.colliderCollisionConstraint.colliderList.Contains(collider))
                data.colliderCollisionConstraint.colliderList.Add(collider);
        data.DataValidate();
        EditorUtility.SetDirty(component);
        return component;
    }

    private static bool IsEmptyCloth(GameObject candidate)
    {
        if (candidate == null || candidate.transform.childCount != 0 ||
            candidate.GetComponents<Component>().Length != 2)
            return false;
        MagicaCloth cloth = candidate.GetComponent<MagicaCloth>();
        return cloth != null && cloth.SerializeData.rootBones.Count == 0 &&
            cloth.SerializeData.sourceRenderers.Count == 0;
    }

    [MenuItem("Tools/Soldier11/Limit Back Hair to Tips")]
    public static void LimitBackHairToTips()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (EditorApplication.isPlaying || !scene.IsValid())
            throw new InvalidOperationException("Open the InGame scene in Edit mode first.");

        GameObject model = scene.GetRootGameObjects().Single(root => root.name == ModelName);
        MagicaCloth back = model.transform.Find(HolderName + "/Soldier11_HairBack_Cloth")
            ?.GetComponent<MagicaCloth>();
        if (back == null || back.SerializeData.rootBones.Count != 1)
            throw new InvalidOperationException("The Soldier11 back hair BoneCloth is missing.");

        using var setup = new RenderSetupData(null, RenderSetupData.SetupType.BoneCloth,
            back.ClothTransform, back.SerializeData.rootBones, null,
            RenderSetupData.BoneConnectionMode.Line, back.name);
        SelectionData selection = back.GetSerializeData2().selectionData.Clone();
        if (!selection.IsValid() || selection.Count != setup.skinBoneCount)
            throw new InvalidOperationException("Build the back hair BoneCloth before editing its bone attributes.");

        var boneSet = new HashSet<Transform>(setup.transformList.Take(setup.skinBoneCount));
        var remainingDepth = new Dictionary<Transform, int>();
        int ToLastBone(Transform bone)
        {
            if (remainingDepth.TryGetValue(bone, out int depth))
                return depth;
            depth = 0;
            foreach (Transform child in bone)
                if (boneSet.Contains(child))
                    depth = Mathf.Max(depth, 1 + ToLastBone(child));
            remainingDepth.Add(bone, depth);
            return depth;
        }
        bool IsMovingTip(Transform bone)
        {
            int depth = ToLastBone(bone);
            return depth == 1 || (depth == 0 && boneSet.Contains(bone.parent) &&
                ToLastBone(bone.parent) == 1);
        }

        // BoneCloth vertices follow the imported bone order; verify it before painting.
        int movingBones = 0;
        for (int i = 0; i < setup.skinBoneCount; i++)
        {
            Transform bone = setup.transformList[i];
            Vector3 local = back.transform.InverseTransformPoint(bone.position);
            var point = selection.positions[i];
            if (Vector3.Distance(local, new Vector3(point.x, point.y, point.z)) > 0.005f)
                throw new InvalidOperationException("Back hair bone and paint vertex order differ; no changes were saved.");
            if (IsMovingTip(bone))
                movingBones++;
        }
        if (movingBones == 0 || movingBones >= setup.skinBoneCount / 2)
            throw new InvalidOperationException($"Back hair tip selection {movingBones}/{setup.skinBoneCount} " +
                "needs inspection; no changes were saved.");

        Undo.RecordObject(back, "Keep Soldier11 back hair shape");
        for (int i = 0; i < setup.skinBoneCount; i++)
        {
            Transform bone = setup.transformList[i];
            selection.attributes[i] = IsMovingTip(bone)
                ? VertexAttribute.Move : VertexAttribute.Fixed;
        }
        selection.userEdit = true;
        ClothEditorManager.ApplySelectionData(back, selection);

        // Limit deflection while retaining the model's existing gravity and spring tuning.
        ClothSerializeData data = back.SerializeData;
        data.damping.SetValue(0.35f);
        data.inertiaConstraint.worldInertia = 0.5f;
        data.inertiaConstraint.localInertia = 0.5f;
        data.angleRestorationConstraint.velocityAttenuation = 0.9f;
        data.angleLimitConstraint.limitAngle.value = 25f;
        data.motionConstraint.useMaxDistance = true;
        data.motionConstraint.maxDistance.SetValue(0.03f);
        data.wind.influence = 0.05f;
        data.DataValidate();
        EditorUtility.SetDirty(back);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject = back.gameObject;
        Debug.Log($"Soldier11 back hair: {setup.skinBoneCount - movingBones} fixed bones, " +
            $"{movingBones} moving tip bones, 0.03 m travel limit. Scene saved.");
    }

    [MenuItem("Tools/Soldier11/Inspect Cloth Bones")]
    public static void Inspect()
    {
        GameObject model = SceneManager.GetActiveScene().GetRootGameObjects()
            .Single(root => root.name == ModelName);
        Transform[] bones = model.GetComponentsInChildren<Transform>(true);
        string[] names = {
            "Head", "LeftShoulder", "RightShoulder", "Hips", "Spine", "Chest",
            "LeftUpperLeg", "LeftLowerLeg", "RightUpperLeg", "RightLowerLeg",
            "Bone_042", "Bone_047", "Bone_052", "Bone_057", "Bone_063", "Bone_111",
            "Skirt_0_0", "Skirt_0_7", "Skirt_0_14", "Skirt_5_0", "Skirt_5_7", "Skirt_5_14",
        };
        var output = new StringBuilder("Soldier11 cloth bone inspection:\n");
        foreach (string name in names)
        {
            Transform bone = bones.SingleOrDefault(item => item.name == name);
            output.AppendLine(bone == null
                ? $"{name}: MISSING"
                : $"{name}: {bone.position:F4} scale={bone.lossyScale:F4} parent={bone.parent.name}");
        }
        foreach (SkinnedMeshRenderer renderer in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            output.AppendLine($"{renderer.name} bounds: center={renderer.bounds.center:F4} extents={renderer.bounds.extents:F4}");
        Debug.Log(output.ToString());
    }
}
