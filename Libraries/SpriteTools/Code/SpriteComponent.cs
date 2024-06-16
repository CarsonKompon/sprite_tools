using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mail;
using System.Text.Json.Serialization;
using Sandbox;

namespace SpriteTools;

[Icon("emoji_emotions")]
public sealed class SpriteComponent : Component, Component.ExecuteInEditor
{
    /// <summary>
    /// The sprite resource that this component uses.
    /// </summary>
    [Property]
    SpriteResource Sprite
    {
        get => _sprite;
        set
        {
            var lastSprite = _sprite;
            _sprite = value;
            if (_sprite != null)
            {
                PlayAnimation(StartingAnimationName);
            }
            else
                CurrentAnimation = null;
            UpdateSprite();
        }
    }
    SpriteResource _sprite;

    /// <summary>
    /// The color tint of the Sprite.
    /// </summary>
    [Property]
    public Color Tint
    {
        get => SceneObject?.ColorTint ?? Color.White;
        set
        {
            if (SceneObject != null)
                SceneObject.ColorTint = value;
        }
    }

    /// <summary>
    /// Used to override the material with your own. Useful for custom shaders.
    /// Shader requires a texture parameter named "Texture".
    /// </summary>
    [Property]
    public Material MaterialOverride
    {
        get => _materialOverride;
        set
        {
            _materialOverride = value;
            SpriteMaterial = null;
        }
    }
    Material _materialOverride;
    private Material SpriteMaterial { get; set; }

    /// <summary>
    /// The playback speed of the animation.
    /// </summary>
    [Property] public float PlaybackSpeed { get; set; } = 1.0f;


    /// <summary>
    /// Whether or not the sprite should render itself/its shadows.
    /// </summary>
    [Property]
    [Category("Lighting")]
    public ShadowRenderType CastShadows { get; set; } = ShadowRenderType.On;

    /// <summary>
    /// A dictionary of broadcast events that this component will send (populated based on the Sprite resource)
    /// </summary>
    [JsonIgnore]
    public Dictionary<string, Action> BroadcastEvents = new();

    /// <summary>
    /// The sprite animation that is currently playing.
    /// </summary>
    [JsonIgnore]
    public SpriteAnimation CurrentAnimation { get; private set; }

    [Property, Category("Sprite"), Title("Current Animation"), AnimationName]
    private string StartingAnimationName
    {
        get => CurrentAnimation?.Name ?? "";
        set
        {
            if (Sprite == null) return;
            var animation = Sprite.Animations.Find(a => a.Name.ToLowerInvariant() == value.ToLowerInvariant());
            if (animation == null) return;
            PlayAnimation(animation.Name);
            _startingAnimationName = value.ToLowerInvariant();
        }
    }
    string _startingAnimationName = "";


    [Property, Category("Sprite"), JsonIgnore]
    BroadcastControls _broadcastEvents = new();

    [Property, Category("Sprite")]
    public bool CreateAttachPoints
    {
        get => _createAttachPoints;
        set
        {
            if (_createAttachPoints != value)
            {
                _createAttachPoints = value;

                AttachPoints.Clear();
                if (value)
                {
                    BuildAttachPoints();
                }
            }
        }
    }
    bool _createAttachPoints = false;
    Dictionary<string, GameObject> AttachPoints = new();

    /// <summary>
    /// Invoked when a broadcast event is triggered.
    /// </summary>
    [Property, Group("Sprite")]
    public Action<string> OnBroadcastEvent { get; set; }

    /// <summary>
    /// The current frame index of the animation playing.
    /// </summary>
    public int CurrentFrameIndex
    {
        get => _currentFrameIndex;
        set
        {
            _currentFrameIndex = value;
            if (CurrentAnimation is not null)
            {
                if (_currentFrameIndex >= CurrentAnimation.Frames.Count)
                    _currentFrameIndex = 0;
                SpriteMaterial?.Set("g_vOffset", CurrentTexture.GetFrameOffset(CurrentFrameIndex));
            }
        }
    }
    private int _currentFrameIndex = 0;
    private float _timeSinceLastFrame = 0;

