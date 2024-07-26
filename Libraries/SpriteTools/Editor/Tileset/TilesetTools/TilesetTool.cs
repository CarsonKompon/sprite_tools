using System;
using System.Buffers;
using System.Linq;
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
    public static TilesetTool Active { get; private set; }

    public TilesetComponent SelectedComponent;
    public TilesetComponent.Layer SelectedLayer
    {
        get => _selectedLayer;
        set
        {
            if (_selectedLayer == value) return;

            _selectedLayer = value;
            _sceneObject?.UpdateTileset(value.TilesetResource);
        }
    }
    TilesetComponent.Layer _selectedLayer;

    public int SelectedIndex { get; set; } = 0;

    internal Action UpdateInspector;

    bool WasGridActive = true;
    int GridSize => SelectedLayer?.TilesetResource?.TileSize ?? 32;
    int CurrentLayerIndex => SelectedComponent.Layers.IndexOf(SelectedLayer);

    TilesetPreviewObject _sceneObject;

    public override void OnEnabled()
    {
        Active = this;

        base.OnEnabled();

        AllowGameObjectSelection = false;
        Selection.Clear();
        Selection.Set(this);

        InitGrid();
        InitPreviewObject();
        UpdateComponent();
    }

    public override void OnDisabled()
    {
        Active = null;

        base.OnDisabled();

        ResetGrid();
        RemovePreviewObject();
    }

    public override void OnUpdate()
    {
        base.OnUpdate();

        var state = SceneViewportWidget.LastSelected.State;
        var gridSize = GridSize;
        using (Gizmo.Scope("grid"))
        {
            Gizmo.Draw.IgnoreDepth = state.Is2D;
            Gizmo.Draw.Grid(state.GridAxis, gridSize, state.GridOpacity);
        }

        if (_sceneObject.IsValid() && SelectedLayer?.TilesetResource is not null)
        {
            var cursorRay = Gizmo.CurrentRay;
            var tr = SceneEditorSession.Active.Scene.Trace
                .Ray(cursorRay, 32000f)
                .Run();

            _sceneObject.Transform = new Transform(
                (tr.EndPosition - new Vector3(gridSize / 2f, gridSize / 2f, CurrentLayerIndex + 0.5f)).SnapToGrid(gridSize).WithZ(CurrentLayerIndex),
                Rotation.Identity, 1
            );
            _sceneObject.Flags.CastShadows = false;
            _sceneObject.Flags.IsOpaque = false;
            _sceneObject.Flags.IsTranslucent = true;
            _sceneObject.RenderingEnabled = true;
        }
    }

    void UpdateComponent()
    {
        var component = Scene.GetAllComponents<TilesetComponent>().FirstOrDefault();

        if (!component.IsValid())
        {
            var go = new GameObject()
            {
                Name = "Tileset Object"
            };
            component = go.Components.GetOrCreate<TilesetComponent>();
        }

        if (component.IsValid())
        {
            SelectedComponent = component;
            SelectedLayer = SelectedComponent.Layers.FirstOrDefault();
        }
    }

    void InitPreviewObject()
    {
        RemovePreviewObject();

        _sceneObject = new TilesetPreviewObject(this, Scene.SceneWorld);
        if (SelectedLayer is not null)
            _sceneObject.UpdateTileset(SelectedLayer.TilesetResource);
    }

    void RemovePreviewObject()
    {
        _sceneObject?.Delete();
        _sceneObject = null;
    }

    void InitGrid()
    {
        WasGridActive = SceneViewportWidget.LastSelected.State.ShowGrid;
        SceneViewportWidget.LastSelected.State.ShowGrid = false;
    }

    void ResetGrid()
    {
        SceneViewportWidget.LastSelected.State.ShowGrid = WasGridActive;
    }

}

internal sealed class TilesetPreviewObject : SceneCustomObject
{
    TilesetTool Tool;
    Material Material;

    public TilesetPreviewObject(TilesetTool tool, SceneWorld world) : base(world)
    {
        Tool = tool;
    }

    public void UpdateTileset(TilesetResource tileset)
    {
        if (tileset is null) return;
        Material = Material.Load("materials/sprite_2d.vmat").CreateCopy();
        Material.Set("Texture", Texture.Load(Sandbox.FileSystem.Mounted, tileset.FilePath));
    }

    public override void RenderSceneObject()
    {
        var layer = Tool?.SelectedLayer;
        if (layer is null) return;

        var tileset = layer.TilesetResource;
        if (tileset is null) return;

        var tiling = tileset.GetTiling();
        var offset = tileset.GetOffset(4);

        var position = Vector3.Zero;
        var size = new Vector2(tileset.TileSize, tileset.TileSize);

        var topLeft = new Vector3(position.x, position.y, position.z);
        var topRight = new Vector3(position.x + size.x, position.y, position.z);
        var bottomRight = new Vector3(position.x + size.x, position.y + size.y, position.z);
        var bottomLeft = new Vector3(position.x, position.y + size.y, position.z);

        var uvTopLeft = new Vector2(offset.x * tiling.x, offset.y * tiling.y);
        var uvTopRight = new Vector2((offset.x + 1) * tiling.x, offset.y * tiling.y);
        var uvBottomRight = new Vector2((offset.x + 1) * tiling.x, (offset.y + 1) * tiling.y);
        var uvBottomLeft = new Vector2(offset.x * tiling.x, (offset.y + 1) * tiling.y);

        var vertex = ArrayPool<Vertex>.Shared.Rent(6);

        vertex[0] = new Vertex(topLeft);
        vertex[0].TexCoord0 = uvTopLeft;
        vertex[0].TexCoord1 = new Vector4(0, 0, 0, 0);
        vertex[0].Color = Color.White;
        vertex[0].Normal = Vector3.Up;

        vertex[1] = new Vertex(topRight);
        vertex[1].TexCoord0 = uvTopRight;
        vertex[1].TexCoord1 = new Vector4(0, 0, 0, 0);
        vertex[1].Color = Color.White;
        vertex[1].Normal = Vector3.Up;

        vertex[2] = new Vertex(bottomRight);
        vertex[2].TexCoord0 = uvBottomRight;
        vertex[2].TexCoord1 = new Vector4(0, 0, 0, 0);
        vertex[2].Color = Color.White;
        vertex[2].Normal = Vector3.Up;

        vertex[3] = new Vertex(bottomRight);
        vertex[3].TexCoord0 = uvBottomRight;
        vertex[3].TexCoord1 = new Vector4(0, 0, 0, 0);
        vertex[3].Color = Color.White;
        vertex[3].Normal = Vector3.Up;

        vertex[4] = new Vertex(bottomLeft);
        vertex[4].TexCoord0 = uvBottomLeft;
        vertex[4].TexCoord1 = new Vector4(0, 0, 0, 0);
        vertex[4].Color = Color.White;
        vertex[4].Normal = Vector3.Up;

        vertex[5] = new Vertex(topLeft);
        vertex[5].TexCoord0 = uvTopLeft;
        vertex[5].TexCoord1 = new Vector4(0, 0, 0, 0);
        vertex[5].Color = Color.White;
        vertex[5].Normal = Vector3.Up;

        Graphics.Draw(vertex, 6, Material, Attributes);
    }
}