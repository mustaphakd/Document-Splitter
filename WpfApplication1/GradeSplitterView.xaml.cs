using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Telerik.Windows.Controls;

namespace GSPDocumentSpliter
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class GradeSplitterView : RadWindow
    {
        GradeSplitterViewModel _viewModel;
        public GradeSplitterView()
        {
            StyleManager.ApplicationTheme = new Windows8Theme();
            InitializeComponent();
            Icon = new Image() { Source = new BitmapImage(new Uri(@"Images/Icon.png", UriKind.Relative)), Height = 48, Width = 48};
            _viewModel = new GradeSplitterViewModel(System.Windows.Application.Current.Dispatcher);


            _viewModel.OnGetSelectedItems = () =>
            {
                List<String> lst = new List<string>();
                System.Windows.Application.Current.Dispatcher.Invoke(() => { 
                    foreach(var itm in OutputNames.SelectedItems)
                    {
                        lst.Add(itm.ToString());
                    }
                });
                return lst;
            };
            DataContext = _viewModel;
        }

        private void bntFile_Click(object sender, RoutedEventArgs e)
        {
            using(var dialog = new OpenFileDialog())
            {
                dialog.CheckFileExists = true;
                dialog.AutoUpgradeEnabled = true;
                dialog.Multiselect = false;
                dialog.Title = "Please select the file to be split";
                var result = dialog.ShowDialog();

                if (result == System.Windows.Forms.DialogResult.OK)
                    _viewModel.FileName = dialog.FileName;
            }
        }

        private void bntFolder_Click(object sender, RoutedEventArgs e)
        {
            using (var dialog = new FolderBrowserDialog())
            {
                dialog.Description = "Select the folder where generated files will be stored";
                dialog.ShowNewFolderButton = true;
                
                var result = dialog.ShowDialog();

                if (result == System.Windows.Forms.DialogResult.OK)
                    _viewModel.Directory = dialog.SelectedPath;
            }
        }

        private void grdContainer_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            var bndingXpr = NewFileName.GetBindingExpression(System.Windows.Controls.TextBox.TextProperty);
            bndingXpr.UpdateSource();
        }

        private void btnLoadNames_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel.Running)
            {
                System.Windows.MessageBox.Show("Files are being processed!", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            using (var dialog = new OpenFileDialog())
            {
                dialog.CheckFileExists = true;
                dialog.AutoUpgradeEnabled = true;
                dialog.Multiselect = false;
                dialog.Title = "Please select the file from which generated file names will be sourced";
                var result = dialog.ShowDialog();

                if (result == System.Windows.Forms.DialogResult.OK)
                    _viewModel.LoadFileNames(dialog.FileName);
            }
        }

        private void TextBlock_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if(e.ClickCount >= 2)
            {
               System.Diagnostics.Process.Start( ((TextBlock)sender).Text);
                
            }
        }
    }
}
