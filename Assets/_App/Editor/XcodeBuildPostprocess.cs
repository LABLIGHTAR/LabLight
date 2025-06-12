using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;
using System.IO;
using UnityEngine;
using System.Linq;
using System.Collections.Generic;

public class XcodeBuildPostprocess
{
    private static readonly string[] AllowedExtensions = { 
        ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".tiff", 
        ".mp4", ".mov", ".avi", ".wmv", ".m4v",
        ".pdf"
    };

    [PostProcessBuild]
    public static void OnPostProcessBuild(BuildTarget buildTarget, string buildPath)
    {
        if (buildTarget != BuildTarget.VisionOS)
        {
            Debug.Log("Build target is not visionOS, skipping resource copy");
            return;
        }

        string projectPath = Path.Combine(buildPath, "Unity-VisionOS.xcodeproj/project.pbxproj");
        if (!File.Exists(projectPath))
        {
            Debug.LogError($"Could not find Xcode project at: {projectPath}");
            return;
        }

        var project = new PBXProject();
        project.ReadFromFile(projectPath);
        string targetGuid = project.GetUnityMainTargetGuid();

        // Setup asset catalog
        string assetCatalogPath = Path.Combine(buildPath, "Unity-VisionOS/Media.xcassets");
        SetupAssetCatalog(assetCatalogPath);

        // Process resources
        ProcessResources(buildPath, project, targetGuid, assetCatalogPath);

        // Save project
        try
        {
            project.WriteToFile(projectPath);
            Debug.Log("Successfully wrote changes to Xcode project");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error saving Xcode project: {e.Message}");
        }

        // Verify asset catalog contents
        VerifyAssetCatalog(assetCatalogPath);
    }

    private static void SetupAssetCatalog(string assetCatalogPath)
    {
        Directory.CreateDirectory(assetCatalogPath);
        string catalogContentsPath = Path.Combine(assetCatalogPath, "Contents.json");
        
        if (!File.Exists(catalogContentsPath))
        {
            File.WriteAllText(catalogContentsPath, @"{
                ""info"" : {
                    ""author"" : ""xcode"",
                    ""version"" : 1
                }
            }");
        }
    }

    private static void ProcessResources(string buildPath, PBXProject project, string targetGuid, string assetCatalogPath)
    {
        string resourcesPath = "Assets/Resources";
        if (!Directory.Exists(resourcesPath))
        {
            Debug.LogError($"Resources directory not found: {resourcesPath}");
            return;
        }

        string[] files = Directory.GetFiles(resourcesPath, "*.*", SearchOption.AllDirectories);
        var processedFileNames = new HashSet<string>();

        foreach (string file in files)
        {
            if (file.EndsWith(".meta")) continue;

            string extension = Path.GetExtension(file).ToLower();
            if (!AllowedExtensions.Contains(extension)) continue;

            string fileName = Path.GetFileName(file);
            if (processedFileNames.Contains(fileName))
            {
                Debug.LogWarning($"Skipping duplicate file name: {fileName}");
                continue;
            }
            processedFileNames.Add(fileName);

            ProcessFile(file, resourcesPath, buildPath, project, targetGuid, assetCatalogPath);
        }

        // Configure asset catalog in project
        string catalogRelativePath = "Unity-VisionOS/Media.xcassets";
        string catalogFileGuid = project.AddFile(catalogRelativePath, catalogRelativePath);
        project.AddFileToBuild(targetGuid, catalogFileGuid);
        project.AddResourcesBuildPhase(targetGuid);
        project.AddBuildProperty(targetGuid, "ASSETCATALOG_COMPILER_APPICON_NAME", "AppIcon");
        project.AddBuildProperty(targetGuid, "ASSETCATALOG_COMPILER_GLOBAL_ACCENT_COLOR_NAME", "AccentColor");
    }

    private static void ProcessFile(string file, string resourcesPath, string buildPath, 
        PBXProject project, string targetGuid, string assetCatalogPath)
    {
        string relativePath = Path.GetRelativePath(resourcesPath, file);
        string extension = Path.GetExtension(file).ToLower();
        string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(file);
        string destination;
        string xcodeRelativePath;

        try
        {
            switch (extension)
            {
                case ".pdf":
                    // Handle PDFs
                    destination = Path.Combine(buildPath, "Data/Resources/Protocol", Path.GetFileName(file));
                    xcodeRelativePath = Path.Combine("Data/Resources/Protocol", Path.GetFileName(file));
                    Directory.CreateDirectory(Path.GetDirectoryName(destination));
                    File.Copy(file, destination, true);
                    Debug.Log($"Copied PDF to: {destination}");
                    break;

                case ".mp4":
                case ".mov":
                case ".m4v":
                    // Handle videos
                    destination = Path.Combine(buildPath, "Data/Resources/Protocol", Path.GetFileName(file));
                    xcodeRelativePath = Path.Combine("Data/Resources/Protocol", Path.GetFileName(file));
                    Directory.CreateDirectory(Path.GetDirectoryName(destination));
                    File.Copy(file, destination, true);
                    Debug.Log($"Copied video to: {destination}");
                    break;

                case ".jpg":
                case ".jpeg":
                case ".png":
                case ".gif":
                case ".bmp":
                case ".tiff":
                    // Handle images in asset catalog
                    string assetFolder = Path.Combine(assetCatalogPath, fileNameWithoutExtension + ".imageset");
                    Directory.CreateDirectory(assetFolder);

                    // Create Contents.json for the image
                    string assetContentsPath = Path.Combine(assetFolder, "Contents.json");
                    File.WriteAllText(assetContentsPath, $@"{{
                        ""images"" : [
                            {{
                                ""filename"" : ""{Path.GetFileName(file)}"",
                                ""idiom"" : ""universal"",
                                ""scale"" : ""1x""
                            }}
                        ],
                        ""info"" : {{
                            ""author"" : ""xcode"",
                            ""version"" : 1
                        }}
                    }}");

                    // Copy the image
                    destination = Path.Combine(assetFolder, Path.GetFileName(file));
                    xcodeRelativePath = Path.Combine("Unity-VisionOS/Media.xcassets", 
                        fileNameWithoutExtension + ".imageset", 
                        Path.GetFileName(file));
                    File.Copy(file, destination, true);
                    Debug.Log($"Copied image to asset catalog: {destination}");
                    break;

                default:
                    Debug.LogWarning($"Unhandled file type: {extension}");
                    return;
            }

            // Add to Xcode project
            string fileGuid = project.AddFile(xcodeRelativePath, xcodeRelativePath);
            project.AddFileToBuild(targetGuid, fileGuid);
            Debug.Log($"Added to Xcode project: {xcodeRelativePath}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error processing file {relativePath}: {e.Message}");
        }
    }

    private static void VerifyAssetCatalog(string assetCatalogPath)
    {
        try
        {
            var assetCatalogFiles = Directory.GetFiles(assetCatalogPath, "*.*", SearchOption.AllDirectories);
            Debug.Log($"Asset Catalog Contents ({assetCatalogFiles.Length} files):");
            foreach (var file in assetCatalogFiles)
            {
                Debug.Log($"- {Path.GetRelativePath(assetCatalogPath, file)}");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error listing asset catalog contents: {e.Message}");
        }
    }
}