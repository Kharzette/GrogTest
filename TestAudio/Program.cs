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
			Turn, TurnLeft, TurnRight,
			Pitch, PitchUp, PitchDown,
			ToggleMouseLookOn, ToggleMouseLookOff,
			BoostSpeedOn, BoostSpeedOff,
			PlayAtLocation, Play2D,
			NextSound, PrevSound,
			SetEmitterPos
		};


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

			PlayerSteering	pSteering	=SetUpSteering();
			Input			inp			=SetUpInput();
			Random			rand		=new Random();
			CommonPrims		comPrims	=new CommonPrims(gd, sk);

			EventHandler	actHandler	=new EventHandler(
				delegate(object s, EventArgs ea)
				{	inp.ClearInputs();	});

			gd.RendForm.Activated	+=actHandler;

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

			Vector3	pos				=Vector3.One * 5f;
			Vector3	lightDir		=-Vector3.UnitY;
			bool	bMouseLookOn	=false;
			long	lastTime		=Stopwatch.GetTimestamp();

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
						else if(act.mAction.Equals(MyActions.BoostSpeedOn))
						{
							pSteering.Speed	=2;
						}
						else if(act.mAction.Equals(MyActions.BoostSpeedOff))
						{
							pSteering.Speed	=0.5f;
						}
					}
				}

				pos	=pSteering.Update(pos, gd.GCam.Forward, gd.GCam.Left, gd.GCam.Up, actions);
				
				gd.GCam.Update(pos, pSteering.Pitch, pSteering.Yaw, pSteering.Roll);

				CheckInputKeys(actions, aud, ref curSound, sounds, emitter, gd.GCam.Position);

				//update status text
				st.ModifyStringText(fonts[0], "Current Sound: " + sounds[curSound], "CurrentSound");
				st.ModifyStringText(fonts[0], "Emitter Pos: " + emitter.Position, "EmitterPosition");
				st.ModifyStringText(fonts[0], "Cam Pos: " + gd.GCam.Position +
					", Sounds Playing: " + aud.GetNumInstances(), "PosStatus");

				st.Update(gd.DC);

				comPrims.Update(gd.GCam, lightDir);

				aud.Update(gd.GCam);

				//Clear views
				gd.ClearViews();

				long	timeNow	=Stopwatch.GetTimestamp();
				long	delta	=timeNow - lastTime;

				comPrims.DrawAxis(gd.DC);

				st.Draw(gd.DC, Matrix.Identity, textProj);

				gd.Present();

				lastTime	=timeNow;
			}, true);	//true here is slow but needed for winforms events

			Settings.Default.Save();
			
			gd.RendForm.Activated	-=actHandler;

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
			Input	inp	=new InputLib.Input();
			
			inp.MapAction(MyActions.PitchUp, ActionTypes.ContinuousHold, Modifiers.None, 16);
			inp.MapAction(MyActions.MoveForward, ActionTypes.ContinuousHold, Modifiers.None, 17);
			inp.MapAction(MyActions.PitchDown, ActionTypes.ContinuousHold, Modifiers.None, 18);
			inp.MapAction(MyActions.MoveLeft, ActionTypes.ContinuousHold, Modifiers.None, 30);
			inp.MapAction(MyActions.MoveBack, ActionTypes.ContinuousHold, Modifiers.None, 31);
			inp.MapAction(MyActions.MoveRight, ActionTypes.ContinuousHold, Modifiers.None, 32);

			inp.MapToggleAction(MyActions.BoostSpeedOn,
				MyActions.BoostSpeedOff, Modifiers.None,
				42);

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

			return	inp;
		}

		static PlayerSteering SetUpSteering()
		{
			PlayerSteering	pSteering	=new PlayerSteering();
			pSteering.Method			=PlayerSteering.SteeringMethod.Fly;

			pSteering.SetMoveEnums(MyActions.MoveLeftRight, MyActions.MoveLeft, MyActions.MoveRight,
				MyActions.MoveForwardBack, MyActions.MoveForward, MyActions.MoveBack);

			pSteering.SetTurnEnums(MyActions.Turn, MyActions.TurnLeft, MyActions.TurnRight);

			pSteering.SetPitchEnums(MyActions.Pitch, MyActions.PitchUp, MyActions.PitchDown);

			pSteering.Speed	=0.5f;

			return	pSteering;
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
