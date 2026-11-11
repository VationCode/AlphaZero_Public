using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Creates editable Toon materials for the character and its separate weapon FBX.
public static class AnbyToonMaterials
{
    private const string RootPath = "Assets/00Arts/Character/Anby";
    private const string CharacterPath = RootPath + "/Anby_UnityReady.fbx";
    private const string WeaponPath = RootPath + "/Anby_Weapon.fbx";
    private const string MaterialPath = RootPath + "/Materials";
    private const string TexturePath = RootPath + "/Textures/";

    private static readonly Dictionary<string, string> BaseTextures = new Dictionary<string, string>
    {
        { "Anby_Face", "Face.tga" },
        { "Anby_Mouth", "Face.tga" },
        { "Anby_Teeth", "Face.tga" },
        { "Anby_Eyebrows", "Face.tga" },
        { "Anby_Eyelashes", "Face.tga" },
        { "Anby_EyelidCrease", "Face.tga" },
        { "Anby_EyeWhite", "Face.tga" },
        { "Anby_Eyes", "Face.tga" },
        { "Anby_Iris", "Face.tga" },
        { "Anby_EyeHighlight", "Face.tga" },
        { "Anby_EyeHighlightSoft", "Face.tga" },
        { "Anby_Jacket", "Outfit.tga" },
        { "Anby_Hair", "Hair.tga" },
        { "Anby_Skin", "Outfit.tga" },
        { "Anby_Pants", "Outfit.tga" },
        { "Anby_ThighStrap", "Outfit.tga" },
        { "Anby_Gloves", "Outfit.tga" },
        { "Anby_Socks", "Outfit.tga" },
        { "Anby_Battery", "Outfit.tga" },
        { "Anby_Headphones", "Outfit.tga" },
        { "Anby_Shoes", "Outfit.tga" },
        { "Anby_Skirt", "Outfit.tga" },
        { "Anby_SkirtTrim", "Outfit.tga" },
        { "Anby_SleeveTrim", "Outfit.tga" },
        { "Anby_ClearCover", "Outfit.tga" },
        { "Anby_EyeShadow", null }, // The PMX points to a missing B.jpg.
        { "Anby_HairOverlay", "HairOverlay.png" },
        { "Anby_OutfitAccent", "Outfit.tga" },
        { "Anby_Weapon", "Weapon.tga" },
    };

    private static readonly HashSet<string> CutoutMaterials = new HashSet<string>
    {
        "Anby_Eyebrows", "Anby_Eyelashes", "Anby_EyelidCrease",
    };

    private static readonly HashSet<string> TransparentMaterials = new HashSet<string>
    {
        "Anby_EyeHighlightSoft", "Anby_EyeShadow", "Anby_HairOverlay",
        "Anby_ClearCover",
    };

    [MenuItem("Tools/Anby/Apply Toon Materials")]
    public static void Apply()
    {
        Shader toonShader = Shader.Find("Toon/Toon");
        if (toonShader == null)
            throw new InvalidOperationException("Unity Toon Shader (Toon/Toon) is unavailable.");

        if (!AssetDatabase.IsValidFolder(MaterialPath))
            AssetDatabase.CreateFolder(RootPath, "Materials");

        int characterMaterials = ApplyModel(CharacterPath, 28, 10, toonShader);
        int weaponMaterials = ApplyModel(WeaponPath, 1, 1, toonShader);
        AssetDatabase.SaveAssets();
        Debug.Log($"Anby Toon setup complete: {characterMaterials} character materials, " +
                  $"{weaponMaterials} weapon material; 10 character meshes and 1 weapon mesh.");
    }

    private static int ApplyModel(string path, int expectedMaterials, int expectedRenderers, Shader shader)
    {
        ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
        if (importer == null)
            throw new InvalidOperationException("Anby FBX importer was not found: " + path);

        Material[] sources = AssetDatabase.LoadAllAssetRepresentationsAtPath(path)
            .OfType<Material>().ToArray();
        string[] unknown = sources.Select(material => material.name).Except(BaseTextures.Keys).ToArray();
        if (sources.Length != expectedMaterials || unknown.Length != 0)
            throw new InvalidOperationException(
                $"Unexpected FBX material slots in {path}: {sources.Length} materials; " +
                $"unknown: {string.Join(", ", unknown)}");

        foreach (Material source in sources)
        {
            string textureFile = BaseTextures[source.name];
            Texture2D texture = textureFile == null
                ? Texture2D.whiteTexture
                : AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath + textureFile);
            if (texture == null)
                throw new InvalidOperationException("Missing Anby texture: " + textureFile);

            string materialFile = MaterialPath + "/" + source.name + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(materialFile);
            if (material == null)
            {
                material = new Material(shader) { name = source.name };
                AssetDatabase.CreateAsset(material, materialFile);
            }

            Configure(material, shader, texture, source.name);
            EditorUtility.SetDirty(material);
            importer.AddRemap(new AssetImporter.SourceAssetIdentifier(source), material);
        }

        AssetDatabase.SaveAssets();
        importer.SaveAndReimport();

        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        SkinnedMeshRenderer[] renderers = model != null
            ? model.GetComponentsInChildren<SkinnedMeshRenderer>(true)
            : Array.Empty<SkinnedMeshRenderer>();
        if (renderers.Length != expectedRenderers || renderers.SelectMany(renderer => renderer.sharedMaterials)
                .Any(material => material == null || material.shader != shader))
            throw new InvalidOperationException("Anby Toon remap did not reach every skinned mesh: " + path);

        return sources.Length;
    }

    private static void Configure(Material material, Shader shader, Texture2D texture, string name)
    {
        material.shader = shader;
        material.SetTexture("_MainTex", texture);
        material.SetTexture("_BaseMap", texture);
        material.SetTexture("_1st_ShadeMap", texture);
        material.SetColor("_BaseColor", name == "Anby_EyeShadow"
            ? new Color(0.2f, 0.2f, 0.2f, 1f) : Color.white);
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
        float opacity = name == "Anby_EyeShadow" ? -0.7f
            : name == "Anby_ClearCover" ? -0.5f
            : name == "Anby_EyeHighlightSoft" ? -0.4f
            : name == "Anby_HairOverlay" ? -0.35f : 0f;
        material.SetFloat("_Tweak_transparency", opacity);
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
