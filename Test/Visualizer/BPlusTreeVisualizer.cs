// using AzotBase.Tree;
//
// namespace Test.Visualizer;
//
// public static class BPlusTreeVisualizer
// {
//     private const int spaceBetweenNodes = 2;
//     
//     public static void Display(this BPlusTree tree, Action<string> writeLine)
//     {
//         var root = ((IBPlusTreeDebugView)tree).Root;
//         var renderTree = Build(root);
//         ComputeTreeParameters(renderTree);
//         Display(renderTree, writeLine);
//     }
//     
//     private static RenderNode Build(Node node)
//     {
//         var renderNode = new RenderNode()
//         {
//             Text = "[" + string.Join(" ", node.Keys.Take(node.KeyCount)) + "]",
//         };
//
//         if (node.IsLeaf) 
//             return renderNode;
//         
//         foreach (var child in ((IndexNode)node).Children.Take(node.KeyCount + 1))
//             renderNode.Children.Add(Build(child));
//
//         return renderNode;
//     }
//
//     private static void ComputeTreeParameters(RenderNode root)
//     {
//         var stack = new Stack<(RenderNode node, int nextChild)>();
//         stack.Push((root, 0));
//         var currentPos = 0;
//
//         while (stack.Count > 0)
//         {
//             var (node, childIndex) = stack.Pop();
//
//             if (childIndex < node.Children.Count)
//             {
//                 stack.Push((node, childIndex + 1));
//                 stack.Push((node.Children[childIndex], 0));
//             }
//             else
//                 ComputeNodeParameters(node, ref currentPos);
//         }
//     }
//
//     private static void ComputeNodeParameters(RenderNode node, ref int nodePos)
//     {
//         if (node.Children.Count == 0)
//         {
//             node.LeftBound = nodePos;
//             node.Width = node.Text.Length;
//             node.Center = node.LeftBound + node.Width / 2;
//             nodePos += node.Width + spaceBetweenNodes;
//             return;
//         }
//         
//         node.LeftBound = node.Children[0].LeftBound;
//         node.Width = node.Children[^1].LeftBound + node.Children[^1].Width - node.Children[0].LeftBound;
//         node.Center = node.LeftBound + node.Width / 2;
//     }
//
//     private static void Display(RenderNode root, Action<string> writeLine)
//     {
//         var nodesDepth = new Dictionary<int, List<RenderNode>>();
//         GetNodesDepth(nodesDepth, root, 1);
//
//         foreach (var nodeList in nodesDepth.Values)
//         {
//             DisplayNodes(nodeList, writeLine);
//             DisplayConnections(nodeList, writeLine);
//         } 
//     }
//
//     private static void DisplayNodes(List<RenderNode> nodeList, Action<string> writeLine)
//     {
//         var last = nodeList[^1];
//         var totalLength = last.LeftBound + last.Width + 1;
//         var buffer = Enumerable.Repeat(' ', totalLength).ToArray();
//         buffer[0] = '.';
//             
//         foreach (var node in nodeList)
//         {
//             var start = node.Center - node.Text.Length / 2;
//                 
//             for (int i = 0; i < node.Text.Length; i++)
//                 buffer[start + i] = node.Text[i];
//         }
//         writeLine(new string(buffer));
//     }
//
//     private static void DisplayConnections(List<RenderNode> nodeList, Action<string> writeLine)
//     {
//         if (nodeList[0].Children.Count == 0)
//             return;
//             
//         var firstChild = nodeList[0].Children[0];
//         var lastChild = nodeList[^1].Children[^1];
//         var totalParentLength = nodeList[^1].LeftBound + nodeList[^1].Width + 1;
//         var verticalLinesBuffer = Enumerable.Repeat(' ', totalParentLength).ToArray();
//         var horizontalLinesBuffer = new char[lastChild.Center + 1];
//         
//         for (int i = 0; i < horizontalLinesBuffer.Length; i++)
//         {
//             if (i < firstChild.Center)
//                 horizontalLinesBuffer[i] = ' ';
//             else
//                 horizontalLinesBuffer[i] = '\u2500';
//         }
//         
//         foreach (var node in nodeList)
//         {
//             verticalLinesBuffer[node.Center] = '|';
//             
//             foreach (var child in node.Children)
//             {
//                 if (child.Center == firstChild.Center)
//                     horizontalLinesBuffer[child.Center] = '\u250C';
//                 else if (child.Center == lastChild.Center)
//                     horizontalLinesBuffer[child.Center] = '\u2510';
//                 else
//                     horizontalLinesBuffer[child.Center] = '\u252C';
//             }
//         }
//         writeLine(new string(verticalLinesBuffer));
//         writeLine(new string(horizontalLinesBuffer));
//     }
//
//     private static void GetNodesDepth(Dictionary<int, List<RenderNode>> list, RenderNode node, int deep)
//     {
//         if (!list.ContainsKey(deep))
//             list[deep] = [];
//         
//         list[deep].Add(node);
//         foreach (var child in node.Children)
//             GetNodesDepth(list, child, deep + 1);
//     }
// }