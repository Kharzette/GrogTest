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

using SharpDX;
using SharpDX.DXGI;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;

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

		//gpu
		GraphicsDevice	mGD;


		internal Game(GraphicsDevice gd, string gameRootDir)
		{
			mGD				=gd;
			mGameRootDir	=gameRootDir;
			mResX			=gd.RendForm.ClientRectangle.Width;
			mResY			=gd.RendForm.ClientRectangle.Height;

			mSKeeper	=new StuffKeeper(mGD, gameRootDir);
			mFontMats	=new MatLib(gd, mSKeeper);

			mFontMats.CreateMaterial("Text");
			mFontMats.SetMaterialEffect("Text", "2D.fx");
			mFontMats.SetMaterialTechnique("Text", "Text");

			mST	=new ScreenText(gd.GD, mFontMats, "Pescadero50", 1000);

			mTextProj	=Matrix.OrthoOffCenterLH(0, mResX, mResY, 0, 0.1f, 5f);

			//static stuff
			mStaticMats	=new MatLib(gd, mSKeeper);
			mStaticMats.ReadFromFile(mGameRootDir + "/Statics/BBGun.MatLib");
			mStatics	=Mesh.LoadAllStaticMeshes(mGameRootDir + "\\Statics", gd.GD);

			mKey1	=new StaticMesh(mStatics["PurpleKey.Static"]);
			mKey2	=new StaticMesh(mStatics["PurpleKey.Static"]);
			mKey3	=new StaticMesh(mStatics["PurpleKey.Static"]);

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

			mStaticMats.InitCelShading(1);
			mStaticMats.GenerateCelTexturePreset(gd.GD,
				(gd.GD.FeatureLevel == FeatureLevel.Level_11_0),
				true, 0);
			mStaticMats.SetCelTexture(0);

			//player character
			mCharAnims	=new AnimLib();
			mCharAnims.ReadFromFile(mGameRootDir + "/Characters/TestAnims.AnimLib");

			mCharArch	=new CharacterArch();
			mChar1		=new Character(mCharArch, mCharAnims);
			mChar2		=new Character(mCharArch, mCharAnims);
			mChar3		=new Character(mCharArch, mCharAnims);

			mCharArch.ReadFromFile(mGameRootDir + "/Characters/Test.Character", mGD.GD, false);
			mChar1.ReadFromFile(mGameRootDir + "/Characters/Test01.CharacterInstance");
			mChar2.ReadFromFile(mGameRootDir + "/Characters/Test02.CharacterInstance");
			mChar3.ReadFromFile(mGameRootDir + "/Characters/Test03.CharacterInstance");

			//character cel 2 stage
			float	[]levels	=new float[2];
			float	[]thresh	=new float[1];

			levels[0]	=0.4f;
			levels[1]	=1f;
			thresh[0]	=0.3f;

			mCharMats	=new MatLib(mGD, mSKeeper);
			mCharMats.ReadFromFile(mGameRootDir + "/Characters/CharacterTest.MatLib");
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
			mChar2.SetTransform(Matrix.Translation(Vector3.UnitX * 20f));
			mChar3.SetTransform(Matrix.Translation(Vector3.UnitX * -20f));

			mChar1StartTime	=mCharAnims.GetAnimStartTime("TestIdle");
			mChar2StartTime	=mCharAnims.GetAnimStartTime("WalkLoop");
			mChar3StartTime	=mCharAnims.GetAnimStartTime("WalkLoop");

			mChar1EndTime	=mChar1StartTime + mCharAnims.GetAnimTime("TestIdle");
			mChar2EndTime	=mChar2StartTime + mCharAnims.GetAnimTime("WalkLoop");
			mChar3EndTime	=mChar3StartTime + mCharAnims.GetAnimTime("WalkLoop");

			mLastTime	=Stopwatch.GetTimestamp();

			Vector4	color	=Vector4.UnitY + (Vector4.UnitW * 0.15f);

			mST.AddString("Pescadero50", "Boing!", "boing",
				color, Vector2.One * 20f, Vector2.One * 1f);

			mTextMover.SetUpMove(Vector2.One * 20f,
				Vector2.UnitX * (mResX - 100f) + Vector2.UnitY * (mResY - 50),
				10f, 0.2f, 0.2f);

			mbForward	=true;
		}


		internal void Update(float msDelta, List<Input.InputAction> actions)
		{
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

			Vector2	randScale;
			randScale.X	=Mathery.RandomFloatNext(mRand, 0.5f, 2f);
			randScale.Y	=Mathery.RandomFloatNext(mRand, 0.5f, 2f);

			mST.ModifyStringColor("boing", Mathery.RandomColorVector4(mRand));
			mST.ModifyStringScale("boing", randScale);

			mST.ModifyStringPosition("boing", mTextMover.GetPos());

			mST.Update(mGD.DC);

			mStaticMats.SetParameterForAll("mView", mGD.GCam.View);
			mStaticMats.SetParameterForAll("mEyePos", mGD.GCam.Position);
			mStaticMats.SetParameterForAll("mProjection", mGD.GCam.Projection);

			mCharMats.SetParameterForAll("mView", mGD.GCam.View);
			mCharMats.SetParameterForAll("mEyePos", mGD.GCam.Position);
			mCharMats.SetParameterForAll("mProjection", mGD.GCam.Projection);

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

			mChar1.Animate("TestIdle", mChar1AnimTime);
			mChar2.Animate("WalkLoop", mChar2AnimTime);
			mChar3.Animate("WalkLoop", mChar3AnimTime);
		}


		internal void Render(DeviceContext dc)
		{
			mChar1.Draw(dc, mCharMats);
			mChar2.Draw(dc, mCharMats);
			mChar3.Draw(dc, mCharMats);
			mKey1.Draw(dc, mStaticMats);
			mKey2.Draw(dc, mStaticMats);
			mKey3.Draw(dc, mStaticMats);

			mST.Draw(dc, Matrix.Identity, mTextProj);
		}


		internal void FreeAll()
		{
			mStaticMats.FreeAll();
			mKeeper.Clear();
			mCharMats.FreeAll();

			mSKeeper.FreeAll();
		}
	}
}
