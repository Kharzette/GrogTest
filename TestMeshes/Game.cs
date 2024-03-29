﻿using System.Numerics;
using System.Diagnostics;
using UtilityLib;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.Mathematics;
using InputLib;
using MaterialLib;
using MeshLib;

//renderform and renderloop
using SharpDX.Windows;

using MatLib	=MaterialLib.MaterialLib;
using Color		=Vortice.Mathematics.Color;


namespace TestMeshes;

class Game
{
	//data
	string			mGameRootDir;
	StuffKeeper		mSKeeper;
	Random			mRand		=new Random();
	Vector3			mLightDir	=-Vector3.UnitY;

	//helpers
	IDKeeper	mKeeper	=new IDKeeper();

	//static stuff
	MatLib						mStaticMats;
	Dictionary<string, IArch>	mStatics	=new Dictionary<string, IArch>();
	List<StaticMesh>			mMeshes		=new List<StaticMesh>();

	//static transform details
	List<Vector3>	mMeshPositions	=new List<Vector3>();
	List<Vector3>	mMeshRotations	=new List<Vector3>();
	List<Vector3>	mMeshScales		=new List<Vector3>();

	//test characters
	Dictionary<string, IArch>		mCharArchs	=new Dictionary<string, IArch>();
	Dictionary<Character, IArch>	mCharToArch	=new Dictionary<Character,IArch>();
	List<Character>					mCharacters	=new List<Character>();
	List<string>					mAnims		=new List<string>();
	float[]							mAnimTimes;
	int[]							mCurAnims;
	bool[]							mbCharAnimPause;
	MatLib							mCharMats;
	AnimLib							mCharAnims;
	int								mCurChar;

	//character transforms
	List<float>		mCharYaws	=new List<float>();
	List<Vector3>	mCharPoss	=new List<Vector3>();

	//fontery
	ScreenText		mST;
	MatLib			mFontMats;
	Matrix4x4		mGumpProj;
	int				mResX, mResY;
	List<string>	mFonts	=new List<string>();

	//2d stuff
	ScreenUI	mSUI;

	//gpu
	GraphicsDevice	mGD;

	//collision debuggery
	CommonPrims	mCPrims;
	Vector4		mHitColor, mTextColor;
	int			mFrameCheck, mCurStatic;
	Mesh		mPartHit;
	StaticMesh	mMeshHit;
	int			mBoneHit;
	Vector3		mHitPos, mHitNorm;
	bool		mbFreezeRay;		//pause to check out a collision

	//constants
	const float	TextScale		=1.5f;
	const int	HitSphereIndex	=6969;	//for finding the sphere used for hit indicator


