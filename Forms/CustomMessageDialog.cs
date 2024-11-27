using System.Windows.Forms;

namespace ImAdjustr.Forms {
    internal partial class CustomMessageDialog : Form {
        internal CustomMessageDialog(string message, string title) {
            InitializeComponent();
            this.Text = title;
            customMessageLabel.Text = message;
        }

        private void customMessageYesButton_Click(object sender, System.EventArgs e) {
            this.DialogResult = DialogResult.OK;
            this.Close();
        }

    }
}
