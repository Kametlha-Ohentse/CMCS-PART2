using System.Windows.Controls;
using System.Windows.Input;

namespace CMCS_Prototype
{
    public partial class LecturerView : UserControl
    {
        public LecturerView()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Event handler to restrict input in the Hours Worked TextBox. (Fixes CS1061)
        /// </summary>
        private void NumberValidationTextBox(object sender, TextCompositionEventArgs e)
        {
            var textBox = (TextBox)sender;

            if (char.IsDigit(e.Text, e.Text.Length - 1) || char.IsControl(e.Text, e.Text.Length - 1))
            {
                e.Handled = false;
            }
            else if (e.Text == "." && !textBox.Text.Contains("."))
            {
                e.Handled = false;
            }
            else
            {
                e.Handled = true;
            }
        }
    }
}