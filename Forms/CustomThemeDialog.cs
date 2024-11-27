using System;
using System.Linq;
using System.Drawing;
using System.Windows.Forms;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace ImAdjustr.Forms {
    internal partial class CustomThemeDialog : Form {

        internal List<dynamic> returnValues = new List<dynamic>();
        private readonly string[] inputHints = { "R", "RG", "RB", "RI", "GR", "G", "GB", "GI", "BR", "BG", "B", "BI" };
        private int currentHintIndex = 0;
        internal CustomThemeDialog() {
            InitializeComponent();
        }
        internal CustomThemeDialog(List<dynamic> initialValues) {
            InitializeComponent();
            InitialiseFromList(initialValues);
        }

        internal void InitialiseFromList(List<dynamic> list) {
            if (list != null && list.Count == 12) {
                customThemeInput1.Text = list[0].ToString();
                customThemeInput2.Text = list[1].ToString();
                customThemeInput3.Text = list[2].ToString();
                customThemeInput4.Text = list[3].ToString();
                customThemeInput5.Text = list[4].ToString();
                customThemeInput6.Text = list[5].ToString();
                customThemeInput7.Text = list[6].ToString();
                customThemeInput8.Text = list[7].ToString();
                customThemeInput9.Text = list[8].ToString();
                customThemeInput10.Text = list[9].ToString();
                customThemeInput11.Text = list[10].ToString();
                customThemeInput12.Text = list[11].ToString();
                customThemeInputField.Text = string.Join(", ", list);
            }
        }

        internal bool GetValuesFromTextBoxes() {
            if (!customThemeInputToggle.Checked) {
                MaskedTextBox[] inputs = { customThemeInput1, customThemeInput2, customThemeInput3, customThemeInput4,
                                          customThemeInput5, customThemeInput6, customThemeInput7, customThemeInput8,
                                          customThemeInput9, customThemeInput10, customThemeInput11, customThemeInput12};
                for (int i = 0; i < inputs.Length; i++) {
                    if (float.TryParse(inputs[i].Text, out float value)) returnValues.Add(value);
                    else if (i == 3 || i == 7 || i == 11) returnValues.Add(1);
                    else returnValues.Add(0);
                }
            }
            else {
                if (customThemeInputField.Text.Any(char.IsLetter)) return false;
                if (customThemeInputField.Text != null) {
                    var inputs = customThemeInputField.Text.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    for (int i = 0; i < inputs.Length; i++) {
                        if (float.TryParse(inputs[i], out float result)) returnValues.Add(result);
                        else if (i == 3 || i == 7 || i == 11) returnValues.Add(1);
                        else returnValues.Add(0f);
                    }
                }
                while (returnValues.Count < 12) returnValues.Add(0f);
            }
            return true; // Successfully parsed all values
        }

        internal async void CustomThemeOkButton_Click(object sender, EventArgs e) {
            if (GetValuesFromTextBoxes()) {
                this.DialogResult = DialogResult.OK;
                this.Close(); // Close the dialog
            }
            else {
                customThemeInvalidLabel.Show();
                await Task.Delay(3000);
                customThemeInvalidLabel.Hide();
            }
        }

        internal void CustomThemeMaskedTextBox_KeyPress(object sender, KeyPressEventArgs e) {
            // Allow only one decimal point
            var currentText = (sender as MaskedTextBox).Text;
            string newText = currentText + e.KeyChar;
            if (newText.Count(c => c == '.') > 1) {
                e.Handled = true;
                return;
            }
            // Allow control keys (e.g., Backspace)
            if (e.KeyChar == (char)Keys.Back || e.KeyChar == (char)Keys.Delete) return;
            string pattern = @"^(|(9(\.99)?|[0-8](\.\d{1,2})?|[1-9](\.\d{0,2})?|0(\.\d{1,2})?))$";
            bool isNegative = newText[0] == '-';
            if (isNegative && newText.Length == 1) return;
            Regex regex = new Regex(pattern);
            if (isNegative) newText = newText.Substring(1);
            if (!regex.IsMatch(newText)) e.Handled = true;
        }

        private void CustomThemeInputField_KeyPress(object sender, KeyPressEventArgs e) {
            // Ignore new input once all hints are displayed
            var validInputs = new char[] { '-', '.', ',', (char)Keys.Space, (char)Keys.Back, (char)Keys.Delete, (char)Keys.ControlKey};
            if (currentHintIndex >= inputHints.Length && !(char.IsControl(e.KeyChar))) e.Handled = true;
            if (char.IsDigit(e.KeyChar)) return;
            if (validInputs.Contains(e.KeyChar)) return;
            e.Handled = true;
        }

        private void CustomThemeInputField_TextChanged(object sender, EventArgs e) {
            string text = customThemeInputField.Text.Trim();
            string[] inputs = text.Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries);
            customThemeHintLabel.Text = ""; // Reset the label text
            currentHintIndex = 0;           // Reset the tag index
            // Append corresponding tags for the entered values
            foreach (var input in inputs) {
                if (currentHintIndex < inputHints.Length) {
                    customThemeHintLabel.Text += inputHints[currentHintIndex] + " ";
                    currentHintIndex++;
                }
                else break; // Ignore any additional inputs beyond the 12 values
            }
            customThemeHintLabel.Text = customThemeHintLabel.Text.Trim(); // Remove trailing space
        }

        private void CustomThemeInputToggle_CheckedChanged(object sender, System.EventArgs e) {
            if ((sender as CheckBox).Checked) {
                customThemeInputPanel.Visible = false;
                customThemeInputField.Parent = this;
                customThemeHintLabel.Parent = this;
                customThemeInputField.Location = new Point(customThemeInputPanel.Location.X, 40);
                customThemeHintLabel.Location = new Point(customThemeInputField.Location.X, 15);
                customThemeInputField.Visible = true;
                customThemeHintLabel.Visible = true;
            }
            else {
                customThemeInputField.Visible = false;
                customThemeHintLabel.Visible = false;
                customThemeInputPanel.Visible = true;
            }
        }
    }
}
