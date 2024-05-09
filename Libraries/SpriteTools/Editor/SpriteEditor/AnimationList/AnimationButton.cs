using Editor;
using Sandbox;
using System.Collections.Generic;
using System.Linq;

namespace SpriteTools.SpriteEditor.AnimationList;

internal class AnimationButton : Widget
{
    MainWindow MainWindow;
    AnimationList AnimationList;
    public SpriteAnimation Animation;

    public bool Selected => Animation == MainWindow?.SelectedAnimation;
    LabelTextEntry labelText;

    Drag dragData;
    bool draggingAbove = false;
    bool draggingBelow = false;

    public AnimationButton(AnimationList list, MainWindow window, SpriteAnimation animation) : base(null)
    {
        AnimationList = list;
        MainWindow = window;
        Animation = animation;

        if (Animation.Name == MainWindow?.SelectedAnimation?.Name)
        {
            MainWindow.SelectedAnimation = Animation;
        }

        Layout = Layout.Row();
        Layout.Margin = 4;

        var serializedObject = Animation.GetSerialized();
        serializedObject.TryGetProperty(nameof(SpriteAnimation.Name), out var name);
        labelText = new LabelTextEntry(MainWindow, name);

        Layout.Add(labelText);

        var duplicateButton = new IconButton("content_copy");
        duplicateButton.ToolTip = "Duplicate";
        duplicateButton.OnClick += () =>
        {
            DuplicateAnimationPopup();
        };
        Layout.Add(duplicateButton);

        Layout.AddSpacingCell(4);

        var deleteButton = new IconButton("delete");
        deleteButton.ToolTip = "Delete";
        deleteButton.OnClick += () =>
        {
            DeleteAnimationPopup();
        };
        Layout.Add(deleteButton);

        IsDraggable = true;
        AcceptDrops = true;
    }

    protected override void OnContextMenu(ContextMenuEvent e)
    {
        base.OnContextMenu(e);

        var m = new Menu(this);

        m.AddOption("Rename", "edit", Rename);
        m.AddOption("Duplicate", "content_copy", DuplicateAnimationPopup);
        m.AddOption("Delete", "delete", Delete);

        m.OpenAtCursor(false);
    }

    protected override void OnPaint()
    {
        if (Selected)
        {
            Paint.SetBrushAndPen(Theme.Selection.WithAlpha(0.5f));
            Paint.DrawRect(LocalRect);
        }

        if (dragData?.IsValid ?? false)
        {
            Paint.SetBrushAndPen(Theme.Black.WithAlpha(0.5f));
            Paint.DrawRect(LocalRect);
        }

        base.OnPaint();

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

    void DeleteAnimationPopup()
    {
        var popup = new PopupWidget(MainWindow);
        popup.Layout = Layout.Column();
        popup.Layout.Margin = 16;
        popup.Layout.Spacing = 8;

        popup.Layout.Add(new Label($"Are you sure you want to delete this animation?"));

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

        popup.Position = Editor.Application.CursorPosition;
        popup.Visible = true;
    }

    void DuplicateAnimationPopup()
    {
        var popup = new PopupWidget(MainWindow);
        popup.Layout = Layout.Column();
        popup.Layout.Margin = 16;
        popup.Layout.Spacing = 8;

        popup.Layout.Add(new Label($"What would you like to name the duplicated animation?"));

        var entry = new LineEdit(popup);
        entry.Text = $"{Animation.Name} 2";
        var button = new Button.Primary("Duplicate");

        button.MouseClick = () =>
        {
            Duplicate(entry.Text);
            popup.Visible = false;
        };

        entry.ReturnPressed += button.MouseClick;

        popup.Layout.Add(entry);

        var bottomBar = popup.Layout.AddRow();
        bottomBar.AddStretchCell();
        bottomBar.Add(button);

        popup.Position = Editor.Application.CursorPosition;
        popup.Visible = true;

        entry.Focus();
    }

    protected override void OnDragStart()
    {
        base.OnDragStart();

        dragData = new Drag(this);
        dragData.Data.Object = Animation;
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

        MainWindow.PushUndo("Re-Order Animations");

        var oldList = new List<SpriteAnimation>(MainWindow.Sprite.Animations);
        MainWindow.Sprite.Animations = new List<SpriteAnimation>();

        var index = oldList.IndexOf(Animation);
        var newIndex = index + delta;

        for (int i = 0; i < oldList.Count; i++)
        {
            if (i == index) continue;

            if (i == newIndex)
            {
                MainWindow.Sprite.Animations.Add(Animation);
            }

            MainWindow.Sprite.Animations.Add(oldList[i]);
        }

        MainWindow.PushRedo();

        AnimationList.UpdateAnimationList();
    }

    bool TryDragOperation(DragEvent ev, out int delta)
    {
        delta = 0;
        var animation = ev.Data.OfType<SpriteAnimation>().FirstOrDefault();

        if (animation == null || Animation == null || animation == Animation)
        {
            return false;
        }

        var animationList = MainWindow.Sprite.Animations;
        var myIndex = animationList.IndexOf(Animation);
        var otherIndex = animationList.IndexOf(animation);

        if (myIndex == -1 || otherIndex == -1)
        {
            return false;
        }

        delta = otherIndex - myIndex;
        return true;
    }

    void Rename()
    {
        labelText.Edit();
    }

    void Delete()
    {
        MainWindow.PushUndo($"Delete Animation {Animation.Name}");
        MainWindow.Sprite.Animations.Remove(Animation);
        AnimationList.UpdateAnimationList();
        MainWindow.PushRedo();
    }

    void Duplicate(string name)
    {
        if (!string.IsNullOrEmpty(name) && !MainWindow.Sprite.Animations.Any(a => a.Name.ToLowerInvariant() == name.ToLowerInvariant()))
        {
            MainWindow.PushUndo($"Duplicate Animation {Animation.Name}");
            var newAnimation = new SpriteAnimation(name)
            {
                FrameRate = Animation.FrameRate,
                Origin = Animation.Origin,
                Looping = Animation.Looping,
            };
            if (Animation.Frames is not null)
            {
                newAnimation.Frames = new List<string>(Animation.Frames);
            }
            int index = MainWindow.Sprite.Animations.IndexOf(Animation);
            MainWindow.Sprite.Animations.Insert(index + 1, newAnimation);
            AnimationList.UpdateAnimationList();
            MainWindow.PushRedo();
        }
        else
        {
            AnimationList.ShowNamingError(name);
        }
    }
}