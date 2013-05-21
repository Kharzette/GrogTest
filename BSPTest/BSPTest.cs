using System;
using System.Linq;
using System.Diagnostics;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using SpriteMapLib;
using UtilityLib;
using BSPZone;
using MeshLib;
using PathLib;
using UILib;


namespace BSPTest
{
	public class BSPTest : Game
	{
		GraphicsDeviceManager	mGDM;
		SpriteBatch				mSB;
		ContentManager			mSLib;

		Dictionary<string, SpriteFont>		mFonts;

		//ui stuff
		MenuBuilder		mMB;
		QuickOptions	mQOptions;
		Vector2			mScreenCenter	=new Vector2(ResX / 2, ResY / 2);
		bool			mbMenuUp;

		//ui texture elements
		//these are game specific, so might need to
		//adjust what the hilite tex is when switching projects
		Dictionary<string, TextureElement>	mUITex	=new Dictionary<string, TextureElement>();

		//audio
		Audio	mAudio	=new Audio();

		//level
		Zone					mZone;
		IndoorMesh				mZoneDraw;
		MaterialLib.MaterialLib	mZoneMats;

		//pathing stuff
		PathGraph		mGraph	=PathGraph.CreatePathGrid();
		List<Vector3>	mPath	=new List<Vector3>();
		Vector3			mTestPoint1, mTestPoint2;
		bool			mbAwaitingPathing, mbPathing;

		//control
		GameCamera		mCam;
		PlayerSteering	mPSteering;
		Input			mInput;
		Mobile			mPMob, mPathTestPMob;
		int				mModelOn	=-1;	//model standing on

		//helpers
		TriggerHelper		mTHelper	=new TriggerHelper();
		BasicModelHelper	mBMHelper	=new BasicModelHelper();

		//level changing stuff
		List<string>	mLevels		=new List<string>();
		int				mCurLevel	=-1;

		//debug stuff
		VertexBuffer	mLineVB, mVisVB;
		IndexBuffer		mLineIB, mVisIB;
		BasicEffect		mBFX;
		int				mModelHit;
		Vector3			mColPos0, mColPos1, mImpacto;
		ZonePlane		mPlaneHit;
		bool			mbStartCol	=true, mbHit;
		bool			mbFreezeVis, mbClusterMode, mbDisplayHelp;
		bool			mbTexturesOn	=true;
		bool			mbVisMode, mbFlyMode, mbShowPathing, mbShowNodeFaces;
		bool			mbPushingForward;	//autorun toggle for collision testing
		Vector3			mVisPos;
		Random			mRand	=new Random();
		int				mCurCluster, mNumClustPortals;
		int				mLastNode, mNumLeafsVisible;
		int				mNumMatsVisible, mNumMaterials;
		List<Int32>		mPortNums	=new List<Int32>();
		Vector3			mClustCenter;
		BSPVis.VisMap	mVisMap;

		//constants
		const float	PlayerSpeed	=0.15f;
		const int	ResX		=1280;
		const int	ResY		=720;
		const float	CloseEnough	=8f;


		public BSPTest()
		{
			mGDM	=new GraphicsDeviceManager(this);

			Content.RootDirectory	="GameContent";

			IsFixedTimeStep	=false;

			mGDM.PreferredBackBufferWidth	=1280;
			mGDM.PreferredBackBufferHeight	=720;

			mLevels.Add("Level01");
			mLevels.Add("PathTest");
			mLevels.Add("Attract2");
		}


		protected override void Initialize()
		{
			mCam	=new GameCamera(
				mGDM.GraphicsDevice.Viewport.Width,
				mGDM.GraphicsDevice.Viewport.Height,
				mGDM.GraphicsDevice.Viewport.AspectRatio, 1.0f, 4000.0f);

			//56, 24 is the general character size
			mPMob	=new Mobile(this, 24f, 56f, 50f, true, mTHelper);

			//this one should be made 16 units shorter
			//path nodes are tested by a move down
			mPathTestPMob	=new Mobile(this, 24f, 40f, 38f, true, mTHelper);

			mInput		=new Input();
			mPSteering	=new PlayerSteering(mGDM.GraphicsDevice.Viewport.Width,
								mGDM.GraphicsDevice.Viewport.Height);

			mPSteering.Method	=PlayerSteering.SteeringMethod.FirstPerson;
			mPSteering.Speed	=PlayerSpeed;

			mTHelper.eFunc	+=OnFunc;

			base.Initialize();
		}


