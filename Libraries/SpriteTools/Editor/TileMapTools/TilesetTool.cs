using Editor;
using Sandbox;

namespace SpriteTools.TilesetTool;

[EditorTool]
[Title("Tileset Tool")]
[Description("Paint 2D tiles from a tileset")]
[Icon("dashboard")]
[Group("7")]
[Shortcut("editortool.tileset", "Shift+T")]
public partial class TilesetTool : EditorTool
{
    bool WasGridActive = true;
    int GridSize = 64;

    SceneObject _sceneObject;

    public override void OnEnabled()
    {
        base.OnEnabled();

        AllowGameObjectSelection = false;
        Selection.Clear();
        Selection.Set(this);

        InitGrid();
        InitComponent();
    }

    public override void OnDisabled()
    {
        base.OnDisabled();

        ResetGrid();
    }

    public override void OnUpdate()
    {
        var state = SceneViewportWidget.LastSelected.State;
        using (Gizmo.Scope("grid"))
        {
            Gizmo.Draw.IgnoreDepth = state.Is2D;
            Gizmo.Draw.Grid(state.GridAxis, GridSize, state.GridOpacity);
        }
    }

    void InitComponent()
    {

    }

    void DoGizmo()
    {
        if (_sceneObject.IsValid())
        {

        }
    }

    void InitGrid()
    {
        WasGridActive = SceneViewportWidget.LastSelected.State.ShowGrid;
        GridSize = ProjectCookie.Get<int>("TilesetTool.GridSize", 64);
        SceneViewportWidget.LastSelected.State.ShowGrid = false;
    }

    void ResetGrid()
    {
        ProjectCookie.Set<int>("TilesetTool.GridSize", GridSize);
        SceneViewportWidget.LastSelected.State.ShowGrid = WasGridActive;
    }

}