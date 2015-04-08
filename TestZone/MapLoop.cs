using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Diagnostics;
using System.Text;
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
using SharpDX.Direct3D11;

using MatLib	=MaterialLib.MaterialLib;


namespace TestZone
{
	internal class MapLoop
	{
		//data
		Zone		mZone;
		IndoorMesh	mZoneDraw;
		MatLib		mZoneMats;
		string		mGameRootDir;
		StuffKeeper	mSKeeper;

		//list of levels
		List<string>	mLevels		=new List<string>();
		int				mCurLevel	=0;

		//list of available anims
		List<string>	mAnims			=new List<string>();
		int				mCurAnim		=0;
		float			mCurAnimTime	=0f;

		//dyn lights
		DynamicLights	mDynLights;
		List<int>		mActiveLights	=new List<int>();

		Random	mRand	=new Random();

		//shader compile progress indicator
		SharedForms.ThreadedProgress	mSProg;

		//helpers
		TriggerHelper		mTHelper		=new TriggerHelper();
		ParticleHelper		mPHelper		=new ParticleHelper();
		StaticHelper		mSHelper		=new StaticHelper();
		IntermissionHelper	mIMHelper		=new IntermissionHelper();
		ShadowHelper		mShadowHelper	=new ShadowHelper();
		IDKeeper			mKeeper			=new IDKeeper();

		//static stuff
		MatLib											mStaticMats;
		Dictionary<string, IArch>						mStatics		=new Dictionary<string, IArch>();
		Dictionary<ZoneEntity, StaticMesh>				mStaticInsts	=new Dictionary<ZoneEntity, StaticMesh>();
		Dictionary<ZoneEntity, LightHelper>				mSLHelpers		=new Dictionary<ZoneEntity, LightHelper>();
		Dictionary<ZoneEntity, ShadowHelper.Shadower>	mStaticShads	=new Dictionary<ZoneEntity, ShadowHelper.Shadower>();

		//player character stuff
		IArch					mPArch;
		Character				mPChar;
		MatLib					mPMats;
		AnimLib					mPAnims;
		ShadowHelper.Shadower	mPShad;
		Mobile					mPMob, mPCamMob;
		LightHelper				mPLHelper;
		bool					mbFly	=true;

		//physics testing stuffs
		Physics	mPhys		=new Physics();
		Physics	mCamPhys	=new Physics();

		//gpu
		GraphicsDevice	mGD;
		PostProcess		mPost;
		ParticleBoss	mPB;
		MatLib			mPartMats;

		//audio
		Audio	mAudio	=new Audio();

		//2d stuff
		ScreenUI	mSUI;

		//Fonts / UI
		ScreenText		mST;
		MatLib			mFontMats;
		Matrix			mTextProj;
		Mover2			mTextMover	=new Mover2();
		int				mResX, mResY;
		List<string>	mFonts	=new List<string>();

		//constants
		const float	ShadowSlop			=12f;
		const float	PlayerMass			=1f;		//Gronks
		const float	JogMoveForce		=20000f;	//Fig Newtons
		const float	MidAirMoveForce		=1000f;		//Slight wiggle midair
		const float	StumbleForce		=10000f;	//Fig Newtons
		const float	GravityForce		=400f;		//Gravitons
		const float	PlayerInertiaTensor	=80f;		//Wild guess
		const float	GroundFriction		=2f;		//Frictols
		const float	AirFriction			=0.1f;		//Frictols
		const float	JumpForce			=12000f;	//leapometers


