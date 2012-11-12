using System;
using System.Linq;
using System.Diagnostics;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.GamerServices;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Media;
using Microsoft.Xna.Framework.Storage;
using BSPZone;


namespace BSPTest
{
	public class BSPTest : Game
	{
		GraphicsDeviceManager	mGDM;
		SpriteBatch				mSB;
		ContentManager			mGameCM;
		ContentManager			mShaderCM;
		SpriteFont				mKoot20, mPesc12;

		Zone						mZone;
		MeshLib.IndoorMesh			mLevel;
		UtilityLib.GameCamera		mGameCam;
		UtilityLib.PlayerSteering	mPlayerControl;
		UtilityLib.Input			mInput;
		MaterialLib.MaterialLib		mMatLib;
		TriggerHelper				mTHelper	=new TriggerHelper();

		MeshLib.StaticMeshObject	mCyl;
		MaterialLib.MaterialLib		mCylLib;

		List<string>	mLevels		=new List<string>();
		int				mCurLevel	=-1;
		float			mWarpFactor;

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
		bool			mbPushingForward;	//autorun toggle for collision testing
		Vector3			mVisPos;
		Random			mRand	=new Random();
		int				mCurCluster, mNumClustPortals;
		int				mLastNode, mNumLeafsVisible;
		int				mNumMatsVisible, mNumMaterials;
		List<Int32>		mPortNums	=new List<Int32>();
		Vector3			mClustCenter;
#if !XBOX
		BSPVis.VisMap	mVisMap;
#endif

		//movement stuff
		Vector3		mVelocity;
		BoundingBox	mCharBox;
		bool		mbOnGround;
		bool		mbFlyMode;
		bool		mbVisMode;
		Vector3		mEyeHeight;

		const float MidAirMoveScale	=0.01f;
		const float	PlayerSpeed		=0.15f;
		const float	JumpVelocity	=4.0f;


		public BSPTest()
		{
			mGDM	=new GraphicsDeviceManager(this);

#if XBOX
			Components.Add(new GamerServicesComponent(this));
#endif

			Content.RootDirectory	="Content";	//don't use this

			IsFixedTimeStep	=false;

			mGDM.PreferredBackBufferWidth	=1280;
			mGDM.PreferredBackBufferHeight	=720;

			mLevels.Add("DoorTest");

			PointsFromPlaneTest();
		}


		protected override void Initialize()
		{
			mGameCam	=new UtilityLib.GameCamera(
				mGDM.GraphicsDevice.Viewport.Width,
				mGDM.GraphicsDevice.Viewport.Height,
				mGDM.GraphicsDevice.Viewport.AspectRatio, 1.0f, 4000.0f);

			//56, 24 is the general character size
			mEyeHeight		=Vector3.UnitY * 22.0f;	//actual height of 50

			mCharBox	=UtilityLib.Misc.MakeBox(24.0f, 56.0f);

			mInput			=new UtilityLib.Input();
			mPlayerControl	=new UtilityLib.PlayerSteering(mGDM.GraphicsDevice.Viewport.Width,
								mGDM.GraphicsDevice.Viewport.Height);

			mPlayerControl.Method	=UtilityLib.PlayerSteering.SteeringMethod.FirstPerson;
			mPlayerControl.Speed	=PlayerSpeed;

			base.Initialize();
		}


		protected override void LoadContent()
		{
			//spritebatch for text
			mSB	=new SpriteBatch(GraphicsDevice);

			//two content managers, one for the gamewide data, another
			//for a shader lib that I share among all games (lives in libs)
			mGameCM		=new ContentManager(Services, "GameContent");
			mShaderCM	=new ContentManager(Services, "ShaderLib");

			//fonts for printing debug stuff
			mKoot20		=mGameCM.Load<SpriteFont>("Fonts/Koot20");
			mPesc12		=mGameCM.Load<SpriteFont>("Fonts/Pescadero12");

			//basic effect, lazy built in shader stuff
			mBFX					=new BasicEffect(mGDM.GraphicsDevice);
			mBFX.VertexColorEnabled	=true;
			mBFX.LightingEnabled	=false;
			mBFX.TextureEnabled		=false;

//			mCylLib	=new MaterialLib.MaterialLib(GraphicsDevice, mGameCM, mShaderCM, false);
//			mCylLib.ReadFromFile(mGameCM.RootDirectory + "/MatLibs/TestCyl.MatLib", false, GraphicsDevice);
//			mCyl	=new MeshLib.StaticMeshObject(mCylLib);
//			mCyl.ReadFromFile(mGameCM.RootDirectory + "/Meshes/TestCyl.Static", GraphicsDevice, false);

			NextLevel();
		}