    internal SceneObject SceneObject { get; set; }
    TextureAtlas CurrentTexture { get; set; }

    protected override void OnStart()
    {
        base.OnStart();

        if (Sprite is null) return;
        if (Sprite.Animations.Count > 0)
        {
            var anim = Sprite.Animations.FirstOrDefault(x => x.Name.ToLowerInvariant() == StartingAnimationName);
            if (anim is null)
                anim = Sprite.Animations.FirstOrDefault();
            PlayAnimation(anim.Name);
        }

        UpdateSceneObject();
    }

    protected override void OnAwake()
    {
        base.OnAwake();

        SceneObject ??= new SceneObject(Scene.SceneWorld, Model.Load("models/sprite_quad.vmdl"))
        {
            Flags = { IsTranslucent = true }
        };
    }

    protected override void OnEnabled()
    {
        base.OnEnabled();

        if (SceneObject.IsValid())
            SceneObject.RenderingEnabled = true;

        if (CreateAttachPoints)
            BuildAttachPoints();
    }

    protected override void OnDisabled()
    {
        base.OnDisabled();

        if (SceneObject.IsValid())
            SceneObject.RenderingEnabled = false;
    }

    protected override void DrawGizmos()
    {
        base.DrawGizmos();

        if (Game.IsPlaying) return;

        // Move bbox by origin
        var bbox = SceneObject.LocalBounds;
        bbox = bbox.Rotate(SceneObject.Transform.Rotation);
        var pos = (Transform.Position - SceneObject.Transform.Position) * Transform.Rotation;
        bbox = bbox.Translate(pos);
        Gizmo.Hitbox.BBox(bbox);

        if (Gizmo.IsHovered)
        {
            using (Gizmo.Scope("hover"))
            {
                Gizmo.Draw.Color = Color.Orange;
                Gizmo.Draw.LineBBox(bbox);
            }
        }
    }
    public void Fill(float shouldFill = 1.0f )
    {
        SpriteMaterial.Set("g_flFill", shouldFill );
	}

    internal void UpdateSceneObject()
    {
        if (SpriteMaterial is null)
        {
            if (MaterialOverride != null)
                SpriteMaterial = MaterialOverride.CreateCopy();
            else
                SpriteMaterial = Material.Create("spritemat", "shaders/pixelated_masked.shader");
            if (CurrentTexture is not null)
            {
                SpriteMaterial.Set("Texture", CurrentTexture);
                SpriteMaterial.Set("g_vTiling", CurrentTexture.GetFrameTiling());
                SpriteMaterial.Set("g_vOffset", CurrentTexture.GetFrameOffset(CurrentFrameIndex));
            }
            SceneObject.SetMaterialOverride(SpriteMaterial);
        }

        SceneObject.RenderingEnabled = true;
        SceneObject.Flags.ExcludeGameLayer = CastShadows == ShadowRenderType.ShadowsOnly;
        SceneObject.Flags.CastShadows = CastShadows == ShadowRenderType.On || CastShadows == ShadowRenderType.ShadowsOnly;

        if (CurrentAnimation == null)
        {
            SceneObject.Transform = new Transform(Transform.Position, Transform.Rotation, Transform.Scale);
            SceneObject.RenderingEnabled = false;
            return;
        }

        var frameRate = (1f / ((PlaybackSpeed == 0) ? 0 : (CurrentAnimation.FrameRate * Math.Abs(PlaybackSpeed))));
        _timeSinceLastFrame += ((Game.IsPlaying) ? Time.Delta : RealTime.Delta);

        if (PlaybackSpeed > 0 && _timeSinceLastFrame >= frameRate)
        {
            var frame = CurrentFrameIndex;
            frame++;
            if (frame >= CurrentAnimation.Frames.Count)
                frame = 0;
            CurrentFrameIndex = frame;
            var currentFrame = CurrentAnimation.Frames[CurrentFrameIndex];
            foreach (var tag in currentFrame.Events)
            {
                BroadcastEvent(tag);
            }
            _timeSinceLastFrame = 0;
        }
        else if (PlaybackSpeed < 0 && _timeSinceLastFrame >= frameRate)
        {
            var frame = CurrentFrameIndex;
            frame--;
            if (frame < 0)
                frame = CurrentAnimation.Frames.Count - 1;
            CurrentFrameIndex = frame;
            _timeSinceLastFrame = 0;
        }

        // var texture = Texture.Load(FileSystem.Mounted, CurrentAnimation.Frames[CurrentFrameIndex].FilePath);
        // if (texture is not null)
        //     SpriteMaterial.Set("Texture", texture);

        // Add pivot to transform
        var pos = Transform.Position;
        var scale = Transform.Scale;
        var origin = CurrentAnimation.Origin - new Vector2(0.5f, 0.5f);
        pos -= new Vector3(origin.y, origin.x, 0) * 100f * scale;
        pos = pos.RotateAround(Transform.Position, Transform.Rotation);
        SceneObject.Transform = new Transform(pos, Transform.Rotation, scale);
    }

