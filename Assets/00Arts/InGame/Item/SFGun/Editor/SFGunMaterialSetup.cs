using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class SFGunMaterialSetup
{
    private const string Root = "Assets/00Arts/Item/SFGun/texture/texture/";
    private const string Prefix = "new gun_low_gun_";
    private const string PackedMap = Root + Prefix + "MetallicSmoothness.png";
    private const string ModelPath = "Assets/00Arts/Item/SFGun/LC+2+export.fbx";
    private const string BallMaterialPath = "Assets/00Arts/Item/SFGun/ball/ball/SFGun_Ball.mat";
    private const string PrefabPath = "Assets/00Arts/Item/SFGun/SFGun.prefab";

    [InitializeOnLoadMethod]
    private static void CreateMissingPrefabAfterImport()
    {
        // The source FBX assigns gun to every mesh; keep the ball override in a prefab.
        EditorApplication.delayCall += () =>
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null &&
                AssetDatabase.LoadAssetAtPath<Material>(BallMaterialPath) != null)
                BuildBallPrefab();
        };
    }

    [MenuItem("Tools/SFGun/Build Ball Prefab")]
    public static void BuildBallPrefab()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null)
            return;

        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        Material ball = AssetDatabase.LoadAssetAtPath<Material>(BallMaterialPath);
        if (source == null || ball == null)
            throw new InvalidOperationException("SFGun model or ball material was not found.");

        Scene previewScene = EditorSceneManager.NewPreviewScene();
        try
        {
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(source, previewScene);
            Renderer target = null;
            foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer.name == "Cylinder.003_low")
                {
                    target = renderer;
                    break;
                }
            }
            if (target == null)
                throw new InvalidOperationException("Cylinder.003_low was not found in the imported model.");

            Material[] slots = target.sharedMaterials;
            for (int i = 0; i < slots.Length; i++)
                slots[i] = ball;
            target.sharedMaterials = slots;

            if (PrefabUtility.SaveAsPrefabAsset(instance, PrefabPath) == null)
                throw new InvalidOperationException("SFGun prefab could not be saved.");
            AssetDatabase.SaveAssets();
            Debug.Log("SFGun prefab created with ball material on Cylinder.003_low.");
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(previewScene);
        }
    }

    [MenuItem("Tools/SFGun/Apply Reference Materials")]
    public static void Apply()
    {
        Shader lit = Shader.Find("Universal Render Pipeline/Lit");
        if (lit == null)
            throw new InvalidOperationException("URP Lit shader was not found.");

        // URP treats these maps as linear data; the normal needs its normal-map importer.
        ConfigureTexture(Prefix + "Normal.png", true, false);
        ConfigureTexture(Prefix + "Metallic.png", false, false);
        ConfigureTexture(Prefix + "Roughness.png", false, false);
        ConfigureTexture(Prefix + "MAO.png", false, false);
        CreateMetallicSmoothnessMap();

        Material gun = LoadMaterial("gun.mat", lit);
        gun.SetTexture("_BaseMap", LoadTexture(Prefix + "BaseColor.png"));
        gun.SetColor("_BaseColor", new Color(0.32f, 0.37f, 0.39f, 1f));
        gun.SetTexture("_BumpMap", LoadTexture(Prefix + "Normal.png"));
        gun.SetFloat("_BumpScale", 1f);
        gun.SetTexture("_MetallicGlossMap", LoadTexture(Prefix + "MetallicSmoothness.png"));
        gun.SetFloat("_Metallic", 1f);
        gun.SetFloat("_Smoothness", 1f);
        gun.SetTexture("_OcclusionMap", LoadTexture(Prefix + "MAO.png"));
        gun.SetFloat("_OcclusionStrength", 0.8f);
        gun.SetTexture("_EmissionMap", LoadTexture(Prefix + "Emissive.png"));
        gun.SetColor("_EmissionColor", new Color(3f, 1.6f, 0.45f, 1f));
        gun.EnableKeyword("_NORMALMAP");
        gun.EnableKeyword("_METALLICSPECGLOSSMAP");
        gun.EnableKeyword("_OCCLUSIONMAP");
        gun.EnableKeyword("_EMISSION");
        MaterialEditor.FixupEmissiveFlag(gun);
        EditorUtility.SetDirty(gun);

        // These three slots are separate meshes in the imported FBX.
        SetSolidMaterial("Material.mat", lit, new Color(0.14f, 0.18f, 0.20f), 0.8f, 0.55f, Color.black);
        SetSolidMaterial("ammo.mat", lit, new Color(0.22f, 0.25f, 0.27f), 0.75f, 0.5f, Color.black);
        SetSolidMaterial("orange.mat", lit, new Color(0.95f, 0.38f, 0.08f), 0f, 0.6f,
            new Color(6f, 2.4f, 0.3f, 1f));

        AssetDatabase.SaveAssets();
        Debug.Log("SFGun materials configured: base color, normal, metallic/smoothness, AO and orange emission.");
    }

    private static void ConfigureTexture(string fileName, bool normalMap, bool sRgb)
    {
        string path = Root + fileName;
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
            throw new FileNotFoundException("SFGun texture importer was not found", path);

        TextureImporterType type = normalMap ? TextureImporterType.NormalMap : TextureImporterType.Default;
        if (importer.textureType == type && importer.sRGBTexture == sRgb)
            return;

        importer.textureType = type;
        importer.sRGBTexture = sRgb;
        importer.SaveAndReimport();
    }

    private static void CreateMetallicSmoothnessMap()
    {
        string metallicPath = Root + Prefix + "Metallic.png";
        string roughnessPath = Root + Prefix + "Roughness.png";
        TextureImporter metallicImporter = (TextureImporter)AssetImporter.GetAtPath(metallicPath);
        TextureImporter roughnessImporter = (TextureImporter)AssetImporter.GetAtPath(roughnessPath);
        bool metallicWasReadable = metallicImporter.isReadable;
        bool roughnessWasReadable = roughnessImporter.isReadable;

        try
        {
            if (!metallicWasReadable)
            {
                metallicImporter.isReadable = true;
                metallicImporter.SaveAndReimport();
            }
            if (!roughnessWasReadable)
            {
                roughnessImporter.isReadable = true;
                roughnessImporter.SaveAndReimport();
            }

            Texture2D metallic = LoadTexture(Prefix + "Metallic.png");
            Texture2D roughness = LoadTexture(Prefix + "Roughness.png");
            if (metallic.width != roughness.width || metallic.height != roughness.height)
                throw new InvalidOperationException("Metallic and roughness textures must have matching dimensions.");

            Color32[] metalPixels = metallic.GetPixels32();
            Color32[] roughPixels = roughness.GetPixels32();
            Color32[] packedPixels = new Color32[metalPixels.Length];
            for (int i = 0; i < packedPixels.Length; i++)
            {
                // URP/Lit reads metallic from R and smoothness from A.
                byte metal = metalPixels[i].r;
                byte smoothness = (byte)(255 - roughPixels[i].r);
                packedPixels[i] = new Color32(metal, metal, metal, smoothness);
            }

            Texture2D packed = new Texture2D(metallic.width, metallic.height, TextureFormat.RGBA32, false, true);
            packed.SetPixels32(packedPixels);
            packed.Apply(false, false);
            File.WriteAllBytes(PackedMap, packed.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(packed);
            AssetDatabase.ImportAsset(PackedMap, ImportAssetOptions.ForceSynchronousImport);

            TextureImporter packedImporter = (TextureImporter)AssetImporter.GetAtPath(PackedMap);
            packedImporter.sRGBTexture = false;
            packedImporter.alphaIsTransparency = false;
            packedImporter.textureCompression = TextureImporterCompression.Uncompressed;
            packedImporter.SaveAndReimport();
        }
        finally
        {
            if (!metallicWasReadable)
            {
                metallicImporter.isReadable = false;
                metallicImporter.SaveAndReimport();
            }
            if (!roughnessWasReadable)
            {
                roughnessImporter.isReadable = false;
                roughnessImporter.SaveAndReimport();
            }
        }
    }

    private static Material LoadMaterial(string fileName, Shader shader)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(Root + fileName);
        if (material == null)
            throw new FileNotFoundException("SFGun material was not found", Root + fileName);
        material.shader = shader;
        return material;
    }

    private static Texture2D LoadTexture(string fileName)
    {
        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(Root + fileName);
        if (texture == null)
            throw new FileNotFoundException("SFGun texture was not found", Root + fileName);
        return texture;
    }

    private static void SetSolidMaterial(string fileName, Shader shader, Color color, float metallic,
        float smoothness, Color emission)
    {
        Material material = LoadMaterial(fileName, shader);
        material.SetTexture("_BaseMap", null);
        material.SetColor("_BaseColor", color);
        material.SetTexture("_BumpMap", null);
        material.SetTexture("_MetallicGlossMap", null);
        material.SetTexture("_OcclusionMap", null);
        material.SetTexture("_EmissionMap", null);
        material.SetFloat("_Metallic", metallic);
        material.SetFloat("_Smoothness", smoothness);
        material.SetColor("_EmissionColor", emission);
        material.DisableKeyword("_NORMALMAP");
        material.DisableKeyword("_METALLICSPECGLOSSMAP");
        material.DisableKeyword("_OCCLUSIONMAP");
        if (emission.maxColorComponent > 0f)
            material.EnableKeyword("_EMISSION");
        else
            material.DisableKeyword("_EMISSION");
        MaterialEditor.FixupEmissiveFlag(material);
        EditorUtility.SetDirty(material);
    }
}
