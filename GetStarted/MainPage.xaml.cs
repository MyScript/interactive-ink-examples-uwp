// Copyright MyScript. All right reserved.

using MyScript.IInk.UIReferenceImplementation.UserControls;
using System;
using System.Linq;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;

namespace MyScript.IInk.GetStarted
{
    public sealed partial class MainPage : Page
    {
        // Defines the type of content (possible values are: "Text Document", "Text", "Diagram", "Math", and "Drawing")
        private const string PartType = "Text Document";

        private Engine _engine;

        private Editor Editor => UcEditor.Editor;

        public MainPage()
        {
            InitializeComponent();

            Loaded += UcEditor.UserControl_Loaded;
            Loaded += Page_Loaded;
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            _engine = App.Engine;

            // Folders "conf" and "resources" are currently parts of the layout
            // (for each conf/res file of the project => properties => "Build Action = content")
            var confDirs = new string[1];
            confDirs[0] = "conf";
            _engine.Configuration.SetStringArray("configuration-manager.search-path", confDirs);

            var localFolder = Windows.Storage.ApplicationData.Current.LocalFolder.Path;
            var tempFolder = System.IO.Path.Combine(localFolder, "tmp");
            _engine.Configuration.SetString("content-package.temp-folder", tempFolder);

            // Initialize the editor with the engine
            UcEditor.Engine = _engine;

            // Force pointer to be a pen, for an automatic detection, set InputMode to AUTO
            SetInputMode(InputMode.PEN);

            NewFile();
        }

        private void AppBar_UndoButton_Click(object sender, RoutedEventArgs e)
        {
            Editor.Undo();
        }

        private void AppBar_RedoButton_Click(object sender, RoutedEventArgs e)
        {
            Editor.Redo();
        }

        private void AppBar_ClearButton_Click(object sender, RoutedEventArgs e)
        {
            Editor.Clear();
        }

        private async void AppBar_ConvertButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var supportedStates = Editor.GetSupportedTargetConversionStates(null);

                if ( (supportedStates != null) && (supportedStates.Count() > 0) )
                  Editor.Convert(null, supportedStates[0]);
            }
            catch (Exception ex)
            {
                var msgDialog = new Windows.UI.Popups.MessageDialog(ex.ToString());
                await msgDialog.ShowAsync();
            }
        }

        private void SetInputMode(InputMode inputMode)
        {
            UcEditor.InputMode = inputMode;
            autoToggleButton.IsChecked = (inputMode == InputMode.AUTO);
            touchPointerToggleButton.IsChecked = (inputMode == InputMode.TOUCH);
            editToggleButton.IsChecked = (inputMode == InputMode.PEN);
        }

        private void AppBar_TouchPointerButton_Click(object sender, RoutedEventArgs e)
        {
            var isChecked = ((ToggleButton)(sender)).IsChecked;
            if (isChecked != null && (bool)isChecked)
            {
                SetInputMode(InputMode.TOUCH);
            }
        }

        private void AppBar_EditButton_Click(object sender, RoutedEventArgs e)
        {
            var isChecked = ((ToggleButton)(sender)).IsChecked;
            if (isChecked != null && (bool)isChecked)
            {
                SetInputMode(InputMode.PEN);
            }
        }

        private void AppBar_AutoButton_Click(object sender, RoutedEventArgs e)
        {
            var isChecked = ((ToggleButton)(sender)).IsChecked;
            if (isChecked != null && (bool)isChecked)
            {
                SetInputMode(InputMode.AUTO);
            }
        }

        private void NewFile()
        {
            // Close current package
            if (Editor.Part != null)
            {
                var part = Editor.Part;
                var package = part?.Package;
                Editor.Part = null;
                part?.Dispose();
                package?.Dispose();
            }

            // Create package and part
            {
                var packageName = MakeUntitledFilename();
                var package = _engine.CreatePackage(packageName);
                var part = package.CreatePart(PartType);
                Editor.Part = part;
                Title.Text = "Type: " + PartType;
            }
        }

        private static string MakeUntitledFilename()
        {
            var localFolder = Windows.Storage.ApplicationData.Current.LocalFolder.Path;
            var num = 0;
            string name;

            do
            {
                var baseName = "File" + (++num) + ".iink";
                name = System.IO.Path.Combine(localFolder, baseName);
            }
            while (System.IO.File.Exists(name));

            return name;
        }
    }
}