		protected override void LoadContent()
		{
			mSB		=new SpriteBatch(GraphicsDevice);
			mSLib	=new ContentManager(Services, "ShaderLib");

			//load all fonts and audio
			mAudio.LoadAllSounds(Content);
			mFonts	=FileUtil.LoadAllFonts(Content);

			//load up ui textures
			TextureElement.LoadTexLib(Content.RootDirectory + "\\TexLibs\\UI.TexLib", Content, mUITex);

			//basic effect, lazy built in shader stuff
			mBFX					=new BasicEffect(mGDM.GraphicsDevice);
			mBFX.VertexColorEnabled	=true;
			mBFX.LightingEnabled	=false;
			mBFX.TextureEnabled		=false;

			//game specific info, change this if switching projects
			string	menuFont	="Kawoszeh48";
			string	menuHilite	="Textures\\UI\\GoldBorder.png";

			//menu stuff
			mMB			=new MenuBuilder(mFonts, mUITex);
			mQOptions	=new QuickOptions(mGDM, mMB, mPSteering,
				mScreenCenter, menuFont, menuHilite);

			mMB.AddScreen("MainMenu", MenuBuilder.ScreenTypes.VerticalMenu,
				mScreenCenter, "Textures\\UI\\GoldBorder.png");

			mMB.AddMenuStop("MainMenu", "Controls", "Controls", menuFont);
			mMB.AddMenuStop("MainMenu", "Video", "Video", menuFont);
			mMB.AddMenuStop("MainMenu", "Exit", "Exit", menuFont);

			mMB.SetUpNav("MainMenu", "Controls");
			mMB.Link("MainMenu", "Controls", "ControlsMenu");
			mMB.Link("MainMenu", "Video", "VideoMenu");

			mMB.eMenuStopInvoke	+=OnMenuInvoke;
			mMB.eNavigating		+=OnNavigating;

			//set menu colors
			mMB.SetScreenTextColor("MainMenu", Color.Gold);
			mMB.SetScreenTextColor("ControlsMenu", Color.Gold);
			mMB.SetScreenTextColor("VideoMenu", Color.Gold);

			NextLevel();
		}


		protected override void UnloadContent()
		{
		}


		protected override void Update(GameTime gameTime)
		{
			if(!IsActive)
			{
				base.Update(gameTime);
				return;
			}

			mInput.Update();
			Input.PlayerInput	pi	=mInput.Player1;

			if(pi.WasButtonPressed(Buttons.Back)
				|| pi.WasKeyPressed(Keys.Escape))
			{
				mbMenuUp	=!mbMenuUp;
				if(mbMenuUp)
				{
					mMB.ActivateScreen("MainMenu");
				}
			}

			int	msDelta	=gameTime.ElapsedGameTime.Milliseconds;

			if(mbMenuUp)
			{
				mMB.Update(msDelta, mInput);
				base.Update(gameTime);
				return;
			}

			mBMHelper.Update(msDelta, mAudio.mListener);

			DoUpdateHotKeys(pi);

			//get player movement vector (running so flat in Y)
			Vector3	startPos	=mPSteering.Position;
			Vector3	endPos		=mPSteering.Position;

			if(mbPathing)
			{
				Vector3	startGround	=mZone.DropToGround(startPos, false);
				UpdateFollowPath(msDelta, startGround, ref endPos);
				mPSteering.Update(msDelta, mCam, pi.mKBS, pi.mMS, pi.mGPS);
			}
			else
			{
				mPSteering.Update(msDelta, mCam, pi.mKBS, pi.mMS, pi.mGPS);
				endPos		=mPSteering.Position;
			}

			//for physics testing
			if(mbPushingForward)
			{
				endPos.X	-=msDelta * PlayerSpeed * mCam.View.M13;
				endPos.Y	-=msDelta * PlayerSpeed * mCam.View.M23;
				endPos.Z	-=msDelta * PlayerSpeed * mCam.View.M33;
			}

			//flatten movement
			if(!mbFlyMode)
			{
				endPos.Y	=startPos.Y;
			}

			Vector3	finalPos, camPos;
			mPMob.Move(endPos, msDelta, false, !mbFlyMode, mbFlyMode, true, out finalPos, out camPos);

			mPSteering.Position	=finalPos;

			DebugVisDataRebuild(camPos);

			mCam.Update(camPos, mPSteering.Pitch, mPSteering.Yaw, mPSteering.Roll);
			
			mZoneDraw.Update(msDelta);
			mZoneMats.UpdateWVP(Matrix.Identity, mCam.View, mCam.Projection, -camPos);

			mBFX.World		=Matrix.Identity;
			mBFX.View		=mCam.View;
			mBFX.Projection	=mCam.Projection;

			mGraph.Update();

			base.Update(gameTime);
		}


		void UpdateFollowPath(int msDelta, Vector3 startPos, ref Vector3 endPos)
		{
			if(mPath.Count <= 0)
			{
				mbPathing	=false;
			}
			else
			{
				Vector3	target	=mPath[0] + Vector3.UnitY;

				Vector3	compareTarget	=target;

				float	yDiff	=Math.Abs(target.Y - mPath[0].Y);
				if(yDiff < Zone.StepHeight)
				{
					//fix for stairs
					compareTarget.Y	=startPos.Y;
				}

				if(Mathery.CompareVectorEpsilon(compareTarget, startPos, CloseEnough))
				{
					mPath.RemoveAt(0);
				}
				else
				{
					ComputeApproach(target, msDelta, out endPos);
				}
			}
		}