		internal MapLoop(GraphicsDevice gd, string gameRootDir)
		{
			mPhys.SetProps(PlayerMass, PlayerInertiaTensor, GroundFriction);
			mCamPhys.SetProps(PlayerMass, PlayerInertiaTensor, GroundFriction);

			mGD				=gd;
			mGameRootDir	=gameRootDir;
			mResX			=gd.RendForm.ClientRectangle.Width;
			mResY			=gd.RendForm.ClientRectangle.Height;

			mSKeeper	=new StuffKeeper();

			mSKeeper.eCompileNeeded	+=OnCompilesNeeded;
			mSKeeper.eCompileDone	+=OnCompileDone;

			mSKeeper.Init(mGD, gameRootDir);

			mZoneMats	=new MatLib(gd, mSKeeper);
			mZone		=new Zone();
			mZoneDraw	=new MeshLib.IndoorMesh(gd, mZoneMats);
			mPartMats	=new MatLib(mGD, mSKeeper);
			mPB			=new ParticleBoss(gd.GD, mPartMats);
			mFontMats	=new MatLib(gd, mSKeeper);

			mFontMats.CreateMaterial("Text");
			mFontMats.SetMaterialEffect("Text", "2D.fx");
			mFontMats.SetMaterialTechnique("Text", "Text");

			mFonts	=mSKeeper.GetFontList();

			mST		=new ScreenText(gd.GD, mFontMats, mFonts[0], 1000);
			mSUI	=new ScreenUI(gd.GD, mFontMats, 100);

			mTextProj	=Matrix.OrthoOffCenterLH(0, mResX, mResY, 0, 0.1f, 5f);

			//grab two UI textures to show how to do gumpery
			List<string>	texs	=mSKeeper.GetTexture2DList();
			List<string>	uiTex	=new List<string>();
			foreach(string tex in texs)
			{
				if(tex.StartsWith("UI"))
				{
					uiTex.Add(tex);
				}
			}

			//draw some ui bits
			if(uiTex.Count > 1)
			{
				mSUI.AddGump(uiTex[0], "TestGump", Vector4.One, Vector2.One * 20f, Vector2.One);
				mSUI.AddGump(uiTex[1], "TestGump2", Vector4.One, Vector2.One * 60f, Vector2.One);
				//mSUI.ModifyGumpScale("TestGump", Vector2.One * 0.25f);
			}

			Vector4	color	=Vector4.UnitY + (Vector4.UnitW * 0.15f);

			//string indicators for various statusy things
			mST.AddString(mFonts[0], "Stuffs", "LevelStatus",
				color, Vector2.UnitX * 20f + Vector2.UnitY * 620f, Vector2.One);
			mST.AddString(mFonts[0], "Stuffs", "PosStatus",
				color, Vector2.UnitX * 20f + Vector2.UnitY * 640f, Vector2.One);
			mST.AddString(mFonts[0], "(G), (H) to clear:  Dynamic Lights: 0", "DynStatus",
				color, Vector2.UnitX * 20f + Vector2.UnitY * 660f, Vector2.One);

			mZoneMats.InitCelShading(1);
			mZoneMats.GenerateCelTexturePreset(gd.GD,
				gd.GD.FeatureLevel == FeatureLevel.Level_9_3, false, 0);
			mZoneMats.SetCelTexture(0);

			mZoneDraw	=new IndoorMesh(gd, mZoneMats);

			mAudio.LoadAllSounds(mGameRootDir + "\\Audio\\SoundFX");

			//set up post processing module
			mPost	=new PostProcess(gd, mZoneMats, "Post.fx");

#if true
			mPost.MakePostTarget(gd, "SceneColor", mResX, mResY, Format.R16G16B16A16_Float);
			mPost.MakePostDepth(gd, "SceneDepth", mResX, mResY,
				(gd.GD.FeatureLevel != FeatureLevel.Level_9_3)?
					Format.D32_Float_S8X24_UInt : Format.D24_UNorm_S8_UInt);
			mPost.MakePostTarget(gd, "SceneDepthMatNorm", mResX, mResY, Format.R16G16B16A16_Float);
			mPost.MakePostTarget(gd, "Bleach", mResX, mResY, Format.R16G16B16A16_Float);
			mPost.MakePostTarget(gd, "Outline", mResX, mResY, Format.R16G16B16A16_Float);
			mPost.MakePostTargetHalfRes(gd, "Bloom1", mResX/2, mResY/2, Format.R16G16B16A16_Float);
			mPost.MakePostTargetHalfRes(gd, "Bloom2", mResX/2, mResY/2, Format.R16G16B16A16_Float);
#elif ThirtyTwo
			mPost.MakePostTarget(gd, "SceneColor", mResX, mResY, Format.R8G8B8A8_UNorm);
			mPost.MakePostDepth(gd, "SceneDepth", mResX, mResY,
				(gd.GD.FeatureLevel != FeatureLevel.Level_9_3)?
					Format.D32_Float_S8X24_UInt : Format.D24_UNorm_S8_UInt);
			mPost.MakePostTarget(gd, "SceneDepthMatNorm", mResX, mResY, Format.R16G16B16A16_Float);
			mPost.MakePostTarget(gd, "Bleach", mResX, mResY, Format.R8G8B8A8_UNorm);
			mPost.MakePostTarget(gd, "Outline", mResX, mResY, Format.R8G8B8A8_UNorm);
			mPost.MakePostTarget(gd, "Bloom1", mResX/2, mResY/2, Format.R8G8B8A8_UNorm);
			mPost.MakePostTarget(gd, "Bloom2", mResX/2, mResY/2, Format.R8G8B8A8_UNorm);
#else
			mPost.MakePostTarget(gd, "SceneColor", mResX, mResY, Format.B5G5R5A1_UNorm);
			mPost.MakePostDepth(gd, "SceneDepth", mResX, mResY,
				(gd.GD.FeatureLevel != FeatureLevel.Level_9_3)?
					Format.D32_Float_S8X24_UInt : Format.D24_UNorm_S8_UInt);
			mPost.MakePostTarget(gd, "SceneDepthMatNorm", mResX, mResY, Format.R16G16B16A16_Float);
			mPost.MakePostTarget(gd, "Bleach", mResX, mResY, Format.B5G5R5A1_UNorm);
			mPost.MakePostTarget(gd, "Outline", mResX, mResY, Format.B5G5R5A1_UNorm);
			mPost.MakePostTarget(gd, "Bloom1", mResX/2, mResY/2, Format.B5G5R5A1_UNorm);
			mPost.MakePostTarget(gd, "Bloom2", mResX/2, mResY/2, Format.B5G5R5A1_UNorm);
#endif

			if(gd.GD.FeatureLevel != FeatureLevel.Level_9_3)
			{
				mDynLights	=new DynamicLights(mGD, mZoneMats, "BSP.fx");
			}

			//see if any static stuff
			if(Directory.Exists(mGameRootDir + "/Statics"))
			{
				DirectoryInfo	di	=new DirectoryInfo(mGameRootDir + "/Statics");

				FileInfo[]	fi	=di.GetFiles("*.MatLib", SearchOption.TopDirectoryOnly);

				if(fi.Length > 0)
				{
					mStaticMats	=new MatLib(gd, mSKeeper);
					mStaticMats.ReadFromFile(fi[0].DirectoryName + "\\" + fi[0].Name);

					mStaticMats.InitCelShading(1);
					mStaticMats.GenerateCelTexturePreset(gd.GD,
						(gd.GD.FeatureLevel == FeatureLevel.Level_9_3),
						true, 0);
					mStaticMats.SetCelTexture(0);
				}
				mStatics	=Mesh.LoadAllStaticMeshes(mGameRootDir + "\\Statics", gd.GD);
			}

			//load character stuff if any around
			if(Directory.Exists(mGameRootDir + "/Characters"))
			{
				DirectoryInfo	di	=new DirectoryInfo(mGameRootDir + "/Characters");

				FileInfo[]	fi	=di.GetFiles("*.AnimLib", SearchOption.TopDirectoryOnly);
				if(fi.Length > 0)
				{
					mPAnims	=new AnimLib();
					mPAnims.ReadFromFile(fi[0].DirectoryName + "\\" + fi[0].Name);

					List<Anim>	anims	=mPAnims.GetAnims();
					foreach(Anim a in anims)
					{
						mAnims.Add(a.Name);
					}
				}

				fi	=di.GetFiles("*.MatLib", SearchOption.TopDirectoryOnly);
				if(fi.Length > 0)
				{
					mPMats	=new MatLib(mGD, mSKeeper);
					mPMats.ReadFromFile(fi[0].DirectoryName + "\\" + fi[0].Name);
					mPMats.InitCelShading(1);
					mPMats.GenerateCelTexturePreset(gd.GD,
						gd.GD.FeatureLevel == FeatureLevel.Level_9_3, false, 0);
					mPMats.SetCelTexture(0);
				}

				fi	=di.GetFiles("*.Character", SearchOption.TopDirectoryOnly);
				if(fi.Length > 0)
				{
					mPArch	=new CharacterArch();
					mPArch.ReadFromFile(fi[0].DirectoryName + "\\" + fi[0].Name, mGD.GD, false);
				}

				fi	=di.GetFiles("*.CharacterInstance", SearchOption.TopDirectoryOnly);
				if(fi.Length > 0)
				{
					mPChar	=new Character(mPArch, mPAnims);
					mPChar.ReadFromFile(fi[0].DirectoryName + "\\" + fi[0].Name);
					mPChar.SetMatLib(mPMats);

					mPShad	=new ShadowHelper.Shadower();

					mPShad.mChar	=mPChar;
					mPShad.mContext	=this;
				}
			}

			mPMob		=new Mobile(mPChar, 16f, 50f, 45f, true, mTHelper);
			mPCamMob	=new Mobile(mPChar, 16f, 50f, 45f, true, mTHelper);
			mPLHelper	=new LightHelper();

			mKeeper.AddLib(mZoneMats);

			if(mStaticMats != null)
			{
				mKeeper.AddLib(mStaticMats);
			}

			if(mPMats != null)
			{
				mKeeper.AddLib(mPMats);
			}

			//example material groups
			//these treat all materials in the group
			//as a single material for the purposes
			//of drawing cartoony outlines around them
			List<string>	skinMats	=new List<string>();

			skinMats.Add("Face");
			skinMats.Add("Skin");
			skinMats.Add("EyeWhite");
			skinMats.Add("EyeLiner");
			skinMats.Add("LeftIris");
			skinMats.Add("LeftPupil");
			skinMats.Add("RightIris");
			skinMats.Add("RightPupil");
//			skinMats.Add("Nails");
			mKeeper.AddMaterialGroup("SkinGroup", skinMats);

			mSHelper.ePickUp	+=OnPickUp;

			if(Directory.Exists(mGameRootDir + "/Levels"))
			{
				DirectoryInfo	di	=new DirectoryInfo(mGameRootDir + "/Levels");

				FileInfo[]	fi	=di.GetFiles("*.Zone", SearchOption.TopDirectoryOnly);
				foreach(FileInfo f in fi)
				{
					mLevels.Add(f.Name.Substring(0, f.Name.Length - 5));
				}
			}

			//if debugger lands here, levels are sort of needed
			//otherwise there's not much point for this test prog
			ChangeLevel(mLevels[mCurLevel]);
			mST.ModifyStringText(mFonts[0], "(L) CurLevel: " + mLevels[mCurLevel], "LevelStatus");
		}


