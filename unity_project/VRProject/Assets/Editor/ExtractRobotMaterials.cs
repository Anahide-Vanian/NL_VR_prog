using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class ExtractRobotMaterials
{
    private const string RobotRoot = "Assets/Robots/Ridgeback_UR5e_2F140";
    private const string ExtractFolder = "Assets/Robots/Ridgeback_UR5e_2F140/ExtractedMaterials";

    [MenuItem("Tools/Extract Robot Materials")]
    public static void Extract()
    {
        if (!AssetDatabase.IsValidFolder(ExtractFolder))
        {
            Directory.CreateDirectory(ExtractFolder);
            AssetDatabase.Refresh();
        }

        string[] modelGuids = AssetDatabase.FindAssets("t:Model", new[] { RobotRoot });
        Debug.Log($"[ExtractRobotMaterials] Found {modelGuids.Length} model files under {RobotRoot}");

        int extractedCount = 0;
        int modelCount = 0;

        foreach (string guid in modelGuids)
        {
            string modelPath = AssetDatabase.GUIDToAssetPath(guid);
            ModelImporter importer = AssetImporter.GetAtPath(modelPath) as ModelImporter;
            if (importer == null) continue;

            modelCount++;

            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(modelPath);
            foreach (Object asset in assets)
            {
                if (asset is Material mat)
                {
                    string destPath = Path.Combine(ExtractFolder, mat.name + ".mat");
                    destPath = AssetDatabase.GenerateUniqueAssetPath(destPath);

                    string error = AssetDatabase.ExtractAsset(mat, destPath);
                    if (string.IsNullOrEmpty(error))
                    {
                        extractedCount++;
                    }
                    else
                    {
                        Debug.LogWarning($"[ExtractRobotMaterials] Could not extract {mat.name} from {modelPath}: {error}");
                    }
                }
            }
        }

        AssetDatabase.Refresh();
        AssetDatabase.WriteImportSettingsIfDirty(RobotRoot);

        foreach (string guid in modelGuids)
        {
            string modelPath = AssetDatabase.GUIDToAssetPath(guid);
            AssetDatabase.ImportAsset(modelPath, ImportAssetOptions.ForceUpdate);
        }

        Debug.Log($"[ExtractRobotMaterials] Done. Extracted {extractedCount} materials from {modelCount} models into {ExtractFolder}");
        EditorUtility.DisplayDialog("Extract Robot Materials",
            $"Extracted {extractedCount} materials from {modelCount} models into:\n{ExtractFolder}\n\nNext: select those materials and run 'Edit → Rendering → Materials → Convert Selected Built-in Materials to URP'",
            "OK");
    }
}
