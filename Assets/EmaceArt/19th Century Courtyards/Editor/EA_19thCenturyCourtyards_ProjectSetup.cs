// EmacEArt Project Setup — 19th Century Courtyards
// Runs once when this package is imported into a new project.
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

[InitializeOnLoad]
public static class EA_19thCenturyCourtyards_ProjectSetup
{
    private const string PIPELINE_ASSET_PATH = "Assets/EmaceArt/19th Century Courtyards/Post Processing_profile/Settings/New Universal Render Pipeline Asset-HighQuality.asset";
    private const string PENDING_KEY = "EA_19thCenturyCourtyards_ProjectSetup_Pending";

    private static string SetupDoneKey =>
        "EA_19thCenturyCourtyards_ProjectSetup_" + Mathf.Abs(UnityEngine.Application.dataPath.GetHashCode()).ToString();

    static EA_19thCenturyCourtyards_ProjectSetup()
    {
        if (EditorPrefs.GetBool(SetupDoneKey, false)) return;
        EditorApplication.delayCall += TryRunSetup;
    }

    private static void TryRunSetup()
    {
        if (EditorPrefs.GetBool(SetupDoneKey, false)) return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += TryRunSetup;
            return;
        }
        // Check editor is fully ready
        if (!UnityEditorInternal.InternalEditorUtility.isApplicationActive)
        {
            EditorApplication.delayCall += TryRunSetup;
            return;
        }
        RunSetup();
    }

    private static void RunSetup()
    {
        if (EditorPrefs.GetBool(SetupDoneKey, false)) return;

        bool ok = EditorUtility.DisplayDialog(
            "19th Century Courtyards — Project Setup",
            "One-click setup for 19th Century Courtyards.\n\n" +
            "This will configure your project so the asset\n" +
            "looks exactly as designed:\n\n" +
            "• Color rendering → Gamma (recommended)\n" +
            "• Lighting calibrated for this asset\n" +
            "• Render Pipeline assigned\n\n" +
            "You can undo this at any time in Project Settings.",
            "Apply", "Skip");

        if (!ok) return;

        PlayerSettings.colorSpace = ColorSpace.Gamma;
        GraphicsSettings.lightsUseLinearIntensity = false;

        var pipeline = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(PIPELINE_ASSET_PATH);
        if (pipeline != null)
        {
            // Assign to Graphics Settings
            GraphicsSettings.defaultRenderPipeline = pipeline;

            // Assign to all Quality levels
            int saved = QualitySettings.GetQualityLevel();
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = pipeline;
            }
            QualitySettings.SetQualityLevel(saved, false);

            // Quality tweaks — LOD, terrain detail
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.lodBias = 2.0f;
                QualitySettings.terrainDetailDistance   = 200f;
                QualitySettings.terrainDetailDensityScale = 1.0f;
                QualitySettings.terrainMaxTrees          = 200;
            }
            QualitySettings.SetQualityLevel(saved, false);

            // Force save ProjectSettings
            EditorUtility.SetDirty(GraphicsSettings.GetGraphicsSettings());
            AssetDatabase.SaveAssets();
            Debug.Log($"[19th Century Courtyards] Pipeline assigned to Graphics + all Quality levels: {pipeline.name}");
        }
        else
        {
            Debug.LogWarning($"[19th Century Courtyards] Pipeline asset not found at: {PIPELINE_ASSET_PATH}");
        }

        // Fix known broken material GUIDs in this package
        FixMaterialGUIDs();

        EditorPrefs.SetBool(SetupDoneKey, true);
        Debug.Log($"[19th Century Courtyards] Project setup complete.");
        EditorUtility.DisplayDialog("19th Century Courtyards — Done",
            "Project configured successfully.\nYou can re-run setup from:\nTools → EmacEArt → 19th Century Courtyards Setup", "OK");
    }

    private static void FixMaterialGUIDs()
    {
        // Fix known stale GUID references in binary materials
        var fixes = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.Dictionary<string, string>>
        {
            // EmacEArt_StylizedGrass_Mat — shader + 3 textures
            { "EmacEArt_StylizedGrass_Mat", new System.Collections.Generic.Dictionary<string, string>
                {
                    { "e7a5f2c8b9d4416eaf3c5b9e1d7a8b9f", FindGUID("EmacEArt_StylizedGrass",  "shader") },
                    { "597373bbd2adc554e94eceffeece7913", FindGUID("GRASS_DIFF",              "texture") },
                    { "1ab55a68e5287084cb9bdb5ded95fa8c", FindGUID("GRASS_NMAP",              "texture") },
                    { "796e3706ceb704247adb957915a9edaf", FindGUID("Noise_Big",               "texture") },
                }
            },
        };

        foreach (var kv in fixes)
        {
            string[] guids = AssetDatabase.FindAssets($"t:Material {kv.Key}");
            if (guids.Length == 0) continue;
            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            string absPath = Application.dataPath.Replace("Assets", "") + path;
            if (!System.IO.File.Exists(absPath)) continue;

            string content = System.IO.File.ReadAllText(absPath);
            bool dirty = false;
            foreach (var replace in kv.Value)
            {
                if (string.IsNullOrEmpty(replace.Value)) continue;
                if (content.Contains(replace.Key))
                {
                    content = content.Replace(replace.Key, replace.Value);
                    dirty = true;
                }
            }
            if (dirty)
            {
                System.IO.File.WriteAllText(absPath, content, System.Text.Encoding.UTF8);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                Debug.Log($"[19th Century Courtyards] Fixed material GUIDs: {kv.Key}");
            }
        }
    }

    private static string FindGUID(string assetName, string type)
    {
        string filter = type == "shader" ? $"t:Shader {assetName}" : $"t:Texture {assetName}";
        string[] guids = AssetDatabase.FindAssets(filter);
        return guids.Length > 0 ? guids[0] : null;
    }

    [MenuItem("Tools/EmacEArt/19th Century Courtyards Setup")]
    public static void RunSetupManual()
    {
        EditorPrefs.DeleteKey(SetupDoneKey);
        SessionState.SetBool(SetupDoneKey + "_session", false);
        RunSetup();
    }
}
