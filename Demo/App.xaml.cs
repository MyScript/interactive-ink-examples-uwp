// Copyright @ MyScript. All rights reserved.

using System;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace MyScript.IInk.Demo
{
    sealed partial class App : Application
    {
        public static Engine Engine { get; private set; }

        public App()
        {
            InitializeComponent();
            Suspending += OnSuspending;
            UnhandledException += OnUnhandledException;
        }

        private static void Initialize(Engine engine)
        {
            // Folders "conf" and "resources" are currently parts of the layout
            // (for each conf/res file of the project => properties => "Build Action = content")
            var confDirs = new string[1];
            confDirs[0] = "conf";
            engine.Configuration.SetStringArray("configuration-manager.search-path", confDirs);

            var localFolder = Windows.Storage.ApplicationData.Current.LocalFolder.Path;
            var tempFolder = System.IO.Path.Combine(localFolder, "tmp");
            engine.Configuration.SetString("content-package.temp-folder", tempFolder);

            EnableRawContentInteractivity(engine);
            ConfigureDiagramInteractivity(engine);
            EnableStrokePrediction(engine, true, 16);

            // Configure multithreading for text recognition
            SetMaxRecognitionThreadCount(engine, 1);
        }

        private static void EnableRawContentInteractivity(Engine engine)
        {
            // Display grid background
            engine.Configuration.SetString("raw-content.line-pattern", "grid");

            // Activate handwriting recognition for text only
            engine.Configuration.SetBoolean("raw-content.recognition.text", true);
            engine.Configuration.SetBoolean("raw-content.recognition.shape", false);

            // Allow conversion of text
            engine.Configuration.SetBoolean("raw-content.convert.text", true);
            engine.Configuration.SetBoolean("raw-content.convert.node", false);
            engine.Configuration.SetBoolean("raw-content.convert.edge", false);

            // Allow converting shapes by holding the pen in position
            engine.Configuration.SetBoolean("raw-content.convert.shape-on-hold", true);

            // Configure interactions
            engine.Configuration.SetString("raw-content.interactive-items", "converted-or-mixed");
            engine.Configuration.SetBoolean("raw-content.tap-interactions", true);
            engine.Configuration.SetBoolean("raw-content.eraser.erase-precisely", false);
            engine.Configuration.SetBoolean("raw-content.eraser.dynamic-radius", true);
            engine.Configuration.SetBoolean("raw-content.auto-connection", true);
            var policies = new string[] { "default-with-drag" };
            engine.Configuration.SetStringArray("raw-content.edge.policy", policies);

            // Show alignment guides and snap to them
            engine.Configuration.SetBoolean("raw-content.guides.enable", true);
            engine.Configuration.SetBoolean("raw-content.guides.snap", true);

            // Allow gesture detection
            var gestures = new string[] { "underline", "scratch-out", "strike-through" };
            engine.Configuration.SetStringArray("raw-content.pen.gestures", gestures);

            // Allow shape & image rotation
            var rotations = new string[] { "shape", "image" };
            engine.Configuration.SetStringArray("raw-content.rotation", rotations);
        }

        private static void ConfigureDiagramInteractivity(Engine engine)
        {
            // Allow shape rotation
            var rotations = new string[] { "shape" };
            engine.Configuration.SetStringArray("diagram.rotation", rotations);
        }

        private static void EnableStrokePrediction(Engine engine, bool enable, uint durationMs = 16)
        {
            engine.Configuration.SetBoolean("renderer.prediction.enable", enable);
            engine.Configuration.SetNumber("renderer.prediction.duration", durationMs);
        }
        private static void SetMaxRecognitionThreadCount(Engine engine, uint threadCount)
        {
            engine.Configuration.SetNumber("max-recognition-thread-count", threadCount);
        }

        private static async void OnUnhandledException(object sender, Windows.UI.Xaml.UnhandledExceptionEventArgs e)
        {
            e.Handled = true;
            await ShowErrorDialog(e.Message);
        }

        private static async System.Threading.Tasks.Task<bool> ShowErrorDialog(string message)
        {
            var dialog = new MessageDialog("Error: " + message);

            dialog.Commands.Add(new UICommand("Abort", delegate
            {
                Current.Exit();
            }));

            await dialog.ShowAsync();
            return false;
        }

        protected override async void OnLaunched(LaunchActivatedEventArgs e)
        {
            var rootFrame = Window.Current.Content as Frame;

            try
            {
                // Initialize Interactive Ink runtime environment
                Initialize(Engine = Engine.Create((byte[])(Array)Certificate.MyCertificate.Bytes));
            }
            catch (Exception err)
            {
                await ShowErrorDialog(err.Message);
            }

            if (rootFrame == null)
            {
                rootFrame = new Frame();

                rootFrame.NavigationFailed += OnNavigationFailed;

                Window.Current.Content = rootFrame;
            }

            if (!e.PrelaunchActivated)
            {
                if (rootFrame.Content == null)
                {
                    rootFrame.Navigate(typeof(MainPage), e.Arguments);
                }

                Window.Current.Activate();
            }
        }

        static void OnNavigationFailed(object sender, NavigationFailedEventArgs e)
        {
            throw new Exception("Failed to load Page " + e.SourcePageType.FullName);
        }

        private static void OnSuspending(object sender, SuspendingEventArgs e)
        {
            var deferral = e.SuspendingOperation.GetDeferral();

            deferral.Complete();
        }
    }
}
