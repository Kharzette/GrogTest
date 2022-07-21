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

namespace TestMeshes;

internal static class Program
{
	internal enum MyActions
	{
		MoveForwardBack, MoveForward, MoveBack,
		MoveLeftRight, MoveLeft, MoveRight,
		MoveForwardFast, MoveBackFast,
		MoveLeftFast, MoveRightFast,
		Turn, TurnLeft, TurnRight,
		Pitch, PitchUp, PitchDown,
		ToggleMouseLookOn, ToggleMouseLookOff,
		NextCharacter, NextAnim,
		CharacterYawInc, CharacterYawDec,
		NextStatic, RandRotateStatic,
		SensitivityUp, SensitivityDown,
		RandLightDirection,
		RandScaleStatic, Exit
	};

	const float	MaxTimeDelta	=0.1f;
	const float	MoveScalar		=1.25f;


	[STAThread]
	static void Main()
	{
		Application.EnableVisualStyles();
		Application.SetCompatibleTextRenderingDefault(false);

		Icon	testIcon	=new Icon("1281737606553.ico");

		//turn this on for help with leaky stuff
		//Configuration.EnableObjectTracking	=true;

		GraphicsDevice	gd	=new GraphicsDevice("Test Meshes",
			testIcon, FeatureLevel.Level_11_0, 0.1f, 3000f);

		//save renderform position
		gd.RendForm.DataBindings.Add(new System.Windows.Forms.Binding("Location",
				Settings.Default,
				"MainWindowPos", true,
				System.Windows.Forms.DataSourceUpdateMode.OnPropertyChanged));

		gd.RendForm.Location	=Settings.Default.MainWindowPos;

		SharedForms.ShaderCompileHelper.mTitle	="Compiling Shaders...";

		StuffKeeper	sk		=new StuffKeeper();

		sk.eCompileNeeded	+=SharedForms.ShaderCompileHelper.CompileNeededHandler;
		sk.eCompileDone		+=SharedForms.ShaderCompileHelper.CompileDoneHandler;

		sk.Init(gd, ".");


		//set title of progress window
		SharedForms.ShaderCompileHelper.mTitle	="Compiling Shaders...";

		//used to have a hard coded path here for #debug
		//but now can just use launch.json to provide it
		string	rootDir	=".";

		Game	theGame	=new Game(gd, rootDir);
		
		PlayerSteering	pSteering		=SetUpSteering();
		UserSettings	sets			=new UserSettings();
		Input			inp				=SetUpInput(gd.RendForm);
		Random			rand			=new Random();
		bool			bMouseLookOn	=false;

		EventHandler	actHandler	=new EventHandler(
			delegate(object ?s, EventArgs ea)
			{	inp.ClearInputs();	});

		EventHandler<EventArgs>	deActHandler	=new EventHandler<EventArgs>(
			delegate(object ?s, EventArgs ea)
			{
				gd.SetCapture(false);
				bMouseLookOn	=false;
			});

		EventHandler	lostHandler	=new EventHandler(
			delegate(object ?s, EventArgs ea)
			{
//				post.FreeAll(gd);
//				post	=new PostProcess(gd, sk);
			});

		gd.eDeviceLost				+=lostHandler;
		gd.RendForm.Activated		+=actHandler;
		gd.RendForm.AppDeactivated	+=deActHandler;

		Vector3		pos			=Vector3.One * 5f;
		UpdateTimer	time		=new UpdateTimer(true, false);

		time.SetFixedTimeStepSeconds(1f / 60f);	//60fps update rate
		time.SetMaxDeltaSeconds(MaxTimeDelta);

		List<Input.InputAction>	acts	=new List<Input.InputAction>();

		RenderLoop.Run(gd.RendForm, () =>
		{
			if(!gd.RendForm.Focused)
			{
				Thread.Sleep(33);
			}

			gd.CheckResize();

			if(bMouseLookOn && gd.RendForm.Focused)
			{
				gd.ResetCursorPos();
			}

			//Clear views
			gd.ClearViews();

			time.Stamp();
			while(time.GetUpdateDeltaSeconds() > 0f)
			{
				acts	=UpdateInput(inp, sets, gd,
					time.GetUpdateDeltaSeconds(), ref bMouseLookOn);
				if(!gd.RendForm.Focused)
				{
					acts.Clear();
					bMouseLookOn	=false;
					gd.SetCapture(false);
					inp.UnMapAxisAction(Input.MoveAxis.MouseYAxis);
					inp.UnMapAxisAction(Input.MoveAxis.MouseXAxis);
				}

				Vector3	moveDelta	=pSteering.Update(pos, gd.GCam.Forward, gd.GCam.Left, gd.GCam.Up, acts);

				moveDelta	*=MoveScalar * theGame.GetScaleFactor();
				pos			+=moveDelta;
			
				theGame.Update(time, acts, pos);

				time.UpdateDone();
			}

			gd.GCam.Update(pos, pSteering.Pitch, pSteering.Yaw, pSteering.Roll);
			theGame.Render(pos);

			gd.Present();

			acts.Clear();
		}, true);

		Settings.Default.Save();
		sets.SaveSettings();

		gd.RendForm.Activated		-=actHandler;
		gd.RendForm.AppDeactivated	-=deActHandler;

		theGame.FreeAll();

		inp.FreeAll(gd.RendForm);
		
		//Release all resources
		gd.ReleaseAll();
	}

