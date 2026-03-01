using AzotBase.Page;
using AzotBase.Utils;

namespace AzotBase.Tree;

public class BPlusTree
{
    private int _rootPageId;
    private readonly PageManager _pageManager;
    
    public BPlusTree(PageManager pageManager, int rootPageId)
    {
        _pageManager = pageManager;
        _rootPageId = rootPageId;
    }
    
    public async Task Insert(int key, int pageId, int slotId)
    {
        var path = new Stack<int>();
        List<int> pinned = new List<int>();
        
        var leaf = await FindLeafPage(key, path);
        PinPage(leaf.Header.Id, pinned);
        
        leaf.InsertKey(key, pageId, slotId);

        if (leaf.Header.KeyCount == LeafPage.MaxKeys)
            await SplitLeaf(leaf, path, pinned);
        else
            await leaf.ExitWriteLock();
        
        UnpinPinnedPages(pinned);
    }
     
    public async Task Delete(int key)
    {
        var path = new Stack<int>();
        List<int> pinned = new List<int>();
        
        var leaf = await FindLeafPage(key, path);
        PinPage(leaf.Header.Id, pinned);

        if (leaf.FindKey(key) < 0)
            return;
        
        await leaf.EnterWriteLock();

        leaf.DeleteKey(key);
        
        await leaf.ExitWriteLock();

        if (leaf.Header.Id == _rootPageId)
            return;
        
        if (leaf.Header.KeyCount < LeafPage.MaxKeys / 2)
            await BalanceAfterDelete(path, pinned);
        
        UnpinPinnedPages(pinned);
    }
     
    private async Task BalanceAfterDelete(Stack<int> path, List<int> pinned)
    {
        int pageId = path.Pop();

        while (path.Count > 0)
        {
            int parentId = path.Pop();
            var parent = await _pageManager.LoadPage<IndexPage>(parentId);
            PinPage(parent.Header.Id, pinned);

            var pageType = await _pageManager.ReadPageType(pageId);
            if (pageType == PageType.LeafPage)
            {
                LeafPage page = await _pageManager.LoadPage<LeafPage>(pageId);
                PinPage(page.Header.Id, pinned);
                
                if (page.Header.KeyCount >= LeafPage.MaxKeys / 2)
                    return;
                
                await parent.EnterWriteLock();
                await page.EnterWriteLock();
                
                if (await TryRedistributeLeaf(parent, page, pinned))
                {
                    await parent.ExitWriteLock();
                    await page.ExitWriteLock();
                    return;
                }
                
                await page.ExitWriteLock();

                await MergeLeaf(parent, pageId, pinned);
            }
            else
            {
                IndexPage page = await _pageManager.LoadPage<IndexPage>(pageId);
                PinPage(page.Header.Id, pinned);
                
                int minKeys = (IndexPage.MaxKeys) / 2;
                if (page.Header.KeyCount >= minKeys)
                    return;
                
                await parent.EnterWriteLock();
                await page.EnterWriteLock();

                if (await TryRedistributeIndex(parent, page, pinned))
                {
                    await parent.ExitWriteLock();
                    await page.ExitWriteLock();
                    return;
                }
                
                await page.ExitWriteLock();

                await MergeIndex(parent, pageId, pinned);
            }

            pageId = parentId;

            if (parent.Header.Id == _rootPageId && parent.Header.KeyCount == 0)
            {
                Interlocked.Exchange(ref _rootPageId, pageId);
                await parent.ExitWriteLock();
                return;
            }
            
            await parent.ExitWriteLock();
        }
    }
     
