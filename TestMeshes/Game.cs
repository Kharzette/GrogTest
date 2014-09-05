using System;
using System.IO;
using System.Threading;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using MeshLib;
using UtilityLib;
using MaterialLib;
using InputLib;
using AudioLib;

using SharpDX;
using SharpDX.DXGI;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.X3DAudio;

using MatLib	=MaterialLib.MaterialLib;


namespace TestMeshes
{
	class Game
	{
		//data
		string		mGameRootDir;
		StuffKeeper	mSKeeper;
		long		mLastTime;

		Random	mRand	=new Random();

		//helpers
		IDKeeper	mKeeper	=new IDKeeper();

		//static stuff
		MatLib						mStaticMats;
		Dictionary<string, IArch>	mStatics	=new Dictionary<string, IArch>();
		StaticMesh					mKey1, mKey2, mKey3;
		StaticMesh					mTestCol;

		//test characters
		IArch		mCharArch;
		Character	mChar1, mChar2, mChar3;
		MatLib		mCharMats;
		AnimLib		mCharAnims;
		float		mChar1AnimTime, mChar2AnimTime, mChar3AnimTime;
		float		mChar1StartTime, mChar2StartTime, mChar3StartTime;
		float		mChar1EndTime, mChar2EndTime, mChar3EndTime;

		//fontery
		ScreenText	mST;
		MatLib		mFontMats;
		Matrix		mTextProj;
		Mover2		mTextMover	=new Mover2();
		bool		mbForward;
		int			mResX, mResY;

		//2d stuff
		ScreenUI	mSUI;

		//gpu
		GraphicsDevice	mGD;

		//collision debuggery
		CommonPrims	mCPrims;
		int			mC1Bone, mC2Bone, mC3Bone;
		Vector4		mHitColor;
		int			mFrameCheck;

		//collision bones
		Dictionary<int, Matrix>	mC1Bones	=new Dictionary<int, Matrix>();
		Dictionary<int, Matrix>	mC2Bones	=new Dictionary<int, Matrix>();
		Dictionary<int, Matrix>	mC3Bones	=new Dictionary<int, Matrix>();

		//audio
		Audio	mAudio	=new Audio();
		Emitter	mEmitter;


