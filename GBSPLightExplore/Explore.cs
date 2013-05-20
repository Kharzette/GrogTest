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
		Texture2D				mCurLightMap;

		OpenFileDialog	mOFD	=new OpenFileDialog();

		//bsp data
		Map			mMap;
		LightParams	mLP;

		IOrderedEnumerable<KeyValuePair<string, SpriteFont>>	mFonts;

		//camera
		GameCamera	mCam			=new GameCamera(ResX, ResY, 16f/9f, 1f, 3000f);
		Vector2		mScreenCenter	=new Vector2(ResX / 2, ResY / 2);

		//input
		Input			mInput		=new Input();
		PlayerSteering	mPSteering	=new PlayerSteering(ResX, ResY);

		//drawing stuff
		VertexBuffer	mLevelVB, mFaceVB;
		IndexBuffer		mLevelIB;
		int				mCurFace, mCurWidth, mCurHeight;

		//state
		bool	mbLevelVBReady, mbLighting, mbFaceVBReady;
		bool	mbLitLoaded;

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
			mPSteering.Speed	=0.5f;

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
				LoadAndLight();
			}

			if(!mbLighting && pi.WasKeyPressed(Microsoft.Xna.Framework.Input.Keys.M))
			{
				LoadLit();
			}

			if(pi.WasKeyPressed(Microsoft.Xna.Framework.Input.Keys.PageDown))
			{
				if(pi.mKBS.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.LeftShift))
				{
					mCurFace	-=10;
				}
				else
				{
					mCurFace--;
				}
				BuildFaceDrawData();
			}
			else if(pi.WasKeyPressed(Microsoft.Xna.Framework.Input.Keys.PageUp))
			{
				if(pi.mKBS.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.LeftShift))
				{
					mCurFace	+=10;
				}
				else
				{
					mCurFace++;
				}
				BuildFaceDrawData();
			}
			base.Update(gameTime);
		}


		protected override void Draw(GameTime gameTime)
		{
			GraphicsDevice	gd	=mGDM.GraphicsDevice;

			gd.Clear(Color.CornflowerBlue);

			gd.BlendState			=BlendState.Opaque;
			gd.DepthStencilState	=DepthStencilState.Default;

			if(mLevelVB != null && mbLevelVBReady)
			{
				gd.SetVertexBuffer(mLevelVB);
				gd.Indices	=mLevelIB;

				mBFX.View		=mCam.View;
				mBFX.Projection	=mCam.Projection;

				mBFX.CurrentTechnique.Passes[0].Apply();

				gd.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0,
					mLevelVB.VertexCount, 0, mLevelIB.IndexCount / 3);

				gd.SetVertexBuffer(null);
			}

			if(mFaceVB != null && mbFaceVBReady)
			{
				gd.SetVertexBuffer(mFaceVB);

				mBFX.View		=mCam.View;
				mBFX.Projection	=mCam.Projection;

				mBFX.CurrentTechnique.Passes[0].Apply();

				gd.DrawPrimitives(PrimitiveType.LineList, 0, mLevelVB.VertexCount / 2);

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

			if(mbFaceVBReady)
			{
				mSB.DrawString(mFonts.First().Value, "Face: " + mCurFace,
					Vector2.UnitY * ResY + Vector2.UnitY * -60f + Vector2.UnitX * 10f, Color.DarkRed);

				if(mCurLightMap != null && mbLitLoaded)
				{
					Rectangle	rect	=new Rectangle(
						ResX - (mCurWidth * 8),
						ResY - (mCurHeight * 8),
						mCurWidth * 8, mCurHeight * 8);
					mSB.Draw(mCurLightMap, rect, Microsoft.Xna.Framework.Color.White);
				}
			}

			mSB.End();

			base.Draw(gameTime);
		}


		Vector3 EmissiveForMaterial(string matName)
		{
			return	Vector3.One;
		}


		void LoadLit()
		{
			mMap	=new Map();

			GFXHeader	hdr	=mMap.LoadGBSPFile(mOFD.FileName);
			if(hdr == null)
			{
				return;
			}
			mbLitLoaded	=true;
		}


		void LoadAndLight()
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

			mLP						=new LightParams();
			mLP.mbSeamCorrection	=true;
			mLP.mbSurfaceLighting	=false;
			mLP.mLightGridSize		=8;
			mLP.mMaxIntensity		=255;
			mLP.mMinLight			=Vector3.Zero;
			mLP.mNumSamples			=1;
			mLP.mbRecording			=true;

			BSPBuildParams	bp	=new BSPBuildParams();
			bp.mMaxThreads		=4;

			mbLighting	=true;

			mMap.LightGBSPFile(mOFD.FileName, EmissiveForMaterial, mLP, bp);
		}


		void BuildFaceDrawData()
		{
			mbFaceVBReady	=false;

			if(!mLP.mFacePoints.ContainsKey(mCurFace))
			{
				return;
			}

			if(mLP.mFacePoints[mCurFace].Count <= 0)
			{
				return;
			}

			List<Vector3>	verts	=mLP.mFacePoints[mCurFace];
			GFXPlane		pln		=mLP.mFacePlanes[mCurFace];

			GraphicsDevice	gd	=mGDM.GraphicsDevice;

			mFaceVB	=new VertexBuffer(gd, typeof(VertexPositionColor),
				verts.Count * 2, BufferUsage.WriteOnly);

			VertexPositionColor	[]vpc	=new VertexPositionColor[verts.Count * 2];

			int	idx	=0;
			for(int i=0;i < verts.Count;i++)
			{
				vpc[idx].Position	=verts[i];
				vpc[idx].Color		=Mathery.RandomColor(mRand);

				idx++;

				vpc[idx].Position	=verts[i] + pln.mNormal * 3f;
				vpc[idx].Color		=Mathery.RandomColor(mRand);

				idx++;
			}

			mFaceVB.SetData<VertexPositionColor>(vpc);

			if(mbLitLoaded)
			{
				Color	[]lmap	=mMap.GetLightMapForFace(mCurFace, out mCurWidth, out mCurHeight);

				if(lmap != null)
				{
					mCurLightMap	=new Texture2D(gd, mCurWidth, mCurHeight);

					mCurLightMap.SetData<Color>(lmap);
				}
			}
			mbFaceVBReady	=true;
		}


		void OnLightDone(object sender, EventArgs ea)
		{
			mbLighting		=false;
		}


		void OnNumClustersChanged(object sender, EventArgs ea)
		{
			mbLevelVBReady	=false;

			GraphicsDevice	gd	=mGDM.GraphicsDevice;

			List<Vector3>	verts	=new List<Vector3>();
			List<UInt32>	inds	=new List<UInt32>();

			mMap.GetTriangles(Vector3.Zero, verts, inds, "GFX Faces");

			mLevelVB	=new VertexBuffer(gd, typeof(VertexPositionColor), verts.Count, BufferUsage.WriteOnly);
			mLevelIB	=new IndexBuffer(gd, IndexElementSize.ThirtyTwoBits, inds.Count, BufferUsage.WriteOnly);

			VertexPositionColor	[]vpc	=new VertexPositionColor[verts.Count];

			for(int i=0;i < verts.Count;i++)
			{
				vpc[i].Position	=verts[i];
				vpc[i].Color	=Mathery.RandomColor(mRand);
			}

			mLevelVB.SetData<VertexPositionColor>(vpc);
			mLevelIB.SetData<UInt32>(inds.ToArray());

			mbLevelVBReady	=true;
		}
	}
}