	internal Game(GraphicsDevice gd, string gameRootDir)
	{
		mGD				=gd;
		mGameRootDir	=gameRootDir;
		mResX			=gd.RendForm.ClientRectangle.Width;
		mResY			=gd.RendForm.ClientRectangle.Height;

		mSKeeper	=new StuffKeeper();

		mSKeeper.eCompileNeeded	+=SharedForms.ShaderCompileHelper.CompileNeededHandler;
		mSKeeper.eCompileDone	+=SharedForms.ShaderCompileHelper.CompileDoneHandler;

		mSKeeper.Init(mGD, gameRootDir);

		mFontMats	=new MatLib(mSKeeper);
		mCPrims		=new CommonPrims(gd.GD, mSKeeper);

		mFonts	=mSKeeper.GetFontList();

		mFontMats.CreateMaterial("Text", false, false);
		mFontMats.SetMaterialVShader("Text", "TextVS");
		mFontMats.SetMaterialPShader("Text", "TextPS");

		mST	=new ScreenText(gd, mSKeeper, mFonts[0], mFonts[0], 1000);

		//full size
		mGumpProj	=Matrix4x4.CreateOrthographicOffCenter(
			0, mResX, mResY, 0, 0f, 1f);

		mSUI	=new ScreenUI(gd, mSKeeper, 100);

		//load avail static stuff
		if(Directory.Exists(mGameRootDir + "/Statics"))
		{
			DirectoryInfo	di	=new DirectoryInfo(mGameRootDir + "/Statics");

			FileInfo[]	fi	=di.GetFiles("*.MatLib", SearchOption.TopDirectoryOnly);

			if(fi.Length > 0)
			{
				mStaticMats	=new MatLib(mSKeeper);
				mStaticMats.Load(fi[0].DirectoryName + "\\" + fi[0].Name, false);

				mKeeper.AddLib(mStaticMats);
			}

			mStatics	=Mesh.LoadAllStaticMeshes(mGameRootDir + "\\Statics", gd.GD);

			foreach(KeyValuePair<string, IArch> arch in mStatics)
			{
				arch.Value.UpdateBounds();
			}

			fi	=di.GetFiles("*.StaticInstance", SearchOption.TopDirectoryOnly);
			foreach(FileInfo f in fi)
			{
				string	archName	=FileUtil.StripExtension(f.Name);
				if(archName.Contains('_'))
				{
					archName	=archName.Substring(0, f.Name.IndexOf('_'));
				}

				archName	+=".Static";

				if(!mStatics.ContainsKey(archName))
				{
					continue;
				}

				StaticMesh	sm	=new StaticMesh(mStatics[archName]);

				sm.ReadFromFile(f.DirectoryName + "\\" + f.Name);

				mMeshes.Add(sm);

//				sm.UpdateBounds();
//				sm.SetMatLib(mStaticMats);
				Vector3	randPos	=Mathery.RandomPosition(mRand,
						Vector3.UnitX * 100f +
						Vector3.UnitZ * 100f);
				mMeshPositions.Add(randPos);
				mMeshRotations.Add(Vector3.Zero);
				mMeshScales.Add(Vector3.One);
				UpdateStaticTransform(mMeshes.Count - 1);
			}
			AddStaticCollision();
		}

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
				mCharMats	=new MatLib(mSKeeper);
				mCharMats.Load(fi[0].DirectoryName + "\\" + fi[0].Name, false);
				mKeeper.AddLib(mCharMats);
			}

			fi	=di.GetFiles("*.Character", SearchOption.TopDirectoryOnly);
			foreach(FileInfo f in fi)
			{
				IArch	arch	=new CharacterArch();
				arch.ReadFromFile(f.DirectoryName + "\\" + f.Name, mGD.GD, true);

				mCharArchs.Add(FileUtil.StripExtension(f.Name), arch);

				mCPrims.SetAxisScale(arch.GetSkin().GetScaleFactor());

				CharacterArch	?ca	=arch as CharacterArch;

				ca?.BuildDebugBoundDrawData(mCPrims);
			}

			fi	=di.GetFiles("*.CharacterInstance", SearchOption.TopDirectoryOnly);
			foreach(FileInfo f in fi)
			{
				string	archName	=FileUtil.StripExtension(f.Name);

				if(!mCharArchs.ContainsKey(archName))
				{
					continue;
				}

				Character	c	=new Character(mCharArchs[archName], mCharAnims);

				//map this to an arch
				mCharToArch.Add(c, mCharArchs[archName]);

				c.ReadFromFile(f.DirectoryName + "\\" + f.Name);

//				c.SetMatLib(mCharMats);
//				c.ComputeBoneBounds(skipMats);
//				c.AutoInvert(true, mInvertInterval);

				mCharacters.Add(c);
				mCharYaws.Add(0);
				mCharPoss.Add(Mathery.RandomPosition(mRand,	Vector3.UnitX * 100f + Vector3.UnitZ * 100f));
			}

