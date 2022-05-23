// Copyright @ MyScript. All rights reserved.

using System;
using System.Linq;
using Windows.Graphics.Display;
using Windows.UI.Xaml;
using MyScript.IInk.UIReferenceImplementation;

namespace MyScript.IInk.GetStarted
{
    public sealed partial class MainPage
    {
        public static readonly DependencyProperty EditorProperty =
            DependencyProperty.Register("Editor", typeof(Editor), typeof(MainPage),
                new PropertyMetadata(default(Editor)));

        public Editor Editor
        {
            get => GetValue(EditorProperty) as Editor;
            set => SetValue(EditorProperty, value);
        }
    }

    public sealed partial class MainPage
    {
        // Offscreen rendering
        private float _dpiX = 96;
        private float _dpiY = 96;

        // Defines the type of content (possible values are: "Text Document", "Text", "Diagram", "Math", "Drawing" and "Raw Content")
        private const string PartType = "Text Document";

        public MainPage()
        {
            InitializeComponent();
            Initialize(App.Engine);
        }

        private void Initialize(Engine engine)
        {
            // Initialize the editor with the engine
            var info = DisplayInformation.GetForCurrentView();
            _dpiX = info.RawDpiX;
            _dpiY = info.RawDpiY;
            var pixelDensity = UcEditor.GetPixelDensity();

            if (pixelDensity > 0.0f)
            {
                _dpiX /= pixelDensity;
                _dpiY /= pixelDensity;
            }

            // RawDpi properties can return 0 when the monitor does not provide physical dimensions and when the user is
            // in a clone or duplicate multiple -monitor setup.
            if (_dpiX == 0 || _dpiY == 0)
                _dpiX = _dpiY = 96;

            var renderer = engine.CreateRenderer(_dpiX, _dpiY, UcEditor);
            renderer.AddListener(new RendererListener(UcEditor));
            var toolController = engine.CreateToolController();
            Initialize(Editor = engine.CreateEditor(renderer, toolController));
            Initialize(Editor.ToolController);

            NewFile();
        }

        private void Initialize(Editor editor)
        {
            editor.SetViewSize((int)ActualWidth, (int)ActualHeight);
            editor.SetFontMetricsProvider(new FontMetricsProvider(_dpiX, _dpiY));
            editor.AddListener(new EditorListener(UcEditor));
        }

        private static void Initialize(ToolController controller)
        {
            controller.SetToolForType(PointerType.MOUSE, PointerTool.PEN);
            controller.SetToolForType(PointerType.PEN, PointerTool.PEN);
            controller.SetToolForType(PointerType.TOUCH, PointerTool.PEN);
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

        private void ClosePackage()
        {
            var part = Editor.Part;
            var package = part?.Package;
            Editor.Part = null;
            part?.Dispose();
            package?.Dispose();
            Title.Text = "";
        }

        private async void NewFile()
        {
            try
            {
                // Close current package
                ClosePackage();

                // Create package and part
                var packageName = MakeUntitledFilename();
                var package = Editor.Engine.CreatePackage(packageName);
                var part = package.CreatePart(PartType);
                Editor.Part = part;
                Title.Text = "Type: " + PartType;
            }
            catch (Exception ex)
            {
                ClosePackage();

                var msgDialog = new Windows.UI.Popups.MessageDialog(ex.ToString());
                await msgDialog.ShowAsync();
                Application.Current.Exit();
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

        private void OnPenClick(object sender, RoutedEventArgs e)
        {
            if (!(Editor?.ToolController is ToolController controller)) return;
            controller.SetToolForType(PointerType.MOUSE, PointerTool.PEN);
            controller.SetToolForType(PointerType.PEN, PointerTool.PEN);
            controller.SetToolForType(PointerType.TOUCH, PointerTool.PEN);
        }

        private void OnTouchClick(object sender, RoutedEventArgs e)
        {
            if (!(Editor?.ToolController is ToolController controller)) return;
            controller.SetToolForType(PointerType.MOUSE, PointerTool.HAND);
            controller.SetToolForType(PointerType.PEN, PointerTool.HAND);
            controller.SetToolForType(PointerType.TOUCH, PointerTool.HAND);
        }

        private void OnAutoClick(object sender, RoutedEventArgs e)
        {
            if (!(Editor?.ToolController is ToolController controller)) return;
            controller.SetToolForType(PointerType.MOUSE, PointerTool.PEN);
            controller.SetToolForType(PointerType.PEN, PointerTool.HAND);
            controller.SetToolForType(PointerType.TOUCH, PointerTool.PEN);
        }
    }
}
