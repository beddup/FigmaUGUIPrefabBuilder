using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;
using UnityEditor;
using System.Threading.Tasks;
using FigmaClient.Editor;

namespace FigmaImporter.Editor
{
    public class FigmaImagesProvider
    {
        private static FigmaImagesLocalPool LocalImages;

        private static void EnsurePool()
        {
            if (LocalImages == null)
            {
                LocalImages = FigmaImagesLocalPool.LoadPool();
            }
        }

        // 这里没有考虑 node 内容变化的情况，比如美术修改了某个component
        public static Sprite GetLocalSpriteForNode(UGUIPrefabNode hierarchyNode)
        {
            EnsurePool();
            if (LocalImages.HasSprite(hierarchyNode))
            {
                return LocalImages.GetSprite(hierarchyNode);
            }
            return null;
        }

        public static async Task DownloadImages(Dictionary<string, string> urlMap, List<UGUIPrefabNode> imageNodes, string spriteSaveFolder, Action<UGUIPrefabNode> onImageSaved = null)
        {
            EnsurePool();
            if (urlMap == null || urlMap.Count == 0 || imageNodes == null || imageNodes.Count == 0)
            {
                Debug.LogError("[FigmaImporter] no url map or imageNodes ");
                return;
            }

            // 先把 URL 注册到 Pool，以便下载失败时可重试
            foreach (var urlInfo in urlMap)
            {
                LocalImages.RegisterDownloadUrl(urlInfo.Key, urlInfo.Value);
            }

            // 限制最大并发下载数为 5，每下载完一张立即保存并回调
            var semaphore = new SemaphoreSlim(5);
            var tasks = urlMap.Select(async urlInfo =>
            {
                var imageUrl = urlInfo.Value;
                var downloadId = urlInfo.Key;
                await semaphore.WaitAsync();
                try
                {
                    var data = await Client.DownloadImageDataAsync(imageUrl, downloadId);
                    if (data != null)
                    {
                        foreach (var imageNode in imageNodes)
                        {
                            if (imageNode.node.IdForDownloadImage() == downloadId)
                            {
                                LocalImages.SaveImageData(imageNode, data, spriteSaveFolder);
                                onImageSaved?.Invoke(imageNode);
                            }
                        }
                    }
                }
                finally
                {
                    semaphore.Release();
                }
            }).ToList();

            await Task.WhenAll(tasks);
            EditorUtility.SetDirty(LocalImages);
        }

        public static async Task<bool> DownloadSingleImage(string imageUrl, string downloadId, FigmaImagesLocalPool pool, string spriteSaveFolder)
        {
            var data = await Client.DownloadImageDataAsync(imageUrl, downloadId);
            if (data != null)
            {
                pool.UpdateImageData(downloadId, data, spriteSaveFolder);
                EditorUtility.SetDirty(pool);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                return true;
            }
            return false;
        }
        
    }
}