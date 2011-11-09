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
		VertexBuffer				mLineVB;
		BasicEffect					mBFX;

		//movement stuff
		Vector3		mVelocity;
		BoundingBox	mCharBox;
		bool		mbOnGround;
		bool		mbFlyMode;
		Vector3		mEyeHeight;

		const float MidAirMoveScale	=0.4f;


		public BSPTest()
		{
			mGDM	=new GraphicsDeviceManager(this);

			Content.RootDirectory	="Content";

			IsFixedTimeStep	=false;
		}


		protected override void Initialize()
		{
			mGameCam	=new UtilityLib.GameCamera(
				mGDM.GraphicsDevice.Viewport.Width,
				mGDM.GraphicsDevice.Viewport.Height,
				mGDM.GraphicsDevice.Viewport.AspectRatio, 1.0f, 4000.0f);

			//70, 32 is the general character size
			mEyeHeight		=Vector3.UnitY * 65.0f;

			//bottom
			mCharBox.Min	=-Vector3.UnitX * 16;
			mCharBox.Min	+=-Vector3.UnitZ * 16;

			//top
			mCharBox.Max	=Vector3.UnitX * 16;
			mCharBox.Max	+=Vector3.UnitZ * 16;
			mCharBox.Max	+=Vector3.UnitY * 70;

			mInput			=new UtilityLib.Input();
			mPlayerControl	=new UtilityLib.PlayerSteering(mGDM.GraphicsDevice.Viewport.Width,
								mGDM.GraphicsDevice.Viewport.Height);

			mPlayerControl.Method	=UtilityLib.PlayerSteering.SteeringMethod.FirstPerson;
			mPlayerControl.Speed	=3.0f;

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

			mMatLib.ReadFromFile("Content/dm2NoTex.MatLib", false);
//			mMatLib.ReadFromFile("Content/eels.MatLib", false);

			mZone	=new Zone();
			mLevel	=new MeshLib.IndoorMesh(GraphicsDevice, mMatLib);
			
//			mZone.Read("Content/end.Zone", false);
			mZone.Read("Content/dm2.Zone", false);
//			mLevel.Read(GraphicsDevice, "Content/end.ZoneDraw", true);
			mLevel.Read(GraphicsDevice, "Content/dm2.ZoneDraw", true);
//			mZone.Read("Content/eels.Zone", false);
//			mLevel.Read(GraphicsDevice, "Content/eels.ZoneDraw", false);

			mPlayerControl.Position	=-mZone.GetPlayerStartPos();
//			mPlayerControl.Position	=-(mZone.GetPlayerStartPos() + (Vector3.Up * 66.0f));

			mMatLib.SetParameterOnAll("mLight0Color", Vector3.One);
			mMatLib.SetParameterOnAll("mLightRange", 200.0f);
			mMatLib.SetParameterOnAll("mLightFalloffRange", 100.0f);

			List<Vector3>	lines	=mLevel.GetNormals();

			mLineVB	=new VertexBuffer(mGDM.GraphicsDevice, typeof(VertexPositionColor), lines.Count, BufferUsage.WriteOnly);

			VertexPositionColor	[]normVerts	=new VertexPositionColor[lines.Count];
			for(int i=0;i < lines.Count;i++)
			{
				normVerts[i].Position	=lines[i];
				normVerts[i].Color		=Color.Green;
			}

			mLineVB.SetData<VertexPositionColor>(normVerts);
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
				Vector3	dynamicLight	=-mPlayerControl.Position;
				if(!mbFlyMode)
				{
					dynamicLight	+=mEyeHeight;
				}
				mMatLib.SetParameterOnAll("mLight0Position", dynamicLight);
				mMatLib.SetParameterOnAll("mLight0Color", Vector3.One);
				mMatLib.SetParameterOnAll("mLightRange", 200.0f);
				mMatLib.SetParameterOnAll("mLightFalloffRange", 100.0f);
			}

			Vector3	startPos	=-mPlayerControl.Position;

			if(mbFlyMode)
			{
				mPlayerControl.Method	=UtilityLib.PlayerSteering.SteeringMethod.Fly;
			}
			else
			{
				mPlayerControl.Method	=UtilityLib.PlayerSteering.SteeringMethod.FirstPerson;
			}
			
			mPlayerControl.Update(msDelta, mGameCam.View, pi.mKBS, pi.mMS, pi.mGPS);
			
			Vector3	endPos		=-mPlayerControl.Position;
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

				mPlayerControl.Position	=-endPos;

				//pop up to eye height
				camPos	=endPos + mEyeHeight;
			}
			else
			{
				camPos	=-mPlayerControl.Position;
			}

			mGameCam.Update(msDelta, -camPos, mPlayerControl.Pitch, mPlayerControl.Yaw, mPlayerControl.Roll);
			
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

			mLevel.Draw(g, mGameCam, mPlayerControl.Position, mZone.IsMaterialVisibleFromPos);

			if(mLineVB != null)
			{
				g.SetVertexBuffer(mLineVB);

				mBFX.CurrentTechnique.Passes[0].Apply();

				g.DrawPrimitives(PrimitiveType.LineList, 0, mLineVB.VertexCount / 2);
			}

			mSB.Begin();
			if(mbFlyMode)
			{
				mSB.DrawString(mKoot, "FlyMode Coords: " + -mPlayerControl.Position,
					Vector2.One * 20.0f, Color.Yellow);
			}
			else
			{
				mSB.DrawString(mKoot, "Coords: " + -mPlayerControl.Position,
					Vector2.One * 20.0f, Color.Yellow);
			}
			mSB.End();

			base.Draw(gameTime);
		}
	}
}