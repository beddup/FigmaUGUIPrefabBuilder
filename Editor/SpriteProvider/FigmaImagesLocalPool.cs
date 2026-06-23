using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System;
using System.IO;
using Color = UnityEngine.Color;

namespace FigmaImporter.Editor
{
    public class FigmaImagesLocalPool : ScriptableObject
    {
        [Serializable]
        public class NodeImageItem
        {
            public string IdForDownload;
            public string guid;
            public string md5;
            public string url;
        }

        public List<NodeImageItem> NodesImages;
        
        public static FigmaImagesLocalPool LoadPool()
        {
            var assetIds = AssetDatabase.FindAssets($"t:{typeof(FigmaImagesLocalPool)}");
            if (assetIds == null || assetIds.Length == 0)
            { 
                var newPool = CreateInstance<FigmaImagesLocalPool>();
                newPool.NodesImages = new List<NodeImageItem>();
                AssetDatabase.CreateAsset(newPool, "Assets/FigmaAssets/FigmaImagesLocalPool.asset");
                AssetDatabase.Refresh();
                return newPool;
            }

            var pool = AssetDatabase.LoadAssetAtPath<FigmaImagesLocalPool>(AssetDatabase.GUIDToAssetPath(assetIds[0]));
            pool.RemoveInvalidItems();
            return pool;
        }
        

        public bool HasSprite(UGUIPrefabNode prefabNode)
        {
            var result = NodesImages.Find(item => item.IdForDownload == prefabNode.node.IdForDownloadImage());
            if (result != null)
            {
                var path = AssetDatabase.GUIDToAssetPath(result.guid);
                return !string.IsNullOrEmpty(path);
            }
            return false;
        }
        
        public Sprite GetSprite(UGUIPrefabNode prefabNode)
        {
            var result = NodesImages.Find(item => item.IdForDownload == prefabNode.node.IdForDownloadImage());
            if (result != null)
            {
                var path = AssetDatabase.GUIDToAssetPath(result.guid);
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                return sprite;
            }
            return null;
        }

        public void RegisterDownloadUrl(string downloadId, string url)
        {
            var item = NodesImages.Find(i => i.IdForDownload == downloadId);
            if (item != null)
            {
                item.url = url;
            }
            else
            {
                NodesImages.Add(new NodeImageItem
                {
                    IdForDownload = downloadId,
                    url = url
                });
            }
        }

        /// <summary>重试下载后更新条目，下载成功会清除 url</summary>
        public void UpdateImageData(string downloadId, byte[] data, string spritesSaveFolder)
        {
            var item = NodesImages.Find(i => i.IdForDownload == downloadId);
            if (item == null) return;

            string dataHash = Md5Sum(data);
            item.md5 = dataHash;

            // MD5 去重：复用已有的 guid
            var sameMd5Item = NodesImages.Find(i => i.md5 == dataHash && !string.IsNullOrEmpty(i.guid));
            if (sameMd5Item != null && sameMd5Item != item)
            {
                item.guid = sameMd5Item.guid;
                return;
            }

            // 写入磁盘
            if (!Directory.Exists(spritesSaveFolder)) Directory.CreateDirectory(spritesSaveFolder);
            var safeName = downloadId.Replace(':', '_');
            string spritePath = Path.Combine(spritesSaveFolder, $"{safeName}.png");
            File.WriteAllBytes(spritePath, data);
            AssetDatabase.Refresh();
            var importer = AssetImporter.GetAtPath(spritePath) as TextureImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(spritePath);
            item.guid = AssetDatabase.AssetPathToGUID(spritePath);
        }

        public void SaveImageData(UGUIPrefabNode prefabNode, byte[] data, string spritesSaveFolder)
        {
            string dataHash = Md5Sum(data);
            string downloadId = prefabNode.node.IdForDownloadImage();

            // 1. 查找是否已有相同 IdForDownload 的记录
            var existingItem = NodesImages.Find(item => item.IdForDownload == downloadId);
            if (existingItem != null)
            {
                // MD5 没变 → 无需重新写文件
                if (existingItem.md5 == dataHash) return;
                // MD5 变了 → 更新图片文件
                WriteImageFile(prefabNode, data, dataHash, spritesSaveFolder, existingItem);
                existingItem.md5 = dataHash;
                return;
            }

            // 2. 查找是否有相同 MD5 的其他记录（图片内容去重）
            var sameMd5Item = NodesImages.Find(item => item.md5 == dataHash);
            if (sameMd5Item != null)
            {
                NodesImages.Add(new NodeImageItem
                {
                    IdForDownload = downloadId,
                    md5 = dataHash,
                    guid = sameMd5Item.guid
                });
                return;
            }

            // 3. 全新图片
            var newItem = new NodeImageItem
            {
                IdForDownload = downloadId,
                md5 = dataHash
            };
            WriteImageFile(prefabNode, data, dataHash, spritesSaveFolder, newItem);
            NodesImages.Add(newItem);
        }

