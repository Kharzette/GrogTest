using System;
using System.Windows.Forms;
using UtilityLib;
using SharpDX;


namespace TestPathing
{
	internal partial class PathingForm : Form
	{
		OpenFileDialog	mOFD	=new OpenFileDialog();
		SaveFileDialog	mSFD	=new SaveFileDialog();

		bool	mbPickMode, mbPickReady;

		Vector3	mA, mB;

		internal event EventHandler	eGenerate;
		internal event EventHandler	eLoadData;
		internal event EventHandler	eSaveData;
		internal event EventHandler	ePickA;
		internal event EventHandler	ePickB;
		internal event EventHandler	ePickBlock;
		internal event EventHandler	ePickUnBlock;
		internal event EventHandler	eDrawChanged;
		internal event EventHandler	eMobChanged;
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
			ACoords.Text	=aPos.IntStr();
			mA				=aPos;

			mbPickMode	=false;
			Enabled		=true;
		}

		internal void SetCoordB(Vector3 bPos)
		{
			BCoords.Text	=bPos.IntStr();
			mB				=bPos;

			mbPickMode	=false;
			Enabled		=true;
		}

		internal void SetNodeA(int node)
		{
			ANode.Text	="" + node;
		}

		internal void SetNodeB(int node)
		{
			BNode.Text	="" + node;
		}

		internal void SetPickReady(bool bReady)
		{
			mbPickReady	=bReady;

			PickA.Enabled	=bReady;
			PickB.Enabled	=bReady;
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
			Enabled		=false;

			Misc.SafeInvoke(ePickA, null);
		}

		void OnPickB(object sender, EventArgs e)
		{
			if(mbPickMode)
			{
				return;
			}

			mbPickMode	=true;
			Enabled		=false;

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
		
		void OnBoundsChanged(object sender, EventArgs e)
		{
			UInt16	boxWidth	=(UInt16)MobWidth.Value;
			UInt16	boxHeight	=(UInt16)MobHeight.Value;

			UInt32	box	=(UInt32)(boxWidth | (boxHeight << 16));

			Misc.SafeInvoke(eMobChanged, box);
		}

		
		void OnPickUnBlock(object sender, EventArgs e)
		{
			Misc.SafeInvoke(ePickUnBlock, null);
		}


		void OnPickBlock(object sender, EventArgs e)
		{
			Misc.SafeInvoke(ePickBlock, null);
		}
	}
}