    private async Task<bool> TryRedistributeLeaf(IndexPage parent, LeafPage page, List<int> pinned)
    {
        int index = Array.IndexOf(parent.ChildrenPageIds, page.Header.Id);

        int leftId = index > 0 ? parent.ChildrenPageIds[index - 1] : -1;
        int rightId = index < parent.Header.KeyCount ? parent.ChildrenPageIds[index + 1] : -1;
        
        if (leftId != -1)
        {
            LeafPage left = await _pageManager.LoadPage<LeafPage>(leftId);
            PinPage(left.Header.Id, pinned);
            
            if (left.Header.KeyCount > LeafPage.MaxKeys / 2)
            {
                await left.EnterWriteLock();
                
                int borrowedKey = left.Keys[left.Header.KeyCount - 1];
                var borrowedValue = left.Values[left.Header.KeyCount - 1];

                page.InsertKeyAt(0, borrowedKey, borrowedValue.PageId, borrowedValue.SlotId);
                left.DeleteKeyAt(left.Header.KeyCount - 1);
                parent.ReplaceKeyAt(index - 1, page.Keys[0]);
                
                await left.ExitWriteLock();
                
                return true;
            }
        }
        
        if (rightId != -1)
        {
            LeafPage right = await _pageManager.LoadPage<LeafPage>(rightId);
            PinPage(right.Header.Id, pinned);

            if (right.Header.KeyCount > LeafPage.MaxKeys / 2)
            {
                await right.EnterWriteLock();
                
                int borrowedKey = right.Keys[0];
                var borrowedValue = right.Values[0];

                page.InsertKeyAt(page.Header.KeyCount, borrowedKey, borrowedValue.PageId, borrowedValue.SlotId);
                right.DeleteKeyAt(0);
                parent.ReplaceKeyAt(index, right.Keys[0]);
                
                await right.ExitWriteLock();
                
                return true;
            }
        }
        
        return false;
    }
    
    private async Task<bool> TryRedistributeIndex(IndexPage parent, IndexPage page, List<int> pinned)
    {
        int index = Array.IndexOf(parent.ChildrenPageIds, page.Header.Id);

        int leftId = index > 0 ? parent.ChildrenPageIds[index - 1] : -1;
        int rightId = index < parent.Header.KeyCount ? parent.ChildrenPageIds[index + 1] : -1;
        
        int minKeys = (IndexPage.MaxKeys) / 2;
        
        if (leftId != -1)
        {
            IndexPage left = await _pageManager.LoadPage<IndexPage>(leftId);
            PinPage(left.Header.Id, pinned);
            
            if (left.Header.KeyCount > minKeys)
            {
                await left.EnterWriteLock();
                
                int borrowedKey = parent.Keys[index - 1];
                int borrowedChild = left.ChildrenPageIds[left.Header.KeyCount];
                
                page.InsertKeyAt(0, borrowedKey, borrowedChild);
                left.DeleteKeyAt(left.Header.KeyCount - 1);
                parent.ReplaceKeyAt(index - 1, left.Keys[left.Header.KeyCount - 1]);
                
                await left.ExitWriteLock();

                return true;
            }
        }
        
        if (rightId != -1)
        {
            IndexPage right = await _pageManager.LoadPage<IndexPage>(rightId);
            PinPage(right.Header.Id, pinned);
            
            if (right.Header.KeyCount > minKeys)
            {
                await right.EnterWriteLock();
                
                int borrowedKey = parent.Keys[index];
                int borrowedChild = right.ChildrenPageIds[0];
                
                page.InsertKeyAt(page.Header.KeyCount, borrowedKey, borrowedChild);
                right.DeleteKeyAt(0);
                parent.ReplaceKeyAt(index, right.Keys[0]);
                
               await right.ExitWriteLock();

               return true;
            }
        }
        
        return false;
    }
    