        private void WriteImageFile(UGUIPrefabNode prefabNode, byte[] data, string dataHash, string spritesSaveFolder, NodeImageItem item)
        {
            if (!Directory.Exists(spritesSaveFolder)) Directory.CreateDirectory(spritesSaveFolder);
            string spritePath = Path.Combine(spritesSaveFolder, $"{prefabNode.gameObjectName}.png");

            if (File.Exists(spritePath))
            {
                var existImageHash = Md5Sum(File.ReadAllBytes(spritePath));
                if (existImageHash != dataHash)
                {
                    spritePath = Path.Combine(spritesSaveFolder, $"{prefabNode.gameObjectName}_{prefabNode.nodeId}.png");
                }
            }

            File.WriteAllBytes(spritePath, data);
            AssetDatabase.Refresh();
            TextureImporter textureImporter = AssetImporter.GetAtPath(spritePath) as TextureImporter;
            textureImporter.textureType = TextureImporterType.Sprite;
            textureImporter.spriteImportMode = SpriteImportMode.Single;
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(spritePath);
            item.guid = AssetDatabase.AssetPathToGUID(spritePath);
        }

        public void RemoveInvalidItems()
        {
            if (NodesImages == null)
            {
                NodesImages = new List<NodeImageItem>();
            }

            for (int i = 0; i < NodesImages.Count; i ++)
            {
                var item = NodesImages[i];

                var path = AssetDatabase.GUIDToAssetPath(item.guid);
                if (string.IsNullOrEmpty(item.IdForDownload) ||
                    string.IsNullOrEmpty(item.md5) ||
                    string.IsNullOrEmpty(path) ||
                    !File.Exists(path))
                {
                    Debug.Log($"Did not find asset for download id {item.IdForDownload}, guid {item.guid}, path {path}, will delete this item");
                    NodesImages.RemoveAt(i);
                    i--;
                }
            }
            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
        
        private string Md5Sum(byte[] bytes)
        {
            var md5 = new System.Security.Cryptography.MD5CryptoServiceProvider();
            byte[] hashBytes = md5.ComputeHash(bytes);

            string hashString = "";
            for (int i = 0; i < hashBytes.Length; i++)
            {
                hashString += System.Convert.ToString(hashBytes[i], 16).PadLeft(2, '0');
            }

            return hashString.PadLeft(32, '0');
        }
    }

    [CustomEditor(typeof(FigmaImagesLocalPool))]
    public class FigmaImagesLocalPoolInspector : UnityEditor.Editor
    {
        private FigmaImagesLocalPool pool;
        private Vector2 scrollPos;
        private int currentPage;
        private const int PageSize = 10;

        private int TotalPages => Mathf.Max(1, Mathf.CeilToInt((pool.NodesImages?.Count ?? 0) / (float)PageSize));