	static List<Input.InputAction> UpdateInput(Input inp, UserSettings sets,
		GraphicsDevice gd, float delta, ref bool bMouseLookOn)
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

		foreach(Input.InputAction act in actions)
		{
			if(act.mAction.Equals(MyActions.ToggleMouseLookOn))
			{
				bMouseLookOn	=true;
				gd.SetCapture(true);
				inp.MapAxisAction(MyActions.Pitch, Input.MoveAxis.MouseYAxis);
				inp.MapAxisAction(MyActions.Turn, Input.MoveAxis.MouseXAxis);
			}
			else if(act.mAction.Equals(MyActions.ToggleMouseLookOff))
			{
				bMouseLookOn	=false;
				gd.SetCapture(false);
				inp.UnMapAxisAction(Input.MoveAxis.MouseYAxis);
				inp.UnMapAxisAction(Input.MoveAxis.MouseXAxis);
			}
		}

		//delta scale analogs, since there's no timestamp stuff in gamepad code
		foreach(Input.InputAction act in actions)
		{
			if(!act.mbTime && act.mDevice == Input.InputAction.DeviceType.ANALOG)
			{
				//analog needs a time scale applied
				act.mMultiplier	*=delta;
			}
		}

		//scale inputs to user prefs
		foreach(Input.InputAction act in actions)
		{
			if(act.mAction.Equals(MyActions.Turn)
				|| act.mAction.Equals(MyActions.TurnLeft)
				|| act.mAction.Equals(MyActions.TurnRight)
				|| act.mAction.Equals(MyActions.Pitch)
				|| act.mAction.Equals(MyActions.PitchDown)
				|| act.mAction.Equals(MyActions.PitchUp))
			{
				if(act.mDevice == Input.InputAction.DeviceType.MOUSE)
				{
					act.mMultiplier	*=UserSettings.MouseTurnMultiplier
						* sets.mTurnSensitivity;
				}
				else if(act.mDevice == Input.InputAction.DeviceType.ANALOG)
				{
					act.mMultiplier	*=UserSettings.AnalogTurnMultiplier;
				}
				else if(act.mDevice == Input.InputAction.DeviceType.KEYS)
				{
					act.mMultiplier	*=UserSettings.KeyTurnMultiplier;
				}
			}
		}

