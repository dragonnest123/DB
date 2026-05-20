using System.Diagnostics;
using AzotBase.Storage;
using AzotBase.Storage.Pages;
using AzotBase.Storage.Pages.Common;

namespace AzotBase.Index;

public class BPlusTree
{
    private int _rootPageId;
    private readonly PageManager _pageManager;
    
    private enum OperationType
    {
        Insert,
        Delete,
        Get
    }
    
    public BPlusTree(PageManager pageManager, int rootPageId)
    {
        _pageManager = pageManager;
        _rootPageId = rootPageId;
    }
    
    public BPlusTree(PageManager pageManager)
    {
        _pageManager = pageManager;
    }
    
    public async Task Insert(int key, int pageId, int slotId)
    {
        var path = new Stack<int>();
        
        var leaf = await FindLeafPage(key, OperationType.Insert, path);
        leaf.InsertKey(key, pageId, slotId);

        if (leaf.Header.KeyCount == LeafPage.MaxKeys)
            await SplitLeaf(leaf, path);
        else
            await FreePage(leaf);
    }
     
    public async Task Delete(int key)
    {
        var path = new Stack<int>();
    
        var leaf = await FindLeafPage(key, OperationType.Delete, path);

        if (leaf.FindKey(key) < 0)
        {
            await FreePage(leaf);
            await ClearPath(path);
            return;
        }

        leaf.DeleteKey(key);
        
        if (leaf.Header.Id == _rootPageId || leaf.Header.KeyCount >= LeafPage.MaxKeys / 2)
        {
            await FreePage(leaf);
            return;
        }

        await BalanceAfterDelete(leaf, path);
    }

    public async Task<(int pageId, int slotId)?> Find(int key)
    {
        PageBase current;
        while (true)
        {
            int rootId = _rootPageId;
            current = await _pageManager.LoadPage<PageBase>(rootId, false, PageLockMode.ReadLock);
        
            if (_rootPageId == rootId)
                break;
            
            await current.ExitReadLock();
        }

        while (current is IndexPage index)
        {
            int childIndex = BinarySearchGreaterIndex(key, index.Keys, index.Header.KeyCount);
            var childId = index.ChildrenPageIds[childIndex];
            var child = await _pageManager.LoadPage<PageBase>(childId, false, PageLockMode.ReadLock);
            
            await index.ExitReadLock();

            current = child;
        } 

        var leaf = (LeafPage)current;
        var pos = leaf.FindKey(key);
        (int, int)? result = pos >= 0 ? leaf.Values[pos] : null;
        
        await leaf.ExitReadLock();

        return result;
    }
     
    private async Task BalanceAfterDelete(LeafPage leafPage, Stack<int> path)
    {
        PageBase page = leafPage;

        while (path.Count > 0)
        {
            int parentId = path.Pop();
            var parent = await _pageManager.LoadPage<IndexPage>(parentId, false);
            
            if (page is LeafPage leaf)
            {
                if (await TryRedistributeLeaf(parent, leaf))
                {
                    await FreePage(leaf);
                    await FreePage(parent);
                    await ClearPath(path);
                    return;
                }

                await MergeLeaf(parent, leaf);
            }
            else if (page is IndexPage index)
            {
                if (await TryRedistributeIndex(parent, index))
                {
                    await FreePage(index);
                    await FreePage(parent);
                    await ClearPath(path);
                    return;
                }

                await MergeIndex(parent, index);
            }
 
            page = parent;
            
            if (parentId == _rootPageId && parent.Header.KeyCount == 0)
            {
                Interlocked.Exchange(ref _rootPageId, parent.ChildrenPageIds[0]);
                await FreePage(parent);
                return;
            }
        }

        await FreePage(page);
    }
     