		protected override void Draw(GameTime gameTime)
		{
			GraphicsDevice	gd	=mGDM.GraphicsDevice;

			gd.Clear(Color.CornflowerBlue);

			//spritebatch turns this off
			gd.DepthStencilState	=DepthStencilState.Default;

			if(mbVisMode || mbShowNodeFaces)
			{
				if(mVisVB != null)
				{
					gd.SetVertexBuffer(mVisVB);
					gd.Indices	=mVisIB;

					mBFX.CurrentTechnique.Passes[0].Apply();

					gd.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, mVisVB.VertexCount, 0, mVisIB.IndexCount / 3);
				}
			}
			else if(mbClusterMode)
			{
				mZoneDraw.Draw(gd, mVisPos, mCam, mZone.IsMaterialVisibleFromPos, mZone.GetModelTransform, RenderExternal);
				if(mVisVB != null)
				{
					gd.DepthStencilState	=DepthStencilState.Default;
					gd.SetVertexBuffer(mVisVB);
					gd.Indices	=mVisIB;

					mBFX.CurrentTechnique.Passes[0].Apply();

					gd.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, mVisVB.VertexCount, 0, mVisIB.IndexCount / 3);
				}
			}
			else if(mbShowPathing)
			{
				mGraph.Render(gd, mBFX);
			}
			else
			{
				mZoneDraw.Draw(gd, mVisPos, mCam, mZone.IsMaterialVisibleFromPos, mZone.GetModelTransform, RenderExternal);
			}

			if(mLineVB != null)
			{
				gd.DepthStencilState	=DepthStencilState.Default;
				gd.SetVertexBuffer(mLineVB);

				//might not need indexes
				if(mLineIB != null)
				{
					gd.Indices	=mLineIB;
				}

				mBFX.CurrentTechnique.Passes[0].Apply();

				if(mLineIB != null)
				{
					gd.DrawIndexedPrimitives(PrimitiveType.LineList, 0, 0, mLineVB.VertexCount, 0, mLineIB.IndexCount / 2);
				}
				else
				{
					gd.DrawPrimitives(PrimitiveType.LineList, 0, mLineVB.VertexCount / 2);
				}
			}

			mSB.Begin();

			SpriteFont	first	=mFonts.First().Value;

			Vector3	groundPos	=mPMob.GetGroundPosition();
			groundPos			=mZone.DropToGround(groundPos, false);

			if(mbShowPathing)
			{
				int	numCon, myIndex;

				List<int>	connectedTo	=new List<int>();

				bool	bFound	=mGraph.GetInfoAboutLocation(groundPos,
					mZone.FindWorldNodeLandedIn, out numCon, out myIndex, connectedTo);

				string	cons	="";
				if(connectedTo.Count > 0)
				{
					foreach(int con in connectedTo)
					{
						cons	+="" + con +", ";
					}
					cons	=cons.Substring(0, cons.Length - 2);
				}
				mSB.DrawString(first, "Path Node " + myIndex + " Valid: " + bFound + " , Connections: " + numCon,
					Vector2.UnitX * 10 + Vector2.UnitY * (ResY - 80), Color.GreenYellow);
				mSB.DrawString(first, "Connected to: " + cons,
					Vector2.UnitX * 10 + Vector2.UnitY * (ResY - 60), Color.IndianRed);
			}
			else if(mbShowNodeFaces)
			{
				int	node	=mZone.FindWorldNodeLandedIn(groundPos);
				if(node != mLastNode)
				{
					DebugNodeDataRebuild(node);
				}
				mSB.DrawString(first, "Node: " + node,
					(Vector2.UnitY * 60.0f) + (Vector2.UnitX * 20.0f),
					Color.Green);
			}
			else if(mbClusterMode)
			{
				mSB.DrawString(first, "Cur Clust: " + mCurCluster +
					", NumClustPortals: " + mNumClustPortals,
					(Vector2.UnitY * 60.0f) + (Vector2.UnitX * 20.0f),
					Color.Green);
				mSB.DrawString(first, "Port Nums: ",
					(Vector2.UnitY * 90.0f) + (Vector2.UnitX * 20.0f),
					Color.Green);
				if(mPortNums.Count > 0)
				{
					string	portNumString	="";
					for(int i=0;i < mPortNums.Count;i++)
					{
						portNumString	+="" + mPortNums[i] + ", ";
					}
					//chop off , on the end
					portNumString	=portNumString.Substring(0, portNumString.Length - 2);
					mSB.DrawString(first, portNumString,
						(Vector2.UnitY * 100.0f) + (Vector2.UnitX * (170.0f)), Color.Red);
					mSB.DrawString(first, "ClustCenter: " + mClustCenter,
						(Vector2.UnitY * 130.0f) + (Vector2.UnitX * 20.0f),
						Color.PowderBlue);
				}
			}
			else if(mbVisMode)
			{
				mSB.DrawString(first, "NumLeafsVisible: " + mNumLeafsVisible,
					(Vector2.UnitY * 60.0f) + (Vector2.UnitX * 20.0f),
					Color.Green);
			}
			else if(mbHit)
			{
				mSB.DrawString(first, "Hit model " + mModelHit + " pos " + mImpacto + ", Plane normal: " + mPlaneHit.mNormal,
					(Vector2.UnitY * 60.0f) + (Vector2.UnitX * 20.0f),
					Color.Green);
			}
			else
			{
				mSB.DrawString(first, "NumMaterialsVisible: " + mNumMatsVisible,
					(Vector2.UnitY * 60.0f) + (Vector2.UnitX * 20.0f),
					Color.Green);
			}