    internal void UpdateAttachments()
    {
        if (AttachPoints is not null && AttachPoints.Count > 0)
        {
            foreach (var attachment in AttachPoints)
            {
                var attachPos = CurrentAnimation.GetAttachmentPosition(attachment.Key, CurrentFrameIndex);
                var origin = CurrentAnimation.Origin - new Vector2(0.5f, 0.5f);
                var pos = (new Vector3(attachPos.y, attachPos.x, 0) - (Vector3.One.WithZ(0) / 2f) - new Vector3(origin.y, origin.x, 0)) * 100f;
                pos *= Transform.LocalScale;
                // pos = pos.RotateAround(Transform.Position, Transform.Rotation);
                attachment.Value.Transform.LocalPosition = pos;
            }
        }
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        SceneObject?.Delete();
        SceneObject = null;
    }

    internal void UpdateSprite()
    {
        BroadcastEvents.Clear();
        if (Sprite == null)
        {
            CurrentAnimation = null;
            return;
        }

        foreach (var animation in Sprite.Animations)
        {
            foreach (var frame in animation.Frames)
            {
                foreach (var tag in frame.Events)
                {
                    if (!BroadcastEvents.ContainsKey(tag))
                        BroadcastEvents[tag] = () => { };
                }
            }
        }
    }

    public void PlayAnimation(string animationName)
    {
        if (Sprite == null) return;
        if (CurrentAnimation?.Name == animationName) return;

        var animation = Sprite.Animations.FirstOrDefault(a => a.Name.ToLowerInvariant() == animationName.ToLowerInvariant());
        if (animation == null) return;

        CurrentAnimation = animation;
        CurrentFrameIndex = 0;

        var atlas = TextureAtlas.FromSprites(animation.Frames.Select(x => x.FilePath).ToList());
        CurrentTexture = atlas;
        SpriteMaterial?.Set("Texture", CurrentTexture);
        SpriteMaterial?.Set("g_vTiling", CurrentTexture.GetFrameTiling());
    }

    void BroadcastEvent(string tag)
    {
        OnBroadcastEvent?.Invoke(tag);
        if (BroadcastEvents.ContainsKey(tag))
            BroadcastEvents[tag]?.Invoke();
    }

    internal void BuildAttachPoints()
    {
        if (Sprite is null) return;
        var attachments = Sprite.GetAttachmentNames();
        foreach (var attachment in attachments)
        {
            var go = GameObject.Children.FirstOrDefault(x => x.Name == attachment);
            if (go is null)
            {
                go = Scene.CreateObject();
                go.Parent = GameObject;
            }

            AttachPoints[attachment] = go;
            go.Flags |= GameObjectFlags.Bone;
            go.Name = attachment;
        }
    }

    public enum ShadowRenderType
    {
        [Icon("wb_shade")]
        [Description("Render the sprite with shadows (default)")]
        On,
        [Icon("wb_twilight")]
        [Description("Render the sprite without shadows")]
        Off,
        [Icon("hide_source")]
        [Title("Shadows Only")]
        [Description("Render ONLY the sprites shadows")]
        ShadowsOnly
    }

    public class BroadcastControls { }
    public class AnimationNameAttribute : Attribute
    {
        public string Parameter { get; set; } = "Sprite";
    }
}