using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using FigmaClient;
using Figma.TMPStyler.Editor;


namespace FigmaImporter.Editor
{
    public class FigmaNodeGameObjectGenerator
    {
        Vector2 figmaRootPosition = Vector2.zero;
        
        public void GenerateNodeGameObject(UGUIPrefabNode hierarchyNode, GameObject parent)
            // , string token, Func<Node, TMP_FontAsset> tmpFontProvider, string assetSaveFolder)
        {
            if (parent == null)
            {
                throw new Exception("[FigmaImporter] Parent is null. Set the canvas reference.");
            }

            // 为 Node 创建GameOjbect 
            GameObject nodeGo = new GameObject(hierarchyNode.gameObjectName);
            RectTransform parentT = parent.GetComponent<RectTransform>();
            RectTransform rectTransform = nodeGo.AddComponent<RectTransform>();
            SetParent(parentT, rectTransform);
            hierarchyNode.gameObject = nodeGo;

            var boundingBox = hierarchyNode.GetBoundingBox();
            if (boundingBox != null)
            {
                var isParentCanvas = parent.GetComponent<Canvas>();
                if (isParentCanvas) figmaRootPosition = boundingBox.GetPosition();
                SetPosition(parentT, rectTransform, boundingBox, hierarchyNode);
                // if (!isParentCanvas) SetConstraints(parentT, rectTransform, node.constraints);
            }
            ReCalNodeLayout(parentT, rectTransform, hierarchyNode);
        }
        
        private void SetParent(RectTransform parentT, RectTransform rectTransform)
        {
            rectTransform.SetParent(parentT);
            rectTransform.localScale = Vector3.one;
        }
        
        // 文字节点宽度/高度的扩展比例，避免 Unity 中文字折行
        private const float TEXT_SIZE_SCALE = 1.1f;

        private void SetPosition(RectTransform parent, RectTransform rectTransform, AbsoluteBoundingBox boundingBox, UGUIPrefabNode hierarchyNode)
        {
            var canvas = parent.GetComponentInParent<Canvas>();
            rectTransform.pivot = Vector2.up;
            var newPosition = boundingBox.GetPosition() - figmaRootPosition;
            var v = ConvertVector((RectTransform)canvas.transform, newPosition);

            float width = boundingBox.width;
            float height = boundingBox.height;
            if (hierarchyNode.renderType == NodeRenderType.Text)
            {
                width *= TEXT_SIZE_SCALE;
                height *= TEXT_SIZE_SCALE;
            }

            rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
            rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);

            rectTransform.position = v;
        }

        private void ReCalNodeLayout(RectTransform parent, RectTransform rectTransform, UGUIPrefabNode hierarchyNode)
        {
            bool isHorizontalStretch = string.Equals(hierarchyNode.horizontal_alignment, "stretch", StringComparison.InvariantCultureIgnoreCase);
            bool isVerticalStretch = string.Equals(hierarchyNode.vertical_alignment, "stretch", StringComparison.InvariantCultureIgnoreCase);

            Vector2 newAnchorMin = rectTransform.anchorMin;
            Vector2 newAnchorMax = rectTransform.anchorMax;

            if (isHorizontalStretch)
            {
                newAnchorMin.x = 0f;
                newAnchorMax.x = 1f;
            }
            else
            {
                float hVal = GetHorizontalAlignmentValue(hierarchyNode.horizontal_alignment);
                newAnchorMin.x = hVal;
                newAnchorMax.x = hVal;
            }

            if (isVerticalStretch)
            {
                newAnchorMin.y = 0f;
                newAnchorMax.y = 1f;
            }
            else
            {
                float vVal = GetVerticalAlignmentValue(hierarchyNode.vertical_alignment);
                newAnchorMin.y = vVal;
                newAnchorMax.y = vVal;
            }

            // If no stretch (all point anchors), use the original position-preserving recalculation
            if (!isHorizontalStretch && !isVerticalStretch)
            {
                var targetAnchor = newAnchorMin; // anchorMin == anchorMax
                var targetPivot = new Vector2(0.5f, 0.5f);
                var currentPivotLocalPosition = rectTransform.anchoredPosition +
                                                (rectTransform.anchorMin - Vector2.one * 0.5f) * parent.rect.size;
                var targetPivotLocalPosition = currentPivotLocalPosition +
                                               (targetPivot - rectTransform.pivot) * rectTransform.rect.size;

                rectTransform.pivot = targetPivot;
                rectTransform.anchorMin = targetAnchor;
                rectTransform.anchorMax = targetAnchor;
                rectTransform.anchoredPosition = targetPivotLocalPosition -
                                                 (targetAnchor - Vector2.one * 0.5f) * parent.rect.size;
                return;
            }

            // At least one axis uses stretch: compute current edge positions in parent space,
            // then apply new anchors and recompute offsets to maintain the same visual position.
            Vector2 offsetMin = rectTransform.offsetMin;
            Vector2 offsetMax = rectTransform.offsetMax;
            Vector2 parentSize = parent.rect.size;

            Vector2 leftBottom = rectTransform.anchorMin * parentSize + offsetMin;
            Vector2 rightTop = rectTransform.anchorMax * parentSize + offsetMax;

            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMin = newAnchorMin;
            rectTransform.anchorMax = newAnchorMax;

            rectTransform.offsetMin = leftBottom - newAnchorMin * parentSize;
            rectTransform.offsetMax = rightTop - newAnchorMax * parentSize;
        }

