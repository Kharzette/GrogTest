using System.Numerics;
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
	MatLib							mCharMats;
	AnimLib							mCharAnims;
	int								mCurChar;

	//character transforms
	List<float>		mCharYaws	=new List<float>();
	List<Vector3>	mCharPoss	=new List<Vector3>();

	//fontery
	ScreenText		mST;
	MatLib			mFontMats;
	Matrix4x4		mTextProj;
	int				mResX, mResY;
	List<string>	mFonts	=new List<string>();
	float			mLazyScale;	//scale projection matrix to make text bigger

	//2d stuff
	//ScreenUI	mSUI;
	//MatLib		mUIMats;

	//gpu
	GraphicsDevice	mGD;

	//collision debuggery
	CommonPrims	mCPrims;
	Vector4		mHitColor, mTextColor;
	int			mFrameCheck, mCurStatic;
	int[]		mCBone;
	Mesh		mPartHit;
	StaticMesh	mMeshHit;

	//constants
	const float	TextScale	=1.5f;


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

		mLazyScale	=1f / TextScale;

		mST	=new ScreenText(gd, mSKeeper, mFonts[0], mFonts[0], 1000);

		//scale this a bit to embiggen text
		mTextProj	=Matrix4x4.CreateOrthographicOffCenter(
			0, gd.RendForm.Width * mLazyScale, gd.RendForm.Height * mLazyScale, 0, 0f, 1f);

		//create some gumpey materials
//		mUIMats	=new MatLib(gd, mSKeeper);
//		mUIMats.CreateMaterial("Gump");
//		mUIMats.SetMaterialEffect("Gump", "2D.fx");
//		mUIMats.SetMaterialTechnique("Gump", "Gump");

//		mUIMats.CreateMaterial("KeyedGump");
//		mUIMats.SetMaterialEffect("KeyedGump", "2D.fx");
//		mUIMats.SetMaterialTechnique("KeyedGump", "KeyedGump");

//		mSUI	=new ScreenUI(gd.GD, mUIMats, 100);


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
				mAnimTimes	=new float[mCharacters.Count];
				mCurAnims	=new int[mCharacters.Count];
//				mCBone		=new int[mCharacters.Count];
//				mCBones		=new Dictionary<int,Matrix>[mCharacters.Count];
			}

			foreach(KeyValuePair<string, IArch> arch in mCharArchs)
			{
				//build draw data for bone bounds
				(arch.Value as CharacterArch).BuildDebugBoundDrawData(mCPrims);
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

//		mSUI.AddGump("Gump", "UI\\CrossHair", null, "CrossHair",
//			Vector4.One, Vector2.UnitX * ((mResX / 2) - 16)
//			+ Vector2.UnitY * ((mResY / 2) - 16),
//			Vector2.One);

		//string indicators for various statusy things
		mST.AddString("", "StaticStatus",
			mTextColor, Vector2.UnitX * 20f + Vector2.UnitY * 460f * mLazyScale, Vector2.One);
		mST.AddString("", "AnimStatus",
			mTextColor, Vector2.UnitX * 20f + Vector2.UnitY * 480f * mLazyScale, Vector2.One);
		mST.AddString("", "CharStatus",
			mTextColor, Vector2.UnitX * 20f + Vector2.UnitY * 500f * mLazyScale, Vector2.One);
		mST.AddString("", "PosStatus",
			mTextColor, Vector2.UnitX * 20f + Vector2.UnitY * 520f * mLazyScale, Vector2.One);
		mST.AddString("", "HitStatus",
			mTextColor, Vector2.UnitX * 20f + Vector2.UnitY * 540f * mLazyScale, Vector2.One);

		UpdateCAStatus();
		UpdateStaticStatus();
	}


	internal void Update(UpdateTimer time, List<Input.InputAction> actions, Vector3 pos)
	{
		mFrameCheck++;

		Vector3	startPos	=pos;
		Vector3	endPos		=startPos + mGD.GCam.Forward * -2000f;

		float	deltaMS		=time.GetUpdateDeltaMilliSeconds();
		float	deltaSec	=time.GetUpdateDeltaSeconds();

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

//			mCBones[i]	=(mCharToArch[c] as CharacterArch).GetBoneTransforms(mCharAnims.GetSkeleton());
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
		}
		
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
		
		mCPrims.Update(mGD.GCam, -Vector3.UnitY);
		
		//this attempts ray hit to characters
		/*
		for(int i=0;i < mCharacters.Count;i++)
		{
			Character	c	=mCharacters[i];
			float?	bHit	=c.RayIntersect(startPos, endPos);
			if(bHit != null)
			{
				c.RayIntersectBones(startPos, endPos, false, out mCBone[i]);
			}
			else
			{
				mCBone[i]	=0;
			}
		}*/

		UpdatePosStatus(pos);
		UpdateHitStatus();

		//this has to behind any text changes
		//otherwise the offsets will be messed up
		mST.Update();
//		mSUI.Update(mGD.DC);
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
		}

		foreach(StaticMesh sm in mMeshes)
		{
			sm.Draw(mStaticMats);
		}

		//this section seems to draw the bounds
		/*
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
		}*/

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

		//change projection to 2D
		cbk.SetProjection(mTextProj);
		cbk.UpdateFrame(mGD.DC);

//		mSUI.Draw(dc, Matrix.Identity, mTextProj);
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
		mST.ModifyStringText("(N) CurAnim: " + mAnims[mCurAnims[mCurChar]], "AnimStatus");
	}


	void UpdatePosStatus(Vector3 pos)
	{
		mST.ModifyStringText("(WASD) :"
			+ (int)pos.X + ", "
			+ (int)pos.Y + ", "
			+ (int)pos.Z, "PosStatus");
	}


	void UpdateHitStatus()
	{
		bool	bAnyHit	=false;
		for(int i=0;i < mCharacters.Count;i++)
		{
//			if(mCBone[i] > 0)
//			{
//				bAnyHit	=true;
//			}
		}

		if(!bAnyHit)
		{
			mST.ModifyStringText("No Collisions", "HitStatus");
			mST.ModifyStringColor("HitStatus", mTextColor);
			return;
		}

		mST.ModifyStringColor("HitStatus", mHitColor);

		string	hitString	="Hit";
		for(int i=0;i < mCharacters.Count;i++)
		{
			if(mCBone[i] <= 0)
			{
				continue;
			}

			hitString	+=" Character " + i + " in bone " +
				mCharAnims.GetSkeleton().GetBoneName(mCBone[i]);
		}

		mST.ModifyStringText(hitString, "HitStatus");
	}
}