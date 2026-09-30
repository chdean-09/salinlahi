using UnityEditor;
using UnityEngine;

/// <summary>Imports the supplied Kadena and Gapos artwork and assigns it to enemy data.</summary>
public static class EnemyRelationshipAbilityArtSetup
{
    private const string GaposFolder = "Assets/Art/VFX/EnemyAbilities/Gapos";
    private const string KadenaFolder = "Assets/Art/VFX/EnemyAbilities/Kadena";
    private const string GaposSheetPath = GaposFolder + "/gapos-binding-sheet.png";
    private const string GaposTileSourcePath = GaposFolder + "/gapos-root-tile-source.png";
    private const string GaposTilePath = GaposFolder + "/gapos-root-tile.png";
    private const string KadenaSheetPath = KadenaFolder + "/kadena-chain-sheet.png";
    private const string GaposDataPath = "Assets/ScriptableObjects/Enemies/EnemyData_Gapos.asset";
    private const string KadenaDataPath = "Assets/ScriptableObjects/Enemies/EnemyData_Kadena.asset";

    [MenuItem("Salinlahi/Art/Import And Assign Kadena And Gapos Connectors")]
    public static void ImportAndAssign()
    {
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

        ConfigureSheet(GaposSheetPath, 512f, TextureWrapMode.Clamp, new[]
        {
            SpriteRect("Gapos_LeftLoop", 64f, 149f, 500f, 400f),
            SpriteRect("Gapos_RightLoop", 1608f, 149f, 500f, 400f)
        });
        ConfigureSheet(KadenaSheetPath, 512f, TextureWrapMode.Clamp, new[]
        {
            SpriteRect("Kadena_Chain", 59f, 449f, 1656f, 210f),
            SpriteRect("Kadena_LeftHook", 59f, 123f, 674f, 235f),
            SpriteRect("Kadena_RightHook", 1041f, 123f, 674f, 235f)
        });
        ConfigureSingleSprite(GaposTileSourcePath, 512f, TextureWrapMode.Clamp);
        ConfigureSingleSprite(GaposTilePath, 2048f, TextureWrapMode.Mirror);

        Sprite gaposLeft = FindSprite(GaposSheetPath, "Gapos_LeftLoop");
        Sprite gaposRight = FindSprite(GaposSheetPath, "Gapos_RightLoop");
        Sprite gaposMiddle = AssetDatabase.LoadAssetAtPath<Sprite>(GaposTilePath);
        Sprite kadenaChain = FindSprite(KadenaSheetPath, "Kadena_Chain");
        Sprite kadenaLeft = FindSprite(KadenaSheetPath, "Kadena_LeftHook");
        Sprite kadenaRight = FindSprite(KadenaSheetPath, "Kadena_RightHook");
        EnemyDataSO gapos = AssetDatabase.LoadAssetAtPath<EnemyDataSO>(GaposDataPath);
        EnemyDataSO kadena = AssetDatabase.LoadAssetAtPath<EnemyDataSO>(KadenaDataPath);

        if (gapos == null || kadena == null || gaposLeft == null || gaposRight == null
            || gaposMiddle == null || kadenaChain == null || kadenaLeft == null || kadenaRight == null)
        {
            Debug.LogError("Kadena/Gapos relationship art import stopped: one or more source assets or sprites could not be loaded.");
            return;
        }

        gapos.relationshipVisual = new EnemyRelationshipVisualDefinition
        {
            firstEndpoint = gaposLeft,
            middle = gaposMiddle,
            secondEndpoint = gaposRight,
            bodyMode = EnemyRelationshipBodyMode.Tile,
            endpointLength = 0.48f,
            endpointHeight = 0.42f,
            bodyHeight = 0.24f,
            sortingOrder = RenderOrder.EnemyDefault - 1
        };
        kadena.relationshipVisual = new EnemyRelationshipVisualDefinition
        {
            firstEndpoint = kadenaLeft,
            middle = kadenaChain,
            secondEndpoint = kadenaRight,
            bodyMode = EnemyRelationshipBodyMode.Stretch,
            endpointLength = 0.42f,
            endpointHeight = 0.22f,
            bodyHeight = 0.22f,
            sortingOrder = RenderOrder.EnemyDefault - 1
        };

        EditorUtility.SetDirty(gapos);
        EditorUtility.SetDirty(kadena);
        AssetDatabase.SaveAssets();
        Debug.Log("Imported and assigned the Gapos root binding and Kadena chain connector sprites.");
    }

    private static SpriteMetaData SpriteRect(string name, float x, float y, float width, float height)
    {
        return new SpriteMetaData
        {
            name = name,
            rect = new Rect(x, y, width, height),
            alignment = (int)SpriteAlignment.Center,
            pivot = new Vector2(0.5f, 0.5f)
        };
    }

    private static void ConfigureSheet(string path, float pixelsPerUnit, TextureWrapMode wrapMode,
        SpriteMetaData[] sprites)
    {
        TextureImporter importer = GetImporter(path);
        if (importer == null)
            return;

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit = pixelsPerUnit;
        SetFullRectMesh(importer);
        importer.filterMode = FilterMode.Point;
        importer.wrapMode = wrapMode;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.crunchedCompression = false;
#pragma warning disable CS0618
        importer.spritesheet = sprites;
#pragma warning restore CS0618
        importer.SaveAndReimport();
    }

    private static void ConfigureSingleSprite(string path, float pixelsPerUnit, TextureWrapMode wrapMode)
    {
        TextureImporter importer = GetImporter(path);
        if (importer == null)
            return;

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = pixelsPerUnit;
        SetFullRectMesh(importer);
        importer.filterMode = FilterMode.Point;
        importer.wrapMode = wrapMode;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.crunchedCompression = false;
        importer.SaveAndReimport();
    }

    private static TextureImporter GetImporter(string path)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
            Debug.LogError($"Unity could not find a texture importer for {path}.");
        return importer;
    }

    private static void SetFullRectMesh(TextureImporter importer)
    {
        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        importer.SetTextureSettings(settings);
    }

    private static Sprite FindSprite(string path, string name)
    {
        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
        for (int i = 0; i < assets.Length; i++)
        {
            if (assets[i] is Sprite sprite && sprite.name == name)
                return sprite;
        }

        return null;
    }
}
