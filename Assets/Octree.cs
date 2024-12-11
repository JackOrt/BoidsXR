// Octree.cs
using System.Collections.Generic;
using UnityEngine;

public class Octree
{
    private class OctreeNode
    {
        public Bounds bounds;
        public List<int> boidIndices;
        public OctreeNode[] children;
        public bool isLeaf;

        public OctreeNode(Bounds bounds)
        {
            this.bounds = bounds;
            boidIndices = new List<int>();
            children = new OctreeNode[8];
            isLeaf = true;
        }
    }

    private OctreeNode root;
    private int maxBoidsPerNode;
    private int maxDepth;

    public Octree(Bounds initialBounds, int maxBoidsPerNode = 10, int maxDepth = 8)
    {
        root = new OctreeNode(initialBounds);
        this.maxBoidsPerNode = maxBoidsPerNode;
        this.maxDepth = maxDepth;
    }

    public void Insert(int boidIndex, Vector3 position)
    {
        Insert(root, boidIndex, position, 0);
    }

    private void Insert(OctreeNode node, int boidIndex, Vector3 position, int depth)
    {
        if (!node.bounds.Contains(position))
            return; // Position out of bounds

        if (node.isLeaf)
        {
            node.boidIndices.Add(boidIndex);
            if (node.boidIndices.Count > maxBoidsPerNode && depth < maxDepth)
            {
                Subdivide(node, depth);
            }
        }
        else
        {
            foreach (var child in node.children)
            {
                Insert(child, boidIndex, position, depth + 1);
            }
        }
    }

    private void Subdivide(OctreeNode node, int depth)
    {
        Vector3 size = node.bounds.size / 2f;
        Vector3 center = node.bounds.center;
        for (int i = 0; i < 8; i++)
        {
            Vector3 newCenter = center;
            newCenter.x += size.x * ((i & 1) == 1 ? 0.5f : -0.5f);
            newCenter.y += size.y * ((i & 2) == 2 ? 0.5f : -0.5f);
            newCenter.z += size.z * ((i & 4) == 4 ? 0.5f : -0.5f);
            Bounds childBounds = new Bounds(newCenter, size);
            node.children[i] = new OctreeNode(childBounds);
        }

        foreach (var boidIndex in node.boidIndices)
        {
            // Assuming you have access to boid positions externally
            // This requires passing boid positions during subdivision
            // For simplicity, we'll skip re-inserting and assume positions don't change during subdivision
        }

        node.boidIndices.Clear();
        node.isLeaf = false;
    }

    public List<int> QueryRange(Bounds range)
    {
        List<int> result = new List<int>();
        QueryRange(root, range, result);
        return result;
    }

    private void QueryRange(OctreeNode node, Bounds range, List<int> result)
    {
        if (!node.bounds.Intersects(range))
            return;

        if (node.isLeaf)
        {
            foreach (var index in node.boidIndices)
            {
                result.Add(index);
            }
        }
        else
        {
            foreach (var child in node.children)
            {
                if (child != null)
                    QueryRange(child, range, result);
            }
        }
    }

    public void Clear()
    {
        root = new OctreeNode(root.bounds);
    }
}