        private float GetHorizontalAlignmentValue(string alignment)
        {
            if (string.Equals(alignment, "left", StringComparison.InvariantCultureIgnoreCase)) return 0f;
            if (string.Equals(alignment, "right", StringComparison.InvariantCultureIgnoreCase)) return 1f;
            return 0.5f;
        }

        private float GetVerticalAlignmentValue(string alignment)
        {
            if (string.Equals(alignment, "bottom", StringComparison.InvariantCultureIgnoreCase)) return 0f;
            if (string.Equals(alignment, "top", StringComparison.InvariantCultureIgnoreCase)) return 1f;
            return 0.5f;
        }
        

        public void AddColor(Node node, GameObject nodeGo)
        {
            var image = nodeGo.AddComponent<Image>();
            image.color = node.GetSolidFillColor();
        }


        public void AddText(Node node, GameObject nodeGo, TMP_FontAsset tmpFont, string materialSaveFolder)
        {
            var tmpText = nodeGo.AddComponent<TextMeshProUGUI>();
            tmpText.text = node.characters;

            var matInfo = TMPMaterialProvider.GetTMPMaterial(node, tmpFont, materialSaveFolder, false);
            if (matInfo == null)
            {
                Debug.LogError($"[Figma Importer] cannot find font for node {node.name}({node.id})");
                return;
            }
            
            var style = node.style;

            tmpText.font = matInfo.Font;
            tmpText.fontSize = style.fontSize;


            tmpText.fontMaterial = matInfo.OutlineAndDropShadow ?? (matInfo.InnerShadow ?? tmpText.font.material);

            // alignment
            var verticalAlignment = style.textAlignVertical;
            var horizontalAlignment = style.textAlignHorizontal;
            int alignment = 0;
            alignment += (verticalAlignment == "TOP" ? 1 : 0) << 8;
            alignment += (verticalAlignment == "CENTER" ? 1 : 0) << 9;
            alignment += (verticalAlignment == "BOTTOM" ? 1 : 0) << 10;
            alignment += (horizontalAlignment == "LEFT" ? 1 : 0) << 0;
            alignment += (horizontalAlignment == "CENTER" ? 1 : 0) << 1;
            alignment += (horizontalAlignment == "RIGHT" ? 1 : 0) << 2;
            alignment += (horizontalAlignment == "JUSTIFIED" ? 1 : 0) << 3;
            tmpText.alignment = (TextAlignmentOptions)alignment;
            FontStyles fontStyle = 0;
            fontStyle |= (style.textDecoration == "UNDERLINE" ? FontStyles.Underline : 0);
            fontStyle |= (style.textDecoration == "STRIKETHROUGH" ? FontStyles.Strikethrough : 0);

            fontStyle |= (style.textCase == "UPPER" ? FontStyles.UpperCase : 0);
            fontStyle |= (style.textCase == "LOWER" ? FontStyles.LowerCase : 0);
            fontStyle |= (style.textCase == "SMALL_CAPS" ? FontStyles.SmallCaps : 0);
            tmpText.fontStyle = fontStyle;
            
            // color
            var fills = node.GetValidFills();
            if (fills.Count > 1) Debug.LogError($"[Figma Importer] Text node {node.name}({node.id} has multiple fill, which is not supported.");
            var fill = fills[0];
            switch (fills[0].renderType)
            {
                case Fill.FillRenderType.Color:
                    tmpText.color = fill.color.ToColor();
                    break;
                case Fill.FillRenderType.GRADIENT:
                    var preset = TMPColorGradientResolver.GetTextGradientColorPreset(fill, materialSaveFolder);
                    tmpText.enableVertexGradient = true;
                    tmpText.colorGradientPreset = preset;
                    break;
                default:
                    Debug.LogError($"[Figma Importer] do not support fill type {fills[0].renderType} in Text node {node.name}({node.id}");
                    break;
            }
            
            if (matInfo.OutlineAndDropShadow != null && matInfo.InnerShadow != null) // need to create extra gameobject for innershadow
            {
                // 同时有 outline/dropShadow 和 innerShadow，需要 clone 承载第二个材质
                var innerShadowText = GameObject.Instantiate(nodeGo, nodeGo.transform).GetComponent<TextMeshProUGUI>();
                innerShadowText.gameObject.name = $"{nodeGo.name} innershadow";
                innerShadowText.fontMaterial = matInfo.InnerShadow;
                (innerShadowText.transform as RectTransform).anchorMin = Vector2.zero;
                (innerShadowText.transform as RectTransform).anchorMax = Vector2.one;
                (innerShadowText.transform as RectTransform).offsetMin = Vector2.zero;
                (innerShadowText.transform as RectTransform).offsetMax = Vector2.zero;
            }
        }

