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
		float		mErrorAmount;

		//list of levels
		List<string>	mLevels		=new List<string>();
		int				mCurLevel	=0;

		Random	mRand	=new Random();
		bool	mbBusy	=false;
		bool	mbPickingA, mbPickingB;
		bool	mbAwaitingPath;

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

		//events
		internal event EventHandler	ePickedA;
		internal event EventHandler	ePickedB;
		internal event EventHandler	ePickReady;

		//constants
		const float	ShadowSlop				=12f;
		const float	MaxGroundAdjust			=16f;
		const int	MaxPathMoveIterations	=3;


		internal MapStuff(GraphicsDevice gd, string gameRootDir)
		{
			mGD				=gd;
			mGameRootDir	=gameRootDir;
			mResX			=gd.RendForm.ClientRectangle.Width;
			mResY			=gd.RendForm.ClientRectangle.Height;

			mSKeeper	=new StuffKeeper();

			mSKeeper.eCompileNeeded	+=SharedForms.ShaderCompileHelper.CompileNeededHandler;
			mSKeeper.eCompileDone	+=SharedForms.ShaderCompileHelper.CompileDoneHandler;

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
				color, Vector2.UnitX * 20f + Vector2.UnitY * 640f, Vector2.One);
			mST.AddString(mFonts[0], "Stuffs", "PosStatus",
				color, Vector2.UnitX * 20f + Vector2.UnitY * 660f, Vector2.One);
			mST.AddString(mFonts[0], "", "PathStatus",
				color, Vector2.UnitX * 20f + Vector2.UnitY * 680f, Vector2.One);

			mZoneMats.InitCelShading(1);
			mZoneMats.GenerateCelTexturePreset(gd.GD,
				gd.GD.FeatureLevel == FeatureLevel.Level_9_3, false, 0);
			mZoneMats.SetCelTexture(0);

			mZoneDraw	=new IndoorMesh(gd, mZoneMats);

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
			mPathMob	=new Mobile(this, 16f, 50f, 45f, false, mTHelper);

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
		internal void Update(UpdateTimer time,
			List<Input.InputAction> actions, PlayerSteering ps)
		{
			//Thread.Sleep(30);

			mZone.UpdateModels(time.GetUpdateDeltaSeconds());

			Vector3	impactPos	=Vector3.Zero;

			if(mbPickingA || mbPickingB)
			{
				System.Drawing.Point	cPos	=System.Windows.Forms.Cursor.Position;

				cPos	=mGD.RendForm.PointToClient(cPos);

				Vector2	cursVec	=Vector2.UnitX * cPos.X;
				cursVec	+=Vector2.UnitY * cPos.Y;
				
				int	modelOn;
				impactPos	=mZone.TraceScreenPointRay(mGD.GCam, mGD.GetScreenViewPort(),
					cursVec, 1000f, out modelOn);

				var clicked =(from act in actions where act.mAction.Equals(Program.MyActions.MouseSelect)
							   select act).FirstOrDefault();
				if(clicked != null)
				{
					int	node, numCons;
					List<int>	conTo	=new List<int>();
					bool	bInfo	=mGraph.GetInfoAboutLocation(impactPos,
						mZone.FindWorldNodeLandedIn, out numCons, out node, conTo);

					if(bInfo && mbPickingA)
					{
						Misc.SafeInvoke(ePickedA, node, new Vector3EventArgs(impactPos));
					}
					else if(bInfo && mbPickingB)
					{
						Misc.SafeInvoke(ePickedB, node, new Vector3EventArgs(impactPos));
					}
					mbPickingA	=false;
					mbPickingB	=false;
					mST.ModifyStringText(mFonts[0], "Picked point: "
						+ impactPos.IntStr(), "PathStatus");
				}
				else
				{
					if(mbPickingA)
					{
						mST.ModifyStringText(mFonts[0], "Picking point A: "
							+ impactPos.IntStr(), "PathStatus");
					}
					else if(mbPickingB)
					{
						mST.ModifyStringText(mFonts[0], "Picking point B: "
							+ impactPos.IntStr(), "PathStatus");
					}
				}
			}

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
				else if(act.mAction.Equals(Program.MyActions.Turn))
				{
					yawAmount	=act.mMultiplier;
				}
				else if(act.mAction.Equals(Program.MyActions.Pitch))
				{
					pitchAmount	=act.mMultiplier;
				}
			}

			Vector3	startPos	=mCamMob.GetGroundPos();
			Vector3	moveVec		=ps.Update(startPos, mGD.GCam.Forward, mGD.GCam.Left, mGD.GCam.Up, actions);

			Vector3	camPos	=Vector3.Zero;
			Vector3	endPos	=mCamMob.GetGroundPos() + moveVec * 100f;

			mCamMob.Move(endPos, time.GetUpdateDeltaMilliSeconds(),
				false, mbFly, true, true, true, out endPos, out camPos);

			mGD.GCam.Update(camPos, ps.Pitch, ps.Yaw, ps.Roll);

			mST.ModifyStringText(mFonts[0], "Position: " + " : "
				+ mGD.GCam.Position.IntStr(), "PosStatus");

			if(mGraph != null)
			{
				mGraph.Update();
			}
			mST.Update(mGD.DC);
		}


		//called once before render with accumulated delta
		//do all once per render style updates in here
		internal void RenderUpdate(float msDelta)
		{
			if(msDelta <= 0f)
			{
				return;
			}

			mZoneDraw.Update(msDelta);

			mZoneMats.UpdateWVP(Matrix.Identity, mGD.GCam.View, mGD.GCam.Projection, mGD.GCam.Position);

			if(mStaticMats !=null)
			{
				mStaticMats.UpdateWVP(Matrix.Identity, mGD.GCam.View, mGD.GCam.Projection, mGD.GCam.Position);
			}

			mSHelper.Update(msDelta);

			foreach(KeyValuePair<ZoneEntity, LightHelper> shelp in mSLHelpers)
			{
				Vector3	pos;

				shelp.Key.GetOrigin(out pos);

				shelp.Value.Update(msDelta, pos, null);
			}
		}


		internal void Render()
		{
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


		internal void GeneratePathing(int gridSize, float error)
		{
			mbBusy			=true;
			mGraph			=PathGraph.CreatePathGrid();
			mErrorAmount	=error;

			mGraph.GenerateGraph(mZone.GetWalkableFaces, gridSize,
				Zone.StepHeight, MobileCanReach, MobileIsValid);

			mPathDraw.BuildDrawInfo(mGraph);

			mbBusy	=false;

			int	numNodes, numCons, avgCon;

			mGraph.GetStats(out numNodes, out numCons, out avgCon);

			mST.ModifyStringText(mFonts[0], "PathGraph Nodes: " + numNodes
				+ ", Connections: " + numCons
				+ ", Average Connections per Node: " + avgCon, "PathStatus");

			Misc.SafeInvoke(ePickReady, true);
		}


		internal void LoadPathing(string path)
		{
			mbBusy	=true;
			mGraph	=PathGraph.CreatePathGrid();

			mGraph.Load(path);

			mPathDraw.BuildDrawInfo(mGraph);

			mbBusy	=false;

			Misc.SafeInvoke(ePickReady, true);
		}


		internal void SavePathing(string path)
		{
			mGraph.Save(path);
		}


		internal void DrawSettings(int stuff)
		{
			mPathDraw.DrawSettings(stuff);
		}


		internal void AlterPathMobile(int boxWidth, int boxHeight)
		{
			mPathMob.SetBoxShape(boxWidth, boxHeight, boxHeight * 0.8f);
		}


		internal void FindPath(Vector3 start, Vector3 end)
		{
			if(mGraph != null && !mbAwaitingPath)
			{
				mbAwaitingPath	=mGraph.FindPath(start, end, OnPathNotify, mZone.FindWorldNodeLandedIn);

				mST.ModifyStringText(mFonts[0], "Path Find returned failure immediately!", "PathStatus");
			}
		}


		internal void PickA()
		{
			mbPickingA	=true;
		}


		internal void PickB()
		{
			mbPickingB	=true;
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
			//reset path and drawing stuff
			if(mGraph != null)
			{
				mGraph.Clear();
				mPathDraw.BuildDrawInfo(mGraph);
				mPathDraw.BuildPathDrawInfo(new List<Vector3>(),
					mPathMob.GetMiddlePos() - mPathMob.GetGroundPos());
				Misc.SafeInvoke(ePickReady, false);
			}

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


		bool MobileIsValid(ref Vector3 pos)
		{
			float	increment	=MaxGroundAdjust / 10f;

			//adjust upwards until good
			for(int i=0;i < 10;i++)
			{
				mPathMob.SetGroundPos(pos + (i * increment * Vector3.Up));

				if(mPathMob.CheckPosition())
				{
					continue;
				}

				mPathMob.DropToGround(false);

				pos	=mPathMob.GetGroundPos();

				return	true;
			}
			return	false;
		}


		bool MobileCanReach(Vector3 start, Vector3 end)
		{
			mPathMob.SetGroundPos(start);
			mPathMob.SetFooting();

			for(int i=0;i < MaxPathMoveIterations;i++)
			{
				Vector3	madeItTo, donutCare;
				mPathMob.Move(end, 1, true, false, true, false, false, out madeItTo, out donutCare);

				float	dist	=madeItTo.Distance(end);

				if(dist < mErrorAmount)
				{
					return	true;
				}
			}
			return	false;
		}


		bool OkToFire(TriggerHelper.FuncEventArgs fea)
		{
			string	funcClass	=fea.mFuncEnt.GetValue("classname");

			if(funcClass == "func_door")
			{
			}
			return	true;
		}


		void OnPathNotify(List<Vector3> resultPath)
		{
			if(resultPath.Count == 0)
			{
				mST.ModifyStringText(mFonts[0], "Path not found!  Probably no valid route.", "PathStatus");
			}
			else
			{
				mST.ModifyStringText(mFonts[0], "Path Found, with "
					+ resultPath.Count + " positions.", "PathStatus");
			}

			mPathDraw.BuildPathDrawInfo(resultPath, mPathMob.GetMiddlePos() - mPathMob.GetGroundPos());

			mbAwaitingPath	=false;
		}
	}
}
