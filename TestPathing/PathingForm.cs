using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

using UtilityLib;

using SharpDX;


namespace TestPathing
{
	internal partial class PathingForm : Form
	{
		OpenFileDialog	mOFD	=new OpenFileDialog();
		SaveFileDialog	mSFD	=new SaveFileDialog();

		bool	mbPickMode;

		Vector3	mA, mB;

		internal event EventHandler	eGenerate;
		internal event EventHandler	eLoadData;
		internal event EventHandler	eSaveData;
		internal event EventHandler	ePickA;
		internal event EventHandler	ePickB;
		internal event EventHandler	eDrawChanged;
		internal event EventHandler	eFindPath;


		internal PathingForm()
		{
			InitializeComponent();
		}

		internal int GetGridSize()
		{
			return	(int)GridSize.Value;
		}

		internal void SetCoordA(Vector3 aPos)
		{
			ACoords.Text	=IntVector(aPos);
			mA				=aPos;

			mbPickMode	=false;
		}

		internal void SetCoordB(Vector3 bPos)
		{
			BCoords.Text	=IntVector(bPos);
			mB				=bPos;

			mbPickMode	=false;
		}

		internal void SetNodeA(int node)
		{
			ANode.Text	="" + node;
		}

		internal void SetNodeB(int node)
		{
			BNode.Text	="" + node;
		}


		string	IntVector(Vector3 vec)
		{
			return	"" + (int)vec.X + ", " + (int)vec.Y + ", " + (int)vec.Z;
		}


		void OnGenerate(object sender, EventArgs e)
		{
			Misc.SafeInvoke(eGenerate, (float)ErrorAmount.Value);
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

		void OnPickA(object sender, EventArgs e)
		{
			if(mbPickMode)
			{
				return;
			}

			mbPickMode	=true;

			Misc.SafeInvoke(ePickA, null);
		}

		void OnPickB(object sender, EventArgs e)
		{
			if(mbPickMode)
			{
				return;
			}

			mbPickMode	=true;

			Misc.SafeInvoke(ePickB, null);
		}

		void OnFindPath(object sender, EventArgs e)
		{
			Misc.SafeInvoke(eFindPath, (float)ErrorAmount.Value, new Vector3PairEventArgs(mA, mB));
		}

		void OnDrawChanged(object sender, EventArgs e)
		{
			int	gack	=0;

			gack	|=(DrawNodeFaces.Checked)? 1 : 0;
			gack	|=(DrawPathConnections.Checked)? 2 : 0;

			Misc.SafeInvoke(eDrawChanged, gack);
		}
	}
}
