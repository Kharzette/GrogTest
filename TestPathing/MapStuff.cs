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
using InputLib;
using PathLib;

using SharpDX;
using SharpDX.DXGI;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;

using MatLib	=MaterialLib.MaterialLib;


namespace TestPathing
{
	internal class MapStuff
	{
		//data
		Zone		mZone;
		IndoorMesh	mZoneDraw;
		MatLib		mZoneMats;
		string		mGameRootDir;
		StuffKeeper	mSKeeper;

		//pathing
		PathGraph	mGraph;
		DrawPathing	mPathDraw;

		//list of levels
		List<string>	mLevels		=new List<string>();
		int				mCurLevel	=0;

		//dyn lights
		DynamicLights	mDynLights;
		List<int>		mActiveLights	=new List<int>();

		Random	mRand	=new Random();
		bool	mbBusy	=false;

		//shader compile progress indicator
		SharedForms.ThreadedProgress	mSProg;

		//helpers
		TriggerHelper		mTHelper		=new TriggerHelper();
		StaticHelper		mSHelper		=new StaticHelper();
		IntermissionHelper	mIMHelper		=new IntermissionHelper();
		IDKeeper			mKeeper			=new IDKeeper();

		//static stuff
		MatLib											mStaticMats;
		Dictionary<string, IArch>						mStatics		=new Dictionary<string, IArch>();
		Dictionary<ZoneEntity, StaticMesh>				mStaticInsts	=new Dictionary<ZoneEntity, StaticMesh>();
		Dictionary<ZoneEntity, LightHelper>				mSLHelpers		=new Dictionary<ZoneEntity, LightHelper>();
		Dictionary<ZoneEntity, ShadowHelper.Shadower>	mStaticShads	=new Dictionary<ZoneEntity, ShadowHelper.Shadower>();

		//mobiles for movement and pathing
		Mobile	mCamMob, mPathMob;
		bool	mbFly	=true;

		//gpu
		GraphicsDevice	mGD;

		//Fonts / UI
		ScreenText		mST;
		MatLib			mFontMats;
		Matrix			mTextProj;
		int				mResX, mResY;
		List<string>	mFonts	=new List<string>();

		//constants
		const float	ShadowSlop			=12f;


		internal MapStuff(GraphicsDevice gd, string gameRootDir)
		{
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
			mFontMats	=new MatLib(gd, mSKeeper);
			mPathDraw	=new DrawPathing(gd, mSKeeper);

			mFontMats.CreateMaterial("Text");
			mFontMats.SetMaterialEffect("Text", "2D.fx");
			mFontMats.SetMaterialTechnique("Text", "Text");

			mFonts	=mSKeeper.GetFontList();

			mST			=new ScreenText(gd.GD, mFontMats, mFonts[0], 1000);
			mTextProj	=Matrix.OrthoOffCenterLH(0, mResX, mResY, 0, 0.1f, 5f);

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

			mCamMob		=new Mobile(this, 16f, 50f, 45f, true, mTHelper);
			mPathMob	=new Mobile(this, 16f, 50f, 45f, true, mTHelper);

			mKeeper.AddLib(mZoneMats);

			if(mStaticMats != null)
			{
				mKeeper.AddLib(mStaticMats);
			}

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


		internal bool Busy()
		{
			return	mbBusy;
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

			foreach(Input.InputAction act in actions)
			{
				if(act.mAction.Equals(Program.MyActions.NextLevel))
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
			}

			UpdateDynamicLights((int)msDelta, actions);

			Vector3	startPos	=mCamMob.GetGroundPos();
			Vector3	moveVec		=ps.Update(startPos, mGD.GCam.Forward, mGD.GCam.Left, mGD.GCam.Up, actions);

			Vector3	camPos	=Vector3.Zero;
			Vector3	endPos	=mCamMob.GetGroundPos() + moveVec * 100f;

			mCamMob.Move(endPos, (int)msDelta, false, mbFly, true, true, out endPos, out camPos);

			mGD.GCam.Update(camPos, ps.Pitch, ps.Yaw, ps.Roll);

			mST.ModifyStringText(mFonts[0], "ModelOn: " + mCamMob.GetModelOn() + " : "
				+ (int)mGD.GCam.Position.X + ", "
				+ (int)mGD.GCam.Position.Y + ", "
				+ (int)mGD.GCam.Position.Z + " (F)lyMode: " + mbFly
				+ " X: " + yawAmount + " Y: " + pitchAmount
				+ (mCamMob.IsBadFooting()? " BadFooting!" : ""), "PosStatus");

			mST.Update(mGD.DC);
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
			if(mDynLights != null)
			{
				mDynLights.SetParameter();
			}

			mZoneDraw.Draw(mGD, 0,
				mZone.IsMaterialVisibleFromPos,
				mZone.GetModelTransform,
				RenderExternal,
				DrawShadows);

			mPathDraw.Draw();

			mST.Draw(mGD.DC, Matrix.Identity, mTextProj);
		}


		internal void FreeAll()
		{
			mZoneMats.FreeAll();
			mZoneDraw.FreeAll();
			mKeeper.Clear();
			if(mStaticMats != null)
			{
				mStaticMats.FreeAll();
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

			mSKeeper.FreeAll();
		}


		internal void GeneratePathing(int gridSize)
		{
			mbBusy	=true;
			mGraph	=PathGraph.CreatePathGrid();

			mGraph.GenerateGraph(mZone.GetWalkableFaces, gridSize, Zone.StepHeight, MobileCanReach);

			mPathDraw.BuildDrawInfo(mGraph);

			mbBusy	=false;
		}


		void RenderExternal(AlphaPool ap, GameCamera gcam)
		{
			mSHelper.Draw(DrawStatic);
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


		bool DrawShadows(int index)
		{
			return	false;	//stubbed
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

			mTHelper.Initialize(mZone, null, mZoneDraw.SwitchLight, OkToFire);
			mSHelper.Initialize(mZone);
			mIMHelper.Initialize(mZone);

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

//			mGraph.Load(lev + ".Pathing");
//			mGraph.GenerateGraph(mZone.GetWalkableFaces, 32, 18f, CanPathReach);
//			mGraph.Save(mLevels[index] + ".Pathing");
//			mGraph.BuildDrawInfo(gd);

//			mPathMobile.SetZone(mZone);

			mPathMob.SetZone(mZone);
			mCamMob.SetZone(mZone);

			float	ang;
			Vector3	gpos	=mZone.GetPlayerStartPos(out ang);
			mPathMob.SetGroundPos(gpos);
			mCamMob.SetGroundPos(gpos);

			mKeeper.Scan();
			mKeeper.AssignIDsToEffectMaterials("BSP.fx");

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
			mDynLights.Update((int)msDelta, mGD);
		}


		bool MobileCanReach(Vector3 start, Vector3 end)
		{
			mPathMob.SetGroundPos(start);
			return	mPathMob.TryMoveTo(end, 0.1f);
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
