using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace UpgradedTaskList
{
    /// <summary>
    /// Interaction logic for TokenEditor.xaml
    /// </summary>
    public partial class TokenEditor : Window
    {
        private TokenEditorViewModel viewModel;

        /// <summary>
        /// Token reference from the View model
        /// </summary>
        public String Token
        {
            get { return viewModel.Token; }
        }

        /// <summary>
        /// Constructor to setup the view model and set it to the data context
        /// </summary>
        public TokenEditor()
        {
            InitializeComponent();

            // Create the view model and set it to the data context
            viewModel = new TokenEditorViewModel();
            this.DataContext = viewModel;
        }

        private void buttonOK_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
        }

        private void buttonCancel_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
        }
    }
}
