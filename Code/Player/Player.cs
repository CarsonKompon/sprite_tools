using System;
using Sandbox;
using SpriteTools;

namespace Quest;

public sealed class Player : Component
{
	public static Player Local { get; private set; }

	[Property] GameObject Body { get; set; }
	[Property] public GameObject PointAt { get; set; }
	[Property] SpriteComponent Sprite { get; set; }
	[Property] float Gravity { get; set; } = 800.0f;
	[Property] float Speed { get; set; } = 100.0f;
	[Property] float GroundControl { get; set; } = 1.0f;
	[Property] float AirControl { get; set; } = 0.2f;

	[RequireComponent] CharacterController CharacterController { get; set; }

	Rotation TargetRotation { get; set; }
	public Vector3 WishVelocity { get; private set; }
	Rotation CamRotation;

	bool footstep = false;

	protected override void OnAwake()
	{
		Local = this;
	}

	protected override void OnStart()
	{
		Sprite.OnBroadcastEvent += OnEvent;
		TargetRotation = Rotation.FromYaw(0f);
	}

    protected override void OnDestroy()
    {
		Sprite.OnBroadcastEvent -= OnEvent;
    }

	float FillAmount = 0f;

	protected override void OnUpdate()
	{
		if (Input.Down("jump"))
		{
			FillAmount = 1;
			Sprite.Tint = Color.Green;
		}
		else
		{
			FillAmount = FillAmount.LerpTo(0,RealTime.Delta * 10.2f);
		}

		Sprite.Fill(FillAmount);
	}

    protected override void OnFixedUpdate()
	{
		var lastWishVelocity = WishVelocity;

		BuildWishVelocity();

		if ( WishVelocity.Length < 0.05f || Vector3.GetAngle( lastWishVelocity, WishVelocity ) > 20f )
			CamRotation = Scene.Camera.Transform.Rotation;
		else
			CamRotation = Rotation.Slerp( CamRotation, Scene.Camera.Transform.Rotation, 1f - MathF.Pow( 0.5f, 1.0f * Time.Delta ) );

		Move();

		float rotLerp = 1f - MathF.Pow(0.5f, 15f * Time.Delta);
		Transform.Rotation = Rotation.Slerp(Transform.Rotation, TargetRotation.Angles().WithYaw(TargetRotation.Yaw() + Scene.Camera.Transform.Rotation.Yaw()), rotLerp);

		if (WishVelocity.Length > 5f)
		{
			Sprite.PlayAnimation("run");
		}
		else
		{
			Sprite.PlayAnimation("idle");
		}

		var idleScale = new Vector3(MathF.Sin(Time.Now * 2f) * 0.05f + 1f, MathF.Cos(Time.Now * 2f) * 0.05f + 1f, 1f);
		Body.Transform.LocalScale = Body.Transform.LocalScale.LerpTo(idleScale, 1f - MathF.Pow(0.5f, 10f * Time.Delta));
	}

	void BuildWishVelocity()
	{
		WishVelocity = 0;
		WishVelocity += Input.AnalogMove.x * CamRotation.Forward.WithZ( 0 );
		WishVelocity += -Input.AnalogMove.y * CamRotation.Right.WithZ( 0 );
		WishVelocity = WishVelocity.Normal * Speed;

		if(Input.AnalogMove.y < -0.1f)
		{
			TargetRotation = Rotation.FromYaw(180f + Random.Shared.Float(-0.1f, 0.1f));
		}
		else if(Input.AnalogMove.y > 0.1f)
		{
			TargetRotation = Rotation.FromYaw(Random.Shared.Float(-0.1f, 0.1f));
		}
	}

	void Move()
	{
		if ( CharacterController.IsOnGround )
		{
			CharacterController.Velocity = CharacterController.Velocity.WithZ( 0 );
			CharacterController.Accelerate( WishVelocity );
			CharacterController.ApplyFriction( GroundControl );
		}
		else
		{
			CharacterController.Velocity += Vector3.Down * Gravity * Time.Delta * 0.5f;
			CharacterController.Accelerate( WishVelocity.ClampLength( Speed ) );
			CharacterController.ApplyFriction( AirControl );
		}

		CharacterController.Move();

		if ( !CharacterController.IsOnGround )
		{
			CharacterController.Velocity += Vector3.Down * Gravity * Time.Delta * 0.5f;
		}
		else
		{
			CharacterController.Velocity = CharacterController.Velocity.WithZ( 0 );
		}
	}

	void OnEvent(string eventName)
	{
		switch (eventName)
		{
			case "step":
				Footstep();
				break;
		}
	}

	void Footstep()
	{
		var tr = Scene.Trace.Ray(Transform.Position + Vector3.Up * 10f, Transform.Position + Vector3.Down * 10f)
			.WithoutTags( "player", "trigger" )
			.Run();

		if(tr.Hit && tr.Surface is not null)
		{
			var sound = footstep ? tr.Surface.Sounds.FootLeft : tr.Surface.Sounds.FootRight;
			if(sound is not null)
			{
				Sound.Play(sound, Transform.Position);
				footstep = !footstep;
			}
		}

		Body.Transform.LocalScale = new Vector3(0.8f, 1.2f, 0.9f);
	}
}