		internal Game(GraphicsDevice gd, string gameRootDir)
		{
			mGD				=gd;
			mGameRootDir	=gameRootDir;
			mResX			=gd.RendForm.ClientRectangle.Width;
			mResY			=gd.RendForm.ClientRectangle.Height;

			mSKeeper	=new StuffKeeper(mGD, gameRootDir);
			mFontMats	=new MatLib(gd, mSKeeper);
			mCPrims		=new CommonPrims(gd, mSKeeper);

			mFontMats.CreateMaterial("Text");
			mFontMats.SetMaterialEffect("Text", "2D.fx");
			mFontMats.SetMaterialTechnique("Text", "Text");

			mST		=new ScreenText(gd.GD, mFontMats, "Pescadero40", 1000);
			mSUI	=new ScreenUI(gd.GD, mFontMats, 100);

			mTextProj	=Matrix.OrthoOffCenterLH(0, mResX, mResY, 0, 0.1f, 5f);

			//static stuff
			mStaticMats	=new MatLib(gd, mSKeeper);
			mStaticMats.ReadFromFile(mGameRootDir + "/Statics/Statics.MatLib");
			mStatics	=Mesh.LoadAllStaticMeshes(mGameRootDir + "\\Statics", gd.GD);

			mStatics["Key.Static"].UpdateBounds();
			mStatics["TestCol.Static"].UpdateBounds();

			mKey1		=new StaticMesh(mStatics["Key.Static"]);
			mKey2		=new StaticMesh(mStatics["Key.Static"]);
			mKey3		=new StaticMesh(mStatics["Key.Static"]);
			mTestCol	=new StaticMesh(mStatics["TestCol.Static"]);
			
			mKey1.UpdateBounds();
			mKey2.UpdateBounds();
			mKey3.UpdateBounds();
			mTestCol.UpdateBounds();

			mTestCol.ReadFromFile(mGameRootDir + "/Statics/TestCol.StaticInstance");
			mTestCol.SetMatLib(mStaticMats);

			mKey1.AddPart(mStaticMats);
			mKey1.SetMatLib(mStaticMats);
			mKey1.SetPartMaterialName(0, "RedSteel");

			mKey2.AddPart(mStaticMats);
			mKey2.SetMatLib(mStaticMats);
			mKey2.SetPartMaterialName(0, "Wood");

			mKey3.AddPart(mStaticMats);
			mKey3.SetMatLib(mStaticMats);
			mKey3.SetPartMaterialName(0, "BlueSteel");

			mKey1.SetTransform(Matrix.Translation(Vector3.UnitZ * 10f));
			mKey2.SetTransform(Matrix.Translation(Vector3.UnitZ * 10f + Vector3.UnitX * 30));
			mKey3.SetTransform(Matrix.Translation(Vector3.UnitZ * 10f - Vector3.UnitX * 30));
			mTestCol.SetTransform(Matrix.Translation(Vector3.One * 100f));

			mCPrims.ReBuildBoundsDrawData(gd.GD, mTestCol);

			mStaticMats.InitCelShading(1);
			mStaticMats.GenerateCelTexturePreset(gd.GD,
				(gd.GD.FeatureLevel == FeatureLevel.Level_11_0),
				true, 0);
			mStaticMats.SetCelTexture(0);

			//player character
			mCharAnims	=new AnimLib();
			mCharAnims.ReadFromFile(mGameRootDir + "/Characters/Frankenstein45.AnimLib");

			mCharArch	=new CharacterArch();
			mChar1		=new Character(mCharArch, mCharAnims);
			mChar2		=new Character(mCharArch, mCharAnims);
			mChar3		=new Character(mCharArch, mCharAnims);

			mCharArch.ReadFromFile(mGameRootDir + "/Characters/TestNaked.Character", mGD.GD, true);
			mChar1.ReadFromFile(mGameRootDir + "/Characters/TestShortSleeveAndJeans.CharacterInstance");
			mChar2.ReadFromFile(mGameRootDir + "/Characters/TestTankAndShorts.CharacterInstance");
			mChar3.ReadFromFile(mGameRootDir + "/Characters/TestUndies.CharacterInstance");

			List<string>	skipMats	=new List<string>();

			skipMats.Add("Hair");

			mChar3.ComputeBoneBounds(skipMats);

			(mCharArch as CharacterArch).BuildDebugBoundDrawData(mGD.GD, mCPrims);

			//character cel 2 stage
			float	[]levels	=new float[2];
			float	[]thresh	=new float[1];

			levels[0]	=0.4f;
			levels[1]	=1f;
			thresh[0]	=0.3f;

			mCharMats	=new MatLib(mGD, mSKeeper);
			mCharMats.ReadFromFile(mGameRootDir + "/Characters/CharCelSkin.MatLib");
			mCharMats.InitCelShading(1);
			mCharMats.GenerateCelTexturePreset(gd.GD,
				gd.GD.FeatureLevel == FeatureLevel.Level_9_3, false, 0);
//			mPMats.GenerateCelTexture(mGD.GD,
//				(gd.GD.FeatureLevel != FeatureLevel.Level_9_3),
//				0, 64, thresh, levels);
			mCharMats.SetCelTexture(0);

			mChar1.SetMatLib(mCharMats);
			mChar2.SetMatLib(mCharMats);
			mChar3.SetMatLib(mCharMats);

			mKeeper.AddLib(mStaticMats);
			mKeeper.AddLib(mCharMats);

			List<string>	skinMats	=new List<string>();

			skinMats.Add("Face");
			skinMats.Add("Skin");
			skinMats.Add("EyeWhite");
			skinMats.Add("EyeLiner");
			skinMats.Add("IrisLeft");
			skinMats.Add("PupilLeft");
			skinMats.Add("IrisRight");
			skinMats.Add("PupilRight");
			skinMats.Add("Nails");
			mKeeper.AddMaterialGroup("SkinGroup", skinMats);

			mChar1.SetTransform(Matrix.Identity);
			mChar2.SetTransform(Matrix.Translation(Vector3.UnitX * 50f));
			mChar3.SetTransform(Matrix.Translation(Vector3.UnitX * -50f));

			mChar1StartTime	=mCharAnims.GetAnimStartTime("TestIdle");
			mChar2StartTime	=mCharAnims.GetAnimStartTime("WalkLoop");
			mChar3StartTime	=mCharAnims.GetAnimStartTime("TestAnim");

			mChar1EndTime	=mChar1StartTime + mCharAnims.GetAnimTime("TestIdle");
			mChar2EndTime	=mChar2StartTime + mCharAnims.GetAnimTime("WalkLoop");
			mChar3EndTime	=mChar3StartTime + mCharAnims.GetAnimTime("TestAnim");

			mLastTime	=Stopwatch.GetTimestamp();

			Vector4	color	=Vector4.UnitY + (Vector4.UnitW * 0.15f);

			mSUI.AddGump("UI\\CrossHair", "CrossHair", Vector4.One,
				Vector2.UnitX * ((mResX / 2) - 16)
				+ Vector2.UnitY * ((mResY / 2) - 16),
				Vector2.One);
			mSUI.AddGump("UI\\GumpElement", "CuteGump", Vector4.One, Vector2.One * 20f, Vector2.One);

			mSUI.ModifyGumpScale("CuteGump", Vector2.One * 0.35f);

			mST.AddString("Pescadero40", "Boing!", "boing",
				color, Vector2.One * 20f, Vector2.One * 1f);

			mTextMover.SetUpMove(Vector2.One * 20f,
				Vector2.UnitX * (mResX - 100f) + Vector2.UnitY * (mResY - 50),
				10f, 0.2f, 0.2f);

			mbForward	=true;

			mAudio.LoadSound("GainItem", mGameRootDir + "/Audio/SoundFX/GainItem.wav");
			mAudio.LoadSound("WinMusic", mGameRootDir + "/Audio/SoundFX/WinMusic.wav");

			mEmitter	=new Emitter();

			mEmitter.Position				=(Vector3.UnitZ * 10f - Vector3.UnitX * 30);
			mEmitter.OrientFront			=Vector3.ForwardRH;
			mEmitter.OrientTop				=Vector3.Up;
			mEmitter.Velocity				=Vector3.Zero;
			mEmitter.CurveDistanceScaler	=50f;
			mEmitter.ChannelCount			=1;
			mEmitter.DopplerScaler			=1f;

			mHitColor	=Vector4.One * 0.5f;

			mHitColor.Y	=mHitColor.Z	=0f;

			mChar1.AutoInvert(true, 0.1f);
			mChar2.AutoInvert(true, 0.1f);
			mChar3.AutoInvert(true, 0.1f);
		}


