// Copyright MyScript. All right reserved.

using MyScript.IInk.UIReferenceImplementation.UserControls;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;

namespace MyScript.IInk.GetStarted
{
    public sealed partial class MainPage : Page
    {
        // Defines the type of content (possible values are: "Text Document", "Text", "Diagram", "Math", and "Drawing")
        private const string PART_TYPE = "Text Document";

        private Engine _engine;

        private Editor _editor
        {
            get
            {
                return UcEditor.Editor;
            }
        }

        public MainPage()
        {
            this.InitializeComponent();

            this.Loaded += UcEditor.UserControl_Loaded;
            this.Loaded += Page_Loaded;
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            _engine = App.Engine;

            // Folders "conf" and "resources" are currently parts of the layout
            // (for each conf/res file of the project => properties => "Build Action = content")
            string[] confDirs = new string[1];
            confDirs[0] = "conf";
            _engine.Configuration.SetStringArray("configuration-manager.search-path", confDirs);

            var localFolder = Windows.Storage.ApplicationData.Current.LocalFolder.Path;
            var tempFolder = System.IO.Path.Combine(localFolder.ToString(), "tmp");
            _engine.Configuration.SetString("content-package.temp-folder", tempFolder);

            // Initialize the editor with the engine
            UcEditor.Engine = _engine;

            // Force pointer to be a pen, for an automatic detection, set InputMode to AUTO
            SetInputMode(InputMode.PEN);

            NewFile();
        }

        private void AppBar_UndoButton_Click(object sender, RoutedEventArgs e)
        {
            _editor.Undo();
        }

        private void AppBar_RedoButton_Click(object sender, RoutedEventArgs e)
        {
            _editor.Redo();
        }

        private void AppBar_ClearButton_Click(object sender, RoutedEventArgs e)
        {
            _editor.Clear();
        }

        private void AppBar_ConvertButton_Click(object sender, RoutedEventArgs e)
        {
            _editor.Convert(null, _editor.GetSupportedTargetConversionStates(null)[0]);
        }

        private void SetInputMode(InputMode inputMode)
        {
            UcEditor.InputMode = inputMode;
            this.autoToggleButton.IsChecked = (inputMode == InputMode.AUTO);
            this.touchPointerToggleButton.IsChecked = (inputMode == InputMode.TOUCH);
            this.editToggleButton.IsChecked = (inputMode == InputMode.PEN);
        }

        private void AppBar_TouchPointerButton_Click(object sender, RoutedEventArgs e)
        {
            if ((bool)((ToggleButton)(sender)).IsChecked)
            {
                SetInputMode(InputMode.TOUCH);
            }
        }

        private void AppBar_EditButton_Click(object sender, RoutedEventArgs e)
        {
            if ((bool)((ToggleButton)(sender)).IsChecked)
            {
                SetInputMode(InputMode.PEN);
            }
        }

        private void AppBar_AutoButton_Click(object sender, RoutedEventArgs e)
        {
            if ((bool)((ToggleButton)(sender)).IsChecked)
            {
                SetInputMode(InputMode.AUTO);
            }
        }

        private void NewFile()
        {
            // Create package and part
            string packageName = MakeUntitledFilename();
            var package = _engine.CreatePackage(packageName);
            var part = package.CreatePart(PART_TYPE);
            _editor.Part = part;
            Title.Text = "Type: " + PART_TYPE;
        }

        private string MakeUntitledFilename()
        {
            var localFolder = Windows.Storage.ApplicationData.Current.LocalFolder.Path;
            int num = 0;
            string name;

            do
            {
                string baseName = "File" + (++num) + ".iink";
                name = System.IO.Path.Combine(localFolder.ToString(), baseName);
            }
            while (System.IO.File.Exists(name));

            return name;
        }
    }
}
