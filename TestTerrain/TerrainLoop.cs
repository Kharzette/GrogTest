using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Diagnostics;
using System.Text;
using System.IO;
using MeshLib;
using UtilityLib;
using MaterialLib;
using TerrainLib;
using AudioLib;
using InputLib;

using SharpDX;
using SharpDX.DXGI;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;

using MatLib	=MaterialLib.MaterialLib;


namespace TestTerrain
{
	internal class TerrainLoop
	{
		//gpu
		GraphicsDevice	mGD;
		StuffKeeper		mSK;

		//terrain stuff
		FractalFactory	mFracFact;
		TerrainModel	mTModel;
		Terrain			mTerrain;
		PrimObject		mSkyCube;
		MatLib			mTerMats;
		int				mChunkRange, mNumStreamThreads;
		Point			mGridCoordinate;
		int				mCellGridMax, mBoundary;
		BoundingFrustum	mFrust	=new BoundingFrustum(Matrix.Identity);

		//audio
		Audio	mAudio	=new Audio();

		//Fonts / UI
		ScreenText		mST;
		MatLib			mFontMats;
		Matrix			mTextProj;
		Mover2			mTextMover	=new Mover2();
		int				mResX, mResY;
		List<string>	mFonts	=new List<string>();

		//player stuff
		//will eventually have a terrain specific
		//mobile like the one in bspzone?
		//or split mobile out into something shared
		bool	mbOnGround, mbFly, mbBadFooting;
		Vector3	mGroundPos, mVelocity;
		Random	mRand	=new Random();

		//debug draw
		MatLib		mDebugMats;

		//constants
		const float	ShadowSlop			=12f;
		const float	JogMoveForce		=2000f;	//Fig Newtons
		const float	MidAirMoveForce		=100f;	//Slight wiggle midair
		const float	StumbleForce		=700f;	//Fig Newtons
		const float	GravityForce		=980f;	//Gravitons
		const float	GroundFriction		=10f;	//Frictols
		const float	StumbleFriction		=6f;	//Frictols
		const float	AirFriction			=0.1f;	//Frictols
		const float	JumpForce			=390;	//leapometers
		const float	FogStart			=8000f;
		const float	FogEnd				=12000f;


