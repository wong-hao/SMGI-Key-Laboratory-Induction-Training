namespace SMGI.Plugin.CartoExt
{
    partial class FrmLayerResult
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
            this.cmbSelLayerName = new System.Windows.Forms.ComboBox();
            this.SuspendLayout();
            // 
            // cmbSelLayerName
            // 
            this.cmbSelLayerName.FormattingEnabled = true;
            this.cmbSelLayerName.Location = new System.Drawing.Point(95, 80);
            this.cmbSelLayerName.Name = "cmbSelLayerName";
            this.cmbSelLayerName.Size = new System.Drawing.Size(121, 20);
            this.cmbSelLayerName.TabIndex = 0;
            // 
            // FrmLayerResult
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(284, 261);
            this.Controls.Add(this.cmbSelLayerName);
            this.Name = "FrmLayerResult";
            this.Text = "FrmLayerResult";
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.ComboBox cmbSelLayerName;
    }
}