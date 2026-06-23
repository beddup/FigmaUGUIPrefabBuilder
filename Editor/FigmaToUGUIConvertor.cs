using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using System.Linq;
using System.Threading.Tasks;
using FigmaClient;

namespace FigmaImporter.Editor
{
    public class FigmaToUGUIConvertor
    {
        public static async Task ConvertFigmaToUGUIPrefab(FigmaToUGUIConfig config, string fileKey, string figmaToken, string figmaRawContent, string prefabHierarchy)
        {
            var hierarchyRoot = UGUIPrefabNode.FromJson(prefabHierarchy);
            var rootNode = new FigmaNodeParser().ParseNode<Node>(figmaRawContent);
            UGUIPrefabNode.BindNode(hierarchyRoot, rootNode);
            
            // 创建层级结构
            GameObject canvasGameObject = GameObject.FindAnyObjectByType<Canvas>().gameObject;

            for (int i = 0; i < canvasGameObject.transform.childCount; i++)
            {
                canvasGameObject.transform.GetChild(i).gameObject.SetActive(false);
            }
            
            var generator = new FigmaNodeGameObjectGenerator(); 
            var allNodes = hierarchyRoot.GetALlNodes();
            foreach (var node  in allNodes)
            {
                var parentNode = hierarchyRoot.FindDirectParent(node);
                bool rootGo = parentNode?.gameObject == null;
                var parentGameObject = rootGo ? canvasGameObject : parentNode.gameObject;
                generator.GenerateNodeGameObject(node, parentGameObject);
                if (rootGo)
                {
                    var rootRT = node.gameObject.transform as RectTransform;
                    rootRT.anchorMin = Vector2.zero;
                    rootRT.anchorMax = Vector2.one;
                    rootRT.offsetMin = Vector2.zero;
                    rootRT.offsetMax = Vector2.zero;
                    rootRT.pivot = Vector2.one * 0.5f;
                }
            }
            
            List<UGUIPrefabNode> imageNodeForDownload = new List<UGUIPrefabNode>();
            
            // 添加component
            foreach (var hierarchyNode in allNodes)
            {
                var node = hierarchyNode.node;
                if (node == null) continue; // hierarchyNode 可能为凭空创建的组织节点，没有对应的figma node
                var gameObject = hierarchyNode.gameObject;
                // generator.SetMask(node, gameObject);

                if (hierarchyNode.renderType == NodeRenderType.Image)
                {
                    bool alreadyInLocalPool = generator.ApplyImageFromPool(hierarchyNode);
                    if (!alreadyInLocalPool)
                    {
                        imageNodeForDownload.Add(hierarchyNode);
                    }
                }

                if (hierarchyNode.renderType == NodeRenderType.Color)
                {
                    generator.AddColor(node, gameObject);
                }

                if (hierarchyNode.renderType == NodeRenderType.Text)
                {
                    if (node.type != "TEXT")
                    {
                        node = node.children.First(item => item.type != "TEXT") ?? hierarchyNode.node;
                    }
                    var tmpFont = config.GetFont(node);

                    generator.AddText(node, gameObject, tmpFont, config.MaterialSaveFolder);
                }

                if (hierarchyNode.isButton)
                {
                    generator.AddButton(gameObject);
                }
            }

            if (imageNodeForDownload.Count > 0)
            {
                
                var downloadIds = imageNodeForDownload.Select(n => n.node.IdForDownloadImage()).Distinct().ToList();
                var urlMap = await Client.GetImageDownloadUrls(fileKey, downloadIds, figmaToken);
                if (urlMap != null && urlMap.Count > 0)
                {
                    await FigmaImagesProvider.DownloadImages(urlMap, imageNodeForDownload, config.SpriteSaveFolder,
                        node => generator.ApplyImageFromPool(node));
                }
            }

            // Save prefabs
            var prefabFolder = string.IsNullOrEmpty(config.PrefabSaveFolder) ? "Assets/Prefabs" : config.PrefabSaveFolder;
            if (!System.IO.Directory.Exists(prefabFolder))
                System.IO.Directory.CreateDirectory(prefabFolder);

            for (int i = 0; i < canvasGameObject.transform.childCount; i++)
            {
                var child = canvasGameObject.transform.GetChild(i).gameObject;
                if (!child.activeSelf) continue;
                string prefabPath = System.IO.Path.Combine(prefabFolder, $"{child.name}.prefab");
                PrefabUtility.SaveAsPrefabAssetAndConnect(child, prefabPath, InteractionMode.AutomatedAction);
                Debug.Log($"[FigmaToUGUI] Saved prefab: {prefabPath}");
            }
        }

        private static void CaptureCanvasScreenshot(GameObject canvasGameObject, string screenshotSavePath)
        {
            Camera camera = canvasGameObject.GetComponent<Canvas>().worldCamera;
            if (camera == null) camera = Camera.main;
            if (camera == null)
            {
                Debug.LogWarning("No camera found for canvas screenshot");
                return;
            }

            int width = Screen.width;
            int height = Screen.height;
            var renderTexture = new RenderTexture(width, height, 24);
            var previousRT = camera.targetTexture;
            camera.targetTexture = renderTexture;
            camera.Render();

            RenderTexture.active = renderTexture;
            var screenshot = new Texture2D(width, height, TextureFormat.RGB24, false);
            screenshot.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            screenshot.Apply();

            camera.targetTexture = previousRT;
            RenderTexture.active = null;

            string fullPath = System.IO.Path.Combine("Library/FigmaToUGUI", screenshotSavePath);
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(fullPath));
            System.IO.File.WriteAllBytes(fullPath, screenshot.EncodeToPNG());
            Debug.Log($"Screenshot saved to {fullPath}");

            Object.DestroyImmediate(screenshot);
            Object.DestroyImmediate(renderTexture);
        }
    }
}