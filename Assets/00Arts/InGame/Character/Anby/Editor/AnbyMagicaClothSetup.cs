using System;
using System.Collections.Generic;
using System.Linq;
using MagicaCloth2;
using Unity.Mathematics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Configures restrained hair-tip and skirt BoneCloth on Anby's FBX instance.
public static class AnbyMagicaClothSetup
{
    private const string Root = "Assets/00Arts/Character/Anby/";
    private const string ModelPath = Root + "Anby_UnityReady.fbx";
    private const string PrefabPath = Root + "Anby_MagicaCloth.prefab";
    private const string PresetPath = "Assets/MagicaCloth2/Res/Preset/MC2_Preset_";

    [MenuItem("Tools/Anby/Create MagicaCloth Prefab")]
    public static void CreatePrefab()
    {
        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        if (model == null)
            throw new InvalidOperationException("Anby FBX was not found: " + ModelPath);
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null)
            throw new InvalidOperationException("Anby cloth prefab already exists: " + PrefabPath);

        Scene preview = EditorSceneManager.NewPreviewScene();
        try
        {
            GameObject instance = PrefabUtility.InstantiatePrefab(model, preview) as GameObject;
            if (instance == null)
                throw new InvalidOperationException("Could not instantiate the Anby FBX.");
            instance.name = "Anby_MagicaCloth";
            Configure(instance);

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(instance, PrefabPath);
            if (saved == null)
                throw new InvalidOperationException("Could not save the Anby cloth prefab.");
            AssetDatabase.SaveAssets();
            Debug.Log("Anby MagicaCloth2 prefab saved: 2 side hair roots, 1 front hair root, " +
                      "16 skirt roots, 3 colliders. " + PrefabPath);
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(preview);
        }
    }

    [MenuItem("Tools/Anby/Apply MagicaCloth to InGame Anby")]
    public static void ApplyToScene()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (EditorApplication.isPlaying || !scene.IsValid() || !scene.path.EndsWith("/InGame.unity"))
            throw new InvalidOperationException("Open the InGame scene in Edit mode first.");
        GameObject instance = scene.GetRootGameObjects().Single(root => root.name == "Anby_UnityReady");
        Configure(instance);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("Anby MagicaCloth2 applied to the InGame scene: 2 side hair roots, " +
                  "1 front hair root, 16 skirt roots, 3 colliders. Scene saved.");
    }

    private static void Configure(GameObject instance)
    {
        if (instance.transform.Find("Anby_MagicaCloth_Setup") != null)
            throw new InvalidOperationException("Anby MagicaCloth2 is already configured.");

        Dictionary<string, Transform> bones = instance.GetComponentsInChildren<Transform>(true)
            .GroupBy(bone => bone.name)
            .ToDictionary(group => group.Key, group => group.Single());
        string[] sides = { "Bone_136", "Bone_141" };
        string[] front = { "Bone_147" };
        string[] skirt = Enumerable.Range(0, 16).Select(index => $"Skirt_0_{index}").ToArray();
        string[] needed = sides.Concat(front).Concat(skirt).Concat(new[] {
            "Hips", "LeftUpperLeg", "LeftLowerLeg",
            "RightUpperLeg", "RightLowerLeg",
        }).ToArray();
        foreach (string name in needed)
            if (!bones.ContainsKey(name))
                throw new InvalidOperationException("Anby cloth bone was not found: " + name);

        Transform holder = NewChild(instance.transform, "Anby_MagicaCloth_Setup");
        MagicaSphereCollider pelvis = Sphere(bones["Hips"], "MC2_Pelvis",
            bones["Hips"].position + Vector3.down * 0.02f, 0.09f);
        MagicaCapsuleCollider leftThigh = Thigh(bones["LeftUpperLeg"], bones["LeftLowerLeg"], "MC2_LeftThigh");
        MagicaCapsuleCollider rightThigh = Thigh(bones["RightUpperLeg"], bones["RightLowerLeg"], "MC2_RightThigh");

        MagicaCloth sideCloth = Cloth(holder, "Anby_HairSides_Cloth", "ShortHair",
            sides.Select(name => bones[name]), RenderSetupData.BoneConnectionMode.Line, true);
        MagicaCloth frontCloth = Cloth(holder, "Anby_HairFront_Cloth", "ShortHair",
            front.Select(name => bones[name]), RenderSetupData.BoneConnectionMode.Line, true);
        MagicaCloth skirtCloth = Cloth(holder, "Anby_Skirt_Cloth", "SoftSkirt",
            skirt.Select(name => bones[name]), RenderSetupData.BoneConnectionMode.SequentialLoopMesh,
            false, pelvis, leftThigh, rightThigh);

        // The first two joints stay fixed; only the lower strands may move.
        PaintTips(sideCloth, 1);
        PaintTips(frontCloth, 1);
        PaintTips(skirtCloth, 1);
    }

    private static Transform NewChild(Transform parent, string name)
    {
        var child = new GameObject(name).transform;
        child.SetParent(parent, false);
        return child;
    }

    private static MagicaSphereCollider Sphere(Transform bone, string name, Vector3 center, float radius)
    {
        Transform anchor = NewChild(bone, name);
        anchor.position = center;
        anchor.rotation = Quaternion.identity;
        anchor.localScale = InverseScale(bone.lossyScale);
        MagicaSphereCollider collider = anchor.gameObject.AddComponent<MagicaSphereCollider>();
        collider.SetSize(radius);
        return collider;
    }

    private static MagicaCapsuleCollider Thigh(Transform upper, Transform lower, string name)
    {
        Transform anchor = NewChild(upper, name);
        Vector3 direction = lower.position - upper.position;
        anchor.position = (upper.position + lower.position) * 0.5f;
        anchor.rotation = Quaternion.FromToRotation(Vector3.up, direction.normalized);
        anchor.localScale = InverseScale(upper.lossyScale);
        MagicaCapsuleCollider collider = anchor.gameObject.AddComponent<MagicaCapsuleCollider>();
        collider.direction = MagicaCapsuleCollider.Direction.Y;
        collider.alignedOnCenter = true;
        collider.SetSize(0.055f, 0.055f, direction.magnitude * 0.85f);
        return collider;
    }

    private static Vector3 InverseScale(Vector3 scale)
    {
        if (Mathf.Abs(scale.x * scale.y * scale.z) < 1e-10f)
            throw new InvalidOperationException("Anby bone has a zero scale.");
        return new Vector3(1f / scale.x, 1f / scale.y, 1f / scale.z);
    }

    private static MagicaCloth Cloth(Transform holder, string name, string preset,
        IEnumerable<Transform> roots, RenderSetupData.BoneConnectionMode connection,
        bool hair, params ColliderComponent[] colliders)
    {
        TextAsset presetAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(PresetPath + preset + ".json");
        if (presetAsset == null)
            throw new InvalidOperationException("Missing MagicaCloth2 preset: " + preset);

        MagicaCloth cloth = NewChild(holder, name).gameObject.AddComponent<MagicaCloth>();
        ClothSerializeData data = cloth.SerializeData;
        if (!data.ImportJson(presetAsset.text))
            throw new InvalidOperationException("Could not load MagicaCloth2 preset: " + preset);
        data.clothType = ClothProcess.ClothType.BoneCloth;
        data.rootBones.Clear();
        data.rootBones.AddRange(roots);
        data.connectionMode = connection;
        data.colliderCollisionConstraint.colliderList.Clear();
        data.colliderCollisionConstraint.colliderList.AddRange(colliders);

        data.damping.SetValue(hair ? 0.42f : 0.35f);
        data.gravity = hair ? 1.2f : 3f;
        data.inertiaConstraint.worldInertia = 0.5f;
        data.inertiaConstraint.localInertia = 0.5f;
        data.angleRestorationConstraint.velocityAttenuation = 0.9f;
        data.angleLimitConstraint.useAngleLimit = true;
        data.angleLimitConstraint.limitAngle.SetValue(hair ? 20f : 35f);
        data.motionConstraint.useMaxDistance = true;
        data.motionConstraint.maxDistance.SetValue(hair ? 0.025f : 0.06f);
        data.wind.influence = hair ? 0.02f : 0.05f;
        data.DataValidate();
        EditorUtility.SetDirty(cloth);
        return cloth;
    }

    private static void PaintTips(MagicaCloth cloth, int fixedDepth)
    {
        ClothSerializeData data = cloth.SerializeData;
        using var setup = new RenderSetupData(null, RenderSetupData.SetupType.BoneCloth,
            cloth.ClothTransform, data.rootBones, null, data.connectionMode, cloth.name);
        if (setup.skinBoneCount <= data.rootBones.Count)
            throw new InvalidOperationException("Anby cloth has no movable bones: " + cloth.name);

        var roots = new HashSet<Transform>(data.rootBones);
        var selection = new SelectionData(setup.skinBoneCount) { userEdit = true };
        float maximumLink = 0f;
        int moving = 0;
        for (int i = 0; i < setup.skinBoneCount; i++)
        {
            Transform bone = setup.transformList[i];
            Vector3 local = cloth.transform.InverseTransformPoint(bone.position);
            selection.positions[i] = new float3(local.x, local.y, local.z);
            int depth = 0;
            for (Transform current = bone; !roots.Contains(current); current = current.parent)
            {
                if (current == null)
                    throw new InvalidOperationException("Cloth bone is outside its root: " + bone.name);
                depth++;
            }
            bool movable = depth > fixedDepth;
            selection.attributes[i] = movable ? VertexAttribute.Move : VertexAttribute.Fixed;
            if (movable)
                moving++;
            int parent = setup.GetParentTransformIndex(i, true);
            if (parent >= 0)
                maximumLink = Mathf.Max(maximumLink,
                    Vector3.Distance(bone.position, setup.transformList[parent].position));
        }
        if (moving == 0)
            throw new InvalidOperationException("No moving cloth tips were selected: " + cloth.name);
        selection.maxConnectionDistance = maximumLink;
        ClothEditorManager.ApplySelectionData(cloth, selection);
    }
}