		//if running on a fixed timestep, this might be called
		//more often with a smaller delta time than RenderUpdate()
		internal void Update(float secDelta, List<Input.InputAction> actions, PlayerSteering ps)
		{
			//Thread.Sleep(30);

			float	msDelta	=secDelta * 1000f;

			mZone.UpdateModels((int)msDelta);

			float	yawAmount	=0f;
			float	pitchAmount	=0f;

			if(!mPMob.IsOnGround() && !mPMob.IsBadFooting())
			{
				//gravity
				mPhys.ApplyForce(Vector3.Down * GravityForce);
				mPhys.SetFriction(AirFriction);
			}
			else
			{
				//friction
				mPhys.SetFriction(GroundFriction);
			}

			if(!mPCamMob.IsOnGround() && !mPCamMob.IsBadFooting())
			{
				//gravity
				if(!mbFly)
				{
					mCamPhys.ApplyForce(Vector3.Down * GravityForce);
				}
				mCamPhys.SetFriction(AirFriction);
			}
			else
			{
				if(!mbFly)
				{
					mCamPhys.SetFriction(GroundFriction);
				}
			}

			foreach(Input.InputAction act in actions)
			{
				if(act.mAction.Equals(Program.MyActions.Jump))
				{
					if(mPMob.IsOnGround())
					{
						mPhys.ApplyForce(Vector3.Up * JumpForce);
						mPhys.SetFriction(AirFriction);
					}
					if(mPCamMob.IsOnGround() && !mbFly)
					{
						mCamPhys.ApplyForce(Vector3.Up * JumpForce);
						mPhys.SetFriction(AirFriction);
					}
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
				else if(act.mAction.Equals(Program.MyActions.Turn))
				{
					yawAmount	=act.mMultiplier;
				}
				else if(act.mAction.Equals(Program.MyActions.Pitch))
				{
					pitchAmount	=act.mMultiplier;
				}
				else if(act.mAction.Equals(Program.MyActions.AccelTest))
				{
					mPhys.ApplyForce(Vector3.UnitX * act.mMultiplier * JogMoveForce);
				}
				else if(act.mAction.Equals(Program.MyActions.AccelTest2))
				{
					mPhys.ApplyForce(-Vector3.UnitX * act.mMultiplier * JogMoveForce);
				}
			}
			mPhys.Update(secDelta);

			UpdateDynamicLights((int)msDelta, actions);

			Vector3	startPos	=mPCamMob.GetGroundPos();
			Vector3	moveVec		=ps.Update(startPos, mGD.GCam.Forward, mGD.GCam.Left, mGD.GCam.Up, actions);

			if(mPCamMob.IsOnGround() || mbFly)
			{
				moveVec	*=JogMoveForce;
			}
			else if(mPCamMob.IsBadFooting())
			{
				moveVec	*=StumbleForce;
			}
			else
			{
				moveVec	*=MidAirMoveForce;
			}

			//update phys position in case of model push
			mCamPhys.SetPosition(startPos);
			mCamPhys.ApplyForce(moveVec);

			mCamPhys.Update(secDelta);

			Vector3	camPos	=Vector3.Zero;
			Vector3	endPos	=mCamPhys.GetPosition();

			mPCamMob.Move(endPos, (int)msDelta, false, mbFly, true, true, out endPos, out camPos);

			mCamPhys.SetPosition(endPos);

			if(mPCamMob.IsOnGround() && !mbFly)
			{
				mCamPhys.ClearVerticalMomentum();
			}

			mGD.GCam.Update(camPos, ps.Pitch, ps.Yaw, ps.Roll);

			if(mPChar != null)
			{
				Vector3	ppos;
				mPMob.Move(mPhys.GetPosition(), (int)msDelta, false, false, true, true, out ppos, out camPos);

				mPhys.SetPosition(ppos);

				if(mPMob.IsOnGround())
				{
					mPhys.ClearVerticalMomentum();
				}

				Matrix	pmat	=Matrix.Translation(ppos);
				Matrix	pOrient	=Matrix.RotationQuaternion(mPhys.GetOrient());

				mPChar.SetTransform(pOrient * pmat);

				float	totTime	=mPAnims.GetAnimTime(mAnims[mCurAnim]);
				float	strTime	=mPAnims.GetAnimStartTime(mAnims[mCurAnim]);
				float	endTime	=totTime + strTime;

				mCurAnimTime	+=secDelta;
				if(mCurAnimTime > endTime)
				{
					mCurAnimTime	=strTime + (mCurAnimTime - endTime);
				}

				mPChar.Animate(mAnims[mCurAnim], mCurAnimTime);

				mPLHelper.Update((int)msDelta, ppos + Vector3.Up * 32f, mDynLights);
			}

			mPB.Update(mGD.DC, msDelta);

			mSHelper.HitCheck(mPMob, mGD.GCam.Position);

			mAudio.Update(mGD.GCam);

			mST.ModifyStringText(mFonts[0], "ModelOn: " + mPCamMob.GetModelOn() + " : "
				+ (int)mGD.GCam.Position.X + ", "
				+ (int)mGD.GCam.Position.Y + ", "
				+ (int)mGD.GCam.Position.Z + " (F)lyMode: " + mbFly
				+ " X: " + yawAmount + " Y: " + pitchAmount
				+ (mPCamMob.IsBadFooting()? " BadFooting!" : ""), "PosStatus");

			mST.Update(mGD.DC);
			mSUI.Update(mGD.DC);
		}


		//called once before render with accumulated delta
		//do all once per render style updates in here
		internal void RenderUpdate(float msDelta)
		{
			mZoneDraw.Update(msDelta);

			mZoneMats.UpdateWVP(Matrix.Identity, mGD.GCam.View, mGD.GCam.Projection, mGD.GCam.Position);

			if(mStaticMats !=null)
			{
				mStaticMats.UpdateWVP(Matrix.Identity, mGD.GCam.View, mGD.GCam.Projection, mGD.GCam.Position);
			}
			if(mPMats != null)
			{
				mPMats.UpdateWVP(Matrix.Identity, mGD.GCam.View, mGD.GCam.Projection, mGD.GCam.Position);
			}

			mSHelper.Update((int)msDelta);

			foreach(KeyValuePair<ZoneEntity, LightHelper> shelp in mSLHelpers)
			{
				Vector3	pos;

				shelp.Key.GetOrigin(out pos);

				shelp.Value.Update((int)msDelta, pos, mDynLights);
			}
		}


		internal void Render()
		{
			mPost.SetTargets(mGD, "SceneDepthMatNorm", "SceneDepth");

			mPost.ClearTarget(mGD, "SceneDepthMatNorm", Color.White);
			mPost.ClearDepth(mGD, "SceneDepth");

			mZoneDraw.DrawDMN(mGD, mZone.IsMaterialVisibleFromPos,
				mZone.GetModelTransform, RenderExternalDMN);

			mPost.SetTargets(mGD, "SceneColor", "SceneDepth");

			mPost.ClearTarget(mGD, "SceneColor", Color.CornflowerBlue);
			mPost.ClearDepth(mGD, "SceneDepth");

			if(mDynLights != null)
			{
				mDynLights.SetParameter();
			}

			mZoneDraw.Draw(mGD, mShadowHelper.GetShadowCount(),
				mZone.IsMaterialVisibleFromPos,
				mZone.GetModelTransform,
				RenderExternal,
				mShadowHelper.DrawShadows);

			mPost.SetTargets(mGD, "Outline", "null");
			mPost.SetParameter("mNormalTex", "SceneDepthMatNorm");
			mPost.DrawStage(mGD, "Outline");

				mPost.SetTargets(mGD, "Bleach", "null");
				mPost.SetParameter("mColorTex", "SceneColor");
				mPost.DrawStage(mGD, "BleachBypass");

				mPost.SetTargets(mGD, "Bloom1", "null");
				mPost.SetParameter("mBlurTargetTex", "Bleach");
				mPost.DrawStage(mGD, "BloomExtract");

				mPost.SetTargets(mGD, "Bloom2", "null");
				mPost.SetParameter("mBlurTargetTex", "Bloom1");
				mPost.DrawStage(mGD, "GaussianBlurX");

				mPost.SetTargets(mGD, "Bloom1", "null");
				mPost.SetParameter("mBlurTargetTex", "Bloom2");
				mPost.DrawStage(mGD, "GaussianBlurY");

				mPost.SetTargets(mGD, "SceneColor", "null");
				mPost.SetParameter("mBlurTargetTex", "Bloom1");
				mPost.SetParameter("mColorTex", "Bleach");
				mPost.DrawStage(mGD, "BloomCombine");

			mPost.SetTargets(mGD, "BackColor", "BackDepth");
			mPost.SetParameter("mBlurTargetTex", "Outline");
			mPost.SetParameter("mColorTex", "SceneColor");
			mPost.DrawStage(mGD, "Modulate");

			mSUI.Draw(mGD.DC, Matrix.Identity, mTextProj);
			mST.Draw(mGD.DC, Matrix.Identity, mTextProj);
		}


		internal void FreeAll()
		{
			mShadowHelper.FreeAll();
			mPost.FreeAll();
			mZoneMats.FreeAll();
			mZoneDraw.FreeAll();
			mPB.FreeAll();
			mKeeper.Clear();
			if(mStaticMats != null)
			{
				mStaticMats.FreeAll();
			}
			if(mPMats != null)
			{
				mPMats.FreeAll();
			}
			if(mPChar != null)
			{
				mPChar.FreeAll();
			}
			mPartMats.FreeAll();

			if(mPAnims != null)
			{
				mPArch.FreeAll();
			}

			if(mDynLights != null)
			{
				mDynLights.FreeAll();
			}

			foreach(KeyValuePair<ZoneEntity, StaticMesh> stat in mStaticInsts)
			{
				stat.Value.FreeAll();
			}

			foreach(KeyValuePair<string, IArch> stat in mStatics)
			{
				stat.Value.FreeAll();
			}
			mStatics.Clear();

			mAudio.FreeAll();

			mSKeeper.FreeAll();
		}


		void RenderExternalDMN(GameCamera gcam)
		{
			mSHelper.Draw(DrawStaticDMN);

			if(mPChar != null)
			{
				mPChar.DrawDMN(mGD.DC, mPMats);
			}

			mPB.DrawDMN(mGD.DC, gcam.View, gcam.Projection, gcam.Position);
		}


		void RenderExternal(AlphaPool ap, GameCamera gcam)
		{
			mSHelper.Draw(DrawStatic);

			Vector4	lightCol0, lightCol1, lightCol2;
			Vector3	lightPos, lightDir;
			bool	bDir;
			float	intensity;
			mPLHelper.GetCurrentValues(
				out lightCol0, out lightCol1, out lightCol2,
				out intensity, out lightPos, out lightDir, out bDir);

			if(mPChar != null)
			{
				mPMats.SetTriLightValues(lightCol0, lightCol1, lightCol2, lightDir);
				mPChar.Draw(mGD.DC, mPMats);
			}

			mPB.Draw(ap, gcam.View, gcam.Projection);
		}


		void DrawStatic(Matrix local, ZoneEntity ze, Vector3 pos)
		{
			if(!mStaticInsts.ContainsKey(ze))
			{
				return;
			}

			Vector4	lightCol0, lightCol1, lightCol2;
			Vector3	lightPos, lightDir;
			bool	bDir;
			float	intensity;

			mSLHelpers[ze].GetCurrentValues(
				out lightCol0, out lightCol1, out lightCol2,
				out intensity, out lightPos, out lightDir, out bDir);

			StaticMesh	sm	=mStaticInsts[ze];

			sm.SetTriLightValues(lightCol0, lightCol1, lightCol2, lightDir);

			sm.SetTransform(local);
			sm.Draw(mGD.DC, mStaticMats);
		}


		void DrawStaticDMN(Matrix local, ZoneEntity ze, Vector3 pos)
		{
			if(!mStaticInsts.ContainsKey(ze))
			{
				return;
			}
			StaticMesh	sm	=mStaticInsts[ze];

			sm.SetTransform(local);
			sm.DrawDMN(mGD.DC, mStaticMats);
		}


		void ChangeLevel(string level)
		{
			string	lev	=mGameRootDir + "/Levels/" + level;

			mZone	=new Zone();

			mZoneMats.ReadFromFile(lev + ".MatLib");
			mZone.Read(lev + ".Zone", false);
			mZoneDraw.Read(mGD, mSKeeper, lev + ".ZoneDraw", false);

			//set new material's cel lookup
			mZoneMats.SetCelTexture(0);
			mZoneMats.SetLightMapsToAtlas();

			mTHelper.Initialize(mZone, mAudio, mZoneDraw.SwitchLight, OkToFire);
			mPHelper.Initialize(mZone, mTHelper, mPB);
			mSHelper.Initialize(mZone);
			mIMHelper.Initialize(mZone);

			List<ZoneEntity>	wEnt	=mZone.GetEntities("worldspawn");
			Debug.Assert(wEnt.Count == 1);

			float	mDirShadowAtten;
			string	ssa	=wEnt[0].GetValue("SunShadowAtten");
			if(!Single.TryParse(ssa, out mDirShadowAtten))
			{
				mDirShadowAtten	=200f;	//default
			}

			mShadowHelper.Initialize(mGD, 512, mDirShadowAtten,
				mZoneMats, mPost, GetCurShadowLightInfo, GetTransdBounds);

			//make lighthelpers for statics
			mSLHelpers	=mSHelper.MakeLightHelpers(mZone, mZoneDraw.GetStyleStrength);

			//make static instances
			List<ZoneEntity>	statEnts	=mSHelper.GetStaticEntities();
			foreach(ZoneEntity ze in statEnts)
			{
				string	meshName	=ze.GetValue("meshname");
				if(meshName == null || meshName == "")
				{
					continue;
				}

				if(!mStatics.ContainsKey(meshName))
				{
					continue;
				}

				StaticMesh	sm	=new StaticMesh(mStatics[meshName]);

				sm.ReadFromFile(mGameRootDir + "\\Statics\\" + meshName + "Instance");

				mStaticInsts.Add(ze, sm);

				sm.SetMatLib(mStaticMats);
			}

			mPLHelper.Initialize(mZone, mZoneDraw.GetStyleStrength);

//			mGraph.Load(lev + ".Pathing");
//			mGraph.GenerateGraph(mZone.GetWalkableFaces, 32, 18f, CanPathReach);
//			mGraph.Save(mLevels[index] + ".Pathing");
//			mGraph.BuildDrawInfo(gd);

//			mPathMobile.SetZone(mZone);

			mPMob.SetZone(mZone);
			mPCamMob.SetZone(mZone);

			MakeStaticShadowers();

			float	ang;
			Vector3	gpos	=mZone.GetPlayerStartPos(out ang);
			mPMob.SetGroundPos(gpos);
			mPCamMob.SetGroundPos(gpos);
			mPhys.SetPosition(gpos);
			mCamPhys.SetPosition(gpos);

			mKeeper.Scan();
			mKeeper.AssignIDsToEffectMaterials("BSP.fx");

			if(mPChar != null)
			{
				mShadowHelper.RegisterShadower(mPShad, mPMats);
				mPChar.AssignMaterialIDs(mKeeper);
			}

			foreach(KeyValuePair<ZoneEntity, StaticMesh> instances in mStaticInsts)
			{
				instances.Value.AssignMaterialIDs(mKeeper);
			}
		}


		void UpdateDynamicLights(int msDelta, List<Input.InputAction> actions)
		{
			if(mDynLights == null)
			{
				return;
			}
			foreach(Input.InputAction act in actions)
			{
				if(act.mAction.Equals(Program.MyActions.PlaceDynamicLight))
				{
					int	id;
					mDynLights.CreateDynamicLight(mGD.GCam.Position,
						Mathery.RandomColorVector(mRand),
						300, out id);
					mActiveLights.Add(id);
					mST.ModifyStringText(mFonts[0], "(G), (H) to clear:  Dynamic Lights: "
						+ mActiveLights.Count, "DynStatus");
				}
				else if(act.mAction.Equals(Program.MyActions.ClearDynamicLights))
				{
					foreach(int id in mActiveLights)
					{
						mDynLights.Destroy(id);
					}
					mActiveLights.Clear();
					mST.ModifyStringText(mFonts[0], "(G), (H) to clear:  Dynamic Lights: 0", "DynStatus");
				}
			}

			mDynLights.Update((int)msDelta, mGD);
		}


		BoundingBox GetTransdBounds(ShadowHelper.Shadower shadower)
		{
			if(shadower.mChar != mPChar)
			{
				return	new BoundingBox();
			}

			BoundingBox	ret	=mPMob.GetTransformedBound();

			//add a little bit to account for a bit of sloppiness
			ret.Maximum	+=Vector3.One * ShadowSlop;
			ret.Minimum	-=Vector3.One * ShadowSlop;

			return	ret;
		}


		bool GetCurShadowLightInfo(ShadowHelper.Shadower shadower,
			out Matrix shadowerTransform,
			out float intensity, out Vector3 lightPos,
			out Vector3 lightDir, out bool bDirectional)
		{
			Vector4	col0, col1, col2;

			if(shadower.mContext is ZoneEntity)
			{
				ZoneEntity	ze=shadower.mContext as ZoneEntity;

				shadowerTransform	=mSHelper.GetTransform(ze);

				return	mSLHelpers[ze].GetCurrentValues(out col0, out col1, out col2,
					out intensity, out lightPos, out lightDir, out bDirectional);
			}

			if(shadower.mChar == mPChar)
			{
				shadowerTransform	=mPChar.GetTransform();
			}
			else
			{
				intensity			=0f;
				lightPos			=Vector3.Zero;
				lightDir			=Vector3.Zero;
				bDirectional		=false;
				shadowerTransform	=Matrix.Identity;

				return	false;
			}

			return	mPLHelper.GetCurrentValues(out col0, out col1, out col2,
				out intensity, out lightPos, out lightDir, out bDirectional);
		}


		void MakeStaticShadowers()
		{
			List<ZoneEntity>	statics	=mSHelper.GetStaticEntities();

			foreach(ZoneEntity ze in statics)
			{
				if(!mStaticInsts.ContainsKey(ze))
				{
					continue;
				}

				ShadowHelper.Shadower	shad	=new ShadowHelper.Shadower();

				shad.mChar		=null;
				shad.mStatic	=mStaticInsts[ze];
				shad.mContext	=ze;

				mStaticShads.Add(ze, shad);

				mShadowHelper.RegisterShadower(shad, mStaticMats);
			}
		}


		void OnPickUp(object sender, StaticHelper.PickUpEventArgs pea)
		{
			if(mStaticShads.ContainsKey(pea.mEntity))
			{
				ShadowHelper.Shadower	shad	=mStaticShads[pea.mEntity];

				mShadowHelper.UnRegisterShadower(shad);

				mStaticShads.Remove(pea.mEntity);
			}
		}


		bool OkToFire(TriggerHelper.FuncEventArgs fea)
		{
			string	funcClass	=fea.mFuncEnt.GetValue("classname");

			if(funcClass == "func_door")
			{
			}
			return	true;
		}


		void OnCompilesNeeded(object sender, EventArgs ea)
		{
			Thread	uiThread	=new Thread(() =>
				{
					mSProg	=new SharedForms.ThreadedProgress("Compiling Shaders...");
					System.Windows.Forms.Application.Run(mSProg);
				});

			uiThread.SetApartmentState(ApartmentState.STA);
			uiThread.Start();

			while(mSProg == null)
			{
				Thread.Sleep(0);
			}

			mSProg.SetSizeInfo(0, (int)sender);
		}


		void OnCompileDone(object sender, EventArgs ea)
		{
			mSProg.SetCurrent((int)sender);

			if((int)sender == mSProg.GetMax())
			{
				mSProg.Nuke();
			}
		}
	}
}
