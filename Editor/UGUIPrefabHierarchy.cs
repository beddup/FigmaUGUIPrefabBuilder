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
        public string horizontal_alignment; // left, center, or right
        public string vertical_alignment; // top, center, or bottom
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
    }
}
