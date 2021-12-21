using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace iSuite
{
    public partial class GenericSingleInputForm : Form
    {
        public string TextBoxContents { get; set; }
        public string Title { get; set; }
        public string LabelText { get; set; }
        public GenericSingleInputForm()
        {
            InitializeComponent();
        }

        private void GenericSingleInputForm_Load(object sender, EventArgs e)
        {
            Text = Title;
            idfk_what_to_name_this.Text = LabelText;
        }

        private void done_Click(object sender, EventArgs e)
        {
            TextBoxContents = input.Text;
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