    private async Task<bool> TryRedistributeLeaf(IndexPage parent, LeafPage page)
    {
        int index = Array.IndexOf(parent.ChildrenPageIds, page.Header.Id, 0, parent.Header.KeyCount + 1);

        int leftId = index > 0 ? parent.ChildrenPageIds[index - 1] : -1;
        int rightId = index < parent.Header.KeyCount ? parent.ChildrenPageIds[index + 1] : -1;
        
        if (leftId != -1)
        {
            LeafPage left = await _pageManager.LoadPage<LeafPage>(leftId, true, PageLockMode.WriteLock);
            
            if (left.Header.KeyCount > LeafPage.MaxKeys / 2)
            {
                int borrowedKey = left.Keys[left.Header.KeyCount - 1];
                var borrowedValue = left.Values[left.Header.KeyCount - 1];

                page.InsertKeyAt(0, borrowedKey, borrowedValue.PageId, borrowedValue.SlotId);
                
                parent.ReplaceKeyAt(index - 1, page.Keys[0]);
                
                left.Header.KeyCount--;
                left.IsDirty = 1;
                
                await FreePage(left);
                
                return true;
            }
            
            await FreePage(left);
        }
        
        if (rightId != -1)
        {
            LeafPage right = await _pageManager.LoadPage<LeafPage>(rightId, true, PageLockMode.WriteLock);

            if (right.Header.KeyCount > LeafPage.MaxKeys / 2)
            {
                int borrowedKey = right.Keys[0];
                var borrowedValue = right.Values[0];
                
                page.InsertKeyAt(page.Header.KeyCount, borrowedKey, borrowedValue.PageId, borrowedValue.SlotId);
                right.DeleteKeyAt(0);
                parent.ReplaceKeyAt(index, right.Keys[0]);
                
                await FreePage(right);
                
                return true;
            }
            
            await FreePage(right);
        }
        
        return false;
    }
    
    private async Task<bool> TryRedistributeIndex(IndexPage parent, IndexPage page)
    {
        int index = Array.IndexOf(parent.ChildrenPageIds, page.Header.Id, 0, parent.Header.KeyCount + 1);

        int leftId = index > 0 ? parent.ChildrenPageIds[index - 1] : -1;
        int rightId = index < parent.Header.KeyCount ? parent.ChildrenPageIds[index + 1] : -1;
        
        int minKeys = IndexPage.MaxKeys / 2;
        
        if (leftId != -1)
        {
            IndexPage left = await _pageManager.LoadPage<IndexPage>(leftId, true, PageLockMode.WriteLock);
            
            if (left.Header.KeyCount > IndexPage.MaxKeys / 2)
            {
                int borrowedKey = parent.Keys[index - 1];
                int borrowedChild = left.ChildrenPageIds[left.Header.KeyCount];
                
                Array.Copy(page.Keys, 0, page.Keys, 1, page.Header.KeyCount);
                Array.Copy(page.ChildrenPageIds, 0, page.ChildrenPageIds, 1, page.Header.KeyCount + 1);
                page.Keys[0] = borrowedKey;
                page.ChildrenPageIds[0] = borrowedChild;
                page.Header.KeyCount++;
                page.IsDirty = 1;

                parent.ReplaceKeyAt(index - 1, left.Keys[left.Header.KeyCount - 1]);
                
                left.Header.KeyCount--;
                left.IsDirty = 1;
                
                await FreePage(left);
                
                return true;
            }
            
            await FreePage(left);
        }
        
        if (rightId != -1)
        {
            IndexPage right = await _pageManager.LoadPage<IndexPage>(rightId, true, PageLockMode.WriteLock);
            
            if (right.Header.KeyCount > minKeys)
            {
                int borrowedKey = parent.Keys[index];
                int borrowedChild = right.ChildrenPageIds[0];

                page.Keys[page.Header.KeyCount] = borrowedKey;
                page.ChildrenPageIds[page.Header.KeyCount + 1] = borrowedChild;
                page.Header.KeyCount++;
                page.IsDirty = 1;
                
                parent.ReplaceKeyAt(index, right.Keys[0]);
                
                Array.Copy(right.Keys, 1, right.Keys, 0, right.Header.KeyCount - 1);
                Array.Copy(right.ChildrenPageIds, 1, right.ChildrenPageIds, 0, right.Header.KeyCount);
                right.Header.KeyCount--;
                right.IsDirty = 1;
                
                await FreePage(right);

               return true;
            }
            
            await FreePage(right);
        }
        
        return false;
    }
    
    private async Task MergeLeaf(IndexPage parent, LeafPage page)
    {
        int index = Array.IndexOf(parent.ChildrenPageIds, page.Header.Id, 0, parent.Header.KeyCount + 1);

        int leftIndex = index > 0 ? index - 1 : index;
        int rightIndex = leftIndex + 1;
        
        LeafPage left, right;
    
        if (index == leftIndex)
        {
            left = page;
            right = await _pageManager.LoadPage<LeafPage>(parent.ChildrenPageIds[rightIndex], true, PageLockMode.WriteLock);
        }
        else
        {
            left = await _pageManager.LoadPage<LeafPage>(parent.ChildrenPageIds[leftIndex], true, PageLockMode.WriteLock);
            right = page;
        }
        
        left.InsertRangeAt(
            left.Header.KeyCount, 
            right.Keys.AsSpan()[..right.Header.KeyCount], 
            right.Values.AsSpan()[..right.Header.KeyCount]);
    
        parent.DeleteKeyAt(leftIndex);
        
        await FreePage(left);
        await FreePage(right);
    }
    
