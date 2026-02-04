// using AzotBase.Page;
//
// namespace AzotBase.Tree;
//
// public static class TreeTraversal
// {
//     public static int[] InOrderTraversal(BPlusTreePage page)
//     {
//         var result = new List<int>();
//         InOrderTraversal(page, result);
//         return result.ToArray();
//     } 
//     
//     private static void InOrderTraversal(BPlusTreePage page, List<int> result)
//     {
//         if (node.IsLeaf)
//         {
//             result.AddRange(node.Keys.Take(node.KeyCount));
//             return;
//         }
//
//         foreach (var child in ((IndexPage)node).ChildrenPageIds.Take(node.KeyCount + 1))
//             InOrderTraversal(child, result);
//     }
// }