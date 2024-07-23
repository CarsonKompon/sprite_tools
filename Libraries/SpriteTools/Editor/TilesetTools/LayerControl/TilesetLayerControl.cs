using Editor;
using Sandbox;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SpriteTools.TilesetTool;

public class TilesetLayerControl : Widget
{
    TilesetLayerListControl ParentList;
    TilesetComponent.Layer Layer;

    LabelTextEntry labelText;

    Drag dragData;
    bool draggingAbove = false;
    bool draggingBelow = false;

    public TilesetLayerControl(TilesetLayerListControl list, TilesetComponent.Layer layer)
    {
        ParentList = list;
        Layer = layer;

        VerticalSizeMode = SizeMode.Flexible;

        StatusTip = $"Select Layer \"{Layer.Name}\"";
        Cursor = CursorShape.Finger;

        Layout = Layout.Row();
        Layout.Margin = 4;
        Layout.Spacing = 4;

        var serializedObject = Layer.GetSerialized();
        serializedObject.TryGetProperty(nameof(TilesetComponent.Layer.Name), out var name);
        labelText = new LabelTextEntry(name);
        Layout.Add(labelText);

        var btnVisible = new IconButton("visibility");
        btnVisible.ToolTip = "Toggle Visibility";
        btnVisible.StatusTip = "Toggle Visibility of Layer " + Layer.Name;
        btnVisible.Icon = Layer.IsVisible ? "visibility" : "visibility_off";
        btnVisible.OnClick += () =>
        {
            Layer.IsVisible = !Layer.IsVisible;
            btnVisible.Icon = Layer.IsVisible ? "visibility" : "visibility_off";
        };
        Layout.Add(btnVisible);

        var btnDelete = new IconButton("delete");
        btnDelete.ToolTip = "Delete";
        btnDelete.StatusTip = "Delete Layer " + Layer.Name;
        btnDelete.OnClick += () =>
        {
            DeleteLayerPopup();
        };
        Layout.Add(btnDelete);

        IsDraggable = true;
        AcceptDrops = true;
    }

    protected override void OnPaint()
    {
        if (TilesetTool.Active?.SelectedLayer == Layer)
        {
            Paint.SetBrushAndPen(Theme.Selection.Darken(0.5f));
            Paint.DrawRect(LocalRect, 4);
        }
        else if (IsUnderMouse)
        {
            Paint.SetBrushAndPen(Theme.White.WithAlpha(0.1f));
            Paint.DrawRect(LocalRect, 4);
        }

        if (draggingAbove)
        {
            Paint.SetPen(Theme.Selection, 2f, PenStyle.Dot);
            Paint.DrawLine(LocalRect.TopLeft, LocalRect.TopRight);
            draggingAbove = false;
        }
        else if (draggingBelow)
        {
            Paint.SetPen(Theme.Selection, 2f, PenStyle.Dot);
            Paint.DrawLine(LocalRect.BottomLeft, LocalRect.BottomRight);
            draggingBelow = false;
        }
    }

    void DeleteLayerPopup()
    {
        var popup = new PopupWidget(ParentList);
        popup.Layout = Layout.Column();
        popup.Layout.Margin = 16;
        popup.Layout.Spacing = 8;

        popup.Layout.Add(new Label($"Are you sure you want to delete this Layer?"));

        var button = new Button.Primary("Delete");


        button.MouseClick = () =>
        {
            Delete();
            popup.Visible = false;
        };

        popup.Layout.Add(button);

        var bottomBar = popup.Layout.AddRow();
        bottomBar.AddStretchCell();
        bottomBar.Add(button);

        var popupPos = new Vector2(Editor.Application.CursorPosition.x - 250, Editor.Application.CursorPosition.y);
        popup.Position = popupPos;
        popup.Visible = true;
    }

    void Delete()
    {
        ParentList.DeleteLayer(Layer);
    }

    protected override void OnDragStart()
    {
        base.OnDragStart();

        dragData = new Drag(this);
        dragData.Data.Object = Layer;
        dragData.Execute();
    }

    public override void OnDragHover(DragEvent ev)
    {
        base.OnDragHover(ev);

        if (!TryDragOperation(ev, out var dragDelta))
        {
            draggingAbove = false;
            draggingBelow = false;
            return;
        }

        draggingAbove = dragDelta > 0;
        draggingBelow = dragDelta < 0;
    }

    public override void OnDragDrop(DragEvent ev)
    {
        base.OnDragDrop(ev);

        if (!TryDragOperation(ev, out var delta)) return;

        var list = ParentList.SerializedProperty.GetValue<List<TilesetComponent.Layer>>();
        var index = list.IndexOf(Layer);
        var movingIndex = index + delta;
        var layer = list[movingIndex];

        list.RemoveAt(movingIndex);
        list.Insert(index, layer);

        ParentList.SerializedProperty.SetValue(list);
        ParentList.UpdateList();
    }

    bool TryDragOperation(DragEvent ev, out int delta)
    {
        delta = 0;
        var layer = ev.Data.OfType<TilesetComponent.Layer>().FirstOrDefault();

        if (layer == null || ParentList == null) return false;

        var layerList = ParentList.SerializedProperty.GetValue<List<TilesetComponent.Layer>>();
        var myIndex = layerList.IndexOf(Layer);
        var otherIndex = layerList.IndexOf(layer);
        if (myIndex == -1 || otherIndex == -1) return false;

        delta = otherIndex - myIndex;
        return true;
    }
}