			if(mCharacters.Count > 0)
			{
				mAnimTimes		=new float[mCharacters.Count];
				mCurAnims		=new int[mCharacters.Count];
				mbCharAnimPause	=new bool[mCharacters.Count];
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

		mTextColor	=Vector4.UnitY + (Vector4.UnitW * 0.75f);
		mHitColor	=Vector4.One * 0.9f;
		mHitColor.Y	=mHitColor.Z	=0f;

		mSUI.AddGump("UI\\CrossHair", null, "CrossHair",
			Vector4.One, Vector2.UnitX * ((mResX / 2) - 16)
			+ Vector2.UnitY * ((mResY / 2) - 16),
			Vector2.One);

		//string indicators for various statusy things
		mST.AddString("", "StaticStatus",
			mTextColor, Vector2.UnitX * 20f + Vector2.UnitY * 460f, Vector2.One);
		mST.AddString("", "AnimStatus",
			mTextColor, Vector2.UnitX * 20f + Vector2.UnitY * 480f, Vector2.One);
		mST.AddString("", "CharStatus",
			mTextColor, Vector2.UnitX * 20f + Vector2.UnitY * 500f, Vector2.One);
		mST.AddString("", "PosStatus",
			mTextColor, Vector2.UnitX * 20f + Vector2.UnitY * 520f, Vector2.One);
		mST.AddString("", "HitStatus",
			mTextColor, Vector2.UnitX * 20f + Vector2.UnitY * 540f, Vector2.One);

		mST.ModifyStringScale("StaticStatus", TextScale * Vector2.One);
		mST.ModifyStringScale("AnimStatus", TextScale * Vector2.One);
		mST.ModifyStringScale("CharStatus", TextScale * Vector2.One);
		mST.ModifyStringScale("PosStatus", TextScale * Vector2.One);
		mST.ModifyStringScale("HitStatus", TextScale * Vector2.One);

		UpdateCAStatus();
		UpdateStaticStatus();

		//add a sphere to cprims for hits
		mCPrims.AddSphere(HitSphereIndex, new BoundingSphere(Vector3.Zero, 1f));

		mStaticMats.SetLightDirection(mLightDir);
		mCharMats.SetLightDirection(mLightDir);
	}


	internal void Update(UpdateTimer time, List<Input.InputAction> actions, Vector3 pos)
	{
		mFrameCheck++;

		Vector3	startPos	=pos;
		Vector3	endPos		=startPos + mGD.GCam.Forward * 2000f;

		float	deltaMS		=time.GetUpdateDeltaMilliSeconds();
		float	deltaSec	=time.GetUpdateDeltaSeconds();

		//animate characters
		for(int i=0;i < mCharacters.Count;i++)
		{
			Character	c	=mCharacters[i];

			c.Update(deltaSec);

			if(mbCharAnimPause[i])
			{
				continue;
			}

			float	totTime	=mCharAnims.GetAnimTime(mAnims[mCurAnims[i]]);
			float	strTime	=mCharAnims.GetAnimStartTime(mAnims[mCurAnims[i]]);
			float	endTime	=totTime + strTime;

			mAnimTimes[i]	+=deltaSec;
			if(mAnimTimes[i] > endTime)
			{
				mAnimTimes[i]	=strTime + (mAnimTimes[i] - endTime);
			}

			c.Animate(mAnims[mCurAnims[i]], mAnimTimes[i]);
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
			else if(act.mAction.Equals(Program.MyActions.NextStatic))
			{
				mCurStatic++;
				if(mCurStatic >= mMeshes.Count)
				{
					mCurStatic	=0;
				}
				UpdateStaticStatus();
			}
			else if(act.mAction.Equals(Program.MyActions.NextAnim))
			{
				if(mCharacters.Count > 0)
				{
					mCurAnims[mCurChar]++;
					if(mCurAnims[mCurChar] >= mAnims.Count)
					{
						mCurAnims[mCurChar]	=0;
					}
					UpdateCAStatus();
				}
			}
			else if(act.mAction.Equals(Program.MyActions.PauseAnim))
			{
				if(mCharacters.Count > 0)
				{
					mbCharAnimPause[mCurChar]	=!mbCharAnimPause[mCurChar];
					UpdateCAStatus();
				}
			}
			else if(act.mAction.Equals(Program.MyActions.CharacterYawInc))
			{
				mCharYaws[mCurChar]	+=deltaSec;
			}
			else if(act.mAction.Equals(Program.MyActions.CharacterYawDec))
			{
				mCharYaws[mCurChar]	-=deltaSec;
			}
			else if(act.mAction.Equals(Program.MyActions.RandLightDirection))
			{
				mLightDir	=Mathery.RandomDirection(mRand);

				mStaticMats.SetLightDirection(mLightDir);
				mCharMats.SetLightDirection(mLightDir);
			}
			else if(act.mAction.Equals(Program.MyActions.RandRotateStatic))
			{
				if(mMeshes.Count > 0)
				{
					//make a random rotation
					mMeshRotations[mCurStatic]	=new Vector3(
						Mathery.RandomFloatNext(mRand, 0, MathHelper.TwoPi),
						Mathery.RandomFloatNext(mRand, 0, MathHelper.TwoPi),
						Mathery.RandomFloatNext(mRand, 0, MathHelper.TwoPi));
					UpdateStaticTransform(mCurStatic);
				}
			}
			else if(act.mAction.Equals(Program.MyActions.RandScaleStatic))
			{
				if(mMeshes.Count > 0)
				{
					//make a random scale
					mMeshScales[mCurStatic]	=new Vector3(
						Mathery.RandomFloatNext(mRand, 0.25f, 5f),
						Mathery.RandomFloatNext(mRand, 0.25f, 5f),
						Mathery.RandomFloatNext(mRand, 0.25f, 5f));
					UpdateStaticTransform(mCurStatic);
				}
			}
			else if(act.mAction.Equals(Program.MyActions.FreezeRay))
			{
				mbFreezeRay	=!mbFreezeRay;
			}
		}

		if(!mbFreezeRay)
		{
			mPartHit	=null;
			mMeshHit	=null;

			float		bestDist	=float.MaxValue;

		//this section attempts ray collide with statics
/*		for(int i=0;i < mMeshes.Count;i++)
		{
			StaticMesh	sm	=mMeshes[i];

			float	?bHit	=sm.RayIntersect(startPos, endPos, true);
			if(bHit != null)
			{
				Mesh	partHit	=null;

				bHit	=sm.RayIntersect(startPos, endPos, true, out partHit);
				if(bHit != null)
				{
					if(bHit.Value < bestDist)
					{
						bestDist	=bHit.Value;
						mPartHit	=partHit;
						mMeshHit	=sm;
					}
				}
			}
		}*/
		

			//this attempts ray hit to characters
			mBoneHit	=-1;
			mHitPos		=mHitNorm	=Vector3.Zero;
			for(int i=0;i < mCharacters.Count;i++)
			{
				Character	c	=mCharacters[i];

				if(c.RayIntersectBones(startPos, endPos, 0f, out mBoneHit, out mHitPos, out mHitNorm))
				{
				}
			}

			UpdateHitStatus(mBoneHit, mHitPos);
		}
		mCPrims.Update(mGD.GCam, -Vector3.UnitY);
		UpdatePosStatus(pos);

		//this has to behind any text changes
		//otherwise the offsets will be messed up
		mST.Update();
		mSUI.Update(mGD.DC);
	}


