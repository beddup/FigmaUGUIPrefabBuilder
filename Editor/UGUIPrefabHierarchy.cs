using System;
using System.Collections.Generic;
using UnityEngine;
using FigmaClient;

namespace FigmaImporter.Editor
{
    public enum NodeRenderType
    {
        Container,
        Image,
        Text,
        Color
    }

    [Serializable]
    public class UGUIPrefabNode
    {
        public string nodeId;
        public string nodeName;
        public string gameObjectCategory;
        public string gameObjectName;
        public bool isButton;
        public string horizontal_alignment; // left, center, right, stretch
        public string vertical_alignment; // top, center, bottom, stretch
        public string image_type = "simple"; // simple, sliced, tiled
        public List<UGUIPrefabNode> children = new List<UGUIPrefabNode>();
        [NonSerialized] public Node node;
        [NonSerialized] public GameObject gameObject;
        
        public NodeRenderType renderType
        {
            get
            {
                return gameObjectCategory switch
                {
                    "container" => NodeRenderType.Container,
                    "image" => NodeRenderType.Image,
                    "text" => NodeRenderType.Text,
                    "color" => NodeRenderType.Color,
                    _ => NodeRenderType.Container
                };
            }
        }
        public bool HasChildren => children != null && children.Count > 0;


        public static UGUIPrefabNode FromJson(string json)
        {
            return JsonUtility.FromJson<UGUIPrefabNode>(json);
        }

        public string ToJson(bool prettyPrint = false)
        {
            return JsonUtility.ToJson(this, prettyPrint);
        }

        public UGUIPrefabNode FindDirectParent(UGUIPrefabNode node)
        {
            if (children == null) return null;
            foreach (var child in children)
            {
                if (child == node) return this;
                var found = child.FindDirectParent(node);
                if (found != null) return found;
            }
            return null;
        }
        public List<UGUIPrefabNode> GetALlNodes() // make sure parent's index is smaller than its child node
        {
            var result = new List<UGUIPrefabNode>();
            var queue = new Queue<UGUIPrefabNode>();
            queue.Enqueue(this);
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                result.Add(current);
                if (current.children == null) continue;
                foreach (var child in current.children)
                    queue.Enqueue(child);
            }
            return result;
        }
        
        public static void BindNode(UGUIPrefabNode rootHierarchyNode, Node rootFigmaNode)
        {
            var figmaNode = rootFigmaNode.FindNode(rootHierarchyNode.nodeId);
            if (figmaNode == null && rootHierarchyNode.renderType != NodeRenderType.Container)
            {
                Debug.LogError($"Did not find node {rootHierarchyNode.nodeId}");   
            }
            rootHierarchyNode.node = figmaNode;
            
            if (rootHierarchyNode.children == null) return;
            foreach (var child in rootHierarchyNode.children)
            {
                BindNode(child, rootFigmaNode);
            }
        }

        #region BoundingBox

        public AbsoluteBoundingBox GetBoundingBox()
        {
            var allNodes = GetALlNodes();
            AbsoluteBoundingBox result = null;

            foreach (var n in allNodes)
            {
                if (n.node == null) continue;

                var box = n.node.absoluteBoundingBox;
                // 文字节点在 Unity 中会扩大 10%，扩展方向根据文字对齐方式决定
                if (n.renderType == NodeRenderType.Text && n.node.style != null)
                {
                    float expandW = box.width * 0.1f;
                    float expandH = box.height * 0.1f;
                    float expandLeft = 0f, expandRight = 0f, expandTop = 0f, expandBottom = 0f;

                    switch (n.node.style.textAlignHorizontal)
                    {
                        case "LEFT":
                            expandRight = expandW;
                            break;
                        case "RIGHT":
                            expandLeft = expandW;
                            break;
                        default: // CENTER, JUSTIFIED
                            expandLeft = expandRight = expandW * 0.5f;
                            break;
                    }

                    switch (n.node.style.textAlignVertical)
                    {
                        case "TOP":
                            expandBottom = expandH;
                            break;
                        case "BOTTOM":
                            expandTop = expandH;
                            break;
                        default: // CENTER
                            expandTop = expandBottom = expandH * 0.5f;
                            break;
                    }

                    box = new AbsoluteBoundingBox
                    {
                        x = box.x - expandLeft,
                        y = box.y - expandTop,
                        width = box.width + expandLeft + expandRight,
                        height = box.height + expandTop + expandBottom
                    };
                }

                if (result == null)
                {
                    result = box;
                }
                else
                {
                    var minX = Mathf.Min(result.x, box.x);
                    var minY = Mathf.Min(result.y, box.y);
                    var maxX = Mathf.Max(result.x + result.width, box.x + box.width);
                    var maxY = Mathf.Max(result.y + result.height, box.y + box.height);
                    result = new AbsoluteBoundingBox
                    {
                        x = minX,
                        y = minY,
                        width = maxX - minX,
                        height = maxY - minY
                    };
                }
            }

            return result;
        }
        #endregion
    }
}
