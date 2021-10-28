using System;
using System.Text;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UtilityLib;
using MaterialLib;
using AudioLib;
using InputLib;

using SharpDX;
using SharpDX.DXGI;
using SharpDX.Direct3D;

using MatLib = MaterialLib.MaterialLib;


namespace TestScreens
{
	internal class RunLoop
	{
		//data
		string			mGameRootDir;
		StuffKeeper		mSKeeper;

		Random	mRand	=new Random();

		//gpu
		GraphicsDevice	mGD;

		//screen (Thing being tested)
		Screen	mScreen;
		MatLib	mScreenMatLib;

		//screen contents
		byte	[]mScreenJunx;


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

			mScreenMatLib	=new MatLib(gd, mSKeeper);
			mScreenMatLib.CreateMaterial("TextMode");
			mScreenMatLib.SetMaterialEffect("TextMode", "TextMode.fx");
			mScreenMatLib.SetMaterialTechnique("TextMode", "TextMode");

			mScreen	=new Screen(gd, mSKeeper, mScreenMatLib);

			mScreenJunx	=new byte[40 * 25];
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

			mScreen.UpdateWVP(mGD.GCam);

			mRand.NextBytes(mScreenJunx);

			mScreen.SetScreenContents(mGD, mScreenJunx);
		}


		internal void Render()
		{
			mScreen.DrawStage(mGD, "TextMode");
		}


		internal void RenderNoPost()
		{
		}


		internal void FreeAll()
		{
			mSKeeper.FreeAll();
		}
	}
}
