// Copyright MyScript. All right reserved.

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
        private static Engine _engine;
        public static Engine Engine
        {
            get
            {
                return _engine;
            }
        }

        public App()
        {
            this.InitializeComponent();
            this.Suspending += OnSuspending;
            this.UnhandledException += OnUnhandledException;
        }

        private async void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            e.Handled = true;
            await ShowErrorDialog(e.Message);
        }

        private async System.Threading.Tasks.Task<bool> ShowErrorDialog(string message)
        {
            MessageDialog dialog = new MessageDialog("Error: " + message);

            dialog.Commands.Add(new UICommand("Abort", delegate (IUICommand command)
            {
                Application.Current.Exit();
            }));

            await dialog.ShowAsync();
            return false;
        }

        protected override async void OnLaunched(LaunchActivatedEventArgs e)
        {
            Frame rootFrame = Window.Current.Content as Frame;

            try
            {
                // Initialize Interactive Ink runtime environment
                _engine = MyScript.IInk.Engine.Create((byte[])(Array)MyScript.Certificate.MyCertificate.Bytes);
            }
            catch (Exception err)
            {
                await ShowErrorDialog(err.Message);
            }

            if (rootFrame == null)
            {
                rootFrame = new Frame();

                rootFrame.NavigationFailed += OnNavigationFailed;

                if (e.PreviousExecutionState == ApplicationExecutionState.Terminated)
                {

                }

                Window.Current.Content = rootFrame;
            }

            if (e.PrelaunchActivated == false)
            {
                if (rootFrame.Content == null)
                {
                    rootFrame.Navigate(typeof(MainPage), e.Arguments);
                }

                Window.Current.Activate();
            }
        }

        void OnNavigationFailed(object sender, NavigationFailedEventArgs e)
        {
            throw new Exception("Failed to load Page " + e.SourcePageType.FullName);
        }

        private void OnSuspending(object sender, SuspendingEventArgs e)
        {
            var deferral = e.SuspendingOperation.GetDeferral();

            deferral.Complete();
        }
    }
}