    private async Task MergeLeaf(IndexPage parent, int pageId, List<int> pinned)
    {
        int index = Array.IndexOf(parent.ChildrenPageIds, pageId);

        int leftIndex = index > 0 ? index - 1 : index;
        int rightIndex = leftIndex + 1;

        int leftId = parent.ChildrenPageIds[leftIndex];
        int rightId = parent.ChildrenPageIds[rightIndex];

        LeafPage left = await _pageManager.LoadPage<LeafPage>(leftId);
        LeafPage right = await _pageManager.LoadPage<LeafPage>(rightId);
        PinPage(leftId, pinned);
        PinPage(rightId, pinned);
        
        await left.EnterWriteLock();
        await right.EnterReadLock();

        left.InsertRangeAt(
            left.Header.KeyCount, 
            right.Keys.AsSpan()[..right.Header.KeyCount], 
            right.Values.AsSpan()[..right.Header.KeyCount]);
        parent.DeleteKey(parent.Keys[leftIndex]);
        
        await left.ExitWriteLock();
        await right.ExitReadLock();
    }
    
    private async Task MergeIndex(IndexPage parent, int pageId, List<int> pinned)
    {
        int index = Array.IndexOf(parent.ChildrenPageIds, pageId);

        int leftIndex = index > 0 ? index - 1 : index;
        int rightIndex = leftIndex + 1;

        int leftId = parent.ChildrenPageIds[leftIndex];
        int rightId = parent.ChildrenPageIds[rightIndex];

        IndexPage left = await _pageManager.LoadPage<IndexPage>(leftId);
        IndexPage right = await _pageManager.LoadPage<IndexPage>(rightId);
        PinPage(leftId, pinned);
        PinPage(rightId, pinned);

        await left.EnterWriteLock();
        await right.EnterReadLock();

        int separator = parent.Keys[leftIndex];
        
        left.InsertKeyAt(left.Header.KeyCount, separator, right.ChildrenPageIds[0]);
        left.InsertRangeAt(
            left.Header.KeyCount, 
            right.Keys.AsSpan()[..right.Header.KeyCount], 
            right.ChildrenPageIds.AsSpan()[..(right.Header.KeyCount + 1)]);
        parent.DeleteKey(separator);
        
        await left.ExitWriteLock();
        await right.ExitReadLock();
    }
    
    private async Task SplitLeaf(LeafPage leaf, Stack<int> path, List<int> pinned)
    {
        LeafPage rightLeaf = await _pageManager.AllocatePage<LeafPage>();
        PinPage(rightLeaf.Header.Id, pinned);

        await rightLeaf.EnterWriteLock();

        int mid = leaf.Header.KeyCount / 2;
        int rightCount = leaf.Header.KeyCount - mid;
        
        rightLeaf.InsertRangeAt(
            0, 
            leaf.Keys.AsSpan()[mid..(mid + rightCount)], 
            leaf.Values.AsSpan()[mid..(mid + rightCount)]);
    
        leaf.Header.KeyCount = mid;
        leaf.IsDirty = 1;

        int parentKey = rightLeaf.Keys[0];
        
        await leaf.ExitWriteLock();
        await rightLeaf.ExitWriteLock();
        
        await InsertIntoParent(path, parentKey, rightLeaf.Header.Id, pinned);
    }
    
    private async Task SplitIndex(IndexPage index, Stack<int> path, List<int> pinned)
    {
        IndexPage rightIndex = await _pageManager.AllocatePage<IndexPage>();
        PinPage(rightIndex.Header.Id, pinned);
        
        await rightIndex.EnterWriteLock();

        int mid = index.Header.KeyCount / 2;
        int promoteKey = index.Keys[mid];
        int rightCount = index.Header.KeyCount - mid - 1;
        
        rightIndex.InsertRangeAt(
            0, 
            index.Keys.AsSpan()[(mid + 1)..(mid + 1 + rightCount)], 
            index.ChildrenPageIds.AsSpan()[(mid + 1)..(mid + 1 + rightCount + 1)]);
        
        index.Header.KeyCount = mid;
        index.IsDirty = 1;
        
        await index.ExitWriteLock();
        await rightIndex.ExitWriteLock();
        
        await InsertIntoParent(path, promoteKey, rightIndex.Header.Id, pinned);
    }
     
