using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using UtilityLib;


namespace TestPathing
{
	internal partial class PathingForm : Form
	{
		OpenFileDialog	mOFD	=new OpenFileDialog();
		SaveFileDialog	mSFD	=new SaveFileDialog();

		internal event EventHandler	eGenerate;
		internal event EventHandler	eLoadData;
		internal event EventHandler	eSaveData;

		internal PathingForm()
		{
			InitializeComponent();
		}

		internal int GetGridSize()
		{
			return	(int)GridSize.Value;
		}

		void OnGenerate(object sender, EventArgs e)
		{
			Misc.SafeInvoke(eGenerate, null);
		}


		void OnLoadPathData(object sender, EventArgs e)
		{
			mOFD.DefaultExt	="*.PathData";
			mOFD.Filter		="Path data files (*.PathData)|*.PathData|All files (*.*)|*.*";
			DialogResult	dr	=mOFD.ShowDialog();

			if(dr == DialogResult.Cancel)
			{
				return;
			}

			Misc.SafeInvoke(eLoadData, mOFD.FileName);
		}


		void OnSavePathData(object sender, EventArgs e)
		{
			mSFD.DefaultExt	="*.PathData";
			mSFD.Filter		="Path data files (*.PathData)|*.PathData|All files (*.*)|*.*";

			DialogResult	dr	=mSFD.ShowDialog();

			if(dr == DialogResult.Cancel)
			{
				return;
			}

			Misc.SafeInvoke(eSaveData, mSFD.FileName);
		}
	}
}
