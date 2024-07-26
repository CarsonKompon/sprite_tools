using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mail;
using Editor;
using Sandbox;

namespace SpriteTools.TilesetEditor.Preview;

public class RenderingWidget : SpriteRenderingWidget
{
    MainWindow MainWindow;

    float planeWidth;
    float planeHeight;
    float startX;
    float startY;
    float frameWidth;
    float frameHeight;
    float xSeparation;
    float ySeparation;

    public RenderingWidget(MainWindow window, Widget parent) : base(parent)
    {
        MainWindow = window;
    }

    [EditorEvent.Frame]
    public void Frame()
    {
        SceneInstance.Input.IsHovered = IsUnderMouse;
        SceneInstance.UpdateInputs(Camera, this);

        Dictionary<Vector2, TilesetResource.Tile> tiles = new();
        foreach (var tile in MainWindow.Tileset.Tiles)
        {
            for (int i = 0; i < tile.SheetRect.Size.x; i++)
            {
                for (int j = 0; j < tile.SheetRect.Size.y; j++)
                {
                    var realTile = (i == 0 && j == 0) ? tile : null;
                    tiles.Add(tile.SheetRect.Position + new Vector2(i, j), realTile);
                }
            }
        }

        using (SceneInstance.Push())
        {
            var hasTiles = (MainWindow?.Tileset?.Tiles?.Count ?? 0) > 0;

            planeWidth = 100f * TextureRect.Transform.Scale.y;
            planeHeight = 100f * TextureRect.Transform.Scale.x;
            startX = -(planeWidth / 2f);
            startY = -(planeHeight / 2f);
            frameWidth = MainWindow.Tileset.TileSize / TextureSize.x * planeWidth;
            frameHeight = MainWindow.Tileset.TileSize / TextureSize.y * planeHeight;
            xSeparation = MainWindow.Tileset.TileSeparation.x / TextureSize.x * planeWidth;
            ySeparation = MainWindow.Tileset.TileSeparation.y / TextureSize.y * planeHeight;

            if (hasTiles)
            {
                int framesPerRow = MainWindow.Tileset.CurrentTextureSize.x / MainWindow.Tileset.CurrentTileSize;
                int framesPerHeight = MainWindow.Tileset.CurrentTextureSize.y / MainWindow.Tileset.CurrentTileSize;

                using (Gizmo.Scope("tiles"))
                {
                    Gizmo.Draw.Color = new Color(0.1f, 0.4f, 1f);
                    Gizmo.Draw.LineThickness = 2f;

                    int xi = 0;
                    int yi = 0;

                    TilesetResource.Tile selectedTile = null;

                    if (framesPerRow * framesPerHeight < 2048)
                    {
                        while (yi < framesPerHeight)
                        {
                            while (xi < framesPerRow)
                            {
                                if (tiles.TryGetValue(new Vector2(xi, yi), out var tile))
                                {
                                    if (tile is not null)
                                    {
                                        if (MainWindow.SelectedTile == tile)
                                            selectedTile = tile;
                                        else
                                            TileControl(xi, yi, tiles[new Vector2(xi, yi)]);
                                    }
                                }
                                else
                                {
                                    EmptyTileControl(xi, yi);
                                }
                                xi++;
                            }
                            xi = 0;
                            yi++;
                        }

                        if (selectedTile is not null)
                        {
                            TileControl((int)selectedTile.SheetRect.Position.x, (int)selectedTile.SheetRect.Position.y, selectedTile);
                        }
                    }
                }
            }

            if (!hasTiles || MainWindow.inspector.btnRegenerate.IsUnderMouse)
            {
                int framesPerRow = (int)TextureSize.x / MainWindow.Tileset.TileSize;
                int framesPerHeight = (int)TextureSize.y / MainWindow.Tileset.TileSize;

                using (Gizmo.Scope("setup"))
                {
                    Gizmo.Draw.Color = Color.White.WithAlpha(0.4f);
                    Gizmo.Draw.LineThickness = 1f;

                    int xi = 0;
                    int yi = 0;

                    if (framesPerRow * framesPerHeight < 2048)
                    {

                        while (yi < framesPerHeight)
                        {
                            while (xi < framesPerRow)
                            {
                                var x = startX + (xi * frameWidth) + (xi * xSeparation);
                                var y = startY + (yi * frameHeight) + (yi * ySeparation);
                                DrawBox(x, y, frameWidth, frameHeight);
                                xi++;
                            }
                            xi = 0;
                            yi++;
                        }
                    }

                }
            }

        }
    }

