using System;
using System.IO;
using System.Threading;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using InputLib;
using MaterialLib;
using UtilityLib;
using MeshLib;
using BSPZone;

using SharpDX;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using SharpDX.Windows;

using Device	=SharpDX.Direct3D11.Device;
using MapFlags	=SharpDX.Direct3D11.MapFlags;
using MatLib	=MaterialLib.MaterialLib;


namespace LibTest
{
	internal static class Program
	{
		internal enum MyActions
		{
			MoveForwardBack, MoveForward, MoveBack,
			MoveLeftRight, MoveLeft, MoveRight,
			MoveForwardFast, MoveBackFast,
			MoveLeftFast, MoveRightFast,
			Turn, TurnLeft, TurnRight, Jump,
			Pitch, PitchUp, PitchDown,
			ToggleMouseLookOn, ToggleMouseLookOff,
			NextAnim, NextLevel, ToggleFly,
			PlaceDynamicLight, ClearDynamicLights,
			AccelTest, AccelTest2
		};

		const float	MouseTurnMultiplier	=.07f;


		[STAThread]
		static void Main()
		{
			GraphicsDevice	gd	=new GraphicsDevice("Basic Map Test Program",
				FeatureLevel.Level_11_0);

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

			//set this to whereever the game data is stored during
			//development.  Release ver will look in .
#if DEBUG
			string	rootDir	="C:\\Games\\CurrentGame";
#else
			string	rootDir	=".";
#endif

			MapLoop	mapLoop	=new MapLoop(gd, rootDir);
			
			PlayerSteering	pSteering	=SetUpSteering();
			Input			inp			=SetUpInput();
			Random			rand		=new Random();

			EventHandler	actHandler	=new EventHandler(
				delegate(object s, EventArgs ea)
				{	inp.ClearInputs();	});

			gd.RendForm.Activated	+=actHandler;

			Vector3	pos				=Vector3.One * 5f;
			Vector3	lightDir		=-Vector3.UnitY;
			bool	bMouseLookOn	=false;
			long	lastTime		=Stopwatch.GetTimestamp();
			bool	bFixedStep		=true;
			float	step			=1f / 60f;
			float	fullTime		=0f;
			long	freq			=Stopwatch.Frequency;
			float	accumTest		=0f;

			RenderLoop.Run(gd.RendForm, () =>
			{
				if(!gd.RendForm.Focused)
				{
					Thread.Sleep(33);
				}
				gd.CheckResize();

				if(bMouseLookOn)
				{
					gd.ResetCursorPos();
				}

				//Clear views
				gd.ClearViews();

				long	timeNow		=Stopwatch.GetTimestamp();
				long	delta		=timeNow - lastTime;
				float	secDelta	=(float)delta / freq;

				accumTest	+=secDelta;

				//set an upper limit on deltas
				//these can get huge if time is spent
				//at a breakpoint etc
				secDelta	=Math.Min(secDelta, 0.1f);

				if(bFixedStep)
				{
					fullTime	+=secDelta;
					while(fullTime >= step)
					{
						List<Input.InputAction>	acts	=UpdateInput(inp, gd, step, ref bMouseLookOn);
						mapLoop.Update(step, acts, pSteering);
						fullTime	-=step;
					}
				}
				else
				{
					List<Input.InputAction>	acts	=UpdateInput(inp, gd, secDelta, ref bMouseLookOn);
					mapLoop.Update(secDelta, acts, pSteering);
				}

				mapLoop.RenderUpdate(secDelta * 1000f);

				mapLoop.Render();

				gd.Present();

				lastTime	=timeNow;
			});

			Settings.Default.Save();

			gd.RendForm.Activated	-=actHandler;

			mapLoop.FreeAll();

			inp.FreeAll();
			
			//Release all resources
			gd.ReleaseAll();
		}

		static List<Input.InputAction> UpdateInput(Input inp, GraphicsDevice gd,
			float delta, ref bool bMouseLookOn)
		{
			List<Input.InputAction>	actions	=inp.GetAction();
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
					inp.UnMapAxisAction(MyActions.Pitch, Input.MoveAxis.MouseYAxis);
					inp.UnMapAxisAction(MyActions.Turn, Input.MoveAxis.MouseXAxis);
				}
			}
			return	actions;
		}

		static Input SetUpInput()
		{
			Input	inp	=new InputLib.Input(MouseTurnMultiplier, 1f / Stopwatch.Frequency);
			
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

			inp.MapAction(MyActions.ToggleFly, ActionTypes.PressAndRelease,
				Modifiers.None, System.Windows.Forms.Keys.F);

			inp.MapAction(MyActions.Jump, ActionTypes.ActivateOnce,
				Modifiers.None, System.Windows.Forms.Keys.Space);
			inp.MapAction(MyActions.Jump, ActionTypes.ActivateOnce,
				Modifiers.ShiftHeld, System.Windows.Forms.Keys.Space);
			inp.MapAction(MyActions.Jump, ActionTypes.ActivateOnce,
				Modifiers.ControlHeld, System.Windows.Forms.Keys.Space);
			inp.MapAction(MyActions.Jump, ActionTypes.ActivateOnce,
				Modifiers.None,	Input.VariousButtons.GamePadY);

			inp.MapAction(MyActions.PlaceDynamicLight, ActionTypes.ActivateOnce,
				Modifiers.None, System.Windows.Forms.Keys.G);
			inp.MapAction(MyActions.ClearDynamicLights, ActionTypes.PressAndRelease,
				Modifiers.None, System.Windows.Forms.Keys.H);

			inp.MapToggleAction(MyActions.ToggleMouseLookOn,
				MyActions.ToggleMouseLookOff, Modifiers.None,
				Input.VariousButtons.RightMouseButton);

			inp.MapAxisAction(MyActions.Pitch, Input.MoveAxis.GamePadRightYAxis);
			inp.MapAxisAction(MyActions.Turn, Input.MoveAxis.GamePadRightXAxis);
			inp.MapAxisAction(MyActions.MoveLeftRight, Input.MoveAxis.GamePadLeftXAxis);
			inp.MapAxisAction(MyActions.MoveForwardBack, Input.MoveAxis.GamePadLeftYAxis);

			inp.MapAction(MyActions.NextAnim, ActionTypes.PressAndRelease,
				Modifiers.None, System.Windows.Forms.Keys.K);
			inp.MapAction(MyActions.NextLevel, ActionTypes.PressAndRelease,
				Modifiers.None, System.Windows.Forms.Keys.L);

			inp.MapAction(MyActions.AccelTest, ActionTypes.ContinuousHold,
				Modifiers.None, System.Windows.Forms.Keys.T);
			inp.MapAction(MyActions.AccelTest2, ActionTypes.ContinuousHold,
				Modifiers.ShiftHeld, System.Windows.Forms.Keys.T);

			return	inp;
		}

		static PlayerSteering SetUpSteering()
		{
			PlayerSteering	pSteering	=new PlayerSteering();
			pSteering.Method			=PlayerSteering.SteeringMethod.Fly;
//			pSteering.Speed				=0.06f;

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