        /// <summary>
        /// 从本地 Pool 同步设置 Image.sprite（需先调用 PrepareImagesBatch 确保图片已缓存）
        /// </summary>
        public bool ApplyImageFromPool(UGUIPrefabNode hierarchyNode)
        {
            var sprite = FigmaImagesProvider.GetLocalSpriteForNode(hierarchyNode);
            if (sprite != null)
            {
                var image = hierarchyNode.gameObject?.GetComponent<Image>();
                if (image == null)
                {
                    image = hierarchyNode.gameObject.AddComponent<Image>();
                }
                image.sprite = sprite;
                return true;
            }
            return false;
        }

        public void AddButton(GameObject nodeGo)
        {
            nodeGo.AddComponent<Button>();
        }

        public void SetMask(Node node, GameObject nodeGo)
        {
            if (!node.clipsContent)
                return;
            if (node.fills.Length == 0)
                nodeGo.AddComponent<RectMask2D>();
            else
                nodeGo.AddComponent<Mask>();
        }

        private void SetConstraints(RectTransform parentTransform, RectTransform rectTransform,
            Constraints nodeConstraints)
        {
            Vector2 offsetMin = rectTransform.offsetMin;
            Vector2 offsetMax = rectTransform.offsetMax;
            var parentSize = parentTransform.rect.size;
            Vector2 positionMin = Vector2.Scale(rectTransform.anchorMin, parentSize) + offsetMin;
            Vector2 positionMax = Vector2.Scale(rectTransform.anchorMax, parentSize) + offsetMax;

            var width = rectTransform.rect.width;
            var height = rectTransform.rect.height;
            Vector3 minAnchor = Vector2.one / 2f;
            Vector3 maxAnchor = Vector2.one / 2f;

            switch (nodeConstraints.horizontal)
            {
                case "LEFT_RIGHT":
                    minAnchor.x = 0f;
                    maxAnchor.x = 1f;
                    break;
                case "LEFT":
                    minAnchor.x = maxAnchor.x = 0f;
                    break;
                case "RIGHT":
                    minAnchor.x = maxAnchor.x = 1f;
                    break;
                case "CENTER":
                    minAnchor.x = maxAnchor.x = 0.5f;
                    break;
                case "SCALE":
                    minAnchor.x = rectTransform.anchorMin.x + rectTransform.offsetMin.x / parentTransform.rect.width;
                    maxAnchor.x = rectTransform.anchorMax.x + rectTransform.offsetMax.x / parentTransform.rect.width;
                    break;
                default:
                    Debug.LogError($"Unknown horizontal constraint {nodeConstraints.horizontal}");
                    break;
            }

            switch (nodeConstraints.vertical)
            {
                case "TOP_BOTTOM":
                    minAnchor.y = 0f;
                    maxAnchor.y = 1f;
                    break;
                case "BOTTOM":
                    minAnchor.y = maxAnchor.y = 0f;
                    break;
                case "TOP":
                    minAnchor.y = maxAnchor.y = 1f;
                    break;
                case "CENTER":
                    minAnchor.y = maxAnchor.y = 0.5f;
                    break;
                case "SCALE":
                    minAnchor.y = rectTransform.anchorMin.y + rectTransform.offsetMin.y / parentTransform.rect.height;
                    maxAnchor.y = rectTransform.anchorMax.y + rectTransform.offsetMax.y / parentTransform.rect.height;
                    break;
                default:
                    Debug.LogError($"Unknown horizontal constraint {nodeConstraints.horizontal}");
                    break;
            }

            rectTransform.anchorMin = minAnchor;
            rectTransform.anchorMax = maxAnchor;

            rectTransform.offsetMin = positionMin - Vector2.Scale(rectTransform.anchorMin, parentSize);
            rectTransform.offsetMax = positionMax - Vector2.Scale(rectTransform.anchorMax, parentSize);
        }


        private Vector3 ConvertVector(RectTransform parent, Vector3 anchoredPosition)
        {
            Vector3[] corners = new Vector3[4];
            parent.GetWorldCorners(corners);
            var deltaX = corners[3] - corners[0];
            var deltaY = corners[3] - corners[2];
            var posX = anchoredPosition.x * deltaX / parent.rect.width;
            var posY = anchoredPosition.y * deltaY / parent.rect.height;
            return posX + posY + corners[1];
        }
    }
}