		protected override void UnloadContent()
		{
		}


		protected override void Update(GameTime gameTime)
		{
			if(GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed)
			{
				this.Exit();
			}

			int	msDelta	=gameTime.ElapsedGameTime.Milliseconds;

			mWarpFactor	+=msDelta / 1000.0f;
			while(mWarpFactor > MathHelper.TwoPi)
			{
				mWarpFactor	-=MathHelper.TwoPi;
			}
			mMatLib.SetParameterOnAll("mWarpFactor", mWarpFactor);

			mInput.Update();

			UtilityLib.Input.PlayerInput	pi	=mInput.Player1;

			mZone.UpdateTriggerPositions();
			mZone.SetPushable(mCharBox, mPlayerControl.Position);


			if(pi.WasKeyPressed(Keys.F1) || pi.WasButtonPressed(Buttons.Start))
			{
				mbDisplayHelp	=!mbDisplayHelp;
			}

			if(pi.WasKeyPressed(Keys.F) || pi.WasButtonPressed(Buttons.LeftShoulder))
			{
				mbFlyMode	=!mbFlyMode;
			}

			if(pi.WasKeyPressed(Keys.M))
			{
				mbPushingForward	=!mbPushingForward;
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

			mZone.RotateModelX(2, msDelta * 0.05f);


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

			if(pi.WasKeyPressed(Keys.N))
			{
				Matrix	testMat	=mZone.GetModelTransform(1);

				Matrix	rot	=Matrix.CreateRotationY(MathHelper.PiOver4 / 4.0f);

				testMat	=rot * testMat;

//				mZone.RotateModelX(5, 5f);
				mZone.RotateModelY(5, 10f);
//				mZone.RotateModelZ(5, 20f);

//				mZone.SetModelTransform(1, testMat);
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
			if((pi.mKBS.IsKeyDown(Keys.Space)
				|| pi.mGPS.IsButtonDown(Buttons.A)) && mbOnGround)
			{
				mVelocity	+=Vector3.UnitY * JumpVelocity;
			}

			//dynamic light, can hold
			if(pi.mGPS.IsButtonDown(Buttons.Y) ||
				pi.mKBS.IsKeyDown(Keys.G))
			{
				Vector3	dynamicLight	=mPlayerControl.Position;
				if(!mbFlyMode)
				{
					dynamicLight	+=mEyeHeight;
				}
				mMatLib.SetParameterOnAll("mLight0Position", dynamicLight);
				mMatLib.SetParameterOnAll("mLight0Color", Vector3.One * 50.0f);
				mMatLib.SetParameterOnAll("mLightRange", 300.0f);
				mMatLib.SetParameterOnAll("mLightFalloffRange", 100.0f);
			}

			Vector3	startPos	=mPlayerControl.Position;

			mPlayerControl.Update(msDelta, mGameCam, pi.mKBS, pi.mMS, pi.mGPS);

			Vector3	endPos		=mPlayerControl.Position;

			if(mbPushingForward)
			{
				endPos.X	-=msDelta * PlayerSpeed * mGameCam.View.M13;
				endPos.Y	-=msDelta * PlayerSpeed * mGameCam.View.M23;
				endPos.Z	-=msDelta * PlayerSpeed * mGameCam.View.M33;
			}

			Vector3	camPos	=MovePlayer(startPos, endPos, msDelta);			

			if(pi.WasKeyPressed(Keys.P))
			{
				if(mbStartCol)
				{
					mColPos0	=mPlayerControl.Position;
				}
				else
				{
					mColPos1	=mPlayerControl.Position;

					//level out the Y
					mColPos1.Y	=mColPos0.Y;

					MakeTraceLine();

					Vector3	backTrans0	=new Vector3(-181.0751f, -67.999f, -74.83295f);
					Vector3	backTrans1	=new Vector3(-187.999f, -67.999f, -77.015621f);

					Vector3	dirVec	=backTrans1 - backTrans0;

					dirVec.Normalize();

					dirVec	*=10.0f;

					backTrans0	-=dirVec;


//					mbHit	=mZone.Trace_All(mCharBox,
//						backTrans0, backTrans1,
//						ref mModelHit, ref mImpacto, ref mPlaneHit);
					bool	bStairs	=false;
					mbHit	=mZone.BipedMoveBox(mCharBox,
						backTrans0, backTrans1, true,
						ref mImpacto, ref bStairs);
				}
				mbStartCol	=!mbStartCol;
			}

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

			mGameCam.Update(camPos, mPlayerControl.Pitch, mPlayerControl.Yaw, mPlayerControl.Roll);
			
			mLevel.Update(msDelta);
			mMatLib.UpdateWVP(Matrix.Identity, mGameCam.View, mGameCam.Projection, -camPos);
//			mCylLib.UpdateWVP(Matrix.Identity, mGameCam.View, mGameCam.Projection, -camPos);
			mBFX.World		=Matrix.Identity;
			mBFX.View		=mGameCam.View;
			mBFX.Projection	=mGameCam.Projection;

			base.Update(gameTime);
		}


		protected override void Draw(GameTime gameTime)
		{
			GraphicsDevice	g	=mGDM.GraphicsDevice;

			g.Clear(Color.CornflowerBlue);

			//spritebatch turns this off
			g.DepthStencilState	=DepthStencilState.Default;

			if(mbVisMode)
			{
				if(mVisVB != null)
				{
					g.SetVertexBuffer(mVisVB);
					g.Indices	=mVisIB;

					mBFX.CurrentTechnique.Passes[0].Apply();

					g.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, mVisVB.VertexCount, 0, mVisIB.IndexCount / 3);
				}
			}
			else if(mbClusterMode)
			{
				mLevel.Draw(g, mGameCam, mVisPos, mZone.IsMaterialVisibleFromPos, mZone.GetModelTransform);
				if(mVisVB != null)
				{
					g.DepthStencilState	=DepthStencilState.Default;
					g.SetVertexBuffer(mVisVB);
					g.Indices	=mVisIB;

					mBFX.CurrentTechnique.Passes[0].Apply();

					g.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, mVisVB.VertexCount, 0, mVisIB.IndexCount / 3);
				}
			}
			else
			{
				mLevel.Draw(g, mGameCam, mVisPos, mZone.IsMaterialVisibleFromPos, mZone.GetModelTransform);
//				mCyl.Draw(g);
			}

			if(mLineVB != null)
			{
				g.DepthStencilState	=DepthStencilState.Default;
				g.SetVertexBuffer(mLineVB);

				//might not need indexes
				if(mLineIB != null)
				{
					g.Indices	=mLineIB;
				}

				mBFX.CurrentTechnique.Passes[0].Apply();

				if(mLineIB != null)
				{
					g.DrawIndexedPrimitives(PrimitiveType.LineList, 0, 0, mLineVB.VertexCount, 0, mLineIB.IndexCount / 2);
				}
				else
				{
					g.DrawPrimitives(PrimitiveType.LineList, 0, mLineVB.VertexCount / 2);
				}
			}

			mSB.Begin();

			if(mbClusterMode)
			{
				mSB.DrawString(mKoot20, "Cur Clust: " + mCurCluster +
					", NumClustPortals: " + mNumClustPortals,
					(Vector2.UnitY * 60.0f) + (Vector2.UnitX * 20.0f),
					Color.Green);
				mSB.DrawString(mKoot20, "Port Nums: ",
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
					mSB.DrawString(mPesc12, portNumString,
						(Vector2.UnitY * 100.0f) + (Vector2.UnitX * (170.0f)), Color.Red);
					mSB.DrawString(mKoot20, "ClustCenter: " + mClustCenter,
						(Vector2.UnitY * 130.0f) + (Vector2.UnitX * 20.0f),
						Color.PowderBlue);
				}
			}
			else if(mbVisMode)
			{
				mSB.DrawString(mKoot20, "NumLeafsVisible: " + mNumLeafsVisible,
					(Vector2.UnitY * 60.0f) + (Vector2.UnitX * 20.0f),
					Color.Green);
			}
			else if(mbHit)
			{
				mSB.DrawString(mKoot20, "Hit model " + mModelHit + " pos " + mImpacto + ", Plane normal: " + mPlaneHit.mNormal,
					(Vector2.UnitY * 60.0f) + (Vector2.UnitX * 20.0f),
					Color.Green);
			}
			else
			{
				mSB.DrawString(mKoot20, "NumMaterialsVisible: " + mNumMatsVisible,
					(Vector2.UnitY * 60.0f) + (Vector2.UnitX * 20.0f),
					Color.Green);
			}

			if(mbFreezeVis)
			{
				mSB.DrawString(mKoot20, "Vis Point Frozen at: " + mVisPos,
					(Vector2.UnitY * 90.0f) + (Vector2.UnitX * 520.0f),
					Color.Magenta);
			}

			if(mbDisplayHelp)
			{
				DisplayHelp();
			}
			else
			{
				if(mInput.Player1.mGPS.IsConnected)
				{
					mSB.DrawString(mPesc12, "Press Start to display help",
						(Vector2.UnitY * 700) + (Vector2.UnitX * 20.0f), Color.Yellow);
				}
				else
				{
					mSB.DrawString(mPesc12, "Press F1 to display help",
						(Vector2.UnitY * 700) + (Vector2.UnitX * 20.0f), Color.Yellow);
				}
			}

			if(mbFlyMode)
			{
				mSB.DrawString(mKoot20, "FlyMode Coords: " + mPlayerControl.Position,
					Vector2.One * 20.0f, Color.Yellow);
			}
			else
			{
				mSB.DrawString(mKoot20, "Coords: " + mPlayerControl.Position,
					Vector2.One * 20.0f, Color.Yellow);
			}
			mSB.End();

			base.Draw(gameTime);
		}


