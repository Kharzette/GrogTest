using System;
using System.IO;
using System.Threading;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

using InputLib;
using UtilityLib;
using MeshLib;
using MaterialLib;
using AudioLib;

using SharpDX;
using SharpDX.Direct3D;
using SharpDX.Windows;
using SharpDX.X3DAudio;

using MatLib	=MaterialLib.MaterialLib;


namespace TestAudio
{
	internal static class Program
	{
		enum MyActions
		{
			MoveForwardBack, MoveForward, MoveBack,
			MoveLeftRight, MoveLeft, MoveRight,
			MoveForwardFast, MoveBackFast,
			MoveLeftFast, MoveRightFast,
			Turn, TurnLeft, TurnRight,
			Pitch, PitchUp, PitchDown,
			ToggleMouseLookOn, ToggleMouseLookOff,
			BoostSpeedOn, BoostSpeedOff,
			PlayAtLocation, Play2D,
			NextSound, PrevSound,
			SetEmitterPos, Exit
		};

		const float	MaxTimeDelta	=0.1f;


		[STAThread]
		static void Main()
		{
			Application.EnableVisualStyles();
			Application.SetCompatibleTextRenderingDefault(false);

			GraphicsDevice	gd	=new GraphicsDevice("Audio Test Program",
				FeatureLevel.Level_9_3);

			//save renderform position
			gd.RendForm.DataBindings.Add(new System.Windows.Forms.Binding("Location",
					Settings.Default,
					"MainWindowPos", true,
					System.Windows.Forms.DataSourceUpdateMode.OnPropertyChanged));

			gd.RendForm.Location	=Settings.Default.MainWindowPos;

			string	gameRootDir	="C:\\Games\\CurrentGame";

			Audio	aud	=new Audio();

			aud.LoadAllSounds(gameRootDir + "/Audio/SoundFX");
			aud.LoadAllSounds(gameRootDir + "/Audio/Music");

			List<string>	sounds	=aud.GetSoundList();

			int	curSound	=0;

			Emitter	emitter	=Audio.MakeEmitter(Vector3.Zero);

			SharedForms.ShaderCompileHelper.mTitle	="Compiling Shaders...";

			StuffKeeper	sk		=new StuffKeeper();

			sk.eCompileNeeded	+=SharedForms.ShaderCompileHelper.CompileNeededHandler;
			sk.eCompileDone		+=SharedForms.ShaderCompileHelper.CompileDoneHandler;

			sk.Init(gd, gameRootDir);

			PlayerSteering	pSteering		=SetUpSteering();
			Input			inp				=SetUpInput();
			Random			rand			=new Random();
			CommonPrims		comPrims		=new CommonPrims(gd, sk);
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

			int	resx	=gd.RendForm.ClientRectangle.Width;
			int	resy	=gd.RendForm.ClientRectangle.Height;

			MatLib	fontMats	=new MatLib(gd, sk);

			fontMats.CreateMaterial("Text");
			fontMats.SetMaterialEffect("Text", "2D.fx");
			fontMats.SetMaterialTechnique("Text", "Text");

			List<string>	fonts	=sk.GetFontList();

			ScreenText	st	=new ScreenText(gd.GD, fontMats, fonts[0], 1000);

			Matrix	textProj	=Matrix.OrthoOffCenterLH(0, resx, resy, 0, 0.1f, 5f);

			Vector4	color	=Vector4.UnitX + (Vector4.UnitW * 0.95f);

			//string indicators for various statusy things
			st.AddString(fonts[0], "P - Play2D   L - Play at Emitter   [] - Prev/Next Sound  E - Set Emitter Pos to Camera Pos",
				"Instructions",	color, Vector2.UnitX * 20f + Vector2.UnitY * 520f, Vector2.One);
			st.AddString(fonts[0], "Stuffs", "CurrentSound",
				color, Vector2.UnitX * 20f + Vector2.UnitY * 540f, Vector2.One);
			st.AddString(fonts[0], "Stuffs", "EmitterPosition",
				color, Vector2.UnitX * 20f + Vector2.UnitY * 560f, Vector2.One);
			st.AddString(fonts[0], "Stuffs", "PosStatus",
				color, Vector2.UnitX * 20f + Vector2.UnitY * 580f, Vector2.One);

			Vector3		pos			=Vector3.One * 5f;
			Vector3		lightDir	=-Vector3.UnitY;
			UpdateTimer	time		=new UpdateTimer(false, false);

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

				time.Stamp();
				while(time.GetUpdateDeltaSeconds() > 0f)
				{
					acts	=UpdateInput(inp, gd,
						time.GetUpdateDeltaSeconds(), ref bMouseLookOn);
					if(!gd.RendForm.Focused)
					{
						acts.Clear();
						bMouseLookOn	=false;
						gd.SetCapture(false);
						inp.UnMapAxisAction(Input.MoveAxis.MouseYAxis);
						inp.UnMapAxisAction(Input.MoveAxis.MouseXAxis);
					}
					Vector3	moveDelta	=pSteering.Update(pos, gd.GCam.Forward,
						gd.GCam.Left, gd.GCam.Up, acts);

					moveDelta	*=200f;

					pos	-=moveDelta;
				
					gd.GCam.Update(pos, pSteering.Pitch, pSteering.Yaw, pSteering.Roll);

					CheckInputKeys(acts, aud, ref curSound, sounds, emitter, gd.GCam.Position);

					//update status text
					st.ModifyStringText(fonts[0], "Current Sound: " + sounds[curSound], "CurrentSound");
					st.ModifyStringText(fonts[0], "Emitter Pos: " + emitter.Position, "EmitterPosition");
					st.ModifyStringText(fonts[0], "Cam Pos: " + gd.GCam.Position +
						", Sounds Playing: " + aud.GetNumInstances(), "PosStatus");
					time.UpdateDone();
				}


				st.Update(gd.DC);

				comPrims.Update(gd.GCam, lightDir);

				aud.Update(gd.GCam);

				//Clear views
				gd.ClearViews();

				comPrims.DrawAxis(gd.DC);

				st.Draw(gd.DC, Matrix.Identity, textProj);

				gd.Present();

				acts.Clear();
			}, true);	//true here is slow but needed for winforms events