    private async Task MergeIndex(IndexPage parent, IndexPage page)
    {
        int index = Array.IndexOf(parent.ChildrenPageIds, page.Header.Id, 0, parent.Header.KeyCount + 1);

        int leftIndex = index > 0 ? index - 1 : index;
        int rightIndex = leftIndex + 1;
        
        IndexPage left, right;
    
        if (index == leftIndex)
        {
            left = page;
            right = await _pageManager.LoadPage<IndexPage>(parent.ChildrenPageIds[rightIndex], true, PageLockMode.WriteLock);
        }
        else
        {
            left = await _pageManager.LoadPage<IndexPage>(parent.ChildrenPageIds[leftIndex], true, PageLockMode.WriteLock);
            right = page;
        }
        
        int separator = parent.Keys[leftIndex];
        
        left.InsertKeyAt(left.Header.KeyCount, separator, right.ChildrenPageIds[0]);
        left.InsertRangeAt(
            left.Header.KeyCount, 
            right.Keys.AsSpan()[..right.Header.KeyCount], 
            right.ChildrenPageIds.AsSpan()[..(right.Header.KeyCount + 1)]);
        parent.DeleteKeyAt(leftIndex);
        
        await FreePage(left);
        await FreePage(right);
    }
    
    private async Task SplitLeaf(LeafPage leaf, Stack<int> path)
    {
        LeafPage rightLeaf = await _pageManager.AllocatePage<LeafPage>(true, PageLockMode.WriteLock);

        int mid = leaf.Header.KeyCount / 2;
        int rightCount = leaf.Header.KeyCount - mid;
        
        rightLeaf.InsertRangeAt(
            0, 
            leaf.Keys.AsSpan()[mid..(mid + rightCount)], 
            leaf.Values.AsSpan()[mid..(mid + rightCount)]);
    
        leaf.Header.KeyCount = mid;
        leaf.IsDirty = 1;

        int parentKey = rightLeaf.Keys[0];
        
        await InsertIntoParent(path, parentKey, rightLeaf.Header.Id, leaf, rightLeaf);
    }
    
    private async Task SplitIndex(IndexPage index, Stack<int> path)
    {
        IndexPage rightIndex = await _pageManager.AllocatePage<IndexPage>(true, PageLockMode.WriteLock);

        int mid = index.Header.KeyCount / 2;
        int promoteKey = index.Keys[mid];
        int rightCount = index.Header.KeyCount - mid - 1;
        
        rightIndex.InsertRangeAt(
            0, 
            index.Keys.AsSpan()[(mid + 1)..(mid + 1 + rightCount)], 
            index.ChildrenPageIds.AsSpan()[(mid + 1)..(mid + 1 + rightCount + 1)]);
        
        index.Header.KeyCount = mid;
        index.IsDirty = 1;
        
        await InsertIntoParent(path, promoteKey, rightIndex.Header.Id, index, rightIndex);
    }
     
    private async Task InsertIntoParent(Stack<int> path, int key, int rightId, PageBase left, PageBase right)
    {
        if (path.Count == 0)
        {
            await CreateNewRoot(key, rightId);
            
            await FreePage(left);
            await FreePage(right);
            
            return;
        }

        int parentId = path.Pop();
        var parent = await _pageManager.LoadPage<IndexPage>(parentId, false);

        parent.InsertKey(key, rightId);
        
        await FreePage(left);
        await FreePage(right);

        if (parent.Header.KeyCount >= IndexPage.MaxKeys)
            await SplitIndex(parent, path);
        else
            await FreePage(parent);
    }
    
    private async Task<LeafPage> FindLeafPage(int key, OperationType op, Stack<int> path)
    {
        while (true)
        {
            var leafPage = await TryFindLeafPage(key, op, path);
            if (leafPage != null)
                return leafPage;
            
            await ClearPath(path);
        }
    }
    
    private async Task<LeafPage?> TryFindLeafPage(int key, OperationType op, Stack<int> path)
    {
        var rootId = _rootPageId;
        PageBase current = await _pageManager.LoadPage<PageBase>(rootId, true, PageLockMode.WriteLock);

        if (rootId != _rootPageId)
        {
            await FreePage(current);
            return null;
        }

        while (current is IndexPage index)
        {
            int childIndex = BinarySearchGreaterIndex(key, index.Keys, index.Header.KeyCount);
            var childId = index.ChildrenPageIds[childIndex];
            var child = await _pageManager.LoadPage<PageBase>(childId, true, PageLockMode.WriteLock);

            if (IsSafe(child, op))
            {
                await FreePage(index);
                await ClearPath(path);
            }
            else
                path.Push(index.Header.Id);
            
            current = child;
        }

        return (LeafPage)current;
    }
    