		//assumes begin has been called, and in draw
		void DisplayHelp()
		{
			if(mInput.Player1.mGPS.IsConnected)
			{
				mSB.DrawString(mPesc12, "List of controller buttons:",
					(Vector2.UnitX * 20.0f) + (Vector2.UnitY * 330.0f),
					Color.Yellow);
				mSB.DrawString(mPesc12, "Start : Toggle help",
					(Vector2.UnitX * 20.0f) + (Vector2.UnitY * 350.0f),
					Color.Yellow);
				mSB.DrawString(mPesc12, "Left Shoulder : Toggle flymode",
					(Vector2.UnitX * 20.0f) + (Vector2.UnitY * 370.0f),
					Color.Yellow);
				mSB.DrawString(mPesc12, "X : Freeze Vis point at current camera location",
					(Vector2.UnitX * 20.0f) + (Vector2.UnitY * 390.0f),
					Color.Yellow);
				mSB.DrawString(mPesc12, "B : Toggle cluster display mode",
					(Vector2.UnitX * 20.0f) + (Vector2.UnitY * 410.0f),
					Color.Yellow);
				mSB.DrawString(mPesc12, "DPad Up/Down : Cycle through cluster number in cluster mode",
					(Vector2.UnitX * 20.0f) + (Vector2.UnitY * 430.0f),
					Color.Yellow);
				mSB.DrawString(mPesc12, "Right Shoulder : Toggle visible geometry display mode",
					(Vector2.UnitX * 20.0f) + (Vector2.UnitY * 450.0f),
					Color.Yellow);
				mSB.DrawString(mPesc12, "A : Jump if not in fly mode",
					(Vector2.UnitX * 20.0f) + (Vector2.UnitY * 470.0f),
					Color.Yellow);
				mSB.DrawString(mPesc12, "Y : Place a dynamic light, or hold for a following light",
					(Vector2.UnitX * 20.0f) + (Vector2.UnitY * 490.0f),
					Color.Yellow);
				mSB.DrawString(mPesc12, "Left Stick Button : Toggle textures on/off",
					(Vector2.UnitX * 20.0f) + (Vector2.UnitY * 510.0f),
					Color.Yellow);
				mSB.DrawString(mPesc12, "Right Stick Button : Next level",
					(Vector2.UnitX * 20.0f) + (Vector2.UnitY * 530.0f),
					Color.Yellow);
				mSB.DrawString(mPesc12, "Back : Exit",
					(Vector2.UnitX * 20.0f) + (Vector2.UnitY * 550.0f),
					Color.Yellow);
			}
			else
			{
				mSB.DrawString(mPesc12, "List of hotkeys: (Hold right mouse to turn!)",
					(Vector2.UnitX * 20.0f) + (Vector2.UnitY * 330.0f),
					Color.Yellow);
				mSB.DrawString(mPesc12, "F1 : Toggle help",
					(Vector2.UnitX * 20.0f) + (Vector2.UnitY * 350.0f),
					Color.Yellow);
				mSB.DrawString(mPesc12, "F : Toggle flymode",
					(Vector2.UnitX * 20.0f) + (Vector2.UnitY * 370.0f),
					Color.Yellow);
				mSB.DrawString(mPesc12, "R : Freeze Vis point at current camera location",
					(Vector2.UnitX * 20.0f) + (Vector2.UnitY * 390.0f),
					Color.Yellow);
				mSB.DrawString(mPesc12, "C : Toggle cluster display mode",
					(Vector2.UnitX * 20.0f) + (Vector2.UnitY * 410.0f),
					Color.Yellow);
				mSB.DrawString(mPesc12, "+/- : Cycle through cluster number in cluster mode",
					(Vector2.UnitX * 20.0f) + (Vector2.UnitY * 430.0f),
					Color.Yellow);
				mSB.DrawString(mPesc12, "T : Toggle visible geometry display mode",
					(Vector2.UnitX * 20.0f) + (Vector2.UnitY * 450.0f),
					Color.Yellow);
				mSB.DrawString(mPesc12, "Spacebar : Jump if not in fly mode",
					(Vector2.UnitX * 20.0f) + (Vector2.UnitY * 470.0f),
					Color.Yellow);
				mSB.DrawString(mPesc12, "G : Place a dynamic light, or hold for a following light",
					(Vector2.UnitX * 20.0f) + (Vector2.UnitY * 490.0f),
					Color.Yellow);
				mSB.DrawString(mPesc12, "X : Toggle textures on/off",
					(Vector2.UnitX * 20.0f) + (Vector2.UnitY * 510.0f),
					Color.Yellow);
				mSB.DrawString(mPesc12, "L : Next level",
					(Vector2.UnitX * 20.0f) + (Vector2.UnitY * 530.0f),
					Color.Yellow);
				mSB.DrawString(mPesc12, "M : Autorun forward (for debugging physics)",
					(Vector2.UnitX * 20.0f) + (Vector2.UnitY * 550.0f),
					Color.Yellow);
			}
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
				mTHelper.eChangeMap	-=OnChangeMap;
				mTHelper.ePickUp	-=OnPickUp;
				mTHelper.eTeleport	-=OnTeleport;
				mTHelper.eMessage	-=OnMessage;
				mTHelper.Clear();

				mZone.ePushObject	-=OnPushObject;
			}

