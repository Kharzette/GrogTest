using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using BSPZone;
using MeshLib;
using UtilityLib;
using MaterialLib;
using ParticleLib;
using AudioLib;
using InputLib;

using SharpDX;
using SharpDX.DXGI;
using SharpDX.Direct3D;

using MatLib = MaterialLib.MaterialLib;


namespace TestZone
{
	internal partial class MapLoop
	{
		enum GameContents
		{
			Water	=(1 << 18),
			Lava	=(1 << 20),
			Slime	=(1 << 19)
		}

		//player character stuff
		IArch					mPArch;
		Character				mPChar;
		MatLib					mPMats;
		AnimLib					mPAnims;
		ShadowHelper.Shadower	mPShad;
		Mobile					mPMob, mPCamMob;
		LightHelper				mPLHelper;
		bool					mbFly	=true;

		//physics stuffs
		Vector3	mVelocity		=Vector3.Zero;
		Vector3	mCamVelocity	=Vector3.Zero;

		//constants
		const float	JogMoveForce		=2000f;	//Fig Newtons
		const float	FlyMoveForce		=1000f;	//Fig Newtons
		const float	FlyUpMoveForce		=300f;	//Fig Newtons
		const float	MidAirMoveForce		=100f;	//Slight wiggle midair
		const float	SwimMoveForce		=900f;	//Swimmery
		const float	SwimUpMoveForce		=900f;	//Swimmery
		const float	StumbleMoveForce	=700f;	//Fig Newtons
		const float	JumpForce			=20000;	//leapometers
		const float	GravityForce		=980f;	//Gravitons
		const float	BouyancyForce		=700f;	//Gravitons
		const float	GroundFriction		=10f;	//Frictols
		const float	StumbleFriction		=6f;	//Frictols
		const float	AirFriction			=0.1f;	//Frictols
		const float	FlyFriction			=2f;	//Frictols
		const float	SwimFriction		=10f;	//Frictols


		void AccumulateVelocity(Vector3 moveVec)
		{
			mCamVelocity	+=moveVec * 0.5f;
		}


		void ApplyFriction(float secDelta, float friction)
		{
			mCamVelocity	-=(friction * mCamVelocity * secDelta * 0.5f);
		}


		void ApplyForce(float force, Vector3 direction, float secDelta)
		{
			mCamVelocity	+=direction * force * (secDelta * 0.5f);
		}


		Vector3 UpdateSwimming(float secDelta, List<Input.InputAction> actions, PlayerSteering ps)
		{
			bool	bSwimUp	=false;

			ps.Method	=PlayerSteering.SteeringMethod.Swim;

			foreach(Input.InputAction act in actions)
			{
				if(act.mAction.Equals(Program.MyActions.Climb))
				{
					//swim up
					bSwimUp	=true;
				}
			}

			Vector3	startPos	=mPCamMob.GetGroundPos();
			Vector3	moveVec		=ps.Update(startPos, mGD.GCam.Forward, mGD.GCam.Left, mGD.GCam.Up, actions);

			moveVec	*=SwimMoveForce;

			AccumulateVelocity(moveVec);

			Vector3	pos	=startPos;

			if(bSwimUp)
			{
				ApplyForce(SwimUpMoveForce, Vector3.Up, secDelta);
			}

			//friction / gravity / bouyancy
			ApplyFriction(secDelta, SwimFriction);
			ApplyForce(GravityForce, Vector3.Down, secDelta);
			ApplyForce(BouyancyForce, Vector3.Up, secDelta);

			pos	+=mCamVelocity * secDelta;

			mCamVelocity	+=moveVec * 0.5f;

			if(bSwimUp)
			{
				ApplyForce(SwimUpMoveForce, Vector3.Up, secDelta);
			}

			//friction / gravity / bouyancy
			ApplyFriction(secDelta, SwimFriction);
			ApplyForce(GravityForce, Vector3.Down, secDelta);
			ApplyForce(BouyancyForce, Vector3.Up, secDelta);

			return	pos;
		}


