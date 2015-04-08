using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using SharpDX;
using InputLib;
using SharpDXStuff;


namespace InputTest
{
	public partial class Form1 : Form
	{
		Input	mInput	=new Input();

		PlayerSteering	mPS;
		GameCamera		mCam	=new GameCamera(69, 69, 1f, 1f, 5000f);

		Vector3	mPosition	=Vector3.Zero;

		enum MyActions
		{
			MoveForward, MoveBack, MoveLeft, MoveRight
		};


		public Form1()
		{
			InitializeComponent();

			mInput.MapAction(MyActions.MoveForward, 17);
			mInput.MapAction(MyActions.MoveLeft, 30);
			mInput.MapAction(MyActions.MoveBack, 31);
			mInput.MapAction(MyActions.MoveRight, 32);

			mPS			=new PlayerSteering(69, 69);
			mPS.Method	=PlayerSteering.SteeringMethod.FirstPerson;

			mPS.SetMoveEnums(MyActions.MoveLeft, MyActions.MoveRight,
				MyActions.MoveForward, MyActions.MoveBack);
		}

		void OnUpdate(object sender, EventArgs e)
		{
			List<Input.InputAction>	actions	=mInput.GetAction();

			string	toPrint	="";
			foreach(Input.InputAction act in actions)
			{
				toPrint	+=act.mAction + " : " + act.mMultiplier + "\r\n";
			}
			Action<TextBox>	ta	=con => con.AppendText(toPrint);
			FormExtensions.Invoke(InfoConsole, ta);

			mPosition	=mPS.Update(mPosition, mCam.Forward, mCam.Left, mCam.Up, actions);

			mCam.Update(mPosition, mPS.Pitch, mPS.Yaw, mPS.Roll);

			toPrint	="CamPos: " + mPosition;
			ta	=con => con.AppendText(toPrint);
			FormExtensions.Invoke(InfoConsole, ta);
		}
	}

	public static class FormExtensions
	{
		public static void Invoke<T>(this T c, Action<T> doStuff)
			where T:System.Windows.Forms.Control
		{
			if(c.InvokeRequired)
			{
				c.Invoke((EventHandler) delegate { doStuff(c); } );
			}
			else
			{
				doStuff(c);
			}
		}
	}
}