			if(mbFreezeVis)
			{
				mSB.DrawString(first, "Vis Point Frozen at: " + mVisPos,
					(Vector2.UnitY * 90.0f) + (Vector2.UnitX * 520.0f),
					Color.Magenta);
			}

			if(mbDisplayHelp)
			{
				DisplayHelp();
			}
			else
			{
				if(mInput.Player1.mGPS.IsConnected
					&& mPSteering.UseGamePadIfPossible)
				{
					mSB.DrawString(first, "Press Start to display help",
						(Vector2.UnitY * 700) + (Vector2.UnitX * 20.0f), Color.Yellow);
				}
				else
				{
					mSB.DrawString(first, "Press F1 to display help " + mModelOn,
						(Vector2.UnitY * 700) + (Vector2.UnitX * 20.0f), Color.Yellow);
				}
			}

			if(mbFlyMode)
			{
				mSB.DrawString(first, "FlyMode Coords: " + mPSteering.Position,
					Vector2.One * 20.0f, Color.Yellow);
			}
			else
			{
				mSB.DrawString(first, "Coords: " + mPSteering.Position,
					Vector2.One * 20.0f, Color.Yellow);
			}
			mSB.End();

			if(mbMenuUp)
			{
				mMB.Draw(mSB);
			}

			base.Draw(gameTime);
		}


		void RenderExternal(MaterialLib.AlphaPool ap, Vector3 camPos, Matrix view, Matrix proj)
		{
			GraphicsDevice	gd	=mGDM.GraphicsDevice;

			//draw non level stuff here
			
//			mPB.Draw(ap, view, proj);
		}


		//assumes begin has been called, and in draw
		void DisplayHelp()
		{
			SpriteFont	first	=mFonts.First().Value;

			if(mInput.Player1.mGPS.IsConnected
				&& mPSteering.UseGamePadIfPossible)
			{
				mSB.DrawString(first, "List of controller buttons:",
					(Vector2.UnitX * 20.0f) + (Vector2.UnitY * 330.0f),
					Color.Yellow);
				mSB.DrawString(first, "Start : Toggle help",
					(Vector2.UnitX * 20.0f) + (Vector2.UnitY * 350.0f),
					Color.Yellow);
				mSB.DrawString(first, "Left Shoulder : Toggle flymode",
					(Vector2.UnitX * 20.0f) + (Vector2.UnitY * 370.0f),
					Color.Yellow);
				mSB.DrawString(first, "X : Freeze Vis point at current camera location",
					(Vector2.UnitX * 20.0f) + (Vector2.UnitY * 390.0f),
					Color.Yellow);
				mSB.DrawString(first, "B : Toggle cluster display mode",
					(Vector2.UnitX * 20.0f) + (Vector2.UnitY * 410.0f),
					Color.Yellow);
				mSB.DrawString(first, "DPad Up/Down : Cycle through cluster number in cluster mode",
					(Vector2.UnitX * 20.0f) + (Vector2.UnitY * 430.0f),
					Color.Yellow);
				mSB.DrawString(first, "Right Shoulder : Toggle visible geometry display mode",
					(Vector2.UnitX * 20.0f) + (Vector2.UnitY * 450.0f),
					Color.Yellow);
				mSB.DrawString(first, "A : Jump if not in fly mode",
					(Vector2.UnitX * 20.0f) + (Vector2.UnitY * 470.0f),
					Color.Yellow);
				mSB.DrawString(first, "Y : Dyn Light but not working right now",
					(Vector2.UnitX * 20.0f) + (Vector2.UnitY * 490.0f),
					Color.Yellow);
				mSB.DrawString(first, "Left Stick Button : Toggle textures on/off",
					(Vector2.UnitX * 20.0f) + (Vector2.UnitY * 510.0f),
					Color.Yellow);
				mSB.DrawString(first, "Right Stick Button : Next level",
					(Vector2.UnitX * 20.0f) + (Vector2.UnitY * 530.0f),
					Color.Yellow);
				mSB.DrawString(first, "Back : Exit",
					(Vector2.UnitX * 20.0f) + (Vector2.UnitY * 550.0f),
					Color.Yellow);
			}
			else
			{
				mSB.DrawString(first, "List of hotkeys: (Hold right mouse to turn!)",
					(Vector2.UnitX * 20.0f) + (Vector2.UnitY * 330.0f),
					Color.Yellow);
				mSB.DrawString(first, "F1 : Toggle help",
					(Vector2.UnitX * 20.0f) + (Vector2.UnitY * 350.0f),
					Color.Yellow);
				mSB.DrawString(first, "F : Toggle flymode",
					(Vector2.UnitX * 20.0f) + (Vector2.UnitY * 370.0f),
					Color.Yellow);
				mSB.DrawString(first, "R : Freeze Vis point at current camera location",
					(Vector2.UnitX * 20.0f) + (Vector2.UnitY * 390.0f),
					Color.Yellow);
				mSB.DrawString(first, "C : Toggle cluster display mode",
					(Vector2.UnitX * 20.0f) + (Vector2.UnitY * 410.0f),
					Color.Yellow);
				mSB.DrawString(first, "+/- : Cycle through cluster number in cluster mode",
					(Vector2.UnitX * 20.0f) + (Vector2.UnitY * 430.0f),
					Color.Yellow);
				mSB.DrawString(first, "T : Toggle visible geometry display mode",
					(Vector2.UnitX * 20.0f) + (Vector2.UnitY * 450.0f),
					Color.Yellow);
				mSB.DrawString(first, "Spacebar : Jump if not in fly mode",
					(Vector2.UnitX * 20.0f) + (Vector2.UnitY * 470.0f),
					Color.Yellow);
				mSB.DrawString(first, "G : Dyn Light but not working right now",
					(Vector2.UnitX * 20.0f) + (Vector2.UnitY * 490.0f),
					Color.Yellow);
				mSB.DrawString(first, "X : Toggle textures on/off",
					(Vector2.UnitX * 20.0f) + (Vector2.UnitY * 510.0f),
					Color.Yellow);
				mSB.DrawString(first, "L : Next level",
					(Vector2.UnitX * 20.0f) + (Vector2.UnitY * 530.0f),
					Color.Yellow);
				mSB.DrawString(first, "M : Autorun forward (for debugging physics)",
					(Vector2.UnitX * 20.0f) + (Vector2.UnitY * 550.0f),
					Color.Yellow);
				mSB.DrawString(first, "P : Show Pathing Info",
					(Vector2.UnitX * 20.0f) + (Vector2.UnitY * 570.0f),
					Color.Yellow);
				mSB.DrawString(first, "N : Draw node faces",
					(Vector2.UnitX * 20.0f) + (Vector2.UnitY * 590.0f),
					Color.Yellow);
			}
		}


		void DoUpdateHotKeys(Input.PlayerInput pi)
		{
			if(pi.WasKeyPressed(Keys.D1))
			{
				mTestPoint1	=mPSteering.Position;
			}

			if(pi.WasKeyPressed(Keys.D2))
			{
				mTestPoint2	=mPSteering.Position;
			}

			if(pi.WasKeyPressed(Keys.H))
			{
				mbAwaitingPathing	=true;
				Vector3	p1	=mZone.DropToGround(mTestPoint1, false);
				Vector3	p2	=mZone.DropToGround(mTestPoint2, false);
				mGraph.FindPath(p1, p2, OnPathDone, mZone.FindWorldNodeLandedIn);
			}

			if(pi.WasKeyPressed(Keys.T) || pi.WasButtonPressed(Buttons.RightShoulder))
			{
				mbVisMode	=!mbVisMode;
				if(mbVisMode)
				{
					List<Vector3>	verts	=new List<Vector3>();
					List<UInt32>	inds	=new List<UInt32>();

					mNumLeafsVisible	=mZone.GetVisibleGeometry(mVisPos, verts, inds);

					BuildDebugDrawData(verts, inds);

					mbClusterMode	=false;
					mLineVB			=null;
					mLineIB			=null;
				}
			}

			//jump, no need for press & release, can hold it down
			if(pi.mKBS.IsKeyDown(Keys.Space) || pi.mGPS.IsButtonDown(Buttons.A))
			{
				mPMob.Jump();
			}

			//dynamic light, can hold
			if(pi.mGPS.IsButtonDown(Buttons.Y) ||
				pi.mKBS.IsKeyDown(Keys.G))
			{
				Vector3	dynamicLight	=mPSteering.Position;
				mZoneMats.SetParameterOnAll("mLight0Position", dynamicLight);
				mZoneMats.SetParameterOnAll("mLight0Color", Vector3.One * 50.0f);
				mZoneMats.SetParameterOnAll("mLightRange", 300.0f);
				mZoneMats.SetParameterOnAll("mLightFalloffRange", 100.0f);
			}

			if(pi.WasKeyPressed(Keys.P))
			{
				mbShowPathing	=!mbShowPathing;
				if(mbShowPathing)
				{
					mGraph.BuildDrawInfo(mGDM.GraphicsDevice);
				}
			}

			if(pi.WasKeyPressed(Keys.F1) || pi.WasButtonPressed(Buttons.Start))
			{
				mbDisplayHelp	=!mbDisplayHelp;
			}

			if(pi.WasKeyPressed(Keys.F) || pi.WasButtonPressed(Buttons.LeftShoulder))
			{
				mbFlyMode	=!mbFlyMode;
				if(mbFlyMode)
				{
					mPSteering.Method	=PlayerSteering.SteeringMethod.Fly;
				}
				else
				{
					mPSteering.Method	=PlayerSteering.SteeringMethod.FirstPerson;
				}
			}

			if(pi.WasKeyPressed(Keys.M))
			{
				mbPushingForward	=!mbPushingForward;
			}

			if(pi.WasKeyPressed(Keys.N))
			{
				mbShowNodeFaces	=!mbShowNodeFaces;
			}

			if(pi.WasKeyPressed(Keys.R) || pi.WasButtonPressed(Buttons.X))
			{
				mbFreezeVis	=!mbFreezeVis;
			}

			if(pi.WasKeyPressed(Keys.X) || pi.WasButtonPressed(Buttons.LeftStick))
			{
				ToggleTextures();
			}

			if(pi.WasKeyPressed(Keys.L) || pi.WasButtonPressed(Buttons.RightStick))
			{
				NextLevel();
			}

			if(pi.WasKeyPressed(Keys.C) || pi.WasButtonPressed(Buttons.B))
			{
				mbClusterMode	=!mbClusterMode;
				if(mbClusterMode)
				{
					mbVisMode	=false;
					MakeClusterDebugInfo();
				}
				else
				{
					mLineVB	=null;
					mLineIB	=null;
				}
			}

			if(pi.WasKeyPressed(Keys.Add) || pi.WasButtonPressed(Buttons.DPadUp))
			{
				mCurCluster++;
				MakeClusterDebugInfo();
			}

			if(pi.WasKeyPressed(Keys.Subtract) || pi.WasButtonPressed(Buttons.DPadDown))
			{
				mCurCluster--;
				MakeClusterDebugInfo();
			}
		}


		void DebugVisDataRebuild(Vector3 camPos)
		{
			if(!mbFreezeVis)
			{
				mVisPos	=-camPos;
				if(mbVisMode)
				{
					int	curNode	=mZone.FindNodeLandedIn(0, mVisPos);
					if(curNode != mLastNode)
					{
						List<Vector3>	verts	=new List<Vector3>();
						List<UInt32>	inds	=new List<UInt32>();

						mNumLeafsVisible	=mZone.GetVisibleGeometry(mVisPos, verts, inds);

						BuildDebugDrawData(verts, inds);

						mLastNode	=curNode;
					}
				}

				mNumMatsVisible	=0;
				for(int i=0;i < mNumMaterials;i++)
				{
					if(mZone.IsMaterialVisibleFromPos(mVisPos, i))
					{
						mNumMatsVisible++;
					}
				}
			}
		}


		void DebugNodeDataRebuild(int node)
		{
			List<Vector3>	verts	=new List<Vector3>();
			List<UInt32>	inds	=new List<UInt32>();

			mZone.GetNodeGeometry(node, verts, inds);

			BuildDebugDrawData(verts, inds);

			mLastNode	=node;
		}


		void MakeTraceLine()
		{
			mLineVB	=new VertexBuffer(mGDM.GraphicsDevice, typeof(VertexPositionColor), 2, BufferUsage.WriteOnly);

			VertexPositionColor	[]line	=new VertexPositionColor[2];

			Matrix	matInv	=Matrix.Invert(mZone.GetModelTransform(5));

			Vector3	backTrans0	=Vector3.Transform(mColPos0, matInv);
			Vector3	backTrans1	=Vector3.Transform(mColPos1, matInv);

			backTrans0	=new Vector3(-181.0751f, -67.999f, -74.83295f);
			backTrans1	=new Vector3(-187.999f, -67.999f, -77.015621f);

			Vector3	dirVec	=backTrans1 - backTrans0;

			dirVec.Normalize();

			dirVec	*=10.0f;

			backTrans0	-=dirVec;

			line[0].Position	=backTrans0;
			line[1].Position	=backTrans1;

			line[0].Color	=Color.Blue;
			line[1].Color	=Color.Red;

			mLineVB.SetData<VertexPositionColor>(line);
		}


		void MakeClusterDebugInfo()
		{
			List<Vector3>	verts		=new List<Vector3>();
			List<Vector3>	norms		=new List<Vector3>();
			List<UInt32>	inds		=new List<UInt32>();

			mPortNums.Clear();
#if !XBOX
			mNumClustPortals	=mVisMap.GetDebugClusterGeometry(mCurCluster,
				verts, inds, norms, mPortNums);
#endif
			if(norms.Count == 0)
			{
				return;
			}

			BuildDebugDrawData(verts, inds);

			mLineVB	=new VertexBuffer(mGDM.GraphicsDevice, typeof(VertexPositionColor), norms.Count, BufferUsage.WriteOnly);

			VertexPositionColor	[]normVerts	=new VertexPositionColor[norms.Count];
			for(int i=0;i < norms.Count;i++)
			{
				normVerts[i].Position	=norms[i];
				normVerts[i].Color		=Color.Green;
			}

			mLineVB.SetData<VertexPositionColor>(normVerts);

			mClustCenter	=mZone.GetClusterCenter(mCurCluster);

			mLineIB	=null;	//donut need indexes for this
		}


		void BuildTriggerBoxDrawData()
		{
			List<Vector3>	verts	=new List<Vector3>();
			List<Int32>		inds	=new List<Int32>();
			mZone.GetTriggerGeometry(verts, inds);

			mLineVB	=new VertexBuffer(mGDM.GraphicsDevice,
				typeof(VertexPositionColor),
				verts.Count, BufferUsage.WriteOnly);

			VertexPositionColor	[]normVerts	=new VertexPositionColor[verts.Count];
			for(int i=0;i < verts.Count;i++)
			{
				normVerts[i].Position	=verts[i];
				normVerts[i].Color		=Color.Green;
			}

			mLineVB.SetData<VertexPositionColor>(normVerts);

			mLineIB	=new IndexBuffer(mGDM.GraphicsDevice, IndexElementSize.ThirtyTwoBits, inds.Count, BufferUsage.WriteOnly);
			mLineIB.SetData<Int32>(inds.ToArray());
		}


		void ChangeLevel(string baseName)
		{
			if(mZone != null)
			{
				mZone.ePushObject	-=OnPushObject;
			}

			GraphicsDevice	gd	=mGDM.GraphicsDevice;

			//levels consist of a zone, which is collision and visibility and
			//entity info, and the zonedraw which is just an indoor mesh
			mZone	=new Zone();
			mZone.Read(Content.RootDirectory + "/Levels/" + mLevels[mCurLevel] + ".Zone", false);

			//material libs hold textures and shaders
			//and the parameters fed to the shaders
			//as well as vid hardware states and such
			mZoneMats	=new MaterialLib.MaterialLib(gd, Content, mSLib, false);
			mZoneMats.ReadFromFile(Content.RootDirectory + "/Levels/"
				+ mLevels[mCurLevel] + ".MatLib", false, gd);

			mZoneDraw	=new IndoorMesh(gd, mZoneMats);
			mZoneDraw.Read(gd, Content.RootDirectory + "/Levels/"
				+ mLevels[mCurLevel] + ".ZoneDraw", false,
				(mGDM.GraphicsProfile == GraphicsProfile.Reach));

			mZoneMats.InitCellShading(1);
			mZoneMats.GenerateCellTexturePreset(gd, false, 0);
			mZoneMats.SetCellTexture(0);

			float		angle;
			Vector3		startPos	=mZone.GetPlayerStartPos(out angle);

			mPSteering.Position	=startPos;
			mPMob.SetZone(mZone);
			mPathTestPMob.SetZone(mZone);
			mPMob.SetGroundPosition(startPos);

			mGraph.GenerateGraph(mZone.GetWalkableFaces, Zone.StepHeight, IsPositionOk);
			mGraph.BuildDrawInfo(gd);

			mVisMap	=new BSPVis.VisMap();
			mVisMap.LoadVisData("GameContent/Levels/" + baseName + ".VisData");
			mVisMap.LoadPortalFile("GameContent/Levels/" + baseName + ".gpf", false);

			mNumMaterials	=mZoneMats.GetMaterials().Count;

			mTHelper.Initialize(mZone, mZoneDraw.SwitchLight);

			//helper stuff
			mTHelper.Initialize(mZone, mZoneDraw.SwitchLight);
			mBMHelper.Initialize(mZone, mAudio, mAudio.mListener);

			mZone.ePushObject	+=OnPushObject;
		}


		void ToggleTextures()
		{
			mbTexturesOn	=!mbTexturesOn;
			mZoneMats.SetParameterOnAll("mbTextureEnabled", mbTexturesOn);
		}


		void OnFunc(object sender, EventArgs ea)
		{
			ZoneEntity	ze	=sender as ZoneEntity;
			if(ze == null)
			{
				return;
			}

			TriggerHelper.FuncEventArgs	fea	=ea as TriggerHelper.FuncEventArgs;
			if(fea == null)
			{
				return;
			}

			Mobile	mob	=fea.mTCEA.mContext as Mobile;
			if(mob == null)
			{
				return;
			}

			int	modIdx;
			ze.GetInt("Model", out modIdx);

			Vector3	org;
			if(ze.GetVectorNoConversion("ModelOrigin", out org))
			{
				org	=mZone.DropToGround(org, false);
			}

			mBMHelper.SetState(modIdx, fea.mbTriggerState);
		}


		void OnPushObject(object sender, EventArgs ea)
		{
			Mobile	mob	=sender as Mobile;
			if(mob != mPMob)
			{
				//not the player
				return;
			}

			Vector3EventArgs	vea	=ea as Vector3EventArgs;
			if(vea == null)
			{
				return;
			}

			Vector3	pushedTo, camTo;
			mPMob.Move(vea.mVector + mPMob.GetGroundPosition(), 1, true, false, false, true, out pushedTo, out camTo);
			mPMob.SetGroundPosition(pushedTo);

			mPSteering.Position	=pushedTo;
		}


		void OnTeleport(object sender, EventArgs ea)
		{
			Nullable<Vector3>	dest	=sender as Nullable<Vector3>;
			if(dest == null)
			{
				return;
			}

			mPSteering.Position	=dest.Value;
		}


		void OnPickUp(object sender, EventArgs ea)
		{
			string	className	=sender as string;

			System.Diagnostics.Debug.WriteLine(className);
		}


		void OnMessage(object sender, EventArgs ea)
		{
			string	className	=sender as string;

			System.Diagnostics.Debug.WriteLine(className);
		}


		void OnMenuInvoke(object sender, EventArgs ea)
		{
			string	stop	=sender as string;

			if(stop == "Exit")
			{
				Exit();
			}
		}


		void OnNavigating(object sender, EventArgs ea)
		{
			//play a sound here or something
		}


		void OnChangeMap(object sender, EventArgs ea)
		{
			string	lev	=sender as string;
			if(lev == null)
			{
				return;
			}

			int	level;
			Mathery.TryParse(lev, out level);

			mCurLevel	=level;
			if(mCurLevel >= mLevels.Count)
			{
				mCurLevel	=0;
			}

			System.Diagnostics.Debug.WriteLine("Changing level to: " + level);

			ChangeLevel(mLevels[mCurLevel]);
		}


		void OnPathDone(List<Vector3> resultPath)
		{
			mPath.Clear();

			if(!mbAwaitingPathing)
			{
				return;
			}
			mbAwaitingPathing	=false;

			foreach(Vector3 spot in resultPath)
			{
				mPath.Add(spot);
			}

			mbPathing	=(mPath.Count > 0);
		}


		void NextLevel()
		{
			mCurLevel++;

			if(mCurLevel >= mLevels.Count)
			{
				mCurLevel	=0;
			}

			ChangeLevel(mLevels[mCurLevel]);
		}


		bool ComputeApproach(Vector3 target, int msDelta, out Vector3 endPos)
		{
			Vector3	myPos		=mPSteering.Position;
			Vector3	aim			=myPos - target;

			myPos	=mZone.DropToGround(myPos, false);

			//flatten for the nearness detection
			//this can cause some hilarity if AI goals are off the ground
			//AI controlled characters can appear to suddenly become
			//Michael Jordan and leap up trying to get to the goal
			Vector3	flatAim	=aim;
			
			flatAim.Y	=0;

			float	targetLen	=flatAim.Length();
			if(targetLen <= 0f)
			{
				endPos	=target;
				return	true;
			}

			flatAim	/=targetLen;

			flatAim	*=mPSteering.Speed * msDelta;

			float	moveLen	=flatAim.Length();

			if(moveLen > targetLen)
			{
				//arrive at goal
				endPos	=target;
				return	true;
			}

			//do unflattened move
			aim.Normalize();
			aim	*=mPSteering.Speed * msDelta;

			endPos	=myPos - aim;
			return	false;
		}


		bool IsPositionOk(Vector3 pos)
		{
			//set off the ground
			mPathTestPMob.SetGroundPosition(pos);

			//test a sphere about midway up the box height
			//sphere should be half the hitbox width
			return	mPathTestPMob.TrySphere(mPathTestPMob.GetMiddlePosition(), 12f, false);
		}


		void BuildDebugDrawData(List<Vector3> verts, List<UInt32> inds)
		{
			if(verts.Count == 0)
			{
				mVisVB	=null;
				mVisIB	=null;
				return;
			}
			mVisVB	=new VertexBuffer(mGDM.GraphicsDevice,
				typeof(VertexPositionColor), verts.Count, BufferUsage.WriteOnly);

			VertexPositionColor	[]vpc	=new VertexPositionColor[verts.Count];

			for(int i=0;i < verts.Count;i++)
			{
				vpc[i].Position	=verts[i];
				vpc[i].Color	=Mathery.RandomColor(mRand);
			}

			for(int i=0;i < inds.Count;i+=3)
			{
				Color	randColor		=Mathery.RandomColor(mRand);
				vpc[inds[i]].Color		=randColor;
				vpc[inds[i + 1]].Color	=randColor;
				vpc[inds[i + 2]].Color	=randColor;
			}

			mVisVB.SetData<VertexPositionColor>(vpc);

			mVisIB	=new IndexBuffer(mGDM.GraphicsDevice, IndexElementSize.ThirtyTwoBits,
				inds.Count, BufferUsage.WriteOnly);

			mVisIB.SetData<UInt32>(inds.ToArray());
		}
	}
}