        private void OnEnable()
        {
            pool = (FigmaImagesLocalPool)target;
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            int ItemCount() => pool.NodesImages?.Count ?? 0;

            // 操作按钮
            EditorGUILayout.BeginHorizontal();
            GUI.backgroundColor = new Color(1f, 0.8f, 0.4f);
            if (GUILayout.Button("Clean Invalid", GUILayout.Height(24)))
            {
                pool.RemoveInvalidItems();
                EditorUtility.SetDirty(pool);
                serializedObject.Update();
                currentPage = Mathf.Min(currentPage, TotalPages - 1);
            }
            GUI.backgroundColor = new Color(1f, 0.5f, 0.4f);
            if (GUILayout.Button("Clear All", GUILayout.Height(24)))
            {
                if (EditorUtility.DisplayDialog("Clear All",
                    $"Clear all {ItemCount()} cached record(s)? (image files will be kept)", "Yes", "Cancel"))
                {
                    ClearAll();
                }
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);

            // 分页导航
            if (ItemCount() > PageSize)
            {
                EditorGUILayout.BeginHorizontal();
                GUI.enabled = currentPage > 0;
                if (GUILayout.Button("◀ Prev", GUILayout.Width(70)))
                {
                    currentPage--;
                    scrollPos = Vector2.zero;
                }
                GUI.enabled = true;
                GUILayout.FlexibleSpace();
                GUILayout.Label($"Page {currentPage + 1} / {TotalPages}  ({ItemCount()} total)", EditorStyles.centeredGreyMiniLabel);
                GUILayout.FlexibleSpace();
                GUI.enabled = currentPage < TotalPages - 1;
                if (GUILayout.Button("Next ▶", GUILayout.Width(70)))
                {
                    currentPage++;
                    scrollPos = Vector2.zero;
                }
                GUI.enabled = true;
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.Space(2);
            }

            // 图片列表
            if (ItemCount() > 0)
            {
                int startIndex = currentPage * PageSize;
                int endIndex = Mathf.Min(startIndex + PageSize, pool.NodesImages.Count);

                scrollPos = EditorGUILayout.BeginScrollView(scrollPos, GUILayout.ExpandHeight(true));
                for (int i = startIndex; i < endIndex; i++)
                {
                    var item = pool.NodesImages[i];
                    var path = AssetDatabase.GUIDToAssetPath(item.guid);
                    bool exists = !string.IsNullOrEmpty(path) && File.Exists(path);

                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                    var sprite = exists ? AssetDatabase.LoadAssetAtPath<Sprite>(path) : null;
                    var preview = sprite != null ? AssetPreview.GetAssetPreview(sprite) : null;

                    // 先渲染信息区，左侧留出预览的空间
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Space(56 + 12);
                    EditorGUILayout.BeginVertical();
                    GUI.color = exists ? Color.white : Color.red;
                    EditorGUILayout.LabelField($"#{i}  {item.IdForDownload}", EditorStyles.boldLabel);
                    GUI.color = Color.white;
                    InfoLine("Path", exists ? path : "<missing>");
                    InfoLine("GUID", item.guid);
                    InfoLine("MD5", item.md5);
                    EditorGUILayout.EndVertical();
                    EditorGUILayout.EndHorizontal();

                    // 根据信息区高度居中绘制预览
                    var rowRect = GUILayoutUtility.GetLastRect();
                    var previewRect = new Rect(rowRect.x + 4, rowRect.y + (rowRect.height - 56) / 2f, 56, 56);
                    if (preview != null)
                    {
                        if (GUI.Button(previewRect, preview, GUIStyle.none))
                        {
                            EditorGUIUtility.PingObject(sprite);
                        }
                    }
                    else
                    {
                        EditorGUI.DrawRect(previewRect, new Color(0.2f, 0.2f, 0.2f, 0.5f));
                        GUI.Label(previewRect, "No\nPreview", EditorStyles.centeredGreyMiniLabel);
                    }

                    // 下载按钮（有 URL 但无图片时显示）
                    if (!exists && !string.IsNullOrEmpty(item.url))
                    {
                        EditorGUILayout.BeginHorizontal();
                        GUILayout.Space(56 + 12);
                        if (GUILayout.Button("Download", GUILayout.Height(20)))
                        {
                            DownloadItem(item);
                        }
                        EditorGUILayout.EndHorizontal();
                    }

                    EditorGUILayout.EndVertical();
                }
                EditorGUILayout.EndScrollView();
            }

            serializedObject.ApplyModifiedProperties();
        }

        private static void InfoLine(string label, string value)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(35));
            GUILayout.Space(10);
            GUILayout.Label(value);
            EditorGUILayout.EndHorizontal();
        }

        private async void DownloadItem(FigmaImagesLocalPool.NodeImageItem item)
        {
            var folder = GetSpriteSaveFolder();
            if (await FigmaImagesProvider.DownloadSingleImage(item.url, item.IdForDownload, pool, folder))
            {
                serializedObject.Update();
                Repaint();
            }
        }

        private static string GetSpriteSaveFolder()
        {
            var guids = AssetDatabase.FindAssets("t:FigmaToUGUIConfig");
            if (guids.Length > 0)
            {
                var config = AssetDatabase.LoadAssetAtPath<FigmaToUGUIConfig>(AssetDatabase.GUIDToAssetPath(guids[0]));
                if (config != null && !string.IsNullOrEmpty(config.SpriteSaveFolder))
                    return config.SpriteSaveFolder;
            }
            return "Assets/Sprites";
        }

        private void ClearAll()
        {
            pool.NodesImages.Clear();
            currentPage = 0;
            EditorUtility.SetDirty(pool);
            AssetDatabase.SaveAssets();
            serializedObject.Update();
        }
    }
}