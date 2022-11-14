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

using Color		=Vortice.Mathematics.Color;
using Screen	=MaterialLib.Screen;


namespace TestScreens
{
	internal class RunLoop
	{
		//data
		string			mGameRootDir;
		StuffKeeper		mSKeeper;

		Random	mRand	=new Random();

		Matrix4x4	mGumpProj, mWorldMat;

		//gpu
		GraphicsDevice	mGD;

		//screen (Thing being tested)
		Screen	mScreen;

		//screen contents
		byte	[]mScreenJunx;

		//user code compiler


		internal RunLoop(GraphicsDevice gd, string gameRootDir)
		{
			mGD				=gd;
			mGameRootDir	=gameRootDir;

			int	resX	=gd.RendForm.ClientRectangle.Width;
			int	resY	=gd.RendForm.ClientRectangle.Height;

			mSKeeper	=new StuffKeeper();

			mSKeeper.eCompileNeeded	+=SharedForms.ShaderCompileHelper.CompileNeededHandler;
			mSKeeper.eCompileDone	+=SharedForms.ShaderCompileHelper.CompileDoneHandler;

			mSKeeper.Init(mGD, gameRootDir);

			mScreen	=new Screen(gd, mSKeeper);

			mScreenJunx	=new byte[40 * 25];

//			mWorldMat	=Matrix4x4.CreateTranslation(Vector3.UnitZ);
			mWorldMat	=Matrix4x4.Identity;

			//full size
			mGumpProj	=Matrix4x4.CreateOrthographicOffCenter(
				0, resX, resY, 0, 0f, 1.5f);
		}


		//if running on a fixed timestep, this might be called
		//more often with a smaller delta time than RenderUpdate()
		internal void Update(UpdateTimer time, List<Input.InputAction> actions)
		{
			//Thread.Sleep(30);

			float	secDelta	=time.GetUpdateDeltaSeconds();

			float	msDelta	=time.GetUpdateDeltaMilliSeconds();

			mGD.GCam.Update(Vector3.Zero, 0f, 0f, 0f);
		}


		//called once before render with accumulated delta
		//do all once per render style updates in here
		internal void RenderUpdate(float msDelta)
		{
			if(msDelta <= 0f)
			{
				return;	//can happen if fixed time and no remainder
			}

			mRand.NextBytes(mScreenJunx);

			mScreen.SetScreenContents(mGD, mScreenJunx);
		}


		internal void Render()
		{
			CBKeeper	cbk	=mSKeeper.GetCBKeeper();

			cbk.SetWorldMat(mWorldMat);
			cbk.SetView(Matrix4x4.Identity, Vector3.Zero);
			cbk.SetProjection(mGumpProj);
			cbk.SetTransposedView(mGD.GCam.ViewTransposed, mGD.GCam.Position);

			cbk.UpdateFrame(mGD.DC);
			cbk.UpdateObject(mGD.DC);

			cbk.SetCommonCBToShaders(mGD.DC);

			mScreen.DrawStage(mGD);
		}


		internal void RenderNoPost()
		{
		}


		internal void DeviceLost(string gameRootDir)
		{
			mScreen.FreeAll(mGD);
			mSKeeper.FreeAll();

			mSKeeper	=new StuffKeeper();

			mSKeeper.Init(mGD, gameRootDir);
			mScreen	=new Screen(mGD, mSKeeper);
		}


		internal void FreeAll()
		{
			mScreen.FreeAll(mGD);
			mSKeeper.FreeAll();
		}
	}
}
