using System.Runtime.CompilerServices;
using System.Text;
using AzotBase.Page;

namespace AzotBase.Tree;

public class BPlusTree
{
    private readonly PageManager _pageManager;
    private int _rootPageId;

    public BPlusTree(PageManager pages, int m)
    {
        _pageManager = pages;

        var root = _pages.AllocateLeaf();
        _rootPageId = root.PageId;
    }
    
    public async Task Insert(int key, int pageId, int slotId)
    {
        var leaf = await FindLeafNode(_rootPageId, key);
        leaf.InsertKey(key, pageId, slotId);

        if (leaf.Header.KeyCount == LeafPage.MaxKeys)
            SplitLeaf(leaf);
    }
    
    public void Delete(int key)
    {
        var leaf = FindLeafNode(_root, key, out var childIndex); 
        
        leaf.DeleteKey(key);
        if (leaf.Parent != null)
        {
            var index = leaf.Parent.FindKey(key);
            if (index >= 0)
                leaf.Parent.Keys[index] = leaf.Keys[0];
        }
        
        if (leaf.KeyCount < m / 2)
            BalanceNode(leaf, childIndex);
    }
    
    private void BalanceNode(Node node, int childIndex)
    {
        while (true)
        {
            var parent = node.Parent;
            if (parent == null) 
                return;
    
            if (TryRedistributeKeys(node, childIndex)) 
                return;
    
            MergeNodes(node, childIndex);
    
            if (parent.KeyCount < m / 2)
            {
                node = parent;
                childIndex = GetChildIndex(parent); 
                continue;
            }
            break;
        }
    }
    
    private bool TryRedistributeKeys(Node node, int childIndex)
    {
        var parent = node.Parent;
        if (parent == null)
            return false;
        
        Node? left = childIndex > 0 ? parent.Children[childIndex - 1] : null;
        Node? right = childIndex < parent.Children.Length - 1 ? parent.Children[childIndex + 1] : null;
        if (TryBorrowFromLeft(node, left, childIndex) || TryBorrowFromRight(node, right, childIndex))
            return true;
        
        return false;
    }
    
    private bool TryBorrowFromLeft(Node node, Node? left, int childIndex)
    {
        var parent = node.Parent;
        if (left == null || left.KeyCount <= m / 2 || parent == null)
            return false;
        
        var lastKeyIndex = left.KeyCount - 1;
        if (node.IsLeaf)
        {
            ((LeafNode)node).InsertKey(
                left.Keys[lastKeyIndex], ((LeafNode)left).Values[lastKeyIndex].PageId, ((LeafNode)left).Values[lastKeyIndex].SlotId);
            parent.Keys[childIndex - 1] = node.Keys[0];
            ((LeafNode)left).DeleteKey(left.Keys[lastKeyIndex]);
        }
        else
        {
            var leftChild = ((IndexNode)left).Children[lastKeyIndex + 1];
            var rightChild = ((IndexNode)node).Children[0];
            ((IndexNode)node).InsertKey(parent.Keys[childIndex - 1], leftChild, rightChild);
            parent.Keys[childIndex - 1] = left.Keys[lastKeyIndex];
            ((IndexNode)node).DeleteKey(left.Keys[lastKeyIndex], ((IndexNode)left).Children[lastKeyIndex]);
        }
    
        return true;
    }
    
    private bool TryBorrowFromRight(Node node, Node? right, int childIndex)
    {
        var parent = node.Parent;
        if (right == null || right.KeyCount <= m / 2 || parent == null)
            return false;
    
        var firstKeyIndex = 0;
        if (node.IsLeaf)
        {
            ((LeafNode)node).InsertKey(
                right.Keys[firstKeyIndex], ((LeafNode)right).Values[firstKeyIndex].PageId, ((LeafNode)right).Values[firstKeyIndex].SlotId);
            ((LeafNode)right).DeleteKey(right.Keys[firstKeyIndex]);
            parent.Keys[childIndex] = node.Keys[0];
        }
        else
        {
            var rightChild = ((IndexNode)right).Children[firstKeyIndex];
            ((IndexNode)node).InsertKey(parent.Keys[childIndex], null, rightChild);
            parent.Keys[childIndex] = right.Keys[firstKeyIndex];
            ((IndexNode)node).DeleteKey(right.Keys[firstKeyIndex], ((IndexNode)right).Children[firstKeyIndex + 1]);
        }
        
        return true;
    }
    
