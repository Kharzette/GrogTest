namespace TestPathing
{
	partial class PathingForm
	{
		/// <summary>
		/// Required designer variable.
		/// </summary>
		private System.ComponentModel.IContainer components = null;

		/// <summary>
		/// Clean up any resources being used.
		/// </summary>
		/// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
		protected override void Dispose(bool disposing)
		{
			if (disposing && (components != null))
			{
				components.Dispose();
			}
			base.Dispose(disposing);
		}

		#region Windows Form Designer generated code

		/// <summary>
		/// Required method for Designer support - do not modify
		/// the contents of this method with the code editor.
		/// </summary>
		private void InitializeComponent()
		{
			this.components = new System.ComponentModel.Container();
			this.GridSize = new System.Windows.Forms.NumericUpDown();
			this.label1 = new System.Windows.Forms.Label();
			this.GenerateGrid = new System.Windows.Forms.Button();
			this.SaveData = new System.Windows.Forms.Button();
			this.LoadData = new System.Windows.Forms.Button();
			this.MobWidth = new System.Windows.Forms.NumericUpDown();
			this.groupBox1 = new System.Windows.Forms.GroupBox();
			this.groupBox2 = new System.Windows.Forms.GroupBox();
			this.label3 = new System.Windows.Forms.Label();
			this.label2 = new System.Windows.Forms.Label();
			this.MobHeight = new System.Windows.Forms.NumericUpDown();
			this.Tips = new System.Windows.Forms.ToolTip(this.components);
			this.groupBox3 = new System.Windows.Forms.GroupBox();
			this.groupBox4 = new System.Windows.Forms.GroupBox();
			this.label6 = new System.Windows.Forms.Label();
			this.ErrorAmount = new System.Windows.Forms.NumericUpDown();
			this.FindPath = new System.Windows.Forms.Button();
			this.PickB = new System.Windows.Forms.Button();
			this.PickA = new System.Windows.Forms.Button();
			this.BCoords = new System.Windows.Forms.TextBox();
			this.label5 = new System.Windows.Forms.Label();
			this.label4 = new System.Windows.Forms.Label();
			this.ACoords = new System.Windows.Forms.TextBox();
			this.groupBox5 = new System.Windows.Forms.GroupBox();
			this.DrawPathConnections = new System.Windows.Forms.CheckBox();
			this.DrawNodeFaces = new System.Windows.Forms.CheckBox();
			this.ANode = new System.Windows.Forms.TextBox();
			this.BNode = new System.Windows.Forms.TextBox();
			((System.ComponentModel.ISupportInitialize)(this.GridSize)).BeginInit();
			((System.ComponentModel.ISupportInitialize)(this.MobWidth)).BeginInit();
			this.groupBox1.SuspendLayout();
			this.groupBox2.SuspendLayout();
			((System.ComponentModel.ISupportInitialize)(this.MobHeight)).BeginInit();
			this.groupBox3.SuspendLayout();
			this.groupBox4.SuspendLayout();
			((System.ComponentModel.ISupportInitialize)(this.ErrorAmount)).BeginInit();
			this.groupBox5.SuspendLayout();
			this.SuspendLayout();
			// 
			// GridSize
			// 
			this.GridSize.Increment = new decimal(new int[] {
            8,
            0,
            0,
            0});
			this.GridSize.Location = new System.Drawing.Point(6, 19);
			this.GridSize.Maximum = new decimal(new int[] {
            1024,
            0,
            0,
            0});
			this.GridSize.Minimum = new decimal(new int[] {
            8,
            0,
            0,
            0});
			this.GridSize.Name = "GridSize";
			this.GridSize.Size = new System.Drawing.Size(53, 20);
			this.GridSize.TabIndex = 0;
			this.Tips.SetToolTip(this.GridSize, "Small sizes can take a long time to compute");
			this.GridSize.Value = new decimal(new int[] {
            32,
            0,
            0,
            0});
			// 
			// label1
			// 
			this.label1.AutoSize = true;
			this.label1.Location = new System.Drawing.Point(65, 21);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(49, 13);
			this.label1.TabIndex = 1;
			this.label1.Text = "Grid Size";
			// 
			// GenerateGrid
			// 
			this.GenerateGrid.Location = new System.Drawing.Point(6, 71);
			this.GenerateGrid.Name = "GenerateGrid";
			this.GenerateGrid.Size = new System.Drawing.Size(66, 23);
			this.GenerateGrid.TabIndex = 2;
			this.GenerateGrid.Text = "Generate";
			this.GenerateGrid.UseVisualStyleBackColor = true;
			this.GenerateGrid.Click += new System.EventHandler(this.OnGenerate);
			// 
			// SaveData
			// 
			this.SaveData.Location = new System.Drawing.Point(6, 48);
			this.SaveData.Name = "SaveData";
			this.SaveData.Size = new System.Drawing.Size(92, 23);
			this.SaveData.TabIndex = 3;
			this.SaveData.Text = "Save PathData";
			this.SaveData.UseVisualStyleBackColor = true;
			this.SaveData.Click += new System.EventHandler(this.OnSavePathData);
			// 
			// LoadData
			// 
			this.LoadData.Location = new System.Drawing.Point(6, 19);
			this.LoadData.Name = "LoadData";
			this.LoadData.Size = new System.Drawing.Size(92, 23);
			this.LoadData.TabIndex = 4;
			this.LoadData.Text = "Load PathData";
			this.LoadData.UseVisualStyleBackColor = true;
			this.LoadData.Click += new System.EventHandler(this.OnLoadPathData);
			// 
			// MobWidth
			// 
			this.MobWidth.Location = new System.Drawing.Point(6, 19);
			this.MobWidth.Maximum = new decimal(new int[] {
            1024,
            0,
            0,
            0});
			this.MobWidth.Minimum = new decimal(new int[] {
            4,
            0,
            0,
            0});
			this.MobWidth.Name = "MobWidth";
			this.MobWidth.Size = new System.Drawing.Size(56, 20);
			this.MobWidth.TabIndex = 5;
			this.Tips.SetToolTip(this.MobWidth, "16 - 24 is about standard human size");
			this.MobWidth.Value = new decimal(new int[] {
            16,
            0,
            0,
            0});
			// 
			// groupBox1
			// 
			this.groupBox1.Controls.Add(this.SaveData);
			this.groupBox1.Controls.Add(this.LoadData);
			this.groupBox1.Location = new System.Drawing.Point(157, 12);
			this.groupBox1.Name = "groupBox1";
			this.groupBox1.Size = new System.Drawing.Size(116, 82);
			this.groupBox1.TabIndex = 6;
			this.groupBox1.TabStop = false;
			this.groupBox1.Text = "File IO";
			// 
			// groupBox2
			// 
			this.groupBox2.Controls.Add(this.label3);
			this.groupBox2.Controls.Add(this.label2);
			this.groupBox2.Controls.Add(this.MobHeight);
			this.groupBox2.Controls.Add(this.MobWidth);
			this.groupBox2.Location = new System.Drawing.Point(12, 121);
			this.groupBox2.Name = "groupBox2";
			this.groupBox2.Size = new System.Drawing.Size(173, 79);
			this.groupBox2.TabIndex = 7;
			this.groupBox2.TabStop = false;
			this.groupBox2.Text = "Mobile";
			// 
			// label3
			// 
			this.label3.AutoSize = true;
			this.label3.Location = new System.Drawing.Point(68, 47);
			this.label3.Name = "label3";
			this.label3.Size = new System.Drawing.Size(90, 13);
			this.label3.TabIndex = 8;
			this.label3.Text = "BoundBox Height";
			// 
			// label2
			// 
			this.label2.AutoSize = true;
			this.label2.Location = new System.Drawing.Point(68, 21);
			this.label2.Name = "label2";
			this.label2.Size = new System.Drawing.Size(87, 13);
			this.label2.TabIndex = 7;
			this.label2.Text = "BoundBox Width";
			// 
			// MobHeight
			// 
			this.MobHeight.Location = new System.Drawing.Point(6, 45);
			this.MobHeight.Name = "MobHeight";
			this.MobHeight.Size = new System.Drawing.Size(56, 20);
			this.MobHeight.TabIndex = 6;
			this.Tips.SetToolTip(this.MobHeight, "50 - 72 is about standard human size");
			this.MobHeight.Value = new decimal(new int[] {
            50,
            0,
            0,
            0});
			// 
			// groupBox3
			// 
			this.groupBox3.Controls.Add(this.GridSize);
			this.groupBox3.Controls.Add(this.label1);
			this.groupBox3.Controls.Add(this.label6);
			this.groupBox3.Controls.Add(this.GenerateGrid);
			this.groupBox3.Controls.Add(this.ErrorAmount);
			this.groupBox3.Location = new System.Drawing.Point(12, 12);
			this.groupBox3.Name = "groupBox3";
			this.groupBox3.Size = new System.Drawing.Size(139, 103);
			this.groupBox3.TabIndex = 8;
			this.groupBox3.TabStop = false;
			this.groupBox3.Text = "Grid";
			// 
			// groupBox4
			// 
			this.groupBox4.Controls.Add(this.BNode);
			this.groupBox4.Controls.Add(this.ANode);
			this.groupBox4.Controls.Add(this.FindPath);
			this.groupBox4.Controls.Add(this.PickB);
			this.groupBox4.Controls.Add(this.PickA);
			this.groupBox4.Controls.Add(this.BCoords);
			this.groupBox4.Controls.Add(this.label5);
			this.groupBox4.Controls.Add(this.label4);
			this.groupBox4.Controls.Add(this.ACoords);
			this.groupBox4.Location = new System.Drawing.Point(12, 206);
			this.groupBox4.Name = "groupBox4";
			this.groupBox4.Size = new System.Drawing.Size(298, 122);
			this.groupBox4.TabIndex = 9;
			this.groupBox4.TabStop = false;
			this.groupBox4.Text = "Find a Path";
			// 
			// label6
			// 
			this.label6.AutoSize = true;
			this.label6.Location = new System.Drawing.Point(65, 47);
			this.label6.Name = "label6";
			this.label6.Size = new System.Drawing.Size(68, 13);
			this.label6.TabIndex = 8;
			this.label6.Text = "Error Amount";
			// 
			// ErrorAmount
			// 
			this.ErrorAmount.DecimalPlaces = 1;
			this.ErrorAmount.Increment = new decimal(new int[] {
            1,
            0,
            0,
            65536});
			this.ErrorAmount.Location = new System.Drawing.Point(6, 45);
			this.ErrorAmount.Maximum = new decimal(new int[] {
            32,
            0,
            0,
            0});
			this.ErrorAmount.Minimum = new decimal(new int[] {
            1,
            0,
            0,
            65536});
			this.ErrorAmount.Name = "ErrorAmount";
			this.ErrorAmount.Size = new System.Drawing.Size(53, 20);
			this.ErrorAmount.TabIndex = 7;
			this.ErrorAmount.Value = new decimal(new int[] {
            2,
            0,
            0,
            0});
			// 
			// FindPath
			// 
			this.FindPath.Location = new System.Drawing.Point(115, 80);
			this.FindPath.Name = "FindPath";
			this.FindPath.Size = new System.Drawing.Size(75, 27);
			this.FindPath.TabIndex = 6;
			this.FindPath.Text = "Find Path";
			this.FindPath.UseVisualStyleBackColor = true;
			this.FindPath.Click += new System.EventHandler(this.OnFindPath);
			// 
			// PickB
			// 
			this.PickB.Location = new System.Drawing.Point(228, 46);
			this.PickB.Name = "PickB";
			this.PickB.Size = new System.Drawing.Size(64, 23);
			this.PickB.TabIndex = 5;
			this.PickB.Text = "Pick B";
			this.PickB.UseVisualStyleBackColor = true;
			this.PickB.Click += new System.EventHandler(this.OnPickB);
			// 
			// PickA
			// 
			this.PickA.Location = new System.Drawing.Point(228, 17);
			this.PickA.Name = "PickA";
			this.PickA.Size = new System.Drawing.Size(64, 23);
			this.PickA.TabIndex = 4;
			this.PickA.Text = "Pick A";
			this.PickA.UseVisualStyleBackColor = true;
			this.PickA.Click += new System.EventHandler(this.OnPickA);
			// 
			// BCoords
			// 
			this.BCoords.Location = new System.Drawing.Point(62, 48);
			this.BCoords.Name = "BCoords";
			this.BCoords.ReadOnly = true;
			this.BCoords.Size = new System.Drawing.Size(96, 20);
			this.BCoords.TabIndex = 3;
			// 
			// label5
			// 
			this.label5.AutoSize = true;
			this.label5.Location = new System.Drawing.Point(6, 51);
			this.label5.Name = "label5";
			this.label5.Size = new System.Drawing.Size(50, 13);
			this.label5.TabIndex = 2;
			this.label5.Text = "B Coords";
			// 
			// label4
			// 
			this.label4.AutoSize = true;
			this.label4.Location = new System.Drawing.Point(6, 22);
			this.label4.Name = "label4";
			this.label4.Size = new System.Drawing.Size(50, 13);
			this.label4.TabIndex = 1;
			this.label4.Text = "A Coords";
			// 
			// ACoords
			// 
			this.ACoords.Location = new System.Drawing.Point(62, 19);
			this.ACoords.Name = "ACoords";
			this.ACoords.ReadOnly = true;
			this.ACoords.Size = new System.Drawing.Size(96, 20);
			this.ACoords.TabIndex = 0;
			// 
			// groupBox5
			// 
			this.groupBox5.Controls.Add(this.DrawPathConnections);
			this.groupBox5.Controls.Add(this.DrawNodeFaces);
			this.groupBox5.Location = new System.Drawing.Point(191, 121);
			this.groupBox5.Name = "groupBox5";
			this.groupBox5.Size = new System.Drawing.Size(119, 79);
			this.groupBox5.TabIndex = 10;
			this.groupBox5.TabStop = false;
			this.groupBox5.Text = "Draw Options";
			// 
			// DrawPathConnections
			// 
			this.DrawPathConnections.AutoSize = true;
			this.DrawPathConnections.Checked = true;
			this.DrawPathConnections.CheckState = System.Windows.Forms.CheckState.Checked;
			this.DrawPathConnections.Location = new System.Drawing.Point(6, 42);
			this.DrawPathConnections.Name = "DrawPathConnections";
			this.DrawPathConnections.Size = new System.Drawing.Size(110, 17);
			this.DrawPathConnections.TabIndex = 1;
			this.DrawPathConnections.Text = "Path Connections";
			this.DrawPathConnections.UseVisualStyleBackColor = true;
			this.DrawPathConnections.CheckedChanged += new System.EventHandler(this.OnDrawChanged);
			// 
			// DrawNodeFaces
			// 
			this.DrawNodeFaces.AutoSize = true;
			this.DrawNodeFaces.Checked = true;
			this.DrawNodeFaces.CheckState = System.Windows.Forms.CheckState.Checked;
			this.DrawNodeFaces.Location = new System.Drawing.Point(6, 19);
			this.DrawNodeFaces.Name = "DrawNodeFaces";
			this.DrawNodeFaces.Size = new System.Drawing.Size(84, 17);
			this.DrawNodeFaces.TabIndex = 0;
			this.DrawNodeFaces.Text = "Node Faces";
			this.DrawNodeFaces.UseVisualStyleBackColor = true;
			this.DrawNodeFaces.CheckedChanged += new System.EventHandler(this.OnDrawChanged);
			// 
			// ANode
			// 
			this.ANode.Location = new System.Drawing.Point(164, 19);
			this.ANode.Name = "ANode";
			this.ANode.ReadOnly = true;
			this.ANode.Size = new System.Drawing.Size(58, 20);
			this.ANode.TabIndex = 9;
			// 
			// BNode
			// 
			this.BNode.Location = new System.Drawing.Point(164, 48);
			this.BNode.Name = "BNode";
			this.BNode.ReadOnly = true;
			this.BNode.Size = new System.Drawing.Size(58, 20);
			this.BNode.TabIndex = 10;
			// 
			// PathingForm
			// 
			this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.ClientSize = new System.Drawing.Size(321, 338);
			this.ControlBox = false;
			this.Controls.Add(this.groupBox5);
			this.Controls.Add(this.groupBox4);
			this.Controls.Add(this.groupBox3);
			this.Controls.Add(this.groupBox2);
			this.Controls.Add(this.groupBox1);
			this.Name = "PathingForm";
			this.Text = "Path Stuff";
			((System.ComponentModel.ISupportInitialize)(this.GridSize)).EndInit();
			((System.ComponentModel.ISupportInitialize)(this.MobWidth)).EndInit();
			this.groupBox1.ResumeLayout(false);
			this.groupBox2.ResumeLayout(false);
			this.groupBox2.PerformLayout();
			((System.ComponentModel.ISupportInitialize)(this.MobHeight)).EndInit();
			this.groupBox3.ResumeLayout(false);
			this.groupBox3.PerformLayout();
			this.groupBox4.ResumeLayout(false);
			this.groupBox4.PerformLayout();
			((System.ComponentModel.ISupportInitialize)(this.ErrorAmount)).EndInit();
			this.groupBox5.ResumeLayout(false);
			this.groupBox5.PerformLayout();
			this.ResumeLayout(false);

		}

		#endregion

		private System.Windows.Forms.NumericUpDown GridSize;
		private System.Windows.Forms.Label label1;
		private System.Windows.Forms.Button GenerateGrid;
		private System.Windows.Forms.Button SaveData;
		private System.Windows.Forms.Button LoadData;
		private System.Windows.Forms.NumericUpDown MobWidth;
		private System.Windows.Forms.GroupBox groupBox1;
		private System.Windows.Forms.GroupBox groupBox2;
		private System.Windows.Forms.Label label3;
		private System.Windows.Forms.Label label2;
		private System.Windows.Forms.NumericUpDown MobHeight;
		private System.Windows.Forms.ToolTip Tips;
		private System.Windows.Forms.GroupBox groupBox3;
		private System.Windows.Forms.GroupBox groupBox4;
		private System.Windows.Forms.Button FindPath;
		private System.Windows.Forms.Button PickB;
		private System.Windows.Forms.Button PickA;
		private System.Windows.Forms.TextBox BCoords;
		private System.Windows.Forms.Label label5;
		private System.Windows.Forms.Label label4;
		private System.Windows.Forms.TextBox ACoords;
		private System.Windows.Forms.GroupBox groupBox5;
		private System.Windows.Forms.CheckBox DrawPathConnections;
		private System.Windows.Forms.CheckBox DrawNodeFaces;
		private System.Windows.Forms.Label label6;
		private System.Windows.Forms.NumericUpDown ErrorAmount;
		private System.Windows.Forms.TextBox BNode;
		private System.Windows.Forms.TextBox ANode;
	}
}

