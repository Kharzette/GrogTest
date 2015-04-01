﻿using System;
using System.IO;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using InputLib;
using MaterialLib;
using UtilityLib;
using MeshLib;

using SharpDX;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using SharpDX.Windows;

using Device	=SharpDX.Direct3D11.Device;
using MapFlags	=SharpDX.Direct3D11.MapFlags;
using MatLib	=MaterialLib.MaterialLib;


namespace TestMeshes
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
			NextCharacter, NextAnim,
			IncreaseInvertInterval,
			DecreaseInvertInterval,
			NextStatic, RandRotateStatic,
			RandScaleStatic
		};


		[STAThread]
		static void Main()
		{
			GraphicsDevice	gd	=new GraphicsDevice("Test Meshes",
				FeatureLevel.Level_11_0);

			//save renderform position
			gd.RendForm.DataBindings.Add(new System.Windows.Forms.Binding("Location",
					Settings.Default,
					"MainWindowPos", true,
					System.Windows.Forms.DataSourceUpdateMode.OnPropertyChanged));

			gd.RendForm.Location	=Settings.Default.MainWindowPos;


#if DEBUG
			string	rootDir	="C:\\Games\\CurrentGame";
#else
			string	rootDir	=".";
#endif

			Game	theGame	=new Game(gd, rootDir);
			
			PlayerSteering	pSteering	=SetUpSteering();
			Input			inp			=SetUpInput();
			Random			rand		=new Random();

			EventHandler	actHandler	=new EventHandler(
				delegate(object s, EventArgs ea)
				{	inp.ClearInputs();	});

			gd.RendForm.Activated	+=actHandler;

			Vector3		pos				=Vector3.One * 5f;
			Vector3		lightDir		=-Vector3.UnitY;
			bool		bMouseLookOn	=false;
			long		lastTime		=Stopwatch.GetTimestamp();
			TimeSpan	frameTime		=new TimeSpan();
			long		freq			=Stopwatch.Frequency;
			long		freqMS			=freq / 1000;

			RenderLoop.Run(gd.RendForm, () =>
			{
				gd.CheckResize();

				if(bMouseLookOn)
				{
					gd.ResetCursorPos();
				}

				List<Input.InputAction>	actions	=inp.GetAction();
				if(!gd.RendForm.Focused)
				{
					actions.Clear();
				}
				else
				{
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
				}

				pos	=pSteering.Update(pos, gd.GCam.Forward, gd.GCam.Left, gd.GCam.Up, actions);
				
				gd.GCam.Update(-pos, pSteering.Pitch, pSteering.Yaw, pSteering.Roll);

				//Clear views
				gd.ClearViews();

				long	timeNow	=Stopwatch.GetTimestamp();
				long	delta	=timeNow - lastTime;
				double	deltaMS	=delta / (double)freqMS;

				//limit long frame times
				deltaMS	=Math.Min(deltaMS, 100.0);

				frameTime	=TimeSpan.FromMilliseconds(deltaMS);

				theGame.Update(frameTime, actions);

				theGame.Render(gd.DC);

				gd.Present();

				lastTime	=timeNow;
			}, true);

			Settings.Default.Save();

			gd.RendForm.Activated	-=actHandler;

			theGame.FreeAll();

			inp.FreeAll();

			
			//Release all resources
			gd.ReleaseAll();
		}

		static Input SetUpInput()
		{
			Input	inp	=new InputLib.Input();
			
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

			inp.MapAction(MyActions.PitchUp, ActionTypes.ContinuousHold, Modifiers.None, 16);
			inp.MapAction(MyActions.PitchDown, ActionTypes.ContinuousHold, Modifiers.None, 18);

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

			inp.MapAction(MyActions.IncreaseInvertInterval, ActionTypes.PressAndRelease,
				Modifiers.None, System.Windows.Forms.Keys.PageUp);
			inp.MapAction(MyActions.DecreaseInvertInterval, ActionTypes.PressAndRelease,
				Modifiers.None, System.Windows.Forms.Keys.PageDown);

			inp.MapAction(MyActions.NextStatic, ActionTypes.PressAndRelease,
				Modifiers.None, System.Windows.Forms.Keys.Oemcomma);
			inp.MapAction(MyActions.RandRotateStatic, ActionTypes.PressAndRelease,
				Modifiers.None, System.Windows.Forms.Keys.Y);
			inp.MapAction(MyActions.RandScaleStatic, ActionTypes.PressAndRelease,
				Modifiers.None, System.Windows.Forms.Keys.U);

			return	inp;
		}

		static PlayerSteering SetUpSteering()
		{
			PlayerSteering	pSteering	=new PlayerSteering();
			pSteering.Method			=PlayerSteering.SteeringMethod.Fly;
			pSteering.Speed				=0.25f;

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