			//material libs hold textures and shaders
			//and the parameters fed to the shaders
			//as well as vid hardware states and such
			mMatLib	=new MaterialLib.MaterialLib(GraphicsDevice,
				mGameCM, mShaderCM, false);

			//levels consist of a zone, which is collision and visibility and
			//entity info, and the zonedraw which is just an indoor mesh
			mZone	=new Zone();
			mLevel	=new MeshLib.IndoorMesh(GraphicsDevice, mMatLib);

			mTHelper.eChangeMap	+=OnChangeMap;
			mTHelper.ePickUp	+=OnPickUp;
			mTHelper.eTeleport	+=OnTeleport;
			mTHelper.eMessage	+=OnMessage;
			mZone.ePushObject	+=OnPushObject;

			mMatLib.ReadFromFile("GameContent/ZoneMaps/" + baseName + ".MatLib", false, mGDM.GraphicsDevice);
			mZone.Read("GameContent/ZoneMaps/" + baseName + ".Zone", false);
			mLevel.Read(GraphicsDevice, "GameContent/ZoneMaps/" + baseName + ".ZoneDraw", true);

			mPlayerControl.Position	=mZone.GetPlayerStartPos() + Vector3.Up * 28.1f;

			mMatLib.SetParameterOnAll("mLight0Color", Vector3.One);
			mMatLib.SetParameterOnAll("mLightRange", 200.0f);
			mMatLib.SetParameterOnAll("mLightFalloffRange", 100.0f);

#if !XBOX
			mVisMap	=new BSPVis.VisMap();
			mVisMap.LoadVisData("GameContent/ZoneMaps/" + baseName + ".VisData");
			mVisMap.LoadPortalFile("GameContent/ZoneMaps/" + baseName + ".gpf", false);
#endif