		internal void Update(float msDelta, List<Input.InputAction> actions)
		{
			mFrameCheck++;

			Vector3	startPos	=mGD.GCam.Position;
			Vector3	endPos		=startPos + mGD.GCam.Forward * -2000f;

			long	timeNow	=Stopwatch.GetTimestamp();
			long	delta	=timeNow - mLastTime;
			float	deltaMS	=((float)delta / (float)Stopwatch.Frequency);

			mLastTime	=timeNow;

			mChar1AnimTime	+=deltaMS;
			mChar2AnimTime	+=deltaMS;
			mChar3AnimTime	+=deltaMS;

			if(mChar1AnimTime > mChar1EndTime)
			{
				mChar1AnimTime	%=mChar1EndTime;
			}
			if(mChar1AnimTime < mChar1StartTime)
			{
				mChar1AnimTime	=mChar1StartTime;
			}

			if(mChar2AnimTime > mChar2EndTime)
			{
				mChar2AnimTime	%=mChar2EndTime;
			}
			if(mChar2AnimTime < mChar2StartTime)
			{
				mChar2AnimTime	=mChar2StartTime;
			}

			if(mChar3AnimTime > mChar3EndTime)
			{
				mChar3AnimTime	%=mChar3EndTime;
			}
			if(mChar3AnimTime < mChar3StartTime)
			{
				mChar3AnimTime	=mChar3StartTime;
			}

			//adjust coordinate system
			Matrix	shiftMat	=Matrix.RotationX(MathUtil.PiOverTwo);
			shiftMat.Invert();

			startPos	=Vector3.TransformCoordinate(startPos, shiftMat);
			endPos		=Vector3.TransformCoordinate(endPos, shiftMat);

			mChar1.Animate("TestIdle", mChar1AnimTime);
//			mChar1.UpdateInvertedBones(true);
			mC1Bones	=(mCharArch as CharacterArch).GetBoneTransforms(mCharAnims.GetSkeleton());

			mChar2.Animate("WalkLoop", mChar2AnimTime);
//			mChar2.UpdateInvertedBones(true);
			mC2Bones	=(mCharArch as CharacterArch).GetBoneTransforms(mCharAnims.GetSkeleton());

			mChar3.Animate("TestAnim", mChar3AnimTime);
//			mChar3.UpdateInvertedBones(true);
			mC3Bones	=(mCharArch as CharacterArch).GetBoneTransforms(mCharAnims.GetSkeleton());

			mTextMover.Update((int)msDelta);

			if(mTextMover.Done())
			{
				if(mbForward)
				{
					mTextMover.SetUpMove(Vector2.UnitX * (mResX - 100f) + Vector2.UnitY * (mResY - 50),
						Vector2.One * 20f,
						10f, 0.2f, 0.2f);
				}
				else
				{
					mTextMover.SetUpMove(Vector2.One * 20f,
						Vector2.UnitX * (mResX - 100f) + Vector2.UnitY * (mResY - 50),
						10f, 0.2f, 0.2f);
				}
				mbForward	=!mbForward;
			}

			mAudio.Update(mGD.GCam);

			foreach(Input.InputAction act in actions)
			{
				if(act.mAction.Equals(Program.MyActions.PlaceDynamicLight))
				{
					mAudio.PlayAtLocation("WinMusic", 2f, mEmitter);
				}
				else if(act.mAction.Equals(Program.MyActions.ClearDynamicLights))
				{
					mAudio.Play("GainItem", true, 0.5f);
				}
			}

			Vector2	randScale;
			randScale.X	=Mathery.RandomFloatNext(mRand, 0.5f, 2f);
			randScale.Y	=Mathery.RandomFloatNext(mRand, 0.5f, 2f);

			Mesh	partHit;

			float?	bHit	=mTestCol.RayIntersect(startPos, endPos, true, out partHit);
			if(bHit != null)
			{
				if(partHit == null)
				{
					mST.ModifyStringColor("boing", Vector4.UnitW + Vector4.UnitX);
				}
				else
				{
					mST.ModifyStringColor("boing", Vector4.UnitW + Vector4.UnitY);
				}
			}
			else
			{
				mST.ModifyStringColor("boing", Mathery.RandomColorVector4(mRand));
			}
			
//			mST.ModifyStringScale("boing", randScale);

//			mST.ModifyStringPosition("boing", mTextMover.GetPos());

			mST.Update(mGD.DC);

			mSUI.Update(mGD.DC);

			mStaticMats.SetParameterForAll("mView", mGD.GCam.View);
			mStaticMats.SetParameterForAll("mEyePos", mGD.GCam.Position);
			mStaticMats.SetParameterForAll("mProjection", mGD.GCam.Projection);

			mCharMats.SetParameterForAll("mView", mGD.GCam.View);
			mCharMats.SetParameterForAll("mEyePos", mGD.GCam.Position);
			mCharMats.SetParameterForAll("mProjection", mGD.GCam.Projection);

			mChar1.RayIntersectBones(startPos, endPos, false, out mC1Bone);
			mChar2.RayIntersectBones(startPos, endPos, false, out mC2Bone);
			mChar3.RayIntersectBones(startPos, endPos, false, out mC3Bone);

			if(mFrameCheck == 10)
			{
				mFrameCheck	=0;

				mST.ModifyStringText("Pescadero40", "1:" + mChar1.GetThreadMisses() +
					", 2: " + mChar2.GetThreadMisses() + ", 3: "
					+ mChar3.GetThreadMisses(), "boing");
			}
		}