    void TileControl(int xi, int yi, TilesetResource.Tile tile)
    {
        using (Gizmo.Scope($"tile_{xi}_{yi}", Transform.Zero))
        {
            int sizeX = 1;
            int sizeY = 1;

            var x = startX + (xi * frameWidth * sizeX + xi * xSeparation);
            var y = startY + (yi * frameHeight * sizeY + yi * ySeparation);
            var width = frameWidth * sizeX;
            var height = frameHeight * sizeY;

            var bbox = BBox.FromPositionAndSize(new Vector3(y + height / 2f, x + width / 2f, 1f), new Vector3(height, width, 1f));
            Gizmo.Hitbox.BBox(bbox);

            bool isSelected = MainWindow.SelectedTile == tile;
            if (isSelected)
            {
                Gizmo.Draw.LineThickness = 4;
                Gizmo.Draw.Color = Color.Yellow;
            }

            if (Gizmo.IsHovered)
            {
                using (Gizmo.Scope("hover"))
                {
                    Gizmo.Draw.Color = Gizmo.Draw.Color.WithAlpha(0.5f);
                    Gizmo.Draw.SolidBox(bbox);
                }
                if (Gizmo.WasLeftMousePressed)
                {
                    MainWindow.SelectedTile = tile;
                }
                else if (Gizmo.WasRightMousePressed)
                {
                    MainWindow.Tileset.Tiles.Remove(tile);
                    MainWindow.inspector.UpdateControlSheet();
                    if (isSelected) MainWindow.SelectedTile = MainWindow.Tileset.Tiles?.FirstOrDefault() ?? null;
                }
            }

            DrawBox(x, y, width, height);
        }
    }

    void EmptyTileControl(int xi, int yi)
    {
        using (Gizmo.Scope($"tile_{xi}_{yi}", Transform.Zero))
        {
            Gizmo.Draw.Color = Gizmo.Draw.Color.WithAlpha(0.1f);

            var x = startX + (xi * frameWidth + xi * xSeparation);
            var y = startY + (yi * frameHeight + yi * ySeparation);
            var width = frameWidth;
            var height = frameHeight;

            var bbox = BBox.FromPositionAndSize(new Vector3(y + height / 2f, x + width / 2f, 1f), new Vector3(height, width, 1f));
            Gizmo.Hitbox.BBox(bbox);

            if (Gizmo.IsHovered)
            {
                using (Gizmo.Scope("hover"))
                {
                    Gizmo.Draw.Color = Gizmo.Draw.Color.WithAlpha(0.2f);
                    Gizmo.Draw.SolidBox(bbox);
                }
                if (Gizmo.WasLeftMousePressed)
                {
                    var tile = new TilesetResource.Tile(new Rect(new Vector2(xi, yi), 1))
                    {
                        Tileset = MainWindow.Tileset
                    };
                    MainWindow.Tileset.Tiles.Add(tile);
                    MainWindow.inspector.UpdateControlSheet();
                }
            }

            DrawBox(x, y, width, height);
        }
    }

    void DrawBox(float x, float y, float width, float height)
    {
        Gizmo.Draw.Line(new Vector3(y, x, 0), new Vector3(y, x + width, 0));
        Gizmo.Draw.Line(new Vector3(y, x, 0), new Vector3(y + height, x, 0));
        Gizmo.Draw.Line(new Vector3(y + height, x, 0), new Vector3(y + height, x + width, 0));
        Gizmo.Draw.Line(new Vector3(y + height, x + width, 0), new Vector3(y, x + width, 0));
    }
}