			mNumMaterials	=mMatLib.GetMaterials().Count;

			mTHelper.Initialize(mZone, mLevel.SwitchLight);
		}


		void ToggleTextures()
		{
			mbTexturesOn	=!mbTexturesOn;
			mMatLib.SetParameterOnAll("mbTextureEnabled", mbTexturesOn);
		}


		Vector3 MovePlayer(Vector3 startPos, Vector3 endPos, int msDelta)
		{
			if(mbFlyMode)
			{
				mPlayerControl.Method	=UtilityLib.PlayerSteering.SteeringMethod.Fly;
			}
			else
			{
				mPlayerControl.Method	=UtilityLib.PlayerSteering.SteeringMethod.FirstPerson;
			}
			
			Vector3	moveDelta	=endPos - startPos;
			Vector3	camPos		=Vector3.Zero;

			if(!mbFlyMode)
			{
				//flatten movement
				moveDelta.Y	=0;

				//if not on the ground, limit midair movement
				if(!mbOnGround)
				{
					moveDelta.X	*=MidAirMoveScale;
					moveDelta.Z	*=MidAirMoveScale;
					mVelocity.Y	-=((9.8f / 1000.0f) * msDelta);	//gravity
				}

				//get ideal final position
				endPos	=startPos + mVelocity + moveDelta;

				//move it through the bsp
				bool	bUsedStairs	=false;
				if(mZone.BipedMoveBox(mCharBox, startPos, endPos, mbOnGround, ref endPos, ref bUsedStairs))
				{
					//on ground, friction velocity
					mVelocity	=endPos - startPos;
					mVelocity	*=0.6f;

					//clamp really small velocities
					if(mVelocity.X < 0.001f && mVelocity.X > -0.001f)
					{
						mVelocity.X	=0.0f;
					}
					if(mVelocity.Y < 0.001f && mVelocity.Y > -0.001f)
					{
						mVelocity.Y	=0.0f;
					}
					if(mVelocity.Z < 0.001f && mVelocity.Z > -0.001f)
					{
						mVelocity.Z	=0.0f;
					}

					mbOnGround	=true;
					if(bUsedStairs)
					{
						mVelocity.Y	=0.0f;
					}
				}
				else
				{
					mVelocity	=endPos - startPos;
					mbOnGround	=false;
				}

				mPlayerControl.Position	=endPos;

				//pop up to eye height
				camPos	=-(endPos + mEyeHeight);

				//do a trigger check
				mTHelper.CheckPlayer(mCharBox, startPos, endPos, msDelta);
			}
			else
			{
				camPos	=-mPlayerControl.Position;
			}

			return	camPos;
		}