    private async Task InsertIntoParent(Stack<int> path, int key, int rightId, List<int> pinned)
    {
        path.Pop();
        if (path.Count == 0)
        {
            var oldRootId = _rootPageId;
            
            IndexPage root = await _pageManager.AllocatePage<IndexPage>();
            PinPage(root.Header.Id, pinned);
            
            await root.EnterWriteLock();
            
            root.Keys[0] = key;
            root.ChildrenPageIds[0] = oldRootId;
            root.ChildrenPageIds[1] = rightId;
            root.Header.KeyCount = 1;
            root.IsDirty = 1;
            
            await root.ExitWriteLock();
            
            Interlocked.Exchange(ref _rootPageId, root.Header.Id);
            return;
        }

        int parentId = path.Peek();
        var parent = await _pageManager.LoadPage<IndexPage>(parentId);
        PinPage(parentId, pinned);
        
        await parent.EnterWriteLock();

        parent.InsertKey(key, rightId);

        if (parent.Header.KeyCount == IndexPage.MaxKeys)
            await SplitIndex(parent, path, pinned);
        else
            await parent.ExitWriteLock();
    }

    private async Task<LeafPage> FindLeafPage(int key, Stack<int> path)
    {
        while (true)
        {
            PageBase current = await _pageManager.LoadPage<PageBase>(_rootPageId);

            await current.EnterReadLock();

            while (current is IndexPage index)
            {
                int childIndex = BinarySearchGreaterIndex(key, index.Keys, index.Header.KeyCount);
                var childId = index.ChildrenPageIds[childIndex];
                var child = await _pageManager.LoadPage<PageBase>(childId);

                await child.EnterReadLock();
                if (IsSafe(child))
                    await index.ExitReadLock();
                else if (await index.TryUpgradeReadLock())
                    path.Push(index.Header.Id);
                else
                {
                    await index.ExitReadLock();
                    await child.EnterReadLock();
                    current = await _pageManager.LoadPage<PageBase>(_rootPageId);
                    continue;
                }

                current = child;
            }

            var leaf = (LeafPage)current;

            if (await leaf.TryUpgradeReadLock())
                path.Push(leaf.Header.Id);
            else
            {
                await leaf.ExitReadLock();
                continue;
            }

            return leaf;
        }
    }

    private static bool IsSafe(PageBase page)
    {
        switch (page)
        {
            case LeafPage leaf: 
                if (leaf.Header.KeyCount < LeafPage.MaxKeys - 1 && leaf.Header.KeyCount >= LeafPage.MaxKeys / 2 ) 
                    return true;
                break;
            case IndexPage index:
                if (index.Header.KeyCount < IndexPage.MaxKeys - 1 && index.Header.KeyCount >= IndexPage.MaxKeys / 2 ) 
                    return true;
                break;
        }
        
        return false;
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

    private void PinPage(int id, List<int> pinned)
    {
        _pageManager.PinPage(id);
        pinned.Add(id);
    }

    private void UnpinPinnedPages(List<int> pinned)
    {
        foreach (int id in pinned)
            _pageManager.UnpinPage(id);
    }
    
    public async Task<int[]> InOrder()
    {
        var result = new List<int>();
        
        await InOrderTraversal(_rootPageId, result);
        return result.ToArray();
    } 
    
    private async Task InOrderTraversal(int pageId, List<int> result)
    {
        var pageType = await _pageManager.ReadPageType(pageId);
        if (pageType == PageType.LeafPage)
        {
            LeafPage page = await _pageManager.LoadPage<LeafPage>(pageId);
            result.AddRange(page.Keys.Take(page.Header.KeyCount));
            return;
        }
        
        var indexPage = await _pageManager.LoadPage<IndexPage>(pageId);
        if (indexPage.Header.KeyCount == 0)
            return;
        
        foreach (var child in indexPage.ChildrenPageIds.Take(indexPage.Header.KeyCount + 1))
            await InOrderTraversal(child, result);
    }
}