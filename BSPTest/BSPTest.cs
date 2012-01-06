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
		ContentManager			mSharedCM;
		SpriteFont				mKoot;

		Zone						mZone;
		MeshLib.IndoorMesh			mLevel;
		UtilityLib.GameCamera		mGameCam;
		UtilityLib.PlayerSteering	mPlayerControl;
		UtilityLib.Input			mInput;
		MaterialLib.MaterialLib		mMatLib;

		//debug stuff
		VertexBuffer	mLineVB, mVisVB;
		IndexBuffer		mVisIB;
		BasicEffect		mBFX;
		bool			mbFreezeVis, mbClusterMode;
		Vector3			mVisPos;
		Random			mRand	=new Random();
		BSPVis.VisMap	mVisMap;
		int				mCurCluster, mNumClustPortals;
		int				mLastNode, mNumLeafsVisible;
		int				mNumMatsVisible, mNumMaterials;
		List<Int32>		mPortNums	=new List<Int32>();
		Vector3			mClustCenter;

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

			Content.RootDirectory	="Content";

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
			mSB			=new SpriteBatch(GraphicsDevice);
			mSharedCM	=new ContentManager(Services, "SharedContent");
			mKoot		=mSharedCM.Load<SpriteFont>("Fonts/Koot20");
			mBFX		=new BasicEffect(mGDM.GraphicsDevice);

			mBFX.VertexColorEnabled	=true;
			mBFX.LightingEnabled	=false;
			mBFX.TextureEnabled		=false;

			mMatLib	=new MaterialLib.MaterialLib(GraphicsDevice,
				Content, mSharedCM, false);

			mMatLib.ReadFromFile("Content/e2m1.MatLib", false);

			mZone	=new Zone();
			mLevel	=new MeshLib.IndoorMesh(GraphicsDevice, mMatLib);
			
			mZone.Read("Content/e2m1.Zone", false);
			mLevel.Read(GraphicsDevice, "Content/e2m1.ZoneDraw", true);

			mPlayerControl.Position	=mZone.GetPlayerStartPos() + Vector3.Up;

			mMatLib.SetParameterOnAll("mLight0Color", Vector3.One);
			mMatLib.SetParameterOnAll("mLightRange", 200.0f);
			mMatLib.SetParameterOnAll("mLightFalloffRange", 100.0f);

			//draw vert normals
			/*
			List<Vector3>	lines	=mLevel.GetNormals();
			mLineVB	=new VertexBuffer(mGDM.GraphicsDevice, typeof(VertexPositionColor), lines.Count, BufferUsage.WriteOnly);

			VertexPositionColor	[]normVerts	=new VertexPositionColor[lines.Count];
			for(int i=0;i < lines.Count;i++)
			{
				normVerts[i].Position	=lines[i];
				normVerts[i].Color		=Color.Green;
			}

			mLineVB.SetData<VertexPositionColor>(normVerts);*/

			mVisMap	=new BSPVis.VisMap();
			mVisMap.LoadVisData("Content/e2m1.VisData");
			mVisMap.LoadPortalFile("Content/e2m1.gpf", false);

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

			if(pi.mKBS.IsKeyUp(Keys.F))
			{
				if(pi.mLastKBS.IsKeyDown(Keys.F))
				{
					mbFlyMode	=!mbFlyMode;
				}
			}

			if(pi.mKBS.IsKeyUp(Keys.R))
			{
				if(pi.mLastKBS.IsKeyDown(Keys.R))
				{
					mbFreezeVis	=!mbFreezeVis;
				}
			}

			if(pi.mKBS.IsKeyUp(Keys.C))
			{
				if(pi.mLastKBS.IsKeyDown(Keys.C))
				{
					mbClusterMode	=!mbClusterMode;
					if(mbClusterMode)
					{
						MakeClusterDebugInfo();
					}
				}
			}

			if(pi.mKBS.IsKeyUp(Keys.Add))
			{
				if(pi.mLastKBS.IsKeyDown(Keys.Add))
				{
					mCurCluster++;
					MakeClusterDebugInfo();
				}
			}

			if(pi.mKBS.IsKeyUp(Keys.Subtract))
			{
				if(pi.mLastKBS.IsKeyDown(Keys.Subtract))
				{
					mCurCluster--;
					MakeClusterDebugInfo();
				}
			}

			if(pi.mKBS.IsKeyUp(Keys.T))
			{
				if(pi.mLastKBS.IsKeyDown(Keys.T))
				{
					mbVisMode	=!mbVisMode;
					if(mbVisMode)
					{
						List<Vector3>	verts	=new List<Vector3>();
						List<UInt32>	inds	=new List<UInt32>();

						mNumLeafsVisible	=mZone.GetVisibleGeometry(mVisPos, verts, inds);

						BuildDebugDrawData(verts, inds);
					}
				}
			}

			if(pi.mGPS.IsButtonUp(Buttons.LeftShoulder))
			{
				if(pi.mLastGPS.IsButtonDown(Buttons.LeftShoulder))
				{
					mbFlyMode	=!mbFlyMode;
				}
			}

			//jump
			if((pi.mKBS.IsKeyDown(Keys.Space)
				|| pi.mGPS.IsButtonDown(Buttons.Y)) && mbOnGround)
			{
				mVelocity	+=Vector3.UnitY * 5.0f;
			}

			if(pi.mGPS.IsButtonDown(Buttons.A) ||
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
					g.RasterizerState	=RasterizerState.CullNone;

					g.SetVertexBuffer(mVisVB);
					g.Indices	=mVisIB;

					mBFX.CurrentTechnique.Passes[0].Apply();

					g.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, mVisVB.VertexCount, 0, mVisIB.IndexCount / 3);

					g.RasterizerState	=RasterizerState.CullCounterClockwise;
				}
			}
			else
			{
				mLevel.Draw(g, mGameCam, mVisPos, mZone.IsMaterialVisibleFromPos);
			}

			if(mLineVB != null)
			{
//				g.DepthStencilState	=DepthStencilState.None;
				g.SetVertexBuffer(mLineVB);

				mBFX.CurrentTechnique.Passes[0].Apply();

				g.DrawPrimitives(PrimitiveType.LineList, 0, mLineVB.VertexCount / 2);
			}

			mSB.Begin();

			if(mbClusterMode)
			{
				mSB.DrawString(mKoot, "Cur Clust: " + mCurCluster +
					", NumClustPortals: " + mNumClustPortals,
					(Vector2.UnitY * 60.0f) + (Vector2.UnitX * 20.0f),
					Color.Green);
				mSB.DrawString(mKoot, "Port Nums: ",
					(Vector2.UnitY * 90.0f) + (Vector2.UnitX * 20.0f),
					Color.Green);
				for(int i=0;i < mPortNums.Count;i++)
				{
					mSB.DrawString(mKoot, "" + mPortNums[i],
						(Vector2.UnitY * 90.0f) + (Vector2.UnitX * (180.0f + (60.0f * i))),
						Color.Red);
				}
				mSB.DrawString(mKoot, "ClustCenter: " + mClustCenter,
					(Vector2.UnitY * 130.0f) + (Vector2.UnitX * 20.0f),
					Color.PowderBlue);
			}
			else if(mbVisMode)
			{
				mSB.DrawString(mKoot, "NumLeafsVisible: " + mNumLeafsVisible,
					(Vector2.UnitY * 60.0f) + (Vector2.UnitX * 20.0f),
					Color.Green);
			}
			else
			{
				mSB.DrawString(mKoot, "NumMaterialsVisible: " + mNumMatsVisible,
					(Vector2.UnitY * 60.0f) + (Vector2.UnitX * 20.0f),
					Color.Green);
			}

			if(mbFlyMode)
			{
				mSB.DrawString(mKoot, "FlyMode Coords: " + mPlayerControl.Position,
					Vector2.One * 20.0f, Color.Yellow);
			}
			else
			{
				mSB.DrawString(mKoot, "Coords: " + mPlayerControl.Position,
					Vector2.One * 20.0f, Color.Yellow);
			}
			mSB.End();

			base.Draw(gameTime);
		}


		void MakeClusterDebugInfo()
		{
			List<Vector3>	verts		=new List<Vector3>();
			List<Vector3>	norms		=new List<Vector3>();
			List<UInt32>	inds		=new List<UInt32>();

			mPortNums.Clear();

			mNumClustPortals	=mVisMap.GetDebugClusterGeometry(mCurCluster,
				verts, inds, norms, mPortNums);

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
				int	switchNum;
				if(!zet.GetInt("LightSwitchNum", out switchNum))
				{
					continue;
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