		internal TerrainLoop(GraphicsDevice gd, StuffKeeper sk, string gameRootDir)
		{
			mGD		=gd;
			mSK		=sk;
			mResX	=gd.RendForm.ClientRectangle.Width;
			mResY	=gd.RendForm.ClientRectangle.Height;

			mFontMats	=new MatLib(gd, sk);

			mFontMats.CreateMaterial("Text");
			mFontMats.SetMaterialEffect("Text", "2D.fx");
			mFontMats.SetMaterialTechnique("Text", "Text");

			mFonts	=sk.GetFontList();

			mST	=new ScreenText(gd.GD, mFontMats, mFonts[0], 1000);

			mTextProj	=Matrix.OrthoOffCenterLH(0, mResX, mResY, 0, 0.1f, 5f);

			string	path	=gameRootDir + "\\Levels\\Test.Terrain";

			mTerrain	=new Terrain(path);

			//build model for raycasts/collisions
			mTModel	=mTerrain.ConstructModel();
			mTModel.FixBoxHeights();

			Vector4	color	=Vector4.UnitY + (Vector4.UnitW * 0.15f);

			//string indicators for various statusy things
			mST.AddString(mFonts[0], "Stuffs", "PosStatus",
				color, Vector2.UnitX * 20f + Vector2.UnitY * 580f, Vector2.One);
			mST.AddString(mFonts[0], "Thread Status...", "ThreadStatus",
				color, Vector2.UnitX * 20f + Vector2.UnitY * 560f, Vector2.One);

			mTerMats	=new MatLib(mGD, sk);

			Vector3	lightDir	=Mathery.RandomDirection(mRand);

			Vector4	lightColor2	=Vector4.One * 0.4f;
			Vector4	lightColor3	=Vector4.One * 0.1f;

			lightColor2.W	=lightColor3.W	=1f;

			mTerMats.CreateMaterial("Terrain");
			mTerMats.SetMaterialEffect("Terrain", "Terrain.fx");
			mTerMats.SetMaterialTechnique("Terrain", "TriTerrain");
			mTerMats.SetMaterialParameter("Terrain", "mLightColor0", Vector4.One);
			mTerMats.SetMaterialParameter("Terrain", "mLightColor1", lightColor2);
			mTerMats.SetMaterialParameter("Terrain", "mLightColor2", lightColor3);
			mTerMats.SetMaterialParameter("Terrain", "mLightDirection", lightDir);
			mTerMats.SetMaterialParameter("Terrain", "mSolidColour", Vector4.One);
			mTerMats.SetMaterialParameter("Terrain", "mSpecPower", 1);
			mTerMats.SetMaterialParameter("Terrain", "mSpecColor", Vector4.One);
			mTerMats.SetMaterialParameter("Terrain", "mWorld", Matrix.Identity);

			mTerMats.CreateMaterial("Sky");
			mTerMats.SetMaterialEffect("Sky", "Terrain.fx");
			mTerMats.SetMaterialTechnique("Sky", "SkyGradient");

			LoadShading(path);

			mTerMats.InitCelShading(1);
			mTerMats.GenerateCelTexturePreset(mGD.GD, mGD.GD.FeatureLevel == FeatureLevel.Level_9_3, false, 0);
			mTerMats.SetCelTexture(0);

			mSkyCube	=PrimFactory.CreateCube(gd.GD, -5f);

			LoadTerrainTextureStuff(path);

			//debug draw
			mDebugMats	=new MatLib(gd, sk);

			Vector4	redColor	=Vector4.One;
			Vector4	greenColor	=Vector4.One;
			Vector4	blueColor	=Vector4.One;

			redColor.Y	=redColor.Z	=greenColor.X	=greenColor.Z	=blueColor.X	=blueColor.Y	=0f;

			mDebugMats.CreateMaterial("DebugBoxes");
			mDebugMats.SetMaterialEffect("DebugBoxes", "Static.fx");
			mDebugMats.SetMaterialTechnique("DebugBoxes", "TriSolidSpec");
			mDebugMats.SetMaterialParameter("DebugBoxes", "mLightColor0", Vector4.One);
			mDebugMats.SetMaterialParameter("DebugBoxes", "mLightColor1", lightColor2);
			mDebugMats.SetMaterialParameter("DebugBoxes", "mLightColor2", lightColor3);
			mDebugMats.SetMaterialParameter("DebugBoxes", "mSolidColour", blueColor);
			mDebugMats.SetMaterialParameter("DebugBoxes", "mSpecPower", 1);
			mDebugMats.SetMaterialParameter("DebugBoxes", "mSpecColor", Vector4.One);

			mNumStreamThreads	=2;
			mGroundPos.Y		=3000f;	//start above
			mCellGridMax		=mTerrain.GetCellGridMax();
			mBoundary			=mTerrain.GetBoundary();

			Viewport	vp	=mGD.GetScreenViewPort();

			mGD.GCam.Projection	=Matrix.PerspectiveFovLH(
				MathUtil.DegreesToRadians(45f),
				vp.Width / (float)vp.Height, 0.1f, FogEnd);

			mGD.SetClip(0.1f, FogEnd);
		}


