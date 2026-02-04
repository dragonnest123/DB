namespace Test.Visualizer;

public class RenderNode
{
    public string Text;
    public List<RenderNode> Children = [];
    
    public int Width;
    public int LeftBound;
    public int Center;
    
    public override string ToString() => Text;
}