		void OnPushObject(object sender, EventArgs ea)
		{
			Nullable<Vector3>	delta	=sender as Nullable<Vector3>;
			if(delta == null)
			{
				return;
			}

			MovePlayer(mPlayerControl.Position,
				mPlayerControl.Position + delta.Value, 0);
		}


		void OnTeleport(object sender, EventArgs ea)
		{
			Nullable<Vector3>	dest	=sender as Nullable<Vector3>;
			if(dest == null)
			{
				return;
			}

			mPlayerControl.Position	=dest.Value;
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


		void OnChangeMap(object sender, EventArgs ea)
		{
			string	lev	=sender as string;
			if(lev == null)
			{
				return;
			}

			int	level;
			UtilityLib.Mathery.TryParse(lev, out level);

			mCurLevel	=level;
			if(mCurLevel >= mLevels.Count)
			{
				mCurLevel	=0;
			}

			System.Diagnostics.Debug.WriteLine("Changing level to: " + level);

			ChangeLevel(mLevels[mCurLevel]);
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


		void PointsFromPlaneTest()
		{
			Vector3	norm	=Vector3.UnitX;
			float	dist	=10.0f;

			Vector3	p0, p1, p2;

			UtilityLib.Mathery.PointsFromPlane(norm, dist, out p0, out p1, out p2);

			List<Vector3>	verts	=new List<Vector3>();
			verts.Add(p0);
			verts.Add(p1);
			verts.Add(p2);

			Vector3	outNorm	=Vector3.Zero;
			float	outDist	=0.0f;

			UtilityLib.Mathery.PlaneFromVerts(verts, out outNorm, out outDist);

			Debug.Assert(norm == outNorm);
			Debug.Assert(dist == outDist);

			norm	=Vector3.UnitY;

			UtilityLib.Mathery.PointsFromPlane(norm, dist, out p0, out p1, out p2);

			verts.Clear();
			verts.Add(p0);
			verts.Add(p1);
			verts.Add(p2);

			UtilityLib.Mathery.PlaneFromVerts(verts, out outNorm, out outDist);

			Debug.Assert(norm == outNorm);
			Debug.Assert(dist == outDist);

			norm	=Vector3.UnitZ;

			UtilityLib.Mathery.PointsFromPlane(norm, dist, out p0, out p1, out p2);

			verts.Clear();
			verts.Add(p0);
			verts.Add(p1);
			verts.Add(p2);

			UtilityLib.Mathery.PlaneFromVerts(verts, out outNorm, out outDist);

			Debug.Assert(norm == outNorm);
			Debug.Assert(dist == outDist);

			norm	=Vector3.UnitX;
			dist	=-10f;

			UtilityLib.Mathery.PointsFromPlane(norm, dist, out p0, out p1, out p2);

			verts.Clear();
			verts.Add(p0);
			verts.Add(p1);
			verts.Add(p2);

			UtilityLib.Mathery.PlaneFromVerts(verts, out outNorm, out outDist);

			Debug.Assert(norm == outNorm);
			Debug.Assert(dist == outDist);

			norm	=Vector3.UnitY;

			UtilityLib.Mathery.PointsFromPlane(norm, dist, out p0, out p1, out p2);

			verts.Clear();
			verts.Add(p0);
			verts.Add(p1);
			verts.Add(p2);

			UtilityLib.Mathery.PlaneFromVerts(verts, out outNorm, out outDist);

			Debug.Assert(norm == outNorm);
			Debug.Assert(dist == outDist);

			norm	=Vector3.UnitZ;

			UtilityLib.Mathery.PointsFromPlane(norm, dist, out p0, out p1, out p2);

			verts.Clear();
			verts.Add(p0);
			verts.Add(p1);
			verts.Add(p2);

			UtilityLib.Mathery.PlaneFromVerts(verts, out outNorm, out outDist);

			Debug.Assert(norm == outNorm);
			Debug.Assert(dist == outDist);
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
				vpc[i].Color	=UtilityLib.Mathery.RandomColor(mRand);
			}

			for(int i=0;i < inds.Count;i+=3)
			{
				Color	randColor		=UtilityLib.Mathery.RandomColor(mRand);
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