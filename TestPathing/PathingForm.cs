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
		internal event EventHandler	eGenerate;

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
	}
}
