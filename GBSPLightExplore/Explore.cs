using System;
using System.Linq;
using System.Windows.Forms;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using UtilityLib;
using BSPCore;


namespace GBSPLightExplore
{
	public class Explore : Game
	{
		GraphicsDeviceManager	mGDM;
		SpriteBatch				mSB;
		ContentManager			mSLib;	//shader library
		Random					mRand	=new Random();
		BasicEffect				mBFX;

		OpenFileDialog	mOFD	=new OpenFileDialog();

		Map	mMap;

		IOrderedEnumerable<KeyValuePair<string, SpriteFont>>	mFonts;

		//camera
		GameCamera	mCam			=new GameCamera(ResX, ResY, 16f/9f, 1f, 3000f);
		Vector2		mScreenCenter	=new Vector2(ResX / 2, ResY / 2);

		//input
		Input			mInput		=new Input();
		PlayerSteering	mPSteering	=new PlayerSteering(ResX, ResY);

		//drawing stuff
		VertexBuffer	mVB;
		IndexBuffer		mIB;

		//state
		bool	mbVBReady, mbLighting;

		//constants
		public const int	ResX	=1280;
		public const int	ResY	=720;


		public Explore()
		{
			mGDM	=new GraphicsDeviceManager(this);

			mGDM.PreferredBackBufferWidth	=ResX;
			mGDM.PreferredBackBufferHeight	=ResY;

			IsFixedTimeStep	=false;

			Content.RootDirectory	="GameContent";
		}


		protected override void Initialize()
		{
			mPSteering.Method	=PlayerSteering.SteeringMethod.Fly;

			mPSteering.UseGamePadIfPossible	=false;

			CoreEvents.eNumClustersChanged	+=OnNumClustersChanged;
			CoreEvents.eLightDone			+=OnLightDone;

			base.Initialize();
		}


		protected override void LoadContent()
		{
			mSB		=new SpriteBatch(GraphicsDevice);
			mSLib	=new ContentManager(Services, "ShaderLib");

			Dictionary<string, SpriteFont>	fonts	=UtilityLib.FileUtil.LoadAllFonts(Content);

			mFonts	=fonts.OrderBy(fnt => fnt.Value.LineSpacing);

			mBFX	=new BasicEffect(GraphicsDevice);

			mBFX.VertexColorEnabled	=true;
			mBFX.LightingEnabled	=false;
			mBFX.TextureEnabled		=false;
		}


		protected override void UnloadContent()
		{
		}


		protected override void Update(GameTime gameTime)
		{
			int	msDelta	=gameTime.ElapsedGameTime.Milliseconds;

			mInput.Update();

			Input.PlayerInput	pi	=mInput.Player1;

			mPSteering.Update(msDelta, mCam, pi.mKBS, pi.mMS, pi.mGPS);

			mCam.Update(-mPSteering.Position, mPSteering.Pitch, mPSteering.Yaw, mPSteering.Roll);

			if(pi.WasKeyPressed(Microsoft.Xna.Framework.Input.Keys.L))
			{
				LoadStuff();
			}
			base.Update(gameTime);
		}


		protected override void Draw(GameTime gameTime)
		{
			GraphicsDevice	gd	=mGDM.GraphicsDevice;

			gd.Clear(Color.CornflowerBlue);

			gd.BlendState			=BlendState.Opaque;
			gd.DepthStencilState	=DepthStencilState.Default;

			if(mVB != null && mbVBReady)
			{
				gd.SetVertexBuffer(mVB);
				gd.Indices	=mIB;

				mBFX.View		=mCam.View;
				mBFX.Projection	=mCam.Projection;

				mBFX.CurrentTechnique.Passes[0].Apply();

				gd.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0,
					mVB.VertexCount, 0, mIB.IndexCount / 3);

				gd.SetVertexBuffer(null);
			}

			mSB.Begin();
			mSB.DrawString(mFonts.First().Value, "Coords: " + mPSteering.Position,
				Vector2.UnitY * ResY + Vector2.UnitY * -20f + Vector2.UnitX * 10f, Color.Yellow);

			if(mbLighting)
			{
				mSB.DrawString(mFonts.First().Value, "Lighting level...",
					Vector2.UnitY * ResY + Vector2.UnitY * -40f + Vector2.UnitX * 10f, Color.PaleGoldenrod);
			}
			mSB.End();

			base.Draw(gameTime);
		}


		Vector3 EmissiveForMaterial(string matName)
		{
			return	Vector3.One;
		}


		void LoadStuff()
		{
			mOFD.DefaultExt		="*.gbsp";
			mOFD.Filter			="GBSP files (*.gbsp)|*.gbsp|All files (*.*)|*.*";
			mOFD.Multiselect	=false;
			DialogResult	dr	=mOFD.ShowDialog();

			if(dr == DialogResult.Cancel)
			{
				return;
			}

			mMap	=new Map();

			LightParams	lp	=new LightParams();
			lp.mbSeamCorrection	=true;
			lp.mbSurfaceLighting	=false;
			lp.mLightGridSize		=8;
			lp.mMaxIntensity		=255;
			lp.mMinLight			=Vector3.Zero;
			lp.mNumSamples			=5;

			BSPBuildParams	bp	=new BSPBuildParams();
			bp.mMaxThreads		=4;

			mbLighting	=true;

			mMap.LightGBSPFile(mOFD.FileName, EmissiveForMaterial, lp, bp);
		}


		void OnLightDone(object sender, EventArgs ea)
		{
			mbLighting	=false;
		}


		void OnNumClustersChanged(object sender, EventArgs ea)
		{
			mbVBReady	=false;

			GraphicsDevice	gd	=mGDM.GraphicsDevice;

			List<Vector3>	verts	=new List<Vector3>();
			List<UInt32>	inds	=new List<UInt32>();

			mMap.GetTriangles(Vector3.Zero, verts, inds, "GFX Faces");

			mVB	=new VertexBuffer(gd, typeof(VertexPositionColor), verts.Count, BufferUsage.WriteOnly);
			mIB	=new IndexBuffer(gd, IndexElementSize.ThirtyTwoBits, inds.Count, BufferUsage.WriteOnly);

			VertexPositionColor	[]vpc	=new VertexPositionColor[verts.Count];

			for(int i=0;i < verts.Count;i++)
			{
				vpc[i].Position	=verts[i];
				vpc[i].Color	=Mathery.RandomColor(mRand);
			}

			mVB.SetData<VertexPositionColor>(vpc);
			mIB.SetData<UInt32>(inds.ToArray());

			mbVBReady	=true;
		}
	}
}