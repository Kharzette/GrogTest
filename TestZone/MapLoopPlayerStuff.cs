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
		const float	FlyUpMoveForce		=30f;	//Fig Newtons
		const float	MidAirMoveForce		=100f;	//Slight wiggle midair
		const float	SwimMoveForce		=900f;	//Swimmery
		const float	SwimUpMoveForce		=60f;	//Swimmery
		const float	StumbleMoveForce	=700f;	//Fig Newtons
		const float	JumpForce			=390;	//leapometers
		const float	GravityForce		=980f;	//Gravitons
		const float	BouyancyForce		=700f;	//Gravitons
		const float	GroundFriction		=10f;	//Frictols
		const float	StumbleFriction		=6f;	//Frictols
		const float	AirFriction			=0.1f;	//Frictols
		const float	FlyFriction			=2f;	//Frictols
		const float	SwimFriction		=10f;	//Frictols


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

			mCamVelocity	+=moveVec * 0.5f;

			Vector3	pos	=startPos;

			if(bSwimUp)
			{
				Vector3	swimVec	=Vector3.Up * SwimUpMoveForce * 0.5f;
				mCamVelocity	+=swimVec * 0.5f;
			}

			//friction / gravity / bouyancy
			mCamVelocity	-=(SwimFriction * mCamVelocity * secDelta * 0.5f);
			mCamVelocity	+=Vector3.Down * GravityForce * (secDelta * 0.5f);
			mCamVelocity	+=Vector3.Up * BouyancyForce * (secDelta * 0.5f);

			pos	+=mCamVelocity * secDelta;

			mCamVelocity	+=moveVec * 0.5f;

			if(bSwimUp)
			{
				Vector3	swimVec	=Vector3.Up * SwimUpMoveForce * 0.5f;
				mCamVelocity	+=swimVec * 0.5f;
			}

			//friction / gravity / bouyancy
			mCamVelocity	-=(SwimFriction * mCamVelocity * secDelta * 0.5f);
			mCamVelocity	+=Vector3.Down * GravityForce * (secDelta * 0.5f);
			mCamVelocity	+=Vector3.Up * BouyancyForce * (secDelta * 0.5f);

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

			mCamVelocity	+=moveVec * 0.5f;
			mCamVelocity	-=(FlyFriction * mCamVelocity * secDelta * 0.5f);

			Vector3	pos	=startPos;

			if(bFlyUp)
			{
				mCamVelocity	+=Vector3.Up * FlyUpMoveForce * 0.5f;
			}
			pos	+=mCamVelocity * secDelta;

			mCamVelocity	+=moveVec * 0.5f;
			mCamVelocity	-=(FlyFriction * mCamVelocity * secDelta * 0.5f);

			if(bFlyUp)
			{
				mCamVelocity	+=Vector3.Up * FlyUpMoveForce * 0.5f;
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

			mCamVelocity	+=moveVec * 0.5f;
			mCamVelocity	-=(friction * mCamVelocity * secDelta * 0.5f);

			Vector3	pos	=startPos;

			if(bGravity)
			{
				mCamVelocity	+=Vector3.Down * GravityForce * (secDelta * 0.5f);
			}
			if(bJumped)
			{
				mCamVelocity	+=Vector3.Up * JumpForce * 0.5f;

				pos	+=mCamVelocity * (1f/60f);
			}
			else
			{
				pos	+=mCamVelocity * secDelta;
			}

			mCamVelocity	+=moveVec * 0.5f;
			mCamVelocity	-=(friction * mCamVelocity * secDelta * 0.5f);
			if(bGravity)
			{
				mCamVelocity	+=Vector3.Down * GravityForce * (secDelta * 0.5f);
			}
			if(bJumped)
			{
				mCamVelocity	+=Vector3.Up * JumpForce * 0.5f;
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