    private async Task CreateNewRoot(int key, int rightId)
    {
        IndexPage root = await _pageManager.AllocatePage<IndexPage>(true, PageLockMode.WriteLock);
            
        root.Keys[0] = key;
        root.ChildrenPageIds[0] = _rootPageId;
        root.ChildrenPageIds[1] = rightId;
        root.Header.KeyCount = 1;
        root.IsDirty = 1;
            
        Interlocked.Exchange(ref _rootPageId, root.Header.Id);
        
        await FreePage(root);
    }

    private static bool IsSafe(PageBase page, OperationType op)
    {
        return op switch
        {
            OperationType.Insert => IsSafeForInsert(page),
            OperationType.Delete => IsSafeForDelete(page),
            OperationType.Get => true,
            _ => throw new ArgumentOutOfRangeException(nameof(op), op, null)
        };
    }

    private static bool IsSafeForInsert(PageBase page)
    {
        return page switch
        {
            LeafPage leafPage => leafPage.Header.KeyCount < LeafPage.MaxKeys - 1,
            IndexPage index => index.Header.KeyCount < IndexPage.MaxKeys - 1,
            _ => throw new ArgumentOutOfRangeException(nameof(page), page, null)
        };
    }
    
    private static bool IsSafeForDelete(PageBase page)
    {
        return page switch
        {
            LeafPage leafPage => leafPage.Header.KeyCount - 1 >= LeafPage.MaxKeys / 2,
            IndexPage index => index.Header.KeyCount - 1 >= IndexPage.MaxKeys / 2,
            _ => throw new ArgumentOutOfRangeException(nameof(page), page, null)
        };
    }

    private async Task ClearPath(Stack<int> path)
    {
        while (path.Count > 0)
        {
            var id = path.Pop();
            var page = await _pageManager.LoadPage<PageBase>(id, false);
            await FreePage(page);
        }
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

    private async Task FreePage(PageBase page)
    {
        await page.ExitWriteLock();
        _pageManager.UnpinPage(page.Id);
    }
    
    public async Task<int[]> InOrder()
    {
        var result = new List<int>();
        var visited = new HashSet<int>();
        
        try 
        {
            await InOrderTraversal(_rootPageId, result, visited);
        }
        catch (Exception ex) when (ex.Message.StartsWith("Cycle"))
        {
            Debug.WriteLine($"CYCLE DETECTED: {ex.Message}");
            Debug.WriteLine($"Visited pages: {string.Join(", ", visited)}");
            throw;
        }
        
        return result.ToArray();
    } 
    
    //not thread safe
    private async Task InOrderTraversal(int pageId, List<int> result, HashSet<int> visited)
    {
        if (!visited.Add(pageId))
            throw new Exception($"Cycle detected! Page {pageId} visited twice");

        var pageType = await _pageManager.ReadPageType(pageId);
        if (pageType == PageType.LeafPage)
        {
            LeafPage page = await _pageManager.LoadPage<LeafPage>(pageId, false, PageLockMode.WriteLock);
            var keys = page.Keys.Take(page.Header.KeyCount).ToArray();
        
            for (int i = 1; i < keys.Length; i++)
                if (keys[i] <= keys[i-1])
                    throw new Exception($"Leaf {pageId} unsorted at {i}: {keys[i-1]} >= {keys[i]}");
        
            result.AddRange(keys);
            await page.ExitWriteLock();
            return;
        }
    
        var indexPage = await _pageManager.LoadPage<IndexPage>(pageId, false, PageLockMode.WriteLock);
        var childCount = indexPage.Header.KeyCount + 1;
        
        for (int i = 1; i < indexPage.Header.KeyCount; i++)
            if (indexPage.Keys[i] <= indexPage.Keys[i-1])
                throw new Exception($"Index {pageId} unsorted at {i}");
        
        for (int i = 0; i < childCount; i++)
            if (indexPage.ChildrenPageIds[i] == -1)
                throw new Exception($"Index {pageId} has zero child at {i}");
        
        await indexPage.ExitWriteLock();
    
        foreach (var child in indexPage.ChildrenPageIds.Take(childCount))
            await InOrderTraversal(child, result, visited);
    }
}

