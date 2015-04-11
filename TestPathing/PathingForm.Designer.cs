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
			((System.ComponentModel.ISupportInitialize)(this.GridSize)).BeginInit();
			((System.ComponentModel.ISupportInitialize)(this.MobWidth)).BeginInit();
			this.groupBox1.SuspendLayout();
			this.groupBox2.SuspendLayout();
			((System.ComponentModel.ISupportInitialize)(this.MobHeight)).BeginInit();
			this.groupBox3.SuspendLayout();
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
			this.GridSize.Value = new decimal(new int[] {
            16,
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
			this.GenerateGrid.Location = new System.Drawing.Point(6, 45);
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
			this.groupBox1.Location = new System.Drawing.Point(145, 12);
			this.groupBox1.Name = "groupBox1";
			this.groupBox1.Size = new System.Drawing.Size(133, 85);
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
			this.groupBox2.Location = new System.Drawing.Point(12, 103);
			this.groupBox2.Name = "groupBox2";
			this.groupBox2.Size = new System.Drawing.Size(200, 79);
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
			this.groupBox3.Controls.Add(this.GenerateGrid);
			this.groupBox3.Location = new System.Drawing.Point(12, 12);
			this.groupBox3.Name = "groupBox3";
			this.groupBox3.Size = new System.Drawing.Size(127, 81);
			this.groupBox3.TabIndex = 8;
			this.groupBox3.TabStop = false;
			this.groupBox3.Text = "Grid";
			// 
			// PathingForm
			// 
			this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.ClientSize = new System.Drawing.Size(292, 203);
			this.ControlBox = false;
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
	}
}

