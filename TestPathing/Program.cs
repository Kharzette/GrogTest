using System;
using System.IO;
using System.Threading;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

using InputLib;
using MaterialLib;
using UtilityLib;
using MeshLib;

using SharpDX;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using SharpDX.Windows;


namespace TestPathing
{
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
			NextLevel, Exit, MouseSelect
		};

		const float	MaxTimeDelta	=0.1f;


		[STAThread]
		static void Main()
		{
			Application.EnableVisualStyles();
			Application.SetCompatibleTextRenderingDefault(false);
			GraphicsDevice	gd	=new GraphicsDevice("Pathfinding Test Program",
				FeatureLevel.Level_11_0);

			PathingForm	pathForm	=new PathingForm();

			//save renderform position
			gd.RendForm.DataBindings.Add(new Binding("Location",
				Properties.Settings.Default, "MainWindowPos", true,
				DataSourceUpdateMode.OnPropertyChanged));
			pathForm.DataBindings.Add(new Binding("Location",
				Properties.Settings.Default, "PathWindowPos", true,
				DataSourceUpdateMode.OnPropertyChanged));

			gd.RendForm.Location	=Properties.Settings.Default.MainWindowPos;
			pathForm.Location		=Properties.Settings.Default.PathWindowPos;
			
			int	borderWidth		=gd.RendForm.Size.Width - gd.RendForm.ClientSize.Width;
			int	borderHeight	=gd.RendForm.Size.Height - gd.RendForm.ClientSize.Height;

			gd.RendForm.Location	=Properties.Settings.Default.MainWindowPos;
			gd.RendForm.Size		=new System.Drawing.Size(
				1280 + borderWidth,
				720 + borderHeight);

			gd.CheckResize();

			PlayerSteering	pSteering		=SetUpSteering();
			Input			inp				=SetUpInput();
			Random			rand			=new Random();
			bool			bMouseLookOn	=false;

			EventHandler	actHandler	=new EventHandler(
				delegate(object s, EventArgs ea)
				{	inp.ClearInputs();	});

			EventHandler<EventArgs>	deActHandler	=new EventHandler<EventArgs>(
				delegate(object s, EventArgs ea)
				{
					gd.SetCapture(false);
					bMouseLookOn	=false;
				});

			gd.RendForm.Activated		+=actHandler;
			gd.RendForm.AppDeactivated	+=deActHandler;

			MapStuff	mapStuff	=new MapStuff(gd, "C:\\Games\\CurrentGame");

			EventHandler	pickedAHandler	=new EventHandler(
				delegate(object s, EventArgs ea)
				{	Vector3EventArgs	v3ea	=ea as Vector3EventArgs;
					pathForm.SetCoordA(v3ea.mVector);
					pathForm.SetNodeA((int)s);	});
			EventHandler	pickedBHandler	=new EventHandler(
				delegate(object s, EventArgs ea)
				{	Vector3EventArgs	v3ea	=ea as Vector3EventArgs;
					pathForm.SetCoordB(v3ea.mVector);
					pathForm.SetNodeB((int)s);	});
			EventHandler	pickReadyHandler	=new EventHandler(
				delegate(object s, EventArgs ea)
				{	pathForm.SetPickReady((bool)s);	});

			mapStuff.ePickedA	+=pickedAHandler;
			mapStuff.ePickedB	+=pickedBHandler;
			mapStuff.ePickReady	+=pickReadyHandler;

			EventHandler	genHandler	=new EventHandler(
				delegate(object s, EventArgs ea)
				{	mapStuff.GeneratePathing(pathForm.GetGridSize(), (float)s);	});
			EventHandler	loadHandler	=new EventHandler(
				delegate(object s, EventArgs ea)
				{	mapStuff.LoadPathing(s as string);	});
			EventHandler	saveHandler	=new EventHandler(
				delegate(object s, EventArgs ea)
				{	mapStuff.SavePathing(s as string);	});
			EventHandler	pickAHandler	=new EventHandler(
				delegate(object s, EventArgs ea)
				{	mapStuff.PickA();
					gd.RendForm.Focus();	});
			EventHandler	pickBHandler	=new EventHandler(
				delegate(object s, EventArgs ea)
				{	mapStuff.PickB();
					gd.RendForm.Focus();	});
			EventHandler	pickBlockedHandler	=new EventHandler(
				delegate(object s, EventArgs ea)
				{	mapStuff.PickBlocked();
					gd.RendForm.Focus();	});
			EventHandler	pickUnBlockedHandler	=new EventHandler(
				delegate(object s, EventArgs ea)
				{	mapStuff.PickUnBlocked();
					gd.RendForm.Focus();	});
			EventHandler	drawChangedHandler	=new EventHandler(
				delegate(object s, EventArgs ea)
				{	mapStuff.DrawSettings((int)s);	});
			EventHandler	mobChangedHandler	=new EventHandler(
				delegate(object s, EventArgs ea)
				{	UInt32	box	=(UInt32)s;
					mapStuff.AlterPathMobile((int)box & 0xFF, (int)box >> 16);	});
			EventHandler	findPathHandler	=new EventHandler(
				delegate(object s, EventArgs ea)
				{	Vector3PairEventArgs	v3pea	=ea as Vector3PairEventArgs;
					mapStuff.FindPath(v3pea.mVecA, v3pea.mVecB);	});

			pathForm.eGenerate		+=genHandler;
			pathForm.eLoadData		+=loadHandler;
			pathForm.eSaveData		+=saveHandler;
			pathForm.ePickA			+=pickAHandler;
			pathForm.ePickB			+=pickBHandler;
			pathForm.ePickBlock		+=pickBlockedHandler;
			pathForm.ePickUnBlock	+=pickUnBlockedHandler;
			pathForm.eDrawChanged	+=drawChangedHandler;
			pathForm.eMobChanged	+=mobChangedHandler;
			pathForm.eFindPath		+=findPathHandler;

			pathForm.SetPickReady(false);

			pathForm.Show();

			UpdateTimer	time	=new UpdateTimer(true, false);

			time.SetFixedTimeStepSeconds(1f / 60f);	//60fps update rate
			time.SetMaxDeltaSeconds(MaxTimeDelta);

			List<Input.InputAction>	acts	=new List<Input.InputAction>();

			RenderLoop.Run(gd.RendForm, () =>
			{
				if(!gd.RendForm.Focused)
				{
					Thread.Sleep(33);
				}
				else if(mapStuff.Busy())
				{
					Thread.Sleep(5);
					return;
				}

				gd.CheckResize();

				if(bMouseLookOn)
				{
					gd.ResetCursorPos();
				}

				//Clear views
				gd.ClearViews();

				time.Stamp();
				while(time.GetUpdateDeltaSeconds() > 0f)
				{
					acts	=UpdateInput(inp, gd,
						time.GetUpdateDeltaSeconds(), ref bMouseLookOn);
					if(!gd.RendForm.Focused)
					{
						acts.Clear();
					}
					mapStuff.Update(time, acts, pSteering);
					time.UpdateDone();
				}

				mapStuff.RenderUpdate(time.GetRenderUpdateDeltaMilliSeconds());

				mapStuff.Render();
				
				gd.Present();

				acts.Clear();
			}, true);	//true here is slow but needed for winforms events

			Properties.Settings.Default.Save();

			gd.RendForm.Activated		-=actHandler;
			gd.RendForm.AppDeactivated	-=deActHandler;

			mapStuff.ePickedA	-=pickedAHandler;
			mapStuff.ePickedB	-=pickedBHandler;
			mapStuff.ePickReady	-=pickReadyHandler;

			pathForm.eGenerate		-=genHandler;
			pathForm.eLoadData		-=loadHandler;
			pathForm.eSaveData		-=saveHandler;
			pathForm.ePickA			-=pickAHandler;
			pathForm.ePickB			-=pickBHandler;
			pathForm.ePickBlock		-=pickBlockedHandler;
			pathForm.ePickUnBlock	-=pickUnBlockedHandler;
			pathForm.eDrawChanged	-=drawChangedHandler;
			pathForm.eMobChanged	-=mobChangedHandler;
			pathForm.eFindPath		-=findPathHandler;

			mapStuff.FreeAll();
			inp.FreeAll();
			
			//Release all resources
			gd.ReleaseAll();
		}

		static List<Input.InputAction> UpdateInput(Input inp,
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
						act.mMultiplier	*=UserSettings.MouseTurnMultiplier;
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
			return	actions;
		}

		static Input SetUpInput()
		{
			Input	inp	=new InputLib.Input(1f / Stopwatch.Frequency);
			
			//wasd
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

			inp.MapAction(MyActions.NextLevel, ActionTypes.PressAndRelease,
				Modifiers.None, System.Windows.Forms.Keys.L);

			inp.MapToggleAction(MyActions.ToggleMouseLookOn,
				MyActions.ToggleMouseLookOff, Modifiers.None,
				Input.VariousButtons.RightMouseButton);

			inp.MapAxisAction(MyActions.Pitch, Input.MoveAxis.GamePadRightYAxis);
			inp.MapAxisAction(MyActions.Turn, Input.MoveAxis.GamePadRightXAxis);
			inp.MapAxisAction(MyActions.MoveLeftRight, Input.MoveAxis.GamePadLeftXAxis);
			inp.MapAxisAction(MyActions.MoveForwardBack, Input.MoveAxis.GamePadLeftYAxis);

			//mouseselect for picking paths
			inp.MapAction(MyActions.MouseSelect, ActionTypes.PressAndRelease,
				Modifiers.None, Input.VariousButtons.LeftMouseButton);

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
}