		//if running on a fixed timestep, this might be called
		//more often with a smaller delta time than RenderUpdate()
		internal void Update(UpdateTimer time, List<Input.InputAction> actions, PlayerSteering ps)
		{
			//Thread.Sleep(30);

			float	secDelta	=time.GetUpdateDeltaSeconds();

//			mZone.UpdateModels(secDelta);

			float	yawAmount	=0f;
			float	pitchAmount	=0f;
			bool	bGravity	=false;
			float	friction	=GroundFriction;
			if(!mbOnGround)
			{
				//gravity
				if(!mbFly)
				{
					bGravity	=true;
				}

				if(mbBadFooting)
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
				if(!mbFly)
				{
					friction	=GroundFriction;
				}
				else
				{
					friction	=AirFriction;
				}
			}

			bool	bCamJumped	=false;

			foreach(Input.InputAction act in actions)
			{
				if(act.mAction.Equals(Program.MyActions.Jump))
				{
					if(mbOnGround && !mbFly)
					{
						friction		=AirFriction;
						bCamJumped		=true;
					}
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
			}

//			UpdateDynamicLights(actions);
			Vector3 hit;
			bool	bHit	=mTModel.Trace(Vector3.One * 55f + Vector3.UnitY * 2000,
				Vector3.One * 233f - Vector3.UnitY * 800f, out hit);

			Vector3	startPos	=mGroundPos;
			Vector3	moveVec		=ps.Update(startPos, mGD.GCam.Forward, mGD.GCam.Left, mGD.GCam.Up, actions);

			if(mbOnGround || mbFly)
			{
				moveVec	*=JogMoveForce;
			}
			else if(mbBadFooting)
			{
				moveVec	*=StumbleForce;
			}
			else
			{
				moveVec	*=MidAirMoveForce;
			}

			mVelocity	+=moveVec * 0.5f;
			mVelocity	-=(friction * mVelocity * secDelta * 0.5f);

			Vector3	pos	=startPos;

			if(bGravity)
			{
				mVelocity	+=Vector3.Down * GravityForce * (secDelta * 0.5f);
			}
			if(bCamJumped)
			{
				mVelocity	+=Vector3.Up * JumpForce * 0.5f;

				pos	+=mVelocity * (1f/60f);
			}
			else
			{
				pos	+=mVelocity * secDelta;
			}

			mVelocity	+=moveVec * 0.5f;
			mVelocity	-=(friction * mVelocity * secDelta * 0.5f);
			if(bGravity)
			{
				mVelocity	+=Vector3.Down * GravityForce * (secDelta * 0.5f);
			}
			if(bCamJumped)
			{
				mVelocity	+=Vector3.Up * JumpForce * 0.5f;
			}


			Vector3	camPos	=Vector3.Zero;
			Vector3	endPos	=pos;

//			Move(endPos, time.GetUpdateDeltaMilliSeconds(), false,
//				mbFly, !bCamJumped, true, true, out endPos, out camPos);

			mGroundPos	+=moveVec;

			bool	bWrapped	=WrapPosition(ref mGroundPos);

			WrapGridCoordinates();

			if(bWrapped && mTerrain != null)
			{
				mTerrain.BuildGrid(mGD, mChunkRange, mNumStreamThreads);
			}

			if(mTerrain != null)
			{
				mTerrain.SetCellCoord(mGridCoordinate);
				mTerrain.UpdatePosition(mGroundPos, mTerMats);
			}

			mGD.GCam.Update(-mGroundPos, ps.Pitch, ps.Yaw, ps.Roll);

			if(!mbFly)
			{
				if(mbOnGround)
				{
					//kill downward velocity so previous
					//falling momentum doesn't contribute to
					//a new jump
					if(mVelocity.Y < 0f)
					{
						mVelocity.Y	=0f;
					}
				}
				if(mbBadFooting)
				{
					//reduce downward velocity to avoid
					//getting stuck in V shaped floors
					if(mVelocity.Y < 0f)
					{
						mVelocity.Y	-=(StumbleFriction * mVelocity.Y * secDelta);
					}
				}
			}

			mAudio.Update(mGD.GCam);

			mST.ModifyStringText(mFonts[0], "Grid: " + mGridCoordinate.ToString() +
				", Position: " + " : "
				+ mGD.GCam.Position.IntStr(), "PosStatus");

			if(mTerrain != null)
			{
				mST.ModifyStringText(mFonts[0], "Threads Active: " + mTerrain.GetThreadsActive()
					+ ", Thread Counter: " + mTerrain.GetThreadCounter(), "ThreadStatus");
			}

			mST.Update(mGD.DC);
		}


		//called once before render with accumulated delta
		//do all once per render style updates in here
		internal void RenderUpdate(float msDelta)
		{
			if(msDelta <= 0f)
			{
				return;	//can happen if fixed time and no remainder
			}

			mTerMats.UpdateWVP(Matrix.Identity, mGD.GCam.View, mGD.GCam.Projection, mGD.GCam.Position);

			mFrust.Matrix	=mGD.GCam.View * mGD.GCam.Projection;

			mSkyCube.World	=Matrix.Translation(mGD.GCam.Position);

			mTerMats.SetMaterialParameter("Sky", "mWorld", mSkyCube.World);
			mDebugMats.UpdateWVP(Matrix.Identity, mGD.GCam.View, mGD.GCam.Projection, mGD.GCam.Position);
		}


		internal void Render()
		{
			mTerMats.ApplyMaterialPass("Sky", mGD.DC, 0);
			mSkyCube.Draw(mGD.DC);

			if(mTerrain != null)
			{
				mTerrain.Draw(mGD, mTerMats, mFrust);
			}

			mST.Draw(mGD.DC, Matrix.Identity, mTextProj);
		}


		internal void FreeAll()
		{
			mFontMats.FreeAll();
			mAudio.FreeAll();
		}


		bool WrapPosition(ref Vector3 pos)
		{
			bool	bWrapped	=false;

			if(pos.X > mBoundary)
			{
				pos.X	-=mBoundary;
				mGridCoordinate.X++;
				bWrapped	=true;
			}
			else if(pos.X < 0f)
			{
				pos.X	+=mBoundary;
				mGridCoordinate.X--;
				bWrapped	=true;
			}

			if(pos.Z > mBoundary)
			{
				pos.Z	-=mBoundary;
				mGridCoordinate.Y++;
				bWrapped	=true;
			}
			else if(pos.Z < 0f)
			{
				pos.Z	+=mBoundary;
				mGridCoordinate.Y--;
				bWrapped	=true;
			}

			return	bWrapped;
		}


		bool WrapGridCoordinates()
		{
			bool	bWrapped	=false;

			if(mGridCoordinate.X >= mCellGridMax)
			{
				mGridCoordinate.X	=0;
				bWrapped	=true;
			}
			else if(mGridCoordinate.X < 0)
			{
				mGridCoordinate.X	=mCellGridMax - 1;
				bWrapped	=true;
			}

			if(mGridCoordinate.Y >= mCellGridMax)
			{
				mGridCoordinate.Y	=0;
				bWrapped	=true;
			}
			else if(mGridCoordinate.Y < 0)
			{
				mGridCoordinate.Y	=mCellGridMax - 1;
				bWrapped	=true;
			}

			return	bWrapped;
		}


		internal void Texture(TexAtlas texAtlas, List<HeightMap.TexData> texInfo, float transHeight)
		{
			mSK.AddMap("TerrainAtlas", texAtlas.GetAtlasSRV());
			mTerMats.SetMaterialTexture("Terrain", "mTexture0", "TerrainAtlas");

			Vector4	[]scaleofs	=new Vector4[16];
			float	[]scale		=new float[16];

			for(int i=0;i < texInfo.Count;i++)
			{
				if(i > 15)
				{
					break;
				}

				scaleofs[i]	=new Vector4(
					(float)texInfo[i].mScaleU,
					(float)texInfo[i].mScaleV,
					(float)texInfo[i].mUOffs,
					(float)texInfo[i].mVOffs);

				//basically a divisor
				scale[i]	=1.0f / texInfo[i].ScaleFactor;
			}
			mTerMats.SetMaterialParameter("Terrain", "mAtlasUVData", scaleofs);
			mTerMats.SetMaterialParameter("Terrain", "mAtlasTexScale", scale);

			if(mTerrain != null)
			{
				mTerrain.SetTextureData(texInfo, transHeight);
			}

			Vector3	lightDir	=Mathery.RandomDirection(mRand);
			mTerMats.SetMaterialParameter("Terrain", "mLightDirection", lightDir);
		}


		void LoadShading(string path)
		{
			string	noExt	=FileUtil.StripExtension(path);

			string	fname	=noExt + ".TerShading";

			FileStream	fs	=new FileStream(fname, FileMode.Open, FileAccess.Read);
			if(fs == null)
			{
				return;
			}

			BinaryReader	br	=new BinaryReader(fs);
			if(br == null)
			{
				return;
			}

			UInt32	magic	=br.ReadUInt32();
			if(magic != 0x5ADE1BF0)
			{
				br.Close();
				fs.Close();
				return;
			}

			bool	bFogEnabled	=br.ReadBoolean();
			float	fogStart	=br.ReadSingle();
			float	fogEnd		=br.ReadSingle();

			mChunkRange	=br.ReadInt32();

			mTerMats.SetMaterialParameter("Terrain", "mFogStart", fogStart);
			mTerMats.SetMaterialParameter("Terrain", "mFogEnd", fogEnd);
			mTerMats.SetMaterialParameter("Terrain", "mFogEnabled", bFogEnabled? 1f : 0f);

			UInt32	fogColor0	=br.ReadUInt32();
			UInt32	fogColor1	=br.ReadUInt32();

			Color	col0	=new Color(fogColor0);
			Color	col1	=new Color(fogColor1);

			mTerMats.SetMaterialParameter("Sky", "mSkyGradient0", col0.ToVector3());
			mTerMats.SetMaterialParameter("Sky", "mSkyGradient1", col1.ToVector3());
		}


		void LoadTerrainTextureStuff(string path)
		{
			string	noExt	=FileUtil.StripExtension(path);

			string	fname	=noExt + ".TerTexData";

			FileStream	fs	=new FileStream(fname, FileMode.Open, FileAccess.Read);
			if(fs == null)
			{
				return;
			}

			BinaryReader	br	=new BinaryReader(fs);
			if(br == null)
			{
				return;
			}


			UInt32	magic	=br.ReadUInt32();
			if(magic != 0x7EC5DA7A)
			{
				br.Close();
				fs.Close();
				return;
			}

			//atlas size
			int	atlasX	=br.ReadInt32();
			int	atlasY	=br.ReadInt32();

			//transition height
			int	transitionHeight	=br.ReadInt32();

			TexAtlas	ta	=new TexAtlas(mGD, atlasX, atlasY);

			//load into a temp list first
			List<HeightMap.TexData>	gridStuffs	=new List<HeightMap.TexData>();

			int	count	=br.ReadInt32();

			for(int i=0;i < count;i++)
			{
				HeightMap.TexData	td	=new HeightMap.TexData();

				td.BottomElevation	=br.ReadSingle();
				td.TopElevation		=br.ReadSingle();
				td.Steep			=br.ReadBoolean();
				td.ScaleFactor		=br.ReadSingle();
				td.TextureName		=br.ReadString();
				td.mScaleU			=br.ReadDouble();
				td.mScaleV			=br.ReadDouble();
				td.mUOffs			=br.ReadDouble();
				td.mVOffs			=br.ReadDouble();

				gridStuffs.Add(td);
			}

			br.Close();
			fs.Close();

			List<string>	textures	=mSK.GetTexture2DList();
			for(int i=0;i < gridStuffs.Count;i++)
			{
				HeightMap.TexData	gd	=gridStuffs[i];
				if(textures.Contains(gd.TextureName))
				{
					if(!mSK.AddTexToAtlas(ta, gd.TextureName, mGD,
						out gd.mScaleU, out gd.mScaleV, out gd.mUOffs, out gd.mVOffs))
					{
						//todo something here
					}
				}
			}

			ta.Finish(mGD);

			Texture(ta, gridStuffs, transitionHeight);
		}
	}
}