		//sensitivity adjust
		foreach(Input.InputAction act in actions)
		{
			float	sense	=sets.mTurnSensitivity;
			if(act.mAction.Equals(MyActions.SensitivityUp))
			{
				sense	+=0.025f;
			}
			else if(act.mAction.Equals(MyActions.SensitivityDown))
			{
				sense	-=0.025f;
			}
			else
			{
				continue;
			}
			sets.mTurnSensitivity	=Math.Clamp(sense, 0.025f, 10f);
		}
		return	actions;
	}

	static Input SetUpInput(RenderForm hwnd)
	{
		Input	inp	=new InputLib.Input(1f / Stopwatch.Frequency, hwnd);
		
		inp.MapAction(MyActions.MoveForward, ActionTypes.ContinuousHold,
			Modifiers.None, System.Windows.Forms.Keys.W);
		inp.MapAction(MyActions.MoveLeft, ActionTypes.ContinuousHold,
			Modifiers.None, System.Windows.Forms.Keys.A);
		inp.MapAction(MyActions.MoveBack, ActionTypes.ContinuousHold,
			Modifiers.None, System.Windows.Forms.Keys.S);
		inp.MapAction(MyActions.MoveRight, ActionTypes.ContinuousHold,
			Modifiers.None, System.Windows.Forms.Keys.D);
		inp.MapAction(MyActions.MoveForwardFast, ActionTypes.ContinuousHold,
			Modifiers.ShiftHeld, System.Windows.Forms.Keys.W);
		inp.MapAction(MyActions.MoveBackFast, ActionTypes.ContinuousHold,
			Modifiers.ShiftHeld, System.Windows.Forms.Keys.S);
		inp.MapAction(MyActions.MoveLeftFast, ActionTypes.ContinuousHold,
			Modifiers.ShiftHeld, System.Windows.Forms.Keys.A);
		inp.MapAction(MyActions.MoveRightFast, ActionTypes.ContinuousHold,
			Modifiers.ShiftHeld, System.Windows.Forms.Keys.D);

		//arrow keys
		inp.MapAction(MyActions.MoveForward, ActionTypes.ContinuousHold,
			Modifiers.None, System.Windows.Forms.Keys.Up);
		inp.MapAction(MyActions.MoveBack, ActionTypes.ContinuousHold,
			Modifiers.None, System.Windows.Forms.Keys.Down);
		inp.MapAction(MyActions.MoveForwardFast, ActionTypes.ContinuousHold,
			Modifiers.ShiftHeld, System.Windows.Forms.Keys.Up);
		inp.MapAction(MyActions.MoveBackFast, ActionTypes.ContinuousHold,
			Modifiers.ShiftHeld, System.Windows.Forms.Keys.Down);
		inp.MapAction(MyActions.TurnLeft, ActionTypes.ContinuousHold,
			Modifiers.None, System.Windows.Forms.Keys.Left);
		inp.MapAction(MyActions.TurnRight, ActionTypes.ContinuousHold,
			Modifiers.None, System.Windows.Forms.Keys.Right);
		inp.MapAction(MyActions.PitchUp, ActionTypes.ContinuousHold,
			Modifiers.None, System.Windows.Forms.Keys.Q);
		inp.MapAction(MyActions.PitchDown, ActionTypes.ContinuousHold,
			Modifiers.None, System.Windows.Forms.Keys.E);

		inp.MapToggleAction(MyActions.ToggleMouseLookOn,
			MyActions.ToggleMouseLookOff, Modifiers.None,
			Input.VariousButtons.RightMouseButton);

		inp.MapAxisAction(MyActions.Pitch, Input.MoveAxis.GamePadRightYAxis);
		inp.MapAxisAction(MyActions.Turn, Input.MoveAxis.GamePadRightXAxis);
		inp.MapAxisAction(MyActions.MoveLeftRight, Input.MoveAxis.GamePadLeftXAxis);
		inp.MapAxisAction(MyActions.MoveForwardBack, Input.MoveAxis.GamePadLeftYAxis);

		inp.MapAction(MyActions.NextCharacter, ActionTypes.PressAndRelease,
			Modifiers.None, System.Windows.Forms.Keys.C);
		inp.MapAction(MyActions.NextAnim, ActionTypes.PressAndRelease,
			Modifiers.None, System.Windows.Forms.Keys.N);

		inp.MapAction(MyActions.CharacterYawInc, ActionTypes.ContinuousHold,
			Modifiers.None, System.Windows.Forms.Keys.J);
		inp.MapAction(MyActions.CharacterYawDec, ActionTypes.ContinuousHold,
			Modifiers.None, System.Windows.Forms.Keys.K);

		inp.MapAction(MyActions.NextStatic, ActionTypes.PressAndRelease,
			Modifiers.None, System.Windows.Forms.Keys.Oemcomma);
		inp.MapAction(MyActions.RandRotateStatic, ActionTypes.PressAndRelease,
			Modifiers.None, System.Windows.Forms.Keys.Y);
		inp.MapAction(MyActions.RandScaleStatic, ActionTypes.PressAndRelease,
			Modifiers.None, System.Windows.Forms.Keys.U);

		//sensitivity adjust
		inp.MapAction(MyActions.SensitivityDown, ActionTypes.PressAndRelease,
			Modifiers.None, System.Windows.Forms.Keys.OemMinus);
		//for numpad
		inp.MapAction(MyActions.SensitivityUp, ActionTypes.PressAndRelease,
			Modifiers.None, System.Windows.Forms.Keys.Oemplus);
		//non numpad will have shift held too
		inp.MapAction(MyActions.SensitivityUp, ActionTypes.PressAndRelease,
			Modifiers.ShiftHeld, System.Windows.Forms.Keys.Oemplus);

		//random light direction
		inp.MapAction(MyActions.RandLightDirection, ActionTypes.PressAndRelease,
			Modifiers.None, System.Windows.Forms.Keys.L);

		//exit
		inp.MapAction(MyActions.Exit, ActionTypes.PressAndRelease,
			Modifiers.None, System.Windows.Forms.Keys.Escape);
		inp.MapAction(MyActions.Exit, ActionTypes.PressAndRelease,
			Modifiers.None, Input.VariousButtons.GamePadBack);

		return	inp;
	}

	static PlayerSteering SetUpSteering()
	{
		PlayerSteering	pSteering	=new PlayerSteering();
		pSteering.Method			=PlayerSteering.SteeringMethod.Fly;

		pSteering.SetMoveEnums(MyActions.MoveForwardBack, MyActions.MoveLeftRight,
			MyActions.MoveForward, MyActions.MoveBack,
			MyActions.MoveLeft, MyActions.MoveRight,
			MyActions.MoveForwardFast, MyActions.MoveBackFast,
			MyActions.MoveLeftFast, MyActions.MoveRightFast);

		pSteering.SetTurnEnums(MyActions.Turn, MyActions.TurnLeft, MyActions.TurnRight);

		pSteering.SetPitchEnums(MyActions.Pitch, MyActions.PitchUp, MyActions.PitchDown);

		return	pSteering;
	}
}