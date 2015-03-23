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

		Random	mRand	=new Random();

		//helpers
		IDKeeper	mKeeper	=new IDKeeper();

		//static stuff
		MatLib						mStaticMats;
		Dictionary<string, IArch>	mStatics	=new Dictionary<string, IArch>();
		List<StaticMesh>			mMeshes		=new List<StaticMesh>();

		//test characters
		Dictionary<string, IArch>		mCharArchs	=new Dictionary<string, IArch>();
		Dictionary<Character, IArch>	mCharToArch	=new Dictionary<Character,IArch>();
		List<Character>					mCharacters	=new List<Character>();
		List<string>					mAnims		=new List<string>();
		float[]							mAnimTimes;
		int[]							mCurAnims;
		MatLib							mCharMats;
		AnimLib							mCharAnims;
		int								mCurChar;

		//fontery
		ScreenText		mST;
		MatLib			mFontMats;
		Matrix			mTextProj;
		int				mResX, mResY;
		List<string>	mFonts	=new List<string>();

		//2d stuff
		ScreenUI	mSUI;

		//shader compile progress indicator
		SharedForms.ThreadedProgress	mSProg;

		//gpu
		GraphicsDevice	mGD;

		//collision debuggery
		CommonPrims	mCPrims;
		Vector4		mHitColor;
		int			mFrameCheck;
		int[]		mCBone;

		//collision bones
		Dictionary<int, Matrix>[]	mCBones;


		internal Game(GraphicsDevice gd, string gameRootDir)
		{
			mGD				=gd;
			mGameRootDir	=gameRootDir;
			mResX			=gd.RendForm.ClientRectangle.Width;
			mResY			=gd.RendForm.ClientRectangle.Height;

			mSKeeper	=new StuffKeeper();

			mSKeeper.eCompilesNeeded	+=OnCompilesNeeded;
			mSKeeper.eCompileDone		+=OnCompileDone;

			mSKeeper.Init(mGD, gameRootDir);

			mFontMats	=new MatLib(gd, mSKeeper);
			mCPrims		=new CommonPrims(gd, mSKeeper);

			mFonts	=mSKeeper.GetFontList();

			mFontMats.CreateMaterial("Text");
			mFontMats.SetMaterialEffect("Text", "2D.fx");
			mFontMats.SetMaterialTechnique("Text", "Text");

			mST		=new ScreenText(gd.GD, mFontMats, mFonts[0], 1000);
			mSUI	=new ScreenUI(gd.GD, mFontMats, 100);

			mTextProj	=Matrix.OrthoOffCenterLH(0, mResX, mResY, 0, 0.1f, 5f);

			//load avail static stuff
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
					mKeeper.AddLib(mStaticMats);
				}
				mStatics	=Mesh.LoadAllStaticMeshes(mGameRootDir + "\\Statics", gd.GD);

				fi	=di.GetFiles("*.StaticInstance", SearchOption.TopDirectoryOnly);
				foreach(FileInfo f in fi)
				{
					string	archName	=f.Name;
					if(archName.Contains('_'))
					{
						archName	=f.Name.Substring(0, f.Name.IndexOf('_'));
					}

					if(!mStatics.ContainsKey(archName))
					{
						continue;
					}

					StaticMesh	sm	=new StaticMesh(mStatics[archName]);

					sm.ReadFromFile(f.DirectoryName + "\\" + f.Name);

					mMeshes.Add(sm);

					sm.UpdateBounds();
					sm.SetMatLib(mStaticMats);
					sm.SetTransform(Matrix.Translation(
						Mathery.RandomPosition(mRand,
							Vector3.UnitX * 100f +
							Vector3.UnitZ * 100f)));
				}
			}

			mStaticMats.InitCelShading(1);
			mStaticMats.GenerateCelTexturePreset(gd.GD,
				(gd.GD.FeatureLevel == FeatureLevel.Level_11_0),
				true, 0);
			mStaticMats.SetCelTexture(0);

			//skip hair stuff when computing bone bounds
			//hits to hair usually wouldn't activate much
			List<string>	skipMats	=new List<string>();
			skipMats.Add("Hair");

			//load character stuff if any around
			if(Directory.Exists(mGameRootDir + "/Characters"))
			{
				DirectoryInfo	di	=new DirectoryInfo(mGameRootDir + "/Characters");

				FileInfo[]	fi	=di.GetFiles("*.AnimLib", SearchOption.TopDirectoryOnly);
				if(fi.Length > 0)
				{
					mCharAnims	=new AnimLib();
					mCharAnims.ReadFromFile(fi[0].DirectoryName + "\\" + fi[0].Name);

					List<Anim>	anims	=mCharAnims.GetAnims();
					foreach(Anim a in anims)
					{
						mAnims.Add(a.Name);
					}
				}

				fi	=di.GetFiles("*.MatLib", SearchOption.TopDirectoryOnly);
				if(fi.Length > 0)
				{
					mCharMats	=new MatLib(mGD, mSKeeper);
					mCharMats.ReadFromFile(fi[0].DirectoryName + "\\" + fi[0].Name);
					mCharMats.InitCelShading(1);
					mCharMats.GenerateCelTexturePreset(gd.GD,
						gd.GD.FeatureLevel == FeatureLevel.Level_9_3, false, 0);
					mCharMats.SetCelTexture(0);
					mKeeper.AddLib(mCharMats);
				}

				fi	=di.GetFiles("*.Character", SearchOption.TopDirectoryOnly);
				foreach(FileInfo f in fi)
				{
					IArch	arch	=new CharacterArch();
					arch.ReadFromFile(f.DirectoryName + "\\" + f.Name, mGD.GD, true);

					mCharArchs.Add(FileUtil.StripExtension(f.Name), arch);
				}

				fi	=di.GetFiles("*.CharacterInstance", SearchOption.TopDirectoryOnly);
				foreach(FileInfo f in fi)
				{
					string	archName	=f.Name;
					if(archName.Contains('_'))
					{
						archName	=f.Name.Substring(0, f.Name.IndexOf('_'));
					}

					if(!mCharArchs.ContainsKey(archName))
					{
						continue;
					}

					Character	c	=new Character(mCharArchs[archName], mCharAnims);

					//map this to an arch
					mCharToArch.Add(c, mCharArchs[archName]);

					c.ReadFromFile(f.DirectoryName + "\\" + f.Name);

					c.SetMatLib(mCharMats);

					c.SetTransform(Matrix.Translation(
						Mathery.RandomPosition(mRand,
							Vector3.UnitX * 100f +
							Vector3.UnitZ * 100f)));

					c.ComputeBoneBounds(skipMats);

					c.AutoInvert(true, 0.15f);

					mCharacters.Add(c);
				}

				if(mCharacters.Count > 0)
				{
					mAnimTimes	=new float[mCharacters.Count];
					mCurAnims	=new int[mCharacters.Count];
					mCBone		=new int[mCharacters.Count];
					mCBones		=new Dictionary<int,Matrix>[mCharacters.Count];
				}

				foreach(KeyValuePair<string, IArch> arch in mCharArchs)
				{
					//build draw data for bone bounds
					(arch.Value as CharacterArch).BuildDebugBoundDrawData(mGD.GD, mCPrims);
				}
			}

			//typical material group for characters
			//or at least it works with the ones
			//I have right now
			//TODO: way to define these in the asset?
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

			Vector4	color	=Vector4.UnitY + (Vector4.UnitW * 0.15f);

			mSUI.AddGump("UI\\CrossHair", "CrossHair", Vector4.One,
				Vector2.UnitX * ((mResX / 2) - 16)
				+ Vector2.UnitY * ((mResY / 2) - 16),
				Vector2.One);

			//string indicators for various statusy things
			mST.AddString(mFonts[0], "", "AnimStatus",
				color, Vector2.UnitX * 20f + Vector2.UnitY * 500f, Vector2.One);
			mST.AddString(mFonts[0], "", "CharStatus",
				color, Vector2.UnitX * 20f + Vector2.UnitY * 520f, Vector2.One);
			mST.AddString(mFonts[0], "", "PosStatus",
				color, Vector2.UnitX * 20f + Vector2.UnitY * 540f, Vector2.One);
			mST.AddString(mFonts[0], "", "BoundsStatus",
				color, Vector2.UnitX * 20f + Vector2.UnitY * 560f, Vector2.One);

			UpdateCAStatus();

			mHitColor	=Vector4.One * 0.9f;
			mHitColor.Y	=mHitColor.Z	=0f;
		}


		internal void Update(TimeSpan frameTime, List<Input.InputAction> actions)
		{
			mFrameCheck++;

			Vector3	startPos	=mGD.GCam.Position;
			Vector3	endPos		=startPos + mGD.GCam.Forward * -2000f;

			float	deltaMS		=(float)frameTime.TotalMilliseconds;
			float	deltaSec	=(float)frameTime.TotalSeconds;

			//animate characters
			for(int i=0;i < mCharacters.Count;i++)
			{
				Character	c	=mCharacters[i];

				c.Update(deltaSec);

				float	totTime	=mCharAnims.GetAnimTime(mAnims[mCurAnims[i]]);
				float	strTime	=mCharAnims.GetAnimStartTime(mAnims[mCurAnims[i]]);
				float	endTime	=totTime + strTime;

				mAnimTimes[i]	+=deltaSec;
				if(mAnimTimes[i] > endTime)
				{
					mAnimTimes[i]	=strTime + (mAnimTimes[i] - endTime);
				}

				c.Animate(mAnims[mCurAnims[i]], mAnimTimes[i]);

				mCBones[i]	=(mCharToArch[c] as CharacterArch).GetBoneTransforms(mCharAnims.GetSkeleton());
			}

			//check for keys
			foreach(Input.InputAction act in actions)
			{
				if(act.mAction.Equals(Program.MyActions.NextCharacter))
				{
					mCurChar++;
					if(mCurChar >= mCharacters.Count)
					{
						mCurChar	=0;
					}
					UpdateCAStatus();
				}
				else if(act.mAction.Equals(Program.MyActions.NextAnim))
				{
					mCurAnims[mCurChar]++;
					if(mCurAnims[mCurChar] >= mAnims.Count)
					{
						mCurAnims[mCurChar]	=0;
					}
					UpdateCAStatus();
				}
			}
			/*
			Mesh	partHit;

			float	?bHit	=mTestCol.RayIntersect(startPos, endPos, true);
			if(bHit != null)
			{
				bHit	=mTestCol.RayIntersect(startPos, endPos, true, out partHit);
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
			}*/
			
			//adjust coordinate system
			Matrix	shiftMat	=Matrix.RotationX(MathUtil.PiOverTwo);
			shiftMat.Invert();

			shiftMat	=Matrix.Identity;

			startPos	=Vector3.TransformCoordinate(startPos, shiftMat);
			endPos		=Vector3.TransformCoordinate(endPos, shiftMat);

			mST.Update(mGD.DC);

			mSUI.Update(mGD.DC);

			mStaticMats.SetParameterForAll("mView", mGD.GCam.View);
			mStaticMats.SetParameterForAll("mEyePos", mGD.GCam.Position);
			mStaticMats.SetParameterForAll("mProjection", mGD.GCam.Projection);

			mCharMats.SetParameterForAll("mView", mGD.GCam.View);
			mCharMats.SetParameterForAll("mEyePos", mGD.GCam.Position);
			mCharMats.SetParameterForAll("mProjection", mGD.GCam.Projection);

			mCPrims.Update(mGD.GCam, Vector3.Down);
			/*
			bHit	=mChar1.RayIntersect(startPos, endPos);
			if(bHit != null)
			{
				mChar1.RayIntersectBones(startPos, endPos, false, out mC1Bone);
			}

			bHit	=mChar2.RayIntersect(startPos, endPos);
			if(bHit != null)
			{
				mChar2.RayIntersectBones(startPos, endPos, false, out mC2Bone);
			}

			bHit	=mChar3.RayIntersect(startPos, endPos);
			if(bHit != null)
			{
				mChar3.RayIntersectBones(startPos, endPos, false, out mC3Bone);
			}

			if(mFrameCheck == 10)
			{
				mFrameCheck	=0;

				mST.ModifyStringText(mFonts[0], "1:" + mChar1.GetThreadMisses() +
					", 2: " + mChar2.GetThreadMisses() + ", 3: "
					+ mChar3.GetThreadMisses(), "boing");
			}
			mCPrims.ReBuildBoundsDrawData(mGD.GD, mCharacters[0]);
			*/

			UpdatePosStatus();
		}


		internal void Render(DeviceContext dc)
		{
			foreach(Character c in mCharacters)
			{
				c.Draw(dc, mCharMats);
			}

			foreach(StaticMesh sm in mMeshes)
			{
				sm.Draw(dc, mStaticMats);
			}

			for(int i=0;i < mCharacters.Count;i++)
			{
				foreach(KeyValuePair<int, Matrix> bone in mCBones[i])
				{
					Matrix	boneTrans	=bone.Value;

					if(bone.Key == mCBone[i])
					{
						mCPrims.DrawBox(dc, bone.Key, boneTrans * mCharacters[i].GetTransform(), mHitColor);
					}
					else
					{
						mCPrims.DrawBox(dc, bone.Key, boneTrans * mCharacters[i].GetTransform(), Vector4.One * 0.5f);
					}
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

			mSKeeper.FreeAll();
		}


		void UpdateCAStatus()
		{
			mST.ModifyStringText(mFonts[0], "(C) CurCharacter: " + mCurChar, "CharStatus");
			mST.ModifyStringText(mFonts[0], "(N) CurAnim: " + mAnims[mCurAnims[mCurChar]], "AnimStatus");
		}


		void UpdatePosStatus()
		{
			mST.ModifyStringText(mFonts[0], "(WASD) :"
				+ (int)mGD.GCam.Position.X + ", "
				+ (int)mGD.GCam.Position.Y + ", "
				+ (int)mGD.GCam.Position.Z, "PosStatus");
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
