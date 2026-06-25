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
            if (node != null)
            {
                return node.absoluteBoundingBox;
            }

            bool hasBounds = false;
            float minX = 0f;
            float minY = 0f;
            float maxX = 0f;
            float maxY = 0f;

            CollectChildrenBoundingBox(ref hasBounds, ref minX, ref minY, ref maxX, ref maxY);
            if (!hasBounds)
            {
                return null;
            }

            return new AbsoluteBoundingBox
            {
                x = minX,
                y = minY,
                width = maxX - minX,
                height = maxY - minY
            };
        }

        private void CollectChildrenBoundingBox(ref bool hasBounds, ref float minX, ref float minY, ref float maxX, ref float maxY)
        {
            if (children == null) return;

            foreach (var child in children)
            {
                if (child.node != null)
                {
                    EncapsulateBoundingBox(child.node.absoluteBoundingBox, ref hasBounds, ref minX, ref minY, ref maxX, ref maxY);
                }

                child.CollectChildrenBoundingBox(ref hasBounds, ref minX, ref minY, ref maxX, ref maxY);
            }
        }

        private static void EncapsulateBoundingBox(AbsoluteBoundingBox bounds, ref bool hasBounds, ref float minX,
            ref float minY, ref float maxX, ref float maxY)
        {
            if (bounds == null) return;

            var boundsMaxX = bounds.x + bounds.width;
            var boundsMaxY = bounds.y + bounds.height;

            if (!hasBounds)
            {
                minX = bounds.x;
                minY = bounds.y;
                maxX = boundsMaxX;
                maxY = boundsMaxY;
                hasBounds = true;
                return;
            }

            minX = Mathf.Min(minX, bounds.x);
            minY = Mathf.Min(minY, bounds.y);
            maxX = Mathf.Max(maxX, boundsMaxX);
            maxY = Mathf.Max(maxY, boundsMaxY);
        }
        #endregion
    }
}