    private void MergeNodes(Node node, int nodeIndex)
    {
        Node left;
        Node right;
        int parentKeyIndex;
        if (nodeIndex == node.Parent.KeyCount)
        {
            left = node.Parent.Children[nodeIndex - 1];
            right = node;
            parentKeyIndex = nodeIndex - 1;
        }
        else
        {
            left = node;
            right = node.Parent.Children[nodeIndex + 1];
            parentKeyIndex = nodeIndex;
        }
    
        Node mergedNode;
        if (node.IsLeaf)
        {
            mergedNode = new LeafNode(m)
            {
                Next = ((LeafNode)right).Next,
                Parent = left.Parent,
                KeyCount = left.KeyCount + right.KeyCount
            };
            Array.Copy(left.Keys, 0, mergedNode.Keys, 0, left.KeyCount);
            Array.Copy(right.Keys, 0, mergedNode.Keys, left.KeyCount, right.KeyCount);
            Array.Copy(((LeafNode)left).Values, 0, ((LeafNode)mergedNode).Values, 0, left.KeyCount);
            Array.Copy(((LeafNode)right).Values, 0, ((LeafNode)mergedNode).Values, left.KeyCount, right.KeyCount);
        }
        else
        {
            var leftChild = ((IndexNode)left).Children[parentKeyIndex + 1];
            var rightChild = ((IndexNode)right).Children[0];
            ((IndexNode)right).InsertKey(left.Parent.Keys[parentKeyIndex], leftChild, rightChild);
            
            mergedNode = new IndexNode(m)
            {
                Parent = left.Parent,
                KeyCount = left.KeyCount + right.KeyCount
            };
            
            //Now left and right has 2 copies so don't include last child of left node in copy func
            
            Array.Copy(left.Keys, 0, mergedNode.Keys, 0, left.KeyCount);
            Array.Copy(right.Keys, 0, mergedNode.Keys, left.KeyCount, right.KeyCount);
            Array.Copy(((IndexNode)left).Children, 0, ((IndexNode)mergedNode).Children, 0, left.KeyCount);
            Array.Copy(((IndexNode)right).Children, 0, ((IndexNode)mergedNode).Children, left.KeyCount, right.KeyCount + 1);
            foreach(var child in ((IndexNode)mergedNode).Children.Take(mergedNode.KeyCount + 1))
                child.Parent = (IndexNode)mergedNode;
        }
        node.Parent.DeleteKey(node.Parent.Keys[parentKeyIndex], mergedNode);
    
        if (node.Parent.KeyCount == 0) //can happen only in root
        {
            mergedNode.Parent = null;
            _root = mergedNode;
        }
    } 
    
    private void SplitNode(Node node)
    {
        Node leftNode;
        Node rightNode;
        IndexNode? parentNode = node.Parent;
        
        var spanKeys = node.Keys.AsSpan();
        int parentKey;
        if (node.IsLeaf)
        {
            LeafNode originNode = (LeafNode)node;
            
            rightNode = new LeafNode(m) { Next = originNode.Next };
            originNode.Values.AsSpan()[(node.KeyCount / 2)..].CopyTo(((LeafNode)rightNode).Values);
            spanKeys[(node.KeyCount / 2)..].CopyTo(rightNode.Keys);
            
            leftNode = new LeafNode(m) { Next = rightNode };
            originNode.Values.AsSpan()[..(node.KeyCount / 2)].CopyTo(((LeafNode)leftNode).Values);
            spanKeys[..(node.KeyCount / 2)].CopyTo(leftNode.Keys);
            
            leftNode.KeyCount = node.KeyCount / 2;
            rightNode.KeyCount = node.KeyCount - leftNode.KeyCount;
            parentKey = rightNode.Keys[0];
        }
        else
        {
            IndexNode originNode = (IndexNode)node;
            
            leftNode = new IndexNode(m);
            leftNode.KeyCount = node.KeyCount / 2;
            originNode.Children.AsSpan()[..(node.KeyCount / 2 + 1)].CopyTo(((IndexNode)leftNode).Children);
            foreach (var child in ((IndexNode)leftNode).Children.Take(leftNode.KeyCount + 1))
                child.Parent = (IndexNode)leftNode;
            spanKeys[..(node.KeyCount / 2)].CopyTo(leftNode.Keys);
            
            rightNode = new IndexNode(m);
            rightNode.KeyCount = node.KeyCount - leftNode.KeyCount - 1;
            originNode.Children.AsSpan()[(node.KeyCount / 2 + 1)..].CopyTo(((IndexNode)rightNode).Children);
            foreach (var child in ((IndexNode)rightNode).Children.Take(rightNode.KeyCount + 1))
                child.Parent = (IndexNode)rightNode;
            spanKeys[(node.KeyCount / 2 + 1)..].CopyTo(rightNode.Keys);
            
            parentKey = spanKeys[node.KeyCount / 2];
        }
        
        if (parentNode != null)
        {
            parentNode.InsertKey(parentKey, leftNode, rightNode);
            if (parentNode.KeyCount == m)
            {
                SplitNode(parentNode);
                return;
            }
        }
        else
        {
            parentNode = new IndexNode(m);
            parentNode.InsertKey(parentKey, leftNode, rightNode);
            _root = parentNode;
        }
        
        leftNode.Parent = parentNode;
        rightNode.Parent = parentNode;
    }
    
    private async Task<LeafPage> FindLeafNode(int pageId, int key)
    {
        int current = pageId;
        while (true)
        {
            var pageType = await _pageManager.ReadPageType(current);
            if (pageType == PageType.LeafPage)
                return await _pageManager.ReadPage<LeafPage>(current);

            var indexPage = await _pageManager.ReadPage<IndexPage>(current);
            int childIndex = BinarySearchGreaterIndex(
                key,
                indexPage.Keys,
                indexPage.Header.KeyCount);

            current = indexPage.ChildrenPageIds[childIndex];
        }
    }
    
    private static int GetChildIndex(Node node)
    {
        var parent = node.Parent;
        if (parent == null)
            return -1;
        
        return BinarySearchGreaterIndex(node.Keys[0], parent.Keys, parent.KeyCount);
    }
    
    private static int BinarySearchGreaterIndex(int key, int[] keys, int keysCount)
    {
        int left = 0;
        int right = keysCount - 1;
    
        while (left <= right)
        {
            int mid = (left + right) / 2;
            if (key < keys[mid])
                right = mid - 1;
            else
                left = mid + 1;
        }
        return left;
    }
}