		internal void Render(DeviceContext dc)
		{
			mChar1.Draw(dc, mCharMats);
			mChar2.Draw(dc, mCharMats);
			mChar3.Draw(dc, mCharMats);
			mKey1.Draw(dc, mStaticMats);
			mKey2.Draw(dc, mStaticMats);
			mKey3.Draw(dc, mStaticMats);
			mTestCol.Draw(dc, mStaticMats);

			mCPrims.DrawBox(dc, mTestCol.GetTransform());

			//adjust coordinate system
			Matrix	shiftMat	=Matrix.RotationX(MathUtil.PiOverTwo);

			foreach(KeyValuePair<int, Matrix> bone in mC1Bones)
			{
				Matrix	boneTrans	=bone.Value;

				if(bone.Key == mC1Bone)
				{
					mCPrims.DrawBox(dc, bone.Key, boneTrans * shiftMat * mChar1.GetTransform(), mHitColor);
				}
				else
				{
					mCPrims.DrawBox(dc, bone.Key, boneTrans * shiftMat * mChar1.GetTransform(), Vector4.One * 0.5f);
				}
			}

			foreach(KeyValuePair<int, Matrix> bone in mC2Bones)
			{
				Matrix	boneTrans	=bone.Value;

				if(bone.Key == mC2Bone)
				{
					mCPrims.DrawBox(dc, bone.Key, boneTrans * shiftMat * mChar2.GetTransform(), mHitColor);
				}
				else
				{
					mCPrims.DrawBox(dc, bone.Key, boneTrans * shiftMat * mChar2.GetTransform(), Vector4.One * 0.5f);
				}
			}

			foreach(KeyValuePair<int, Matrix> bone in mC3Bones)
			{
				Matrix	boneTrans	=bone.Value;

				if(bone.Key == mC3Bone)
				{
					mCPrims.DrawBox(dc, bone.Key, boneTrans * shiftMat * mChar3.GetTransform(), mHitColor);
				}
				else
				{
					mCPrims.DrawBox(dc, bone.Key, boneTrans * shiftMat * mChar3.GetTransform(), Vector4.One * 0.5f);
				}
			}

			mSUI.Draw(dc, Matrix.Identity, mTextProj);
			mST.Draw(dc, Matrix.Identity, mTextProj);
		}


		internal void FreeAll()
		{
			mStaticMats.FreeAll();
			mKeeper.Clear();
			mCharMats.FreeAll();
			mAudio.FreeAll();

			mSKeeper.FreeAll();
		}
	}
}
