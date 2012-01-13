using System;
using System.Collections.Generic;
using System.Linq;
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

		//debug stuff
		VertexBuffer	mLineVB, mVisVB;
		IndexBuffer		mLineIB, mVisIB;
		BasicEffect		mBFX;
		bool			mbFreezeVis, mbClusterMode, mbDisplayHelp;
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

		const float MidAirMoveScale	=0.4f;
		const float	PlayerSpeed		=0.3f;


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
		}


		protected override void Initialize()
		{
			mGameCam	=new UtilityLib.GameCamera(
				mGDM.GraphicsDevice.Viewport.Width,
				mGDM.GraphicsDevice.Viewport.Height,
				mGDM.GraphicsDevice.Viewport.AspectRatio, 1.0f, 4000.0f);

			//56, 24 is the general character size
			mEyeHeight		=Vector3.UnitY * 50.0f;

			//bottom
			mCharBox.Min	=-Vector3.UnitX * 12;
			mCharBox.Min	+=-Vector3.UnitZ * 12;

			//top
			mCharBox.Max	=Vector3.UnitX * 12;
			mCharBox.Max	+=Vector3.UnitZ * 12;
			mCharBox.Max	+=Vector3.UnitY * 56;

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

			//material libs hold textures and shaders
			//and the parameters fed to the shaders
			//as well as vid hardware states and such
			mMatLib	=new MaterialLib.MaterialLib(GraphicsDevice,
				mGameCM, mShaderCM, false);
			mMatLib.ReadFromFile("GameContent/Levels/eels.MatLib", false);

			//levels consist of a zone, which is collision and visibility and
			//entity info, and the zonedraw which is just an indoor mesh
			mZone	=new Zone();
			mLevel	=new MeshLib.IndoorMesh(GraphicsDevice, mMatLib);			
			mZone.Read("GameContent/Levels/eels.Zone", false);
			mLevel.Read(GraphicsDevice, "GameContent/Levels/eels.ZoneDraw", true);

			mPlayerControl.Position	=mZone.GetPlayerStartPos() + Vector3.Up;

			mMatLib.SetParameterOnAll("mLight0Color", Vector3.One);
			mMatLib.SetParameterOnAll("mLightRange", 200.0f);
			mMatLib.SetParameterOnAll("mLightFalloffRange", 100.0f);

#if !XBOX
			mVisMap	=new BSPVis.VisMap();
			mVisMap.LoadVisData("GameContent/Levels/eels.VisData");
			mVisMap.LoadPortalFile("GameContent/Levels/eels.gpf", false);
#endif

			mNumMaterials	=mMatLib.GetMaterials().Count;

			mZone.eTriggerHit	+=OnTriggerHit;

			List<ZoneEntity>	switchedOn	=mZone.GetSwitchedOnLights();
			foreach(ZoneEntity ze in switchedOn)
			{
				int	switchNum;
				if(ze.GetInt("LightSwitchNum", out switchNum))
				{
					mLevel.SwitchLight(switchNum, true);
				}
			}
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

			float	msDelta	=gameTime.ElapsedGameTime.Milliseconds;

			mInput.Update(msDelta);

			UtilityLib.Input.PlayerInput	pi	=mInput.Player1;

			if(pi.WasKeyPressed(Keys.F1) || pi.WasButtonPressed(Buttons.Start))
			{
				mbDisplayHelp	=!mbDisplayHelp;
			}

			if(pi.WasKeyPressed(Keys.F) || pi.WasButtonPressed(Buttons.LeftShoulder))
			{
				mbFlyMode	=!mbFlyMode;
			}

			if(pi.WasKeyPressed(Keys.R) || pi.WasButtonPressed(Buttons.X))
			{
				mbFreezeVis	=!mbFreezeVis;
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
				mVelocity	+=Vector3.UnitY * 5.0f;
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
				mMatLib.SetParameterOnAll("mLight0Color", Vector3.One);
				mMatLib.SetParameterOnAll("mLightRange", 300.0f);
				mMatLib.SetParameterOnAll("mLightFalloffRange", 100.0f);
			}

			Vector3	startPos	=mPlayerControl.Position;

			if(mbFlyMode)
			{
				mPlayerControl.Method	=UtilityLib.PlayerSteering.SteeringMethod.Fly;
			}
			else
			{
				mPlayerControl.Method	=UtilityLib.PlayerSteering.SteeringMethod.FirstPerson;
			}
			
			mPlayerControl.Update(msDelta, mGameCam.View, pi.mKBS, pi.mMS, pi.mGPS);
			
			Vector3	endPos		=mPlayerControl.Position;
			Vector3	moveDelta	=endPos - startPos;
			Vector3	camPos		=Vector3.Zero;

			if(!mbFlyMode)
			{
				//flatten movement
				moveDelta.Y	=0;
				mVelocity	+=moveDelta;

				//if not on the ground, limit midair movement
				if(!mbOnGround)
				{
					mVelocity.X	*=MidAirMoveScale;
					mVelocity.Z	*=MidAirMoveScale;
				}

				mVelocity.Y	-=((9.8f / 1000.0f) * msDelta);	//gravity

				//get ideal final position
				endPos	=startPos + mVelocity;

				//move it through the bsp
				if(mZone.BipedMoveBox(mCharBox, startPos, endPos, ref endPos))
				{
					//on ground, zero out velocity
					mVelocity	=Vector3.Zero;
					mbOnGround	=true;
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
				mZone.BoxTriggerCheck(mCharBox, startPos, endPos);
			}
			else
			{
				camPos	=-mPlayerControl.Position;
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

			mGameCam.Update(msDelta, camPos, mPlayerControl.Pitch, mPlayerControl.Yaw, mPlayerControl.Roll);
			
			mLevel.Update(msDelta);
			mMatLib.UpdateWVP(mGameCam.World, mGameCam.View, mGameCam.Projection, -camPos);
			mBFX.World		=mGameCam.World;
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
				mLevel.Draw(g, mGameCam, mVisPos, mZone.IsMaterialVisibleFromPos);
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
				mLevel.Draw(g, mGameCam, mVisPos, mZone.IsMaterialVisibleFromPos);
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

				if(mbFreezeVis)
				{
					mSB.DrawString(mKoot20, "Vis Point Frozen at: " + mVisPos,
						(Vector2.UnitY * 90.0f) + (Vector2.UnitX * 20.0f),
						Color.Magenta);
				}
			}
			else
			{
				mSB.DrawString(mKoot20, "NumMaterialsVisible: " + mNumMatsVisible,
					(Vector2.UnitY * 60.0f) + (Vector2.UnitX * 20.0f),
					Color.Green);
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
			}
			else
			{
				mSB.DrawString(mPesc12, "List of hotkeys:",
					(Vector2.UnitX * 20.0f) + (Vector2.UnitY * 330.0f),
					Color.Yellow);
				mSB.DrawString(mPesc12, "F1 : Toggle help",
					(Vector2.UnitX * 20.0f) + (Vector2.UnitY * 350.0f),
					Color.Yellow);
				mSB.DrawString(mPesc12, "f : Toggle flymode",
					(Vector2.UnitX * 20.0f) + (Vector2.UnitY * 370.0f),
					Color.Yellow);
				mSB.DrawString(mPesc12, "r : Freeze Vis point at current camera location",
					(Vector2.UnitX * 20.0f) + (Vector2.UnitY * 390.0f),
					Color.Yellow);
				mSB.DrawString(mPesc12, "c : Toggle cluster display mode",
					(Vector2.UnitX * 20.0f) + (Vector2.UnitY * 410.0f),
					Color.Yellow);
				mSB.DrawString(mPesc12, "+/- : Cycle through cluster number in cluster mode",
					(Vector2.UnitX * 20.0f) + (Vector2.UnitY * 430.0f),
					Color.Yellow);
				mSB.DrawString(mPesc12, "t : Toggle visible geometry display mode",
					(Vector2.UnitX * 20.0f) + (Vector2.UnitY * 450.0f),
					Color.Yellow);
				mSB.DrawString(mPesc12, "spacebar : Jump if not in fly mode",
					(Vector2.UnitX * 20.0f) + (Vector2.UnitY * 470.0f),
					Color.Yellow);
				mSB.DrawString(mPesc12, "g : Place a dynamic light, or hold for a following light",
					(Vector2.UnitX * 20.0f) + (Vector2.UnitY * 490.0f),
					Color.Yellow);
			}
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


		void TriggerLight(ZoneEntity zet)
		{
			int	switchNum;
			if(!zet.GetInt("LightSwitchNum", out switchNum))
			{
				return;
			}

			//see if already on
			bool	bOn	=true;

			int	spawnFlags;
			if(zet.GetInt("spawnflags", out spawnFlags))
			{
				if(UtilityLib.Misc.bFlagSet(spawnFlags, 1))
				{
					bOn	=false;
				}
			}

			//switch!
			bOn	=!bOn;
			mLevel.SwitchLight(switchNum, bOn);
		}


		void TriggerTeleport(ZoneEntity ent)
		{
			Vector3	dest;
			if(!ent.GetOrigin(out dest))
			{
				return;
			}

			mPlayerControl.Position	=dest;
		}


		void OnTriggerHit(object sender, EventArgs ea)
		{
			ZoneEntity	ze	=sender as ZoneEntity;
			if(ze == null)
			{
				return;
			}

			string	targ	=ze.GetTarget();
			if(targ == "")
			{
				return;
			}

			List<ZoneEntity>	targs	=mZone.GetEntitiesByTargetName(targ);
			foreach(ZoneEntity zet in targs)
			{
				string	className	=zet.GetValue("classname");

				if(className.StartsWith("light") || className.StartsWith("_light"))
				{
					TriggerLight(zet);
				}
				else if(className.Contains("teleport_destination"))
				{
					TriggerTeleport(zet);
				}
			}
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