	internal void Render(Vector3 eyePos)
	{
		CBKeeper	cbk	=mSKeeper.GetCBKeeper();

		//set the frame / camera stuff
		cbk.SetTransposedView(mGD.GCam.ViewTransposed, eyePos);

		//set projection to 3D
		cbk.SetTransposedProjection(mGD.GCam.ProjectionTransposed);
		cbk.UpdateFrame(mGD.DC);

		foreach(Character c in mCharacters)
		{
			Matrix4x4	t	=Matrix4x4.CreateTranslation(mCharPoss[mCurChar]);
			Matrix4x4	rot	=Matrix4x4.CreateFromYawPitchRoll(mCharYaws[mCurChar], 0f, 0f);

			c.SetTransform(rot * t);

			c.Draw(mCharMats);

			Skin		?sk		=mCharToArch[c].GetSkin();
			Skeleton	skel	=mCharAnims.GetSkeleton();

			for(int i=0;i < skel.GetNumIndexedBones();i++)
			{
				int			choice	=sk.GetBoundChoice(i);
				Matrix4x4	mat		=sk.GetBoneByIndexNoBind(i, skel);

				mat	*=c.GetTransform();

				Vector4	boneColour	=Vector4.One * 0.5f;

				if(i == mBoneHit)
				{
					boneColour	=mHitColor;
				}

				if(choice == Skin.Box)
				{
					mCPrims.DrawBox(i, mat, boneColour);
				}
				if(choice == Skin.Sphere)
				{
					mCPrims.DrawSphere(i, mat, boneColour);
				}
				if(choice == Skin.Capsule)
				{
					mCPrims.DrawCapsule(i, mat, boneColour);
				}
			}
		}

		foreach(StaticMesh sm in mMeshes)
		{
			sm.Draw(mStaticMats);
		}


		//this bit seems to draw hit bounds in red
		/*
		int	idx	=10000;
		for(int i=0;i < mMeshes.Count;i++)
		{
			StaticMesh	sm	=mMeshes[i];

			Matrix	mat	=sm.GetTransform();

			Dictionary<Mesh, BoundingBox>	bnd	=mMeshBounds[i];

			foreach(KeyValuePair<Mesh, BoundingBox> b in bnd)
			{
				if(b.Key == mPartHit && mMeshHit == sm)
				{
					mCPrims.DrawBox(dc, idx++, b.Key.GetTransform() * mat, mHitColor);
				}
				else
				{
					mCPrims.DrawBox(dc, idx++, b.Key.GetTransform() * mat, Vector4.One * 0.5f);
				}
			}
		}*/

		mCPrims.DrawAxis();

		Matrix4x4	lightArrowXForm	=Mathery.MatrixFromDirection(mLightDir);

		mCPrims.DrawLightArrow(lightArrowXForm, Vector4.One);

		if(mBoneHit != -1)
		{
			Matrix4x4	hitMat	=Matrix4x4.CreateTranslation(mHitPos);
			mCPrims.DrawSphere(HitSphereIndex, hitMat, Vector4.UnitY + Vector4.UnitW);

			Matrix4x4	normXForm	=Mathery.MatrixFromDirection(mHitNorm);
			Matrix4x4	normScale	=Matrix4x4.CreateScale(0.15f);
			mCPrims.DrawLightArrow(normXForm * normScale * hitMat, Vector4.UnitZ + Vector4.UnitW);
		}

		mSUI.Draw(Matrix4x4.Identity, mGumpProj);
		mST.Draw();
	}


	internal void FreeAll()
	{
		if(mStaticMats != null)
		{
			mStaticMats.FreeAll();
		}
		mKeeper.Clear();
		if(mCharMats != null)
		{
			mCharMats.FreeAll();
		}

		mSKeeper.FreeAll();
	}


	internal float GetScaleFactor()
	{
		Character	c	=mCharacters[mCurChar];

		IArch	ca	=mCharToArch[c];

		Skin	sk	=ca.GetSkin();

		return	sk.GetScaleFactor();
	}


	void UpdateStaticTransform(int index)
	{
		StaticMesh	sm	=mMeshes[index];

		Matrix4x4	trans	=Matrix4x4.CreateTranslation(mMeshPositions[index]);
		Matrix4x4	rot		=Matrix4x4.CreateFromYawPitchRoll(
			mMeshRotations[index].X,
			mMeshRotations[index].Y,
			mMeshRotations[index].Z);
		Matrix4x4	scl		=Matrix4x4.CreateScale(
			mMeshScales[index].X,
			mMeshScales[index].Y,
			mMeshScales[index].Z);

		sm.SetTransform(scl * rot * trans);
	}


	void AddStaticCollision()
	{
		int	statIndex	=10000;
		foreach(StaticMesh sm in mMeshes)
		{
			Dictionary<Mesh, BoundingBox>	bd	=sm.GetBoundData();

			foreach(KeyValuePair<Mesh, BoundingBox> box in bd)
			{
				mCPrims.AddBox(statIndex++, box.Value);
			}
		}
	}


	void UpdateStaticStatus()
	{
		mST.ModifyStringText("(,) CurStatic: " + mCurStatic
			+ " (Y) To Random Rotate, (U) To Randomly Scale", "StaticStatus");
	}


	void UpdateCAStatus()
	{
		if(mAnims.Count == 0 || mCharacters.Count == 0)
		{
			return;
		}
		mST.ModifyStringText("(C) CurCharacter: " + mCurChar, "CharStatus");
		mST.ModifyStringText("(N) CurAnim: " + mAnims[mCurAnims[mCurChar]]
			+ (mbCharAnimPause[mCurChar]? ":Paused" : ":Playing"), "AnimStatus");
	}


	void UpdatePosStatus(Vector3 pos)
	{
		mST.ModifyStringText("(WASD) :"
			+ (int)pos.X + ", "
			+ (int)pos.Y + ", "
			+ (int)pos.Z, "PosStatus");
	}


	void UpdateHitStatus(int hitBone, Vector3 hitPos)
	{
		if(hitBone == -1)
		{
			mST.ModifyStringText("No Collisions", "HitStatus");
			mST.ModifyStringColor("HitStatus", mTextColor);
			return;
		}

		mST.ModifyStringColor("HitStatus", mHitColor);

		string	hitString	="Hit Character in bone " +
				mCharAnims.GetSkeleton().GetBoneName(hitBone);

		mST.ModifyStringText(hitString, "HitStatus");
	}
}