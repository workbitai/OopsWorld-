using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public class UnusedAssetsFinderWindow : EditorWindow
{
    DefaultAsset scopeFolder;

    bool excludeResourcesFolder = true;
    bool excludeStreamingAssetsFolder = true;
    bool excludePluginsFolder = true;
    bool includeDisabledBuildScenes = false;
    bool groupByFolder = true;
    string extraExcludeFolders = "";

    Vector2 scroll;
    List<string> unusedPaths = new List<string>();
    Dictionary<string, List<string>> unusedByCategory = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
    Dictionary<string, List<string>> unusedByFolder = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<string, bool> categoryFoldouts = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<string, bool> folderFoldouts = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

    [MenuItem("Tools/Unused Assets Finder")]
    static void Open()
    {
        GetWindow<UnusedAssetsFinderWindow>("Unused Assets");
    }

    [MenuItem("Assets/Unused Assets/Delete Unused In This Folder (Build Scenes)", true)]
    static bool ValidateDeleteUnusedInSelectedFolder()
    {
        var obj = Selection.activeObject as DefaultAsset;
        if (obj == null) return false;
        string path = AssetDatabase.GetAssetPath(obj);
        return !string.IsNullOrEmpty(path) && AssetDatabase.IsValidFolder(path);
    }

    [MenuItem("Assets/Unused Assets/Delete Unused In This Folder (Build Scenes)")]
    static void DeleteUnusedInSelectedFolder()
    {
        var obj = Selection.activeObject as DefaultAsset;
        if (obj == null) return;
        string folderPath = AssetDatabase.GetAssetPath(obj);
        if (string.IsNullOrEmpty(folderPath) || !AssetDatabase.IsValidFolder(folderPath)) return;

        var window = CreateInstance<UnusedAssetsFinderWindow>();
        window.scopeFolder = obj;
        window.excludeResourcesFolder = true;
        window.excludeStreamingAssetsFolder = true;
        window.excludePluginsFolder = true;
        window.includeDisabledBuildScenes = false;
        window.FindUnusedAssets();

        if (window.unusedPaths == null || window.unusedPaths.Count == 0)
        {
            EditorUtility.DisplayDialog("Unused Assets", $"No unused assets found in: {folderPath}", "OK");
            DestroyImmediate(window);
            return;
        }

        bool ok = EditorUtility.DisplayDialog(
            "Delete unused assets?",
            $"Folder: {folderPath}\nUnused assets found: {window.unusedPaths.Count}\n\nThis will permanently delete assets. Continue?",
            "Delete",
            "Cancel");

        if (ok)
        {
            DeleteAssets(window.unusedPaths);
        }

        DestroyImmediate(window);
    }

    void OnGUI()
    {
        EditorGUILayout.LabelField("Scans only Build Settings scenes (Build Index).", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Note: Runtime loading via Resources.Load/Addressables/AssetBundles cannot be reliably detected.");

        EditorGUILayout.Space(6);

        scopeFolder = (DefaultAsset)EditorGUILayout.ObjectField(
            "Scope Folder (Optional)",
            scopeFolder,
            typeof(DefaultAsset),
            false);

        if (scopeFolder != null)
        {
            string folderPath = AssetDatabase.GetAssetPath(scopeFolder);
            if (!AssetDatabase.IsValidFolder(folderPath))
            {
                EditorGUILayout.HelpBox("Scope Folder must be a folder from the Project window.", MessageType.Warning);
            }
            else
            {
                EditorGUILayout.HelpBox($"Scanning only: {folderPath}", MessageType.Info);
            }
        }

        excludeResourcesFolder = EditorGUILayout.ToggleLeft("Exclude Assets/Resources (treat as used)", excludeResourcesFolder);
        excludeStreamingAssetsFolder = EditorGUILayout.ToggleLeft("Exclude Assets/StreamingAssets (treat as used)", excludeStreamingAssetsFolder);
        excludePluginsFolder = EditorGUILayout.ToggleLeft("Exclude Assets/Plugins (treat as used)", excludePluginsFolder);
        includeDisabledBuildScenes = EditorGUILayout.ToggleLeft("Include disabled scenes from Build Settings in scan", includeDisabledBuildScenes);

        groupByFolder = EditorGUILayout.ToggleLeft("Group results by folder", groupByFolder);

        EditorGUILayout.LabelField("Extra excluded folders (one per line)");
        extraExcludeFolders = EditorGUILayout.TextArea(extraExcludeFolders, GUILayout.MinHeight(50));

        EditorGUILayout.Space(8);

        using (new EditorGUI.DisabledScope(EditorApplication.isCompiling))
        {
            if (GUILayout.Button("Find Unused Assets", GUILayout.Height(30)))
            {
                FindUnusedAssets();
            }
        }

        using (new EditorGUI.DisabledScope(EditorApplication.isCompiling || unusedPaths.Count == 0))
        {
            if (GUILayout.Button("Delete All Listed Unused", GUILayout.Height(26)))
            {
                bool ok = EditorUtility.DisplayDialog(
                    "Delete unused assets?",
                    $"Unused assets listed: {unusedPaths.Count}\n\nThis will permanently delete assets. Continue?",
                    "Delete",
                    "Cancel");

                if (ok)
                {
                    DeleteAssets(unusedPaths);
                    FindUnusedAssets();
                }
            }
        }

        EditorGUILayout.Space(8);

        EditorGUILayout.LabelField($"Unused count: {unusedPaths.Count}");

        scroll = EditorGUILayout.BeginScrollView(scroll);

        if (groupByFolder)
        {
            var orderedFolders = unusedByFolder.Keys
                .OrderByDescending(k => unusedByFolder[k].Count)
                .ThenBy(k => k)
                .ToList();

            for (int f = 0; f < orderedFolders.Count; f++)
            {
                string folder = orderedFolders[f];
                var list = unusedByFolder[folder];
                if (list == null || list.Count == 0)
                    continue;

                if (!folderFoldouts.ContainsKey(folder))
                    folderFoldouts[folder] = true;

                using (new EditorGUILayout.HorizontalScope())
                {
                    folderFoldouts[folder] = EditorGUILayout.Foldout(
                        folderFoldouts[folder],
                        $"{folder} ({list.Count})",
                        true);

                    if (GUILayout.Button("Delete Folder", GUILayout.Width(95)))
                    {
                        bool ok = EditorUtility.DisplayDialog(
                            "Delete folder unused assets?",
                            $"Folder: {folder}\nAssets to delete: {list.Count}\n\nThis will permanently delete assets. Continue?",
                            "Delete",
                            "Cancel");
                        if (ok)
                        {
                            DeleteAssets(list);
                            FindUnusedAssets();
                            EditorGUILayout.EndScrollView();
                            return;
                        }
                    }

                    if (GUILayout.Button("Remove Folder", GUILayout.Width(95)))
                    {
                        RemovePathsFromListing(list);
                        EditorGUILayout.EndScrollView();
                        return;
                    }
                }

                if (!folderFoldouts[folder])
                    continue;

                using (new EditorGUI.IndentLevelScope())
                {
                    for (int i = 0; i < list.Count; i++)
                    {
                        string path = list[i];
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            var obj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
                            EditorGUILayout.ObjectField(obj, typeof(UnityEngine.Object), false);

                            if (GUILayout.Button("Ping", GUILayout.Width(45)))
                            {
                                if (obj != null)
                                {
                                    EditorGUIUtility.PingObject(obj);
                                    Selection.activeObject = obj;
                                }
                            }

                            if (GUILayout.Button("Del", GUILayout.Width(35)))
                            {
                                bool ok = EditorUtility.DisplayDialog(
                                    "Delete asset?",
                                    $"{path}\n\nThis will permanently delete the asset. Continue?",
                                    "Delete",
                                    "Cancel");
                                if (ok)
                                {
                                    DeleteAssets(new List<string> { path });
                                    FindUnusedAssets();
                                    EditorGUILayout.EndScrollView();
                                    return;
                                }
                            }

                            if (GUILayout.Button("X", GUILayout.Width(22)))
                            {
                                RemovePathsFromListing(new List<string> { path });
                                EditorGUILayout.EndScrollView();
                                return;
                            }
                        }
                    }
                }

                EditorGUILayout.Space(4);
            }
        }
        else
        {
            var orderedCategories = unusedByCategory.Keys
                .OrderByDescending(k => unusedByCategory[k].Count)
                .ThenBy(k => k)
                .ToList();

            for (int c = 0; c < orderedCategories.Count; c++)
            {
                string category = orderedCategories[c];
                var list = unusedByCategory[category];
                if (list == null || list.Count == 0)
                    continue;

                if (!categoryFoldouts.ContainsKey(category))
                    categoryFoldouts[category] = true;

                categoryFoldouts[category] = EditorGUILayout.Foldout(
                    categoryFoldouts[category],
                    $"{category} ({list.Count})",
                    true);

                if (!categoryFoldouts[category])
                    continue;

                using (new EditorGUI.IndentLevelScope())
                {
                    for (int i = 0; i < list.Count; i++)
                    {
                        string path = list[i];
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            var obj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
                            EditorGUILayout.ObjectField(obj, typeof(UnityEngine.Object), false);

                            if (GUILayout.Button("Ping", GUILayout.Width(45)))
                            {
                                if (obj != null)
                                {
                                    EditorGUIUtility.PingObject(obj);
                                    Selection.activeObject = obj;
                                }
                            }

                            if (GUILayout.Button("Del", GUILayout.Width(35)))
                            {
                                bool ok = EditorUtility.DisplayDialog(
                                    "Delete asset?",
                                    $"{path}\n\nThis will permanently delete the asset. Continue?",
                                    "Delete",
                                    "Cancel");
                                if (ok)
                                {
                                    DeleteAssets(new List<string> { path });
                                    FindUnusedAssets();
                                    EditorGUILayout.EndScrollView();
                                    return;
                                }
                            }

                            if (GUILayout.Button("X", GUILayout.Width(22)))
                            {
                                RemovePathsFromListing(new List<string> { path });
                                EditorGUILayout.EndScrollView();
                                return;
                            }
                        }
                    }
                }

                EditorGUILayout.Space(4);
            }
        }
        EditorGUILayout.EndScrollView();
    }

    static bool IsSceneInBuild(string scenePath, bool includeDisabled)
    {
        var scenes = EditorBuildSettings.scenes;
        for (int i = 0; i < scenes.Length; i++)
        {
            if (!includeDisabled && !scenes[i].enabled)
                continue;

            if (string.Equals(scenes[i].path, scenePath, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    static IEnumerable<string> GetBuildScenePaths(bool includeDisabled)
    {
        foreach (var s in EditorBuildSettings.scenes)
        {
            if (!includeDisabled && !s.enabled)
                continue;

            if (!string.IsNullOrEmpty(s.path))
                yield return s.path;
        }
    }

    void FindUnusedAssets()
    {
        unusedPaths.Clear();
        unusedByCategory.Clear();
        unusedByFolder.Clear();

        var buildScenes = GetBuildScenePaths(includeDisabledBuildScenes).ToArray();
        if (buildScenes.Length == 0)
        {
            EditorUtility.DisplayDialog("Unused Assets Finder", "No scenes found in Build Settings.", "OK");
            return;
        }

        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            EditorUtility.DisplayProgressBar("Unused Assets Finder", "Collecting dependencies from Build Settings scenes...", 0f);

            for (int i = 0; i < buildScenes.Length; i++)
            {
                string scenePath = buildScenes[i];
                float p = (float)i / Mathf.Max(1, buildScenes.Length);
                EditorUtility.DisplayProgressBar("Unused Assets Finder", $"Scanning: {Path.GetFileName(scenePath)}", p);

                used.Add(NormalizeAssetPath(scenePath));

                string[] deps = AssetDatabase.GetDependencies(scenePath, true);
                for (int d = 0; d < deps.Length; d++)
                    used.Add(NormalizeAssetPath(deps[d]));
            }

            if (excludeResourcesFolder)
                AddFolderAsUsed("Assets/Resources", used);

            if (excludeStreamingAssetsFolder)
                AddFolderAsUsed("Assets/StreamingAssets", used);

            if (excludePluginsFolder)
                AddFolderAsUsed("Assets/Plugins", used);

            foreach (var folder in ParseExtraExcludedFolders(extraExcludeFolders))
                AddFolderAsUsed(folder, used);

            string[] all = AssetDatabase.GetAllAssetPaths();
            string scopeFolderPath = scopeFolder != null ? NormalizeAssetPath(AssetDatabase.GetAssetPath(scopeFolder)) : null;
            bool useScope = !string.IsNullOrEmpty(scopeFolderPath) && AssetDatabase.IsValidFolder(scopeFolderPath);

            EditorUtility.DisplayProgressBar("Unused Assets Finder", "Filtering unused assets...", 0.95f);

            for (int i = 0; i < all.Length; i++)
            {
                string path = all[i];

                if (useScope)
                {
                    string norm = NormalizeAssetPath(path);
                    if (!norm.StartsWith(scopeFolderPath + "/", StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(norm, scopeFolderPath, StringComparison.OrdinalIgnoreCase))
                        continue;
                }

                if (!path.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (AssetDatabase.IsValidFolder(path))
                    continue;

                if (IsExcludedByExtension(path))
                    continue;

                // Ignore editor-only scripts/assets by path
                if (path.IndexOf("/Editor/", StringComparison.OrdinalIgnoreCase) >= 0)
                    continue;

                // Scenes: if not in build settings then user asked to list
                if (path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
                {
                    if (!IsSceneInBuild(path, includeDisabledBuildScenes))
                    {
                        unusedPaths.Add(path);
                        continue;
                    }
                }

                if (!used.Contains(NormalizeAssetPath(path)))
                    unusedPaths.Add(path);
            }

            unusedPaths = unusedPaths.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(p => p).ToList();

            BuildCategories();
            BuildFolders();
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            Repaint();
        }
    }

    void BuildCategories()
    {
        unusedByCategory.Clear();

        for (int i = 0; i < unusedPaths.Count; i++)
        {
            string path = unusedPaths[i];
            string category = GetCategory(path);
            if (!unusedByCategory.TryGetValue(category, out var list))
            {
                list = new List<string>();
                unusedByCategory[category] = list;
            }
            list.Add(path);
        }

        foreach (var kv in unusedByCategory)
        {
            kv.Value.Sort(StringComparer.OrdinalIgnoreCase);
            if (!categoryFoldouts.ContainsKey(kv.Key))
                categoryFoldouts[kv.Key] = true;
        }
    }

    void BuildFolders()
    {
        unusedByFolder.Clear();

        for (int i = 0; i < unusedPaths.Count; i++)
        {
            string path = unusedPaths[i];
            string folder = NormalizeAssetPath(Path.GetDirectoryName(path) ?? string.Empty);
            if (string.IsNullOrEmpty(folder))
                folder = "Assets";

            if (!unusedByFolder.TryGetValue(folder, out var list))
            {
                list = new List<string>();
                unusedByFolder[folder] = list;
            }
            list.Add(path);
        }

        foreach (var kv in unusedByFolder)
        {
            kv.Value.Sort(StringComparer.OrdinalIgnoreCase);
            if (!folderFoldouts.ContainsKey(kv.Key))
                folderFoldouts[kv.Key] = true;
        }
    }

    static IEnumerable<string> ParseExtraExcludedFolders(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            yield break;

        var parts = text
            .Split(new[] { '\r', '\n', ';', ',' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(p => NormalizeAssetPath(p.Trim()))
            .Where(p => !string.IsNullOrEmpty(p))
            .Distinct(StringComparer.OrdinalIgnoreCase);

        foreach (var p in parts)
        {
            if (p.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase) && AssetDatabase.IsValidFolder(p))
                yield return p;
        }
    }

    void RemovePathsFromListing(List<string> paths)
    {
        if (paths == null || paths.Count == 0)
            return;

        for (int i = 0; i < paths.Count; i++)
            unusedPaths.Remove(paths[i]);

        unusedPaths = unusedPaths.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(p => p).ToList();
        BuildCategories();
        BuildFolders();
        Repaint();
    }

    static string GetCategory(string path)
    {
        string ext = Path.GetExtension(path).ToLowerInvariant();

        if (path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase)) return "Scenes";
        if (path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase)) return "Prefabs";
        if (path.EndsWith(".mat", StringComparison.OrdinalIgnoreCase)) return "Materials";
        if (path.EndsWith(".controller", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".overrideController", StringComparison.OrdinalIgnoreCase)) return "Animators";
        if (path.EndsWith(".anim", StringComparison.OrdinalIgnoreCase)) return "Animations";
        if (path.EndsWith(".asset", StringComparison.OrdinalIgnoreCase)) return "ScriptableObjects";
        if (path.EndsWith(".shader", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".shadergraph", StringComparison.OrdinalIgnoreCase)) return "Shaders";

        if (ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".tga" || ext == ".psd" || ext == ".tif" || ext == ".tiff" || ext == ".exr" || ext == ".hdr")
            return "Textures";

        if (ext == ".fbx" || ext == ".obj" || ext == ".dae" || ext == ".blend")
            return "Models";

        if (ext == ".wav" || ext == ".mp3" || ext == ".ogg" || ext == ".aiff" || ext == ".aif")
            return "Audio";

        if (ext == ".ttf" || ext == ".otf")
            return "Fonts";

        if (ext == ".physicmaterial" || ext == ".physicsmaterial2d")
            return "Physics Materials";

        if (ext == ".rendertexture")
            return "RenderTextures";

        return "Other";
    }

    static string NormalizeAssetPath(string path)
    {
        if (string.IsNullOrEmpty(path))
            return string.Empty;
        return path.Replace('\\', '/');
    }

    static void AddFolderAsUsed(string folderPath, HashSet<string> used)
    {
        if (!AssetDatabase.IsValidFolder(folderPath))
            return;

        string[] guids = AssetDatabase.FindAssets(string.Empty, new[] { folderPath });
        foreach (string guid in guids)
        {
            string p = AssetDatabase.GUIDToAssetPath(guid);
            if (!string.IsNullOrEmpty(p) && !AssetDatabase.IsValidFolder(p))
                used.Add(NormalizeAssetPath(p));
        }
    }

    static bool IsExcludedByExtension(string path)
    {
        string ext = Path.GetExtension(path);
        if (string.IsNullOrEmpty(ext))
            return false;

        ext = ext.ToLowerInvariant();

        // user asked: ignore scripts/assemblies
        if (ext == ".cs" || ext == ".asmdef" || ext == ".dll")
            return true;

        // ignore meta
        if (ext == ".meta")
            return true;

        return false;
    }

    static void DeleteAssets(List<string> paths)
    {
        if (paths == null || paths.Count == 0) return;

        AssetDatabase.StartAssetEditing();
        try
        {
            for (int i = 0; i < paths.Count; i++)
            {
                string p = paths[i];
                if (string.IsNullOrEmpty(p)) continue;
                if (!p.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase)) continue;
                if (AssetDatabase.IsValidFolder(p)) continue;
                if (p.IndexOf("/Editor/", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                if (IsExcludedByExtension(p)) continue;

                AssetDatabase.DeleteAsset(p);
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
            AssetDatabase.Refresh();
        }
    }
}