			Settings.Default.Save();
			
			gd.RendForm.Activated		-=actHandler;
			gd.RendForm.AppDeactivated	-=deActHandler;

			//Release all resources
			st.FreeAll();
			fontMats.FreeAll();
			comPrims.FreeAll();
			inp.FreeAll();

			sk.eCompileDone		-=SharedForms.ShaderCompileHelper.CompileDoneHandler;
			sk.eCompileNeeded	-=SharedForms.ShaderCompileHelper.CompileNeededHandler;
			sk.FreeAll();

			aud.FreeAll();
			gd.ReleaseAll();
		}

		static Input SetUpInput()
		{
			Input	inp	=new InputLib.Input(1f / Stopwatch.Frequency);
			
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

			inp.MapAction(MyActions.PitchUp, ActionTypes.ContinuousHold, Modifiers.None, 16);
			inp.MapAction(MyActions.PitchDown, ActionTypes.ContinuousHold, Modifiers.None, 18);

			inp.MapToggleAction(MyActions.ToggleMouseLookOn,
				MyActions.ToggleMouseLookOff, Modifiers.None,
				Input.VariousButtons.RightMouseButton);

			inp.MapAxisAction(MyActions.Pitch, Input.MoveAxis.GamePadRightYAxis);
			inp.MapAxisAction(MyActions.Turn, Input.MoveAxis.GamePadRightXAxis);
			inp.MapAxisAction(MyActions.MoveLeftRight, Input.MoveAxis.GamePadLeftXAxis);
			inp.MapAxisAction(MyActions.MoveForwardBack, Input.MoveAxis.GamePadLeftYAxis);

			inp.MapAction(MyActions.PlayAtLocation, ActionTypes.PressAndRelease,
				Modifiers.None, Keys.L);
			inp.MapAction(MyActions.Play2D, ActionTypes.PressAndRelease,
				Modifiers.None, Keys.P);
			inp.MapAction(MyActions.NextSound, ActionTypes.PressAndRelease,
				Modifiers.None, Keys.OemCloseBrackets);
			inp.MapAction(MyActions.PrevSound, ActionTypes.PressAndRelease,
				Modifiers.None, Keys.OemOpenBrackets);
			inp.MapAction(MyActions.SetEmitterPos, ActionTypes.PressAndRelease,
				Modifiers.None, Keys.E);

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
				MyActions.MoveForward, MyActions.MoveBack, MyActions.MoveLeft,
				MyActions.MoveRight, MyActions.MoveForwardFast, MyActions.MoveBackFast,
				MyActions.MoveLeftFast, MyActions.MoveRightFast);

			pSteering.SetTurnEnums(MyActions.Turn, MyActions.TurnLeft, MyActions.TurnRight);

			pSteering.SetPitchEnums(MyActions.Pitch, MyActions.PitchUp, MyActions.PitchDown);

			return	pSteering;
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

		static void CheckInputKeys(List<Input.InputAction> acts,
			Audio aud, ref int curSound, List<string> sounds,
			Emitter em, Vector3 pos)
		{
			foreach(Input.InputAction act in acts)
			{
				if(act.mAction.Equals(MyActions.PlayAtLocation))
				{
					aud.PlayAtLocation(sounds[curSound], 0.5f, false, em);
				}
				else if(act.mAction.Equals(MyActions.Play2D))
				{
					aud.Play(sounds[curSound], false, 0.5f);
				}
				else if(act.mAction.Equals(MyActions.NextSound))
				{
					curSound++;
					if(curSound >= sounds.Count)
					{
						curSound	=0;
					}
				}
				else if(act.mAction.Equals(MyActions.PrevSound))
				{
					curSound--;
					if(curSound < 0)
					{
						curSound	=sounds.Count - 1;
					}
				}
				else if(act.mAction.Equals(MyActions.SetEmitterPos))
				{
					em.Position	=pos;
				}
			}
		}

	}
}
