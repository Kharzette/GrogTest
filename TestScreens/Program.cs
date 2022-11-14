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

using MatLib	=MaterialLib.MaterialLib;
using Color		=Vortice.Mathematics.Color;


namespace TestScreens;

internal static class Program
{
	internal enum MyActions
	{
		Exit
	};

	const float	MaxTimeDelta	=0.1f;


	[STAThread]
	static void Main()
	{
		//turn this on for help with leaky stuff
		//Configuration.EnableObjectTracking	=true;

		Icon	testIcon	=new Icon("1281737606553.ico");
		
		GraphicsDevice	gd	=new GraphicsDevice("Screen Thing Test",
			testIcon, FeatureLevel.Level_11_0, 0.1f, 3000f);

		//save renderform position
		gd.RendForm.DataBindings.Add(new System.Windows.Forms.Binding("Location",
				Settings.Default,
				"MainWindowPos", true,
				System.Windows.Forms.DataSourceUpdateMode.OnPropertyChanged));

		int	borderWidth		=gd.RendForm.Size.Width - gd.RendForm.ClientSize.Width;
		int	borderHeight	=gd.RendForm.Size.Height - gd.RendForm.ClientSize.Height;

		gd.RendForm.Location	=Settings.Default.MainWindowPos;
		gd.RendForm.Size		=new System.Drawing.Size(
			1280 + borderWidth,
			720 + borderHeight);

		gd.CheckResize();

		//used to have a hard coded path here for #debug
		//but now can just use launch.json to provide it
		string	rootDir	=".";

		//set title of progress window
		SharedForms.ShaderCompileHelper.mTitle	="Compiling Shaders...";

		//hold right click to turn, or turn anytime mouse moves?
		bool	bRightClickToTurn	=true;

		RunLoop	loop	=new RunLoop(gd, rootDir);
		
		Input			inp			=SetUpInput(bRightClickToTurn, gd.RendForm);
		Random			rand		=new Random();
		UserSettings	sets		=new UserSettings();

		UpdateTimer	time	=new UpdateTimer(true, false);

		time.SetFixedTimeStepSeconds(1f / 60f);	//60fps update rate
		time.SetMaxDeltaSeconds(MaxTimeDelta);

		EventHandler	actHandler	=new EventHandler(
			delegate(object s, EventArgs ea)
			{
				inp.ClearInputs();
			});

		EventHandler<EventArgs>	deActHandler	=new EventHandler<EventArgs>(
			delegate(object s, EventArgs ea)
			{
				gd.SetCapture(false);
			});

		EventHandler	lostHandler	=new EventHandler(
			delegate(object s, EventArgs ea)
			{
				loop.DeviceLost(rootDir);
			});

		gd.RendForm.Activated		+=actHandler;
		gd.RendForm.AppDeactivated	+=deActHandler;
		gd.eDeviceLost				+=lostHandler;

		List<Input.InputAction>	acts	=new List<Input.InputAction>();

		RenderLoop.Run(gd.RendForm, () =>
		{
			if(!gd.RendForm.Focused)
			{
				Thread.Sleep(33);
			}

			gd.CheckResize();

			//Clear views
			gd.ClearViews();

			time.Stamp();
			while(time.GetUpdateDeltaSeconds() > 0f)
			{
				acts	=UpdateInput(inp, sets, gd, bRightClickToTurn,
					time.GetUpdateDeltaSeconds());
				if(!gd.RendForm.Focused)
				{
					acts.Clear();
					gd.SetCapture(false);
				}
				loop.Update(time, acts);
				time.UpdateDone();
			}

			loop.RenderUpdate(time.GetRenderUpdateDeltaMilliSeconds());

			loop.Render();

			gd.Present();

			acts.Clear();
		});

		Settings.Default.Save();
		sets.SaveSettings();

		gd.RendForm.Activated		-=actHandler;
		gd.RendForm.AppDeactivated	-=deActHandler;

		loop.FreeAll();
		inp.FreeAll(gd.RendForm);
		
		//Release all resources
		gd.ReleaseAll();
	}

	static List<Input.InputAction> UpdateInput(
		Input inp, UserSettings sets,
		GraphicsDevice gd, bool bHoldClickTurn,
		float delta)
	{
		List<Input.InputAction>	actions	=inp.GetAction();

		//check for exit
		foreach(Input.InputAction act in actions)
		{
			if(act.mAction.Equals(MyActions.Exit))
			{
				gd.RendForm.Close();
				return	actions;
			}
		}

		inp.ClampInputTimes(MaxTimeDelta);

		return	actions;
	}

	static Input SetUpInput(bool bHoldClickTurn, RenderForm hwnd)
	{
		Input	inp	=new InputLib.Input(1f / Stopwatch.Frequency, hwnd);
		
		//exit
		inp.MapAction(MyActions.Exit, ActionTypes.PressAndRelease,
			Modifiers.None, System.Windows.Forms.Keys.Escape);
		inp.MapAction(MyActions.Exit, ActionTypes.PressAndRelease,
			Modifiers.None, Input.VariousButtons.GamePadBack);

		return	inp;
	}
}