		Vector3 UpdateFly(float secDelta, List<Input.InputAction> actions, PlayerSteering ps)
		{
			bool	bFlyUp	=false;

			foreach(Input.InputAction act in actions)
			{
				if(act.mAction.Equals(Program.MyActions.Climb))
				{
					//fly up
					bFlyUp	=true;
				}
			}
			Vector3	startPos	=mPCamMob.GetGroundPos();
			Vector3	moveVec		=ps.Update(startPos, mGD.GCam.Forward, mGD.GCam.Left, mGD.GCam.Up, actions);

			moveVec	*=FlyMoveForce;

			AccumulateVelocity(moveVec);
			ApplyFriction(secDelta, FlyFriction);

			Vector3	pos	=startPos;

			if(bFlyUp)
			{
				ApplyForce(FlyUpMoveForce, Vector3.Up, secDelta);
			}
			pos	+=mCamVelocity * secDelta;

			AccumulateVelocity(moveVec);
			ApplyFriction(secDelta, FlyFriction);

			if(bFlyUp)
			{
				ApplyForce(FlyUpMoveForce, Vector3.Up, secDelta);
			}

			return	pos;
		}


		Vector3 UpdateGround(float secDelta, List<Input.InputAction> actions,
							PlayerSteering ps, out bool bJumped)
		{
			bool	bGravity	=false;
			float	friction	=GroundFriction;

			ps.Method	=PlayerSteering.SteeringMethod.FirstPerson;

			if(mPCamMob.IsOnGround())
			{
				if(!mPCamMob.IsBadFooting())
				{
					friction	=GroundFriction;
				}
				else
				{
					friction	=AirFriction;
				}
			}
			else
			{
				bGravity	=true;
				friction	=AirFriction;
			}

			bJumped	=false;

			foreach(Input.InputAction act in actions)
			{
				if(act.mAction.Equals(Program.MyActions.Jump))
				{
					if(mPCamMob.IsOnGround())
					{
						friction	=AirFriction;
						bJumped		=true;
					}
				}
			}

			Vector3	startPos	=mPCamMob.GetGroundPos();
			Vector3	moveVec		=ps.Update(startPos, mGD.GCam.Forward, mGD.GCam.Left, mGD.GCam.Up, actions);

			if(mPCamMob.IsOnGround())
			{
				moveVec	*=JogMoveForce;
			}
			else if(mPCamMob.IsBadFooting())
			{
				moveVec	*=StumbleMoveForce;
			}
			else
			{
				moveVec	*=MidAirMoveForce;
			}

			AccumulateVelocity(moveVec);
			ApplyFriction(secDelta, friction);

			Vector3	pos	=startPos;

			if(bGravity)
			{
				ApplyForce(GravityForce, Vector3.Down, secDelta);
			}
			if(bJumped)
			{
				//mCamVelocity	+=Vector3.Up * JumpForce * 0.5f;
				ApplyForce(JumpForce, Vector3.Up, secDelta);

				//jump use a 60fps delta time for consistency
				pos	+=mCamVelocity * (1f/60f);
			}
			else
			{
				pos	+=mCamVelocity * secDelta;
			}

			AccumulateVelocity(moveVec);
			ApplyFriction(secDelta, friction);
			if(bGravity)
			{
				ApplyForce(GravityForce, Vector3.Down, secDelta);
			}
			if(bJumped)
			{
				ApplyForce(JumpForce, Vector3.Up, secDelta);
			}

			return	pos;
		}


		void UpdateMiscKeys(List<Input.InputAction> actions, PlayerSteering ps)
		{
			foreach(Input.InputAction act in actions)
			{
				if(act.mAction.Equals(Program.MyActions.SpawnTestParticles))
				{
					SpawnTestParticles(mPMob.GetMiddlePos());
				}
				else if(act.mAction.Equals(Program.MyActions.NextAnim)
					&& mAnims.Count > 0)
				{
					mCurAnim++;
					if(mCurAnim >= mAnims.Count)
					{
						mCurAnim	=0;
					}
					mST.ModifyStringText(mFonts[0], "(K) CurAnim: " + mAnims[mCurAnim] + ", " + mCurAnimTime, "AnimStatus");
				}
				else if(act.mAction.Equals(Program.MyActions.NextLevel))
				{
					mCurLevel++;
					if(mCurLevel >= mLevels.Count)
					{
						mCurLevel	=0;
					}
					ChangeLevel(mLevels[mCurLevel]);
					mST.ModifyStringText(mFonts[0], "(L) CurLevel: " + mLevels[mCurLevel], "LevelStatus");
				}
				else if(act.mAction.Equals(Program.MyActions.ToggleFly))
				{
					mbFly		=!mbFly;
					ps.Method	=(mbFly)? PlayerSteering.SteeringMethod.Fly : PlayerSteering.SteeringMethod.FirstPerson;
				}
			}
		}
	}
}
