using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Creates editable Toon materials and remaps the FBX's embedded material slots.
public static class Soldier11ToonMaterials
{
    private const string ModelPath = "Assets/00Arts/Character/Soldier11/Soldier11_UnityReady.fbx";
    private const string RootPath = "Assets/00Arts/Character/Soldier11";
    private const string MaterialPath = RootPath + "/Materials";
    private const string TexturePath = RootPath + "/Textures/";

    private static readonly Dictionary<string, string> BaseTextures = new Dictionary<string, string>
    {
        { "Soldier11_Face", "Face.png" },
        { "Soldier11_FaceOverlay", "Face.png" },
        { "Soldier11_Eyelashes", "Face.png" },
        { "Soldier11_Eyebrows", "Face.png" },
        { "Soldier11_EyelidCrease", "Face.png" },
        { "Soldier11_MouthLine", "Face.png" },
        { "Soldier11_EyeWhite", "Face.png" },
        { "Soldier11_Iris", "Face.png" },
        { "Soldier11_Tongue", "Face.png" },
        { "Soldier11_MouthInterior", "Face.png" },
        { "Soldier11_Teeth", "Face.png" },
        { "Soldier11_EyeShadow", "Face.png" },
        { "Soldier11_Outfit", "Outfit.png" },
        { "Soldier11_Boots", "Outfit.png" },
        { "Soldier11_BackAccessory", "Outfit.png" },
        { "Soldier11_Skin", "Outfit.png" },
        { "Soldier11_SkinDetail", "Skin.png" },
        { "Soldier11_Hair", "Hair.png" },
        { "Soldier11_HairOverlay", "SharedDetails.png" },
        { "Soldier11_Weapon", "Weapon.png" },
        { "Soldier11_Blade", "Weapon.png" },
        { "Soldier11_Goggles", "Outfit.png" },
    };

    private static readonly HashSet<string> CutoutMaterials = new HashSet<string>
    {
        "Soldier11_FaceOverlay", "Soldier11_Eyelashes", "Soldier11_Eyebrows",
        "Soldier11_EyelidCrease", "Soldier11_MouthLine",
    };

    private static readonly HashSet<string> TransparentMaterials = new HashSet<string>
    {
        "Soldier11_EyeShadow", "Soldier11_SkinDetail", "Soldier11_HairOverlay",
        "Soldier11_Goggles",
    };

    [MenuItem("Tools/Soldier11/Apply Toon Materials")]
    public static void Apply()
    {
        Shader toonShader = Shader.Find("Toon/Toon");
        if (toonShader == null)
            throw new InvalidOperationException("Unity Toon Shader (Toon/Toon) is unavailable.");

        ModelImporter importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
        if (importer == null)
            throw new InvalidOperationException("Soldier11 FBX importer was not found.");

        Material[] sources = AssetDatabase.LoadAllAssetRepresentationsAtPath(ModelPath)
            .OfType<Material>().ToArray();
        string[] missing = BaseTextures.Keys.Except(sources.Select(material => material.name)).ToArray();
        string[] unknown = sources.Select(material => material.name).Except(BaseTextures.Keys).ToArray();
        if (sources.Length != BaseTextures.Count || missing.Length != 0 || unknown.Length != 0)
            throw new InvalidOperationException(
                $"FBX material slots differ from the expected 22. Missing: {string.Join(", ", missing)}; unknown: {string.Join(", ", unknown)}");

        if (!AssetDatabase.IsValidFolder(MaterialPath))
            AssetDatabase.CreateFolder(RootPath, "Materials");

        // Match each FBX material name to its own editable Unity material.
        foreach (Material source in sources)
        {
            string textureFile = BaseTextures[source.name];
            Texture2D baseTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath + textureFile);
            if (baseTexture == null)
                throw new InvalidOperationException("Missing Soldier11 texture: " + textureFile);

            string path = MaterialPath + "/" + source.name + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(toonShader) { name = source.name };
                AssetDatabase.CreateAsset(material, path);
            }

            Configure(material, toonShader, baseTexture, source.name);
            EditorUtility.SetDirty(material);
            importer.AddRemap(new AssetImporter.SourceAssetIdentifier(source), material);
        }

        AssetDatabase.SaveAssets();
        importer.SaveAndReimport();

        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        SkinnedMeshRenderer[] renderers = model != null
            ? model.GetComponentsInChildren<SkinnedMeshRenderer>(true)
            : Array.Empty<SkinnedMeshRenderer>();
        if (renderers.Length != 7 || renderers.SelectMany(renderer => renderer.sharedMaterials)
                .Any(material => material == null || material.shader != toonShader))
            throw new InvalidOperationException("Soldier11 Toon remap did not reach all seven skinned meshes.");

        Debug.Log($"Soldier11 Toon setup complete: {sources.Length} materials, {renderers.Length} skinned meshes.");
    }

    private static void Configure(Material material, Shader shader, Texture2D texture, string name)
    {
        material.shader = shader;
        material.SetTexture("_MainTex", texture);
        material.SetTexture("_BaseMap", texture);
        material.SetTexture("_1st_ShadeMap", texture);
        material.SetColor("_BaseColor", Color.white);
        material.SetColor("_1st_ShadeColor", new Color(0.65f, 0.65f, 0.65f, 1f));
        material.SetFloat("_BaseColor_Step", 0.55f);
        material.SetFloat("_BaseShade_Feather", 0.08f);
        material.SetFloat("_isUnityToonshader", 1f);
        material.SetFloat("_OUTLINE", 0f);
        material.SetFloat("_Outline_Width", 0f);
        material.EnableKeyword("_EMISSIVE_SIMPLE");
        material.EnableKeyword("_IS_ANGELRING_OFF");
        material.EnableKeyword("_OUTLINE_NML");

        bool transparent = TransparentMaterials.Contains(name);
        bool cutout = !transparent && CutoutMaterials.Contains(name);
        material.SetFloat("_TransparentEnabled", transparent ? 1f : 0f);
        material.SetFloat("_ClippingMode", transparent ? 2f : cutout ? 1f : 0f);
        material.SetFloat("_IsBaseMapAlphaAsClippingMask", transparent || cutout ? 1f : 0f);
        // The goggle lens uses the shared outfit texture; lower only its own opacity.
        if (name == "Soldier11_Goggles")
            material.SetFloat("_Tweak_transparency", -0.45f);
        material.SetFloat("_Clipping_Level", cutout ? 0.5f : 0f);
        material.SetFloat("_ZWriteMode", transparent ? 0f : 1f);
        material.SetFloat("_ZOverDrawMode", transparent ? 1f : 0f);

        material.DisableKeyword("_IS_CLIPPING_OFF");
        material.DisableKeyword("_IS_CLIPPING_MODE");
        material.DisableKeyword("_IS_CLIPPING_TRANSMODE");
        material.DisableKeyword("_IS_OUTLINE_CLIPPING_NO");
        material.DisableKeyword("_IS_OUTLINE_CLIPPING_YES");
        material.EnableKeyword(transparent ? "_IS_CLIPPING_TRANSMODE" : cutout ? "_IS_CLIPPING_MODE" : "_IS_CLIPPING_OFF");
        material.EnableKeyword(transparent || cutout ? "_IS_OUTLINE_CLIPPING_YES" : "_IS_OUTLINE_CLIPPING_NO");

        material.renderQueue = transparent ? 3000 : cutout ? 2450 : -1;
        material.SetOverrideTag("RenderType", transparent ? "Transparent" : cutout ? "TransparentCutOut" : "Opaque");
        material.SetOverrideTag("IgnoreProjection", transparent ? "True" : "False");
    }
}
