// Copyright MyScript. All right reserved.

using MyScript.IInk.UIReferenceImplementation.UserControls;
using System;
using System.Collections.Generic;
using System.Linq;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;

namespace MyScript.IInk.Demo
{
    public sealed partial class MainPage : Page
    {
        private Engine _engine;

        private Editor _editor
        {
            get
            {
                return UcEditor.Editor;
            }
        }

        private Graphics.Point _lastPointerPosition;
        private int _filenameIndex;
        private string _packageName;

        public MainPage()
        {
            this._filenameIndex = 0;
            this._packageName = "";

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

        private void SetInputMode(InputMode inputMode)
        {
            UcEditor.InputMode = inputMode;
            this.penModeToggleButton.IsChecked = (inputMode == InputMode.PEN);
            this.touchModeToggleButton.IsChecked = (inputMode == InputMode.TOUCH);
            this.autoModeToggleButton.IsChecked = (inputMode == InputMode.AUTO);
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

        private void AppBar_PenModeButton_Click(object sender, RoutedEventArgs e)
        {
            if ((bool)((ToggleButton)(sender)).IsChecked)
            {
                SetInputMode(InputMode.PEN);
            }
        }

        private void AppBar_TouchModeButton_Click(object sender, RoutedEventArgs e)
        {
            if ((bool)((ToggleButton)(sender)).IsChecked)
            {
                SetInputMode(InputMode.TOUCH);
            }
        }

        private void AppBar_AutoModeButton_Click(object sender, RoutedEventArgs e)
        {
            if ((bool)((ToggleButton)(sender)).IsChecked)
            {
                SetInputMode(InputMode.AUTO);
            }
        }

        private void AppBar_NewPackageButton_Click(object sender, RoutedEventArgs e)
        {
            NewFile();
        }

        private async void AppBar_NewPartButton_Click(object sender, RoutedEventArgs e)
        {
            if (_editor.Part == null)
            {
                NewFile();
                return;
            }

            string partType = await ChoosePartType(true);

            if (!string.IsNullOrEmpty(partType))
            {
                // Reset viewing parameters
                UcEditor.ResetView(false);

                // Create package and part
                var package = _editor.Part.Package;
                var part = package.CreatePart(partType);
                _editor.Part = part;
                Title.Text = _packageName + " - " + part.Type;
            }
        }

        private void AppBar_PreviousPartButton_Click(object sender, RoutedEventArgs e)
        {
            var part = _editor.Part;

            if (part != null)
            {
                var index = part.Package.IndexOfPart(part);

                if (index > 0)
                {
                    // Reset viewing parameters
                    UcEditor.ResetView(false);

                    // Select new part
                    var newPart = part.Package.GetPart(index - 1);
                    _editor.Part = newPart;
                    Title.Text = _packageName + " - " + newPart.Type;
                }
            }
        }

        private void AppBar_NextPartButton_Click(object sender, RoutedEventArgs e)
        {
            var part = _editor.Part;

            if (part != null)
            {
                var index = part.Package.IndexOfPart(part);

                if (index < part.Package.PartCount - 1)
                {
                    // Reset viewing parameters
                    UcEditor.ResetView(false);

                    // Select new part
                    var newPart = part.Package.GetPart(index + 1);
                    _editor.Part = newPart;
                    Title.Text = _packageName + " - " + newPart.Type;
                }
            }
        }
        private void AppBar_ResetViewButton_Click(object sender, RoutedEventArgs e)
        {
            UcEditor.ResetView(true);
        }

        private void AppBar_ZoomInButton_Click(object sender, RoutedEventArgs e)
        {
            UcEditor.ZoomIn(1);
        }

        private void AppBar_ZoomOutButton_Click(object sender, RoutedEventArgs e)
        {
            UcEditor.ZoomOut(1);
        }

        private async void AppBar_OpenPackageButton_Click(object sender, RoutedEventArgs e)
        {
            string filePath = null;
            string fileName = null;

            // Show open dialog
            {
                var picker = new Windows.Storage.Pickers.FileOpenPicker();
                picker.ViewMode = Windows.Storage.Pickers.PickerViewMode.List;
                picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary;
                picker.FileTypeFilter.Add(".iink");
                picker.FileTypeFilter.Add("*");

                Windows.Storage.StorageFile file = await picker.PickSingleFileAsync();
                if (file != null)
                {
                    filePath = file.Path;
                    fileName = file.Name;
                }
            }

            if (!string.IsNullOrEmpty(filePath))
            {
                // Reset viewing parameters
                UcEditor.ResetView(false);

                // Open package and select first part
                _editor.Part = null;
                var package = _engine.OpenPackage(filePath);
                var part = package.GetPart(0);
                _editor.Part = part;
                _packageName = fileName;
                Title.Text = _packageName + " - " + part.Type;
            }
        }

        private void AppBar_SavePackageButton_Click(object sender, RoutedEventArgs e)
        {
            var part = _editor.Part;

            if (part == null)
                return;

            part.Package.Save();
        }

        private async void AppBar_SaveAsButton_Click(object sender, RoutedEventArgs e)
        {
            string filePath = null;
            string fileName = null;

            // Show save dialog
            {
                var picker = new Windows.Storage.Pickers.FileSavePicker();
                picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary;
                picker.FileTypeChoices.Add("Interactive Ink Document", new List<string>() { ".iink" });

                Windows.Storage.StorageFile file = await picker.PickSaveFileAsync();
                if (file != null)
                {
                    filePath = file.Path;
                    fileName = file.Name;
                }
            }

            if (!string.IsNullOrEmpty(filePath))
            {
                var part = _editor.Part;

                if (part == null)
                    return;

                part.Package.SaveAs(filePath);
                _packageName = fileName;
                Title.Text = _packageName + " - " + part.Type;
            }
        }

        private void UcEditor_RightTapped(object sender, Windows.UI.Xaml.Input.RightTappedRoutedEventArgs e)
        {
            DisplayContextualMenu(e.GetPosition(UcEditor));
        }

        private async void DisplayContextualMenu(Windows.Foundation.Point pos)
        {
            _lastPointerPosition = new Graphics.Point((float)pos.X, (float)pos.Y);

            var contentBlock = _editor.HitBlock(_lastPointerPosition.X, _lastPointerPosition.Y);
            if (contentBlock == null)
                contentBlock = _editor.GetRootBlock();

            var supportedTypes = _editor.SupportedAddBlockTypes;
            bool isContainer = contentBlock.Type == "Container";
            bool isRoot = contentBlock.Id == _editor.GetRootBlock().Id;

            bool displayConvert  = !isContainer;
            bool displayAddBlock = supportedTypes != null && supportedTypes.Count() > 0 && isContainer;
            bool displayAddImage = false; // supportedTypes != null && supportedTypes.Count() > 0 && isContainer;
            bool displayRemove   = !isRoot && !isContainer;
            bool displayCopy     = !isRoot && !isContainer;
            bool displayPaste    = supportedTypes != null && supportedTypes.Count() > 0 && isContainer;
            bool displayImport   = false;
            bool displayExport   = false;

            var addLabel = "Add...";
            var importExportLabel = "Import/Export...";

            var menu = new PopupMenu();

            if (displayConvert)
            {
                var handlerConvert = new Windows.UI.Popups.UICommandInvokedHandler(this.Popup_CommandHandler_Convert);
                menu.Commands.Add(new Windows.UI.Popups.UICommand("Convert", handlerConvert, "Convert"));
            }

            if (displayRemove)
            {
                var handlerRemove = new Windows.UI.Popups.UICommandInvokedHandler(this.Popup_CommandHandler_Remove);
                menu.Commands.Add(new Windows.UI.Popups.UICommand("Remove", handlerRemove, "Remove"));
            }

            if (displayCopy)
            {
                var handlerCopy = new Windows.UI.Popups.UICommandInvokedHandler(this.Popup_CommandHandler_Copy);
                menu.Commands.Add(new Windows.UI.Popups.UICommand("Copy", handlerCopy, "Copy"));
            }

            if (displayPaste)
            {
                var handlerPaste = new Windows.UI.Popups.UICommandInvokedHandler(this.Popup_CommandHandler_Paste);
                menu.Commands.Add(new Windows.UI.Popups.UICommand("Paste", handlerPaste, "Paste"));
            }

            if (displayAddBlock || displayAddImage)
            {
                menu.Commands.Add(new Windows.UI.Popups.UICommand(addLabel, null, addLabel));
            }

            if (displayImport || displayExport)
            {
                menu.Commands.Add(new Windows.UI.Popups.UICommand(importExportLabel, null, importExportLabel));
            }

            var chosenCommand = await menu.ShowAsync(pos);
            if (chosenCommand != null)
            {
                var menu_ = new PopupMenu();
                if (chosenCommand.Label == addLabel)
                {
                    if (displayAddBlock)
                    {
                        var handlerAddBlock = new Windows.UI.Popups.UICommandInvokedHandler(this.Popup_CommandHandler_AddBlock);
                        for (int i = 0; i < supportedTypes.Count(); ++i)
                        {
                            // filter out "Text" block, until we have a popup menu to ask for text data to import
                            if (supportedTypes[i].Equals("Text"))
                                continue;
                            menu_.Commands.Add(new Windows.UI.Popups.UICommand("Add " + supportedTypes[i], handlerAddBlock, supportedTypes[i]));
                        }
                    }

                    if (displayAddImage)
                    {
                        var handlerAddImage = new Windows.UI.Popups.UICommandInvokedHandler(this.Popup_CommandHandler_AddImage);
                        menu_.Commands.Add(new Windows.UI.Popups.UICommand("Add Image", handlerAddImage, "Image"));
                    }
                }
                else if (chosenCommand.Label == importExportLabel)
                {
                    if (displayImport)
                    {
                        var handlerImport = new Windows.UI.Popups.UICommandInvokedHandler(this.Popup_CommandHandler_Import);
                        menu_.Commands.Add(new Windows.UI.Popups.UICommand("Import", handlerImport, "Import"));
                    }

                    if (displayExport)
                    {
                        var handlerExport = new Windows.UI.Popups.UICommandInvokedHandler(this.Popup_CommandHandler_Export);
                        menu_.Commands.Add(new Windows.UI.Popups.UICommand("Export", handlerExport, "Export"));
                    }
                }

                await menu_.ShowAsync(pos);
            }
        }

        private async void Popup_CommandHandler_Convert(Windows.UI.Popups.IUICommand command)
        {
            try
            {
                 var contentBlock = _editor.HitBlock(_lastPointerPosition.X, _lastPointerPosition.Y);
                _editor.Convert(contentBlock, _editor.GetSupportedTargetConversionStates(contentBlock)[0]);
            }
            catch (Exception ex)
            {
                MessageDialog msgDialog = new MessageDialog(ex.ToString());
                await msgDialog.ShowAsync();
            }
        }

        private async void Popup_CommandHandler_AddBlock(Windows.UI.Popups.IUICommand command)
        {
            try
            {
                // Uses Id as block type
                _editor.AddBlock(_lastPointerPosition.X, _lastPointerPosition.Y, command.Id.ToString());
                UcEditor.Invalidate(LayerType.LayerType_ALL);
            }
            catch (Exception ex)
            {
                MessageDialog msgDialog = new MessageDialog(ex.ToString());
                await msgDialog.ShowAsync();
            }
        }

        private void Popup_CommandHandler_AddImage(Windows.UI.Popups.IUICommand command)
        {
            // TODO
        }

        private async void Popup_CommandHandler_Remove(Windows.UI.Popups.IUICommand command)
        {
            try
            {
                var contentBlock = _editor.HitBlock(_lastPointerPosition.X, _lastPointerPosition.Y);
                if (contentBlock != null && contentBlock.Type != "Container")
                  _editor.RemoveBlock(contentBlock);
            }
            catch (Exception ex)
            {
                MessageDialog msgDialog = new MessageDialog(ex.ToString());
                await msgDialog.ShowAsync();
            }
        }

        private async void Popup_CommandHandler_Copy(Windows.UI.Popups.IUICommand command)
        {
            try
            {
                var contentBlock = _editor.HitBlock(_lastPointerPosition.X, _lastPointerPosition.Y);
                _editor.Copy(contentBlock);
            }
            catch (Exception ex)
            {
                MessageDialog msgDialog = new MessageDialog(ex.ToString());
                await msgDialog.ShowAsync();
            }
        }

        private async void Popup_CommandHandler_Paste(Windows.UI.Popups.IUICommand command)
        {
            try
            {
                _editor.Paste(_lastPointerPosition.X, _lastPointerPosition.Y);
            }
            catch (Exception ex)
            {
                MessageDialog msgDialog = new MessageDialog(ex.ToString());
                await msgDialog.ShowAsync();
            }
        }

        private void Popup_CommandHandler_Import(Windows.UI.Popups.IUICommand command)
        {
            // TODO
        }

        private void Popup_CommandHandler_Export(Windows.UI.Popups.IUICommand command)
        {
            // TODO
        }

        private async void NewFile()
        {
            bool cancelable = _editor.Part != null;
            string partType = await ChoosePartType(cancelable);
            if (string.IsNullOrEmpty(partType))
                return;

            string packageName = MakeUntitledFilename();

            // Create package and part
            _editor.Part = null;
            var package = _engine.CreatePackage(packageName);
            var part = package.CreatePart(partType);
            _editor.Part = part;
            _packageName = System.IO.Path.GetFileName(packageName);
            Title.Text = _packageName + " - " + part.Type;
        }

        private string MakeUntitledFilename()
        {
            var localFolder = Windows.Storage.ApplicationData.Current.LocalFolder.Path;
            string name;

            do
            {
                string baseName = "File" + (++_filenameIndex) + ".iink";
                name = System.IO.Path.Combine(localFolder.ToString(), baseName);
            }
            while (System.IO.File.Exists(name));

            return name;
        }


        private async System.Threading.Tasks.Task<string> ChoosePartType(bool cancelable)
        {
            List<string> types = new List<string>();
            foreach (string type in _engine.SupportedPartTypes)
                types.Add(type);

            if (types.Count == 0)
                return null;

            ListView view = new ListView();
            view.ItemsSource = types;
            view.IsItemClickEnabled = true;
            view.SelectionMode = ListViewSelectionMode.Single;
            view.SelectedIndex = -1;

            Grid grid = new Grid();
            grid.Children.Add(view);

            ContentDialog dialog = new ContentDialog
            {
                Title = "Choose type of content",
                Content = grid,
                PrimaryButtonText = "OK",
                SecondaryButtonText = cancelable ? "Cancel" : "",
                IsPrimaryButtonEnabled = false,
                IsSecondaryButtonEnabled = cancelable
            };

            view.ItemClick += (sender, args) => { dialog.IsPrimaryButtonEnabled = true; };
            dialog.PrimaryButtonClick += (sender, args) => { if (view.SelectedIndex < 0) args.Cancel = true; };

            ContentDialogResult result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
                return types[view.SelectedIndex];
            else
                return null;
        }
    }
}
