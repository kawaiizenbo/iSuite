namespace iSuite
{
    partial class GenericSingleInputForm
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
            this.idfk_what_to_name_this = new System.Windows.Forms.Label();
            this.input = new System.Windows.Forms.TextBox();
            this.done = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // idfk_what_to_name_this
            // 
            this.idfk_what_to_name_this.AutoSize = true;
            this.idfk_what_to_name_this.Location = new System.Drawing.Point(12, 9);
            this.idfk_what_to_name_this.Name = "idfk_what_to_name_this";
            this.idfk_what_to_name_this.Size = new System.Drawing.Size(106, 15);
            this.idfk_what_to_name_this.TabIndex = 0;
            this.idfk_what_to_name_this.Text = "should not see this";
            // 
            // input
            // 
            this.input.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.input.Location = new System.Drawing.Point(12, 27);
            this.input.Name = "input";
            this.input.Size = new System.Drawing.Size(250, 23);
            this.input.TabIndex = 1;
            // 
            // done
            // 
            this.done.FlatStyle = System.Windows.Forms.FlatStyle.Popup;
            this.done.Location = new System.Drawing.Point(187, 56);
            this.done.Name = "done";
            this.done.Size = new System.Drawing.Size(75, 23);
            this.done.TabIndex = 2;
            this.done.Text = "OK";
            this.done.UseVisualStyleBackColor = true;
            this.done.Click += new System.EventHandler(this.done_Click);
            // 
            // GenericSingleInputForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(274, 91);
            this.Controls.Add(this.done);
            this.Controls.Add(this.input);
            this.Controls.Add(this.idfk_what_to_name_this);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.Name = "GenericSingleInputForm";
            this.Text = "should not see this";
            this.Load += new System.EventHandler(this.GenericSingleInputForm_Load);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label idfk_what_to_name_this;
        private System.Windows.Forms.TextBox input;
        private System.Windows.Forms.Button done;
    }
}