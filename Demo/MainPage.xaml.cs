// Copyright @ MyScript. All rights reserved.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Windows.Graphics.Display;
using Windows.Storage;
using Windows.System;
using Windows.UI.Core;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

using MyScript.IInk.UIReferenceImplementation;
using AvailableActions = MyScript.IInk.UIReferenceImplementation.UserControls.EditorUserControl.ContextualActions;

namespace MyScript.IInk.Demo
{
    public class FlyoutCommand : System.Windows.Input.ICommand
    {
        public delegate void InvokedHandler(FlyoutCommand command);

        public string Id { get; set; }
        private InvokedHandler _handler = null;

        public FlyoutCommand(string id, InvokedHandler handler)
        {
            Id = id;
            _handler = handler;
        }

        public bool CanExecute(object parameter)
        {
            return _handler != null;
        }

        public void Execute(object parameter)
        {
            _handler(this);
        }

        public event EventHandler CanExecuteChanged;
    }

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
        private Graphics.Point _lastPointerPosition;
        private IContentSelection _lastContentSelection;

        private int _filenameIndex;
        private string _packageName;

        // Offscreen rendering
        private float _dpiX = 96;
        private float _dpiY = 96;

        public MainPage()
        {
            _filenameIndex = 0;
            _packageName = "";

            InitializeComponent();
            Initialize(App.Engine);
            KeyDown +=  Page_KeyDown;
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

            FontMetricsProvider.Initialize();

            var renderer = engine.CreateRenderer(_dpiX, _dpiY, UcEditor);
            renderer.AddListener(new RendererListener(UcEditor));
            var toolController = engine.CreateToolController();
            Initialize(Editor = engine.CreateEditor(renderer, toolController));

            UcEditor.SmartGuide.MoreClicked += ShowSmartGuideMenu;

            NewFile();
        }

        private void Initialize(Editor editor)
        {
            editor.SetViewSize((int)ActualWidth, (int)ActualHeight);
            editor.SetFontMetricsProvider(new FontMetricsProvider(_dpiX, _dpiY));
            editor.AddListener(new EditorListener(UcEditor));

            // see https://developer.myscript.com/docs/interactive-ink/latest/reference/styling for styling reference
            editor.Theme =
                "glyph {" +
                "  font-family: MyScriptInter;" +
                "}" +
                ".math {" +
                "  font-family: STIX;" +
                "}" +
                ".math-variable {" +
                "  font-style: italic;" +
                "};";
        }

        private void ResetSelection()
        {
            if (_lastContentSelection != null)
            {
                var contentBlock = _lastContentSelection as ContentBlock;
                if (contentBlock != null)
                    contentBlock.Dispose();
                else
                {
                    var contentSelection = _lastContentSelection as ContentSelection;
                    contentSelection?.Dispose();
                }
                _lastContentSelection = null;
            }
        }

        private void Page_KeyDown(object sender, Windows.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            var ctrlKey = Window.Current.CoreWindow.GetKeyState(VirtualKey.Control);
            var shftKey = Window.Current.CoreWindow.GetKeyState(VirtualKey.Shift);

            var ctrl = ctrlKey.HasFlag(CoreVirtualKeyStates.Down);
            var shft = shftKey.HasFlag(CoreVirtualKeyStates.Down);

            if (e.Key == VirtualKey.Z)
            {
                if (ctrl)
                {
                    if (shft)
                        Editor.Redo();
                    else
                        Editor.Undo();

                    e.Handled = true;
                }
            }
            else
            if (e.Key == VirtualKey.Y)
            {
                if (ctrl && !shft)
                {
                    Editor.Redo();
                    e.Handled = true;
                }
            }
        }

        private void AppBar_UndoButton_Click(object sender, RoutedEventArgs e)
        {
            Editor.Undo();
        }

        private void AppBar_RedoButton_Click(object sender, RoutedEventArgs e)
        {
            Editor.Redo();
        }

        private async void AppBar_ClearButton_Click(object sender, RoutedEventArgs e)
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
                var msgDialog = new MessageDialog(ex.ToString());
                await msgDialog.ShowAsync();
            }
        }

        private void AppBar_NewPackageButton_Click(object sender, RoutedEventArgs e)
        {
            NewFile();
        }

        private async void AppBar_NewPartButton_Click(object sender, RoutedEventArgs e)
        {
            if (Editor.Part == null)
            {
                NewFile();
                return;
            }

            var partType = await ChoosePartType(true);

            if (!string.IsNullOrEmpty(partType))
            {
                ResetSelection();

                var previousPart = Editor.Part;
                var package = previousPart.Package;

                try
                {
                    Editor.Part = null;

                    var part = package.CreatePart(partType);
                    Editor.Part = part;
                    Title.Text = _packageName + " - " + part.Type;

                    previousPart.Dispose();
                }
                catch (Exception ex)
                {
                    Editor.Part = previousPart;
                    Title.Text = _packageName + " - " + Editor.Part.Type;

                    var msgDialog = new MessageDialog(ex.ToString());
                    await msgDialog.ShowAsync();
                }

                // Reset viewing parameters
                UcEditor.ResetView(false);
            }
        }

        private void AppBar_PreviousPartButton_Click(object sender, RoutedEventArgs e)
        {
            var part = Editor.Part;

            if (part != null)
            {
                var package = part.Package;
                var index = package.IndexOfPart(part);

                if (index > 0)
                {
                    ResetSelection();
                    Editor.Part = null;

                    while (--index >= 0)
                    {
                        ContentPart newPart = null;

                        try
                        {
                            // Select new part
                            newPart = part.Package.GetPart(index);
                            Editor.Part = newPart;
                            Title.Text = _packageName + " - " + newPart.Type;
                            part.Dispose();
                            break;
                        }
                        catch
                        {
                            // Can't set this part, try the previous one
                            Editor.Part = null;
                            Title.Text = "";
                            newPart?.Dispose();
                        }
                    }

                    if (index < 0)
                    {
                        // Restore current part if none can be set
                        Editor.Part = part;
                        Title.Text = _packageName + " - " + part.Type;
                    }

                    // Reset viewing parameters
                    UcEditor.ResetView(false);
                }
            }
        }

        private void AppBar_NextPartButton_Click(object sender, RoutedEventArgs e)
        {
            var part = Editor.Part;

            if (part != null)
            {
                var package = part.Package;
                var count = package.PartCount;
                var index = package.IndexOfPart(part);

                if (index < count - 1)
                {
                    ResetSelection();
                    Editor.Part = null;

                    while (++index < count)
                    {
                        ContentPart newPart = null;

                        try
                        {
                            // Select new part
                            newPart = part.Package.GetPart(index);
                            Editor.Part = newPart;
                            Title.Text = _packageName + " - " + newPart.Type;
                            part.Dispose();
                            break;
                        }
                        catch
                        {
                            // Can't set this part, try the next one
                            Editor.Part = null;
                            Title.Text = "";
                            newPart?.Dispose();
                        }
                    }

                    if (index >= count)
                    {
                        // Restore current part if none can be set
                        Editor.Part = part;
                        Title.Text = _packageName + " - " + part.Type;
                    }

                    // Reset viewing parameters
                    UcEditor.ResetView(false);
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
            List<string> files = new List<string>();

            // List iink files inside LocalFolders
            var localFolder = Windows.Storage.ApplicationData.Current.LocalFolder;
            var items = await localFolder.GetItemsAsync();
            foreach (var item in items)
            {
                if(item.IsOfType(StorageItemTypes.File) && item.Path.EndsWith(".iink"))
                    files.Add(item.Name.ToString());
            }
            if (files.Count == 0)
                return;

            // Display file list
            ListBox fileList = new ListBox
            {
                ItemsSource = files,
                SelectedIndex = 0
            };
            ContentDialog fileNameDialog = new ContentDialog
            {
                Title = "Select Package Name",
                Content = fileList,
                IsSecondaryButtonEnabled = true,
                PrimaryButtonText = "Ok",
                SecondaryButtonText = "Cancel",
            };
            if (await fileNameDialog.ShowAsync() == ContentDialogResult.Secondary)
                return;

            var fileName = fileList.SelectedValue.ToString();
            var filePath = System.IO.Path.Combine(localFolder.Path.ToString(), fileName);

            ResetSelection();

            try
            {
                // Save and close current package
                SavePackage();
                ClosePackage();

                // Open package and select first part
                var package = Editor.Engine.OpenPackage(filePath);
                var part = package.GetPart(0);
                Editor.Part = part;
                _packageName = fileName;
                Title.Text = _packageName + " - " + part.Type;
            }
            catch (Exception ex)
            {
                ClosePackage();

                var msgDialog = new MessageDialog(ex.ToString());
                await msgDialog.ShowAsync();
            }

            // Reset viewing parameters
            UcEditor.ResetView(false);
        }

        private async void AppBar_SavePackageButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var part = Editor.Part;
                var package = part?.Package;
                package?.Save();
            }
            catch (Exception ex)
            {
                var msgDialog = new MessageDialog(ex.ToString());
                await msgDialog.ShowAsync();
            }
        }

        private async void AppBar_SaveAsButton_Click(object sender, RoutedEventArgs e)
        {
            var localFolder = Windows.Storage.ApplicationData.Current.LocalFolder;

            // Show file name input dialog
            TextBox inputTextBox = new TextBox
            {
                AcceptsReturn = false,
                Height = 32
            };
            ContentDialog fileNameDialog = new ContentDialog
            {
                Title = "Enter New Package Name",
                Content = inputTextBox,
                IsSecondaryButtonEnabled = true,
                PrimaryButtonText = "Ok",
                SecondaryButtonText = "Cancel",
            };

            if (await fileNameDialog.ShowAsync() == ContentDialogResult.Secondary)
                return;

            var fileName = inputTextBox.Text;
            if (fileName == null || fileName == "")
                return;

            // Add iink extension if needed
            if (!fileName.EndsWith(".iink"))
                fileName = fileName + ".iink";

            // Display overwrite dialog (if needed)
            string filePath = null;
            var item = await localFolder.TryGetItemAsync(fileName);
            if (item != null)
            {
                ContentDialog overwriteDialog = new ContentDialog
                {
                    Title = "File Already Exists",
                    Content = "A file with that name already exists, overwrite it?",
                    PrimaryButtonText = "Cancel",
                    SecondaryButtonText = "Overwrite"
                };

                ContentDialogResult result = await overwriteDialog.ShowAsync();
                if (result == ContentDialogResult.Primary)
                    return;

                filePath = item.Path.ToString();
            }
            else
            {
                filePath = System.IO.Path.Combine(localFolder.Path.ToString(), fileName);
            }

            var part = Editor.Part;
            if (part != null)
            {
                try
                {
                    // Save Package with new name
                    part.Package.SaveAs(filePath);

                    // Update internals
                    _packageName = fileName;
                    Title.Text = _packageName + " - " + part.Type;
                }
                catch (Exception ex)
                {
                    var msgDialog = new MessageDialog(ex.ToString());
                    await msgDialog.ShowAsync();
                }
            }
        }

        private void DisplayBlockContextualMenu(Windows.Foundation.Point globalPos)
        {
            var contentBlock = _lastContentSelection as ContentBlock;

            var flyoutMenu = new MenuFlyout();

            var availableActions = UcEditor.GetAvailableActions(contentBlock);
            var supportedTypes = Editor.SupportedAddBlockTypes;

            if (availableActions.HasFlag(AvailableActions.ADD_BLOCK))
            {
                var flyoutSubItem = new MenuFlyoutSubItem { Text = "Add..." };
                for (var i = 0; i < supportedTypes.Count(); ++i)
                {
                    if (supportedTypes[i] != "Image") // Not supported in this demo
                    {
                        var command = new FlyoutCommand(supportedTypes[i], (cmd) => { Popup_CommandHandler_AddBlock(cmd); });
                        var flyoutItem = new MenuFlyoutItem { Text = "Add " + supportedTypes[i], Command = command };
                        flyoutSubItem.Items.Add(flyoutItem);
                    }
                }
                if (flyoutSubItem.Items.Any())
                    flyoutMenu.Items.Add(flyoutSubItem);
            }

            if (availableActions.HasFlag(AvailableActions.REMOVE))
            {
                var command = new FlyoutCommand("Remove", (cmd) => { Popup_CommandHandler_Remove(cmd); });
                var flyoutItem = new MenuFlyoutItem { Text = "Remove", Command = command };
                flyoutMenu.Items.Add(flyoutItem);
            }

            if (availableActions.HasFlag(AvailableActions.CONVERT))
            {
                var command = new FlyoutCommand("Convert", (cmd) => { Popup_CommandHandler_Convert(cmd); });
                var flyoutItem = new MenuFlyoutItem { Text = "Convert", Command = command };
                flyoutMenu.Items.Add(flyoutItem);
            }

            if ( availableActions.HasFlag(AvailableActions.COPY)
              || availableActions.HasFlag(AvailableActions.OFFICE_CLIPBOARD)
              || availableActions.HasFlag(AvailableActions.PASTE) )
            {
                var flyoutSubItem = new MenuFlyoutSubItem { Text = "Copy/Paste..." };
                flyoutMenu.Items.Add(flyoutSubItem);

                {
                    var command = new FlyoutCommand("Copy", (cmd) => { Popup_CommandHandler_Copy(cmd); });
                    var flyoutItem = new MenuFlyoutItem { Text = "Copy", Command = command, IsEnabled = availableActions.HasFlag(AvailableActions.COPY) };
                    flyoutSubItem.Items.Add(flyoutItem);
                }
                {
                    var command = new FlyoutCommand("Copy To Clipboard (Microsoft Office)", (cmd) => { Popup_CommandHandler_OfficeClipboard(cmd); });
                    var flyoutItem = new MenuFlyoutItem { Text = "Copy To Clipboard (Microsoft Office)", Command = command,
                        IsEnabled = availableActions.HasFlag(AvailableActions.OFFICE_CLIPBOARD) };
                    flyoutSubItem.Items.Add(flyoutItem);
                }
                {
                    var command = new FlyoutCommand("Paste", (cmd) => { Popup_CommandHandler_Paste(cmd); });
                    var flyoutItem = new MenuFlyoutItem { Text = "Paste", Command = command, IsEnabled = availableActions.HasFlag(AvailableActions.PASTE) };
                    flyoutSubItem.Items.Add(flyoutItem);
                }
            }

            if ( availableActions.HasFlag(AvailableActions.IMPORT)
              || availableActions.HasFlag(AvailableActions.EXPORT) )
            {
                var flyoutSubItem = new MenuFlyoutSubItem { Text = "Import/Export..." };
                flyoutMenu.Items.Add(flyoutSubItem);

                {
                    var command = new FlyoutCommand("Import", (cmd) => { Popup_CommandHandler_Import(cmd); });
                    var flyoutItem = new MenuFlyoutItem { Text = "Import", Command = command, IsEnabled = availableActions.HasFlag(AvailableActions.IMPORT) };
                    flyoutSubItem.Items.Add(flyoutItem);
                }
                {
                    var command = new FlyoutCommand("Export", (cmd) => { Popup_CommandHandler_Export(cmd); });
                    var flyoutItem = new MenuFlyoutItem { Text = "Export", Command = command, IsEnabled = availableActions.HasFlag(AvailableActions.EXPORT) };
                    flyoutSubItem.Items.Add(flyoutItem);
                }
            }

            if (availableActions.HasFlag(AvailableActions.FORMAT_TEXT))
            {
                var supportedFormats = Editor.GetSupportedTextFormats(contentBlock);

                var flyoutSubItem = new MenuFlyoutSubItem { Text = "Format..." };
                flyoutMenu.Items.Add(flyoutSubItem);

                if (supportedFormats.Contains(TextFormat.H1))
                {
                    var command = new FlyoutCommand("H1", (cmd) => { Popup_CommandHandler_FormatH1(cmd); });
                    var flyoutItem = new MenuFlyoutItem { Text = "H1", Command = command };
                    flyoutSubItem.Items.Add(flyoutItem);
                }
                if (supportedFormats.Contains(TextFormat.H2))
                {
                    var command = new FlyoutCommand("H2", (cmd) => { Popup_CommandHandler_FormatH2(cmd); });
                    var flyoutItem = new MenuFlyoutItem { Text = "H2", Command = command };
                    flyoutSubItem.Items.Add(flyoutItem);
                }
                if (supportedFormats.Contains(TextFormat.PARAGRAPH))
                {
                    var command = new FlyoutCommand("P", (cmd) => { Popup_CommandHandler_FormatP(cmd); });
                    var flyoutItem = new MenuFlyoutItem { Text = "P", Command = command };
                    flyoutSubItem.Items.Add(flyoutItem);
                }
                if (supportedFormats.Contains(TextFormat.LIST_BULLET))
                {
                    var command = new FlyoutCommand("Bullet list", (cmd) => { Popup_CommandHandler_FormatBulletList(cmd); });
                    var flyoutItem = new MenuFlyoutItem { Text = "Bullet list", Command = command };
                    flyoutSubItem.Items.Add(flyoutItem);
                }
                if (supportedFormats.Contains(TextFormat.LIST_CHECKBOX))
                {
                    var command = new FlyoutCommand("Checkbox list", (cmd) => { Popup_CommandHandler_FormatCheckboxList(cmd); });
                    var flyoutItem = new MenuFlyoutItem { Text = "Checkbox list", Command = command };
                    flyoutSubItem.Items.Add(flyoutItem);
                }
                if (supportedFormats.Contains(TextFormat.LIST_NUMBERED))
                {
                    var command = new FlyoutCommand("Numbered list", (cmd) => { Popup_CommandHandler_FormatNumberedList(cmd); });
                    var flyoutItem = new MenuFlyoutItem { Text = "Numbered list", Command = command };
                    flyoutSubItem.Items.Add(flyoutItem);
                }
            }

            if (!Editor.IsEmpty(contentBlock))
            {
                IndentationLevels indentLevels = Editor.GetIndentationLevels(contentBlock);

                bool indentable = (int)indentLevels.Low < (int)indentLevels.Max - 1;
                bool deindentable = indentLevels.High > 0;

                if (indentable || deindentable)
                {
                    var flyoutSubItem = new MenuFlyoutSubItem { Text = "Indentation..." };
                    flyoutMenu.Items.Add(flyoutSubItem);
                    if (indentable)
                    {
                        var command = new FlyoutCommand("Increase", (cmd) => { Popup_CommandHandler_IncreaseIndentation(cmd); });
                        var flyoutItem = new MenuFlyoutItem { Text = "Increase", Command = command };
                        flyoutSubItem.Items.Add(flyoutItem);
                    }
                    if (deindentable)
                    {
                        var command = new FlyoutCommand("Decrease", (cmd) => { Popup_CommandHandler_DecreaseIndentation(cmd); });
                        var flyoutItem = new MenuFlyoutItem { Text = "Decrease", Command = command };
                        flyoutSubItem.Items.Add(flyoutItem);
                    }
                }
            }

            if (flyoutMenu.Items.Count > 0)
            {
                flyoutMenu.ShowAt(null, globalPos);
            }
        }

        private void DisplaySelectionContextualMenu(Windows.Foundation.Point globalPos)
        {
            var contentSelection = _lastContentSelection as ContentSelection;

            var flyoutMenu = new MenuFlyout();

            var availableActions = UcEditor.GetAvailableActions(contentSelection);

            if (availableActions.HasFlag(AvailableActions.REMOVE))
            {
                var command = new FlyoutCommand("Erase", (cmd) => { Popup_CommandHandler_Remove(cmd); });
                var flyoutItem = new MenuFlyoutItem { Text = "Erase", Command = command };
                flyoutMenu.Items.Add(flyoutItem);
            }

            if (availableActions.HasFlag(AvailableActions.CONVERT))
            {
                var command = new FlyoutCommand("Convert", (cmd) => { Popup_CommandHandler_Convert(cmd); });
                var flyoutItem = new MenuFlyoutItem { Text = "Convert", Command = command };
                flyoutMenu.Items.Add(flyoutItem);
            }

            if (availableActions.HasFlag(AvailableActions.COPY))
            {
                var flyoutSubItem = new MenuFlyoutSubItem { Text = "Copy/Paste..." };
                flyoutMenu.Items.Add(flyoutSubItem);

                {
                    var command = new FlyoutCommand("Copy", (cmd) => { Popup_CommandHandler_Copy(cmd); });
                    var flyoutItem = new MenuFlyoutItem { Text = "Copy", Command = command };
                    flyoutSubItem.Items.Add(flyoutItem);
                }
                {
                    var command = new FlyoutCommand("Copy To Clipboard (Microsoft Office)", (cmd) => { Popup_CommandHandler_OfficeClipboard(cmd); });
                    var flyoutItem = new MenuFlyoutItem { Text = "Copy To Clipboard (Microsoft Office)", Command = command,
                        IsEnabled = availableActions.HasFlag(AvailableActions.OFFICE_CLIPBOARD) };
                    flyoutSubItem.Items.Add(flyoutItem);
                }
            }

            if (availableActions.HasFlag(AvailableActions.EXPORT))
            {
                var command = new FlyoutCommand("Export", (cmd) => { Popup_CommandHandler_Export(cmd); });
                var flyoutItem = new MenuFlyoutItem { Text = "Export", Command = command };
                flyoutMenu.Items.Add(flyoutItem);
            }

            if (availableActions.HasFlag(AvailableActions.FORMAT_TEXT))
            {
                var supportedFormats = Editor.GetSupportedTextFormats(contentSelection);

                var flyoutSubItem = new MenuFlyoutSubItem { Text = "Format..." };
                flyoutMenu.Items.Add(flyoutSubItem);

                if (supportedFormats.Contains(TextFormat.H1))
                {
                    var command = new FlyoutCommand("H1", (cmd) => { Popup_CommandHandler_FormatH1(cmd); });
                    var flyoutItem = new MenuFlyoutItem { Text = "H1", Command = command };
                    flyoutSubItem.Items.Add(flyoutItem);
                }
                if (supportedFormats.Contains(TextFormat.H2))
                {
                    var command = new FlyoutCommand("H2", (cmd) => { Popup_CommandHandler_FormatH2(cmd); });
                    var flyoutItem = new MenuFlyoutItem { Text = "H2", Command = command };
                    flyoutSubItem.Items.Add(flyoutItem);
                }
                if (supportedFormats.Contains(TextFormat.PARAGRAPH))
                {
                    var command = new FlyoutCommand("P", (cmd) => { Popup_CommandHandler_FormatP(cmd); });
                    var flyoutItem = new MenuFlyoutItem { Text = "P", Command = command };
                    flyoutSubItem.Items.Add(flyoutItem);
                }
                if (supportedFormats.Contains(TextFormat.LIST_BULLET))
                {
                    var command = new FlyoutCommand("Bullet list", (cmd) => { Popup_CommandHandler_FormatBulletList(cmd); });
                    var flyoutItem = new MenuFlyoutItem { Text = "Bullet list", Command = command };
                    flyoutSubItem.Items.Add(flyoutItem);
                }
                if (supportedFormats.Contains(TextFormat.LIST_CHECKBOX))
                {
                    var command = new FlyoutCommand("Checkbox list", (cmd) => { Popup_CommandHandler_FormatCheckboxList(cmd); });
                    var flyoutItem = new MenuFlyoutItem { Text = "Checkbox list", Command = command };
                    flyoutSubItem.Items.Add(flyoutItem);
                }
                if (supportedFormats.Contains(TextFormat.LIST_NUMBERED))
                {
                    var command = new FlyoutCommand("Numbered list", (cmd) => { Popup_CommandHandler_FormatNumberedList(cmd); });
                    var flyoutItem = new MenuFlyoutItem { Text = "Numbered list", Command = command };
                    flyoutSubItem.Items.Add(flyoutItem);
                }
            }

            if (!Editor.IsEmpty(contentSelection))
            {
                IndentationLevels indentLevels = Editor.GetIndentationLevels(contentSelection);

                bool indentable = (int)indentLevels.Low < (int)indentLevels.Max - 1;
                bool deindentable = indentLevels.High > 0;

                if (indentable || deindentable)
                {
                    var flyoutSubItem = new MenuFlyoutSubItem { Text = "Indentation..." };
                    flyoutMenu.Items.Add(flyoutSubItem);
                    if (indentable)
                    {
                        var command = new FlyoutCommand("Increase", (cmd) => { Popup_CommandHandler_IncreaseIndentation(cmd); });
                        var flyoutItem = new MenuFlyoutItem { Text = "Increase", Command = command };
                        flyoutSubItem.Items.Add(flyoutItem);
                    }
                    if (deindentable)
                    {
                        var command = new FlyoutCommand("Decrease", (cmd) => { Popup_CommandHandler_DecreaseIndentation(cmd); });
                        var flyoutItem = new MenuFlyoutItem { Text = "Decrease", Command = command };
                        flyoutSubItem.Items.Add(flyoutItem);
                    }
                }
            }

            if (flyoutMenu.Items.Count > 0)
            {
                flyoutMenu.ShowAt(null, globalPos);
            }
        }

        private void UcEditor_Holding(object sender, Windows.UI.Xaml.Input.HoldingRoutedEventArgs e)
        {
            // Only for Pen and Touch (but it should not been fired for a Mouse)
            // Do not wait for the Release event, open the menu immediately

            if (e.PointerDeviceType == Windows.Devices.Input.PointerDeviceType.Mouse)
                return;

            if (e.HoldingState != Windows.UI.Input.HoldingState.Started)
                return;

            var uiElement = sender as UIElement;
            var pos = e.GetPosition(uiElement);

            ResetSelection();
            _lastPointerPosition = new Graphics.Point((float)pos.X, (float)pos.Y);

            _lastContentSelection = Editor.HitSelection(_lastPointerPosition.X, _lastPointerPosition.Y);
            if (_lastContentSelection != null)
            {
                var globalPos = e.GetPosition(null);
                DisplaySelectionContextualMenu(globalPos);
            }
            else
            {
                var contentBlock = Editor.HitBlock(_lastPointerPosition.X, _lastPointerPosition.Y);
                if ((contentBlock == null) || (contentBlock.Type == "Container"))
                {
                    contentBlock?.Dispose();
                    contentBlock = Editor.GetRootBlock();
                }
                _lastContentSelection = contentBlock;

                // Discard current stroke
                UcEditor.CancelSampling(UcEditor.GetPointerId(e));

                if (_lastContentSelection != null)
                {
                    var globalPos = e.GetPosition(null);
                    DisplayBlockContextualMenu(globalPos);
                }
            }

            e.Handled = true;
        }

        private void UcEditor_RightTapped(object sender, Windows.UI.Xaml.Input.RightTappedRoutedEventArgs e)
        {
            // Only for Mouse to avoid issue with LongPress becoming RightTap with Pen/Touch
            if (e.PointerDeviceType != Windows.Devices.Input.PointerDeviceType.Mouse)
                return;

            var uiElement = sender as UIElement;
            var pos = e.GetPosition(uiElement);

            ResetSelection();
            _lastPointerPosition = new Graphics.Point((float)pos.X, (float)pos.Y);

            _lastContentSelection = Editor.HitSelection(_lastPointerPosition.X, _lastPointerPosition.Y);
            if (_lastContentSelection != null)
            {
                var globalPos = e.GetPosition(null);
                DisplaySelectionContextualMenu(globalPos);
            }
            else
            {
                var contentBlock = Editor.HitBlock(_lastPointerPosition.X, _lastPointerPosition.Y);
                if ((contentBlock == null) || (contentBlock.Type == "Container"))
                {
                    contentBlock?.Dispose();
                    contentBlock = Editor.GetRootBlock();
                }
                _lastContentSelection = contentBlock;

                if (_lastContentSelection != null)
                {
                    var globalPos = e.GetPosition(null);
                    DisplayBlockContextualMenu(globalPos);
                }
            }

            e.Handled = true;
        }

        private void UcEditor_RightDown(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            // Only for Pen to avoid issue with LongPress becoming RightTap with Pen/Touch
            var uiElement = sender as UIElement;
            var p = e.GetCurrentPoint(uiElement);

            if (e.Pointer.PointerDeviceType != Windows.Devices.Input.PointerDeviceType.Pen)
                return;

            if (!p.Properties.IsRightButtonPressed)
                return;

            ResetSelection();
            _lastPointerPosition = new Graphics.Point((float)p.Position.X, (float)p.Position.Y);

            _lastContentSelection = Editor.HitSelection(_lastPointerPosition.X, _lastPointerPosition.Y);
            if (_lastContentSelection != null)
            {
                var globalPos = e.GetCurrentPoint(null).Position;
                DisplaySelectionContextualMenu(globalPos);
            }
            else
            {
                var contentBlock = Editor.HitBlock(_lastPointerPosition.X, _lastPointerPosition.Y);
                if ((contentBlock == null) || (contentBlock.Type == "Container"))
                {
                    contentBlock?.Dispose();
                    contentBlock = Editor.GetRootBlock();
                }
                _lastContentSelection = contentBlock;

                if (_lastContentSelection != null)
                {
                    var globalPos = e.GetCurrentPoint(null).Position;
                    DisplayBlockContextualMenu(globalPos);
                }
            }

            e.Handled = true;
        }

        private void ShowSmartGuideMenu(Windows.Foundation.Point globalPos)
        {
            ResetSelection();
            _lastContentSelection = UcEditor.SmartGuide.ContentBlock?.ShallowCopy();

            if (_lastContentSelection != null)
                DisplayBlockContextualMenu(globalPos);
        }

        private async void Popup_CommandHandler_Convert(FlyoutCommand command)
        {
            try
            {
                if (_lastContentSelection != null)
                {
                    var supportedStates = Editor.GetSupportedTargetConversionStates(_lastContentSelection);

                    if ( (supportedStates != null) && (supportedStates.Count() > 0) )
                        Editor.Convert(_lastContentSelection, supportedStates[0]);
                }
            }
            catch (Exception ex)
            {
                var msgDialog = new MessageDialog(ex.ToString());
                await msgDialog.ShowAsync();
            }
        }

        private async void Popup_CommandHandler_AddBlock(FlyoutCommand command)
        {
            try
            {
              // Uses Id as block type
              var blockType = command.Id.ToString();
              var mimeTypes = Editor.GetSupportedAddBlockDataMimeTypes(blockType);
              var useDialog = (mimeTypes != null) && (mimeTypes.Count() > 0);

              if (!useDialog)
              {
                  Editor.AddBlock(_lastPointerPosition.X, _lastPointerPosition.Y, blockType);
                  UcEditor.Invalidate(LayerType.LayerType_ALL);
              }
              else
              {
                var result = await EnterImportData("Add Content Block", mimeTypes);

                if (result != null)
                {
                    var idx = result.Item1;
                    var data = result.Item2;

                    if ( (idx >= 0) && (idx < mimeTypes.Count()) && (String.IsNullOrWhiteSpace(data) == false) )
                    {
                      Editor.AddBlock(_lastPointerPosition.X, _lastPointerPosition.Y, blockType, mimeTypes[idx], data);
                      UcEditor.Invalidate(LayerType.LayerType_ALL);
                    }
                }
              }
            }
            catch (Exception ex)
            {
                var msgDialog = new MessageDialog(ex.ToString());
                await msgDialog.ShowAsync();
            }
        }

        private async void Popup_CommandHandler_Remove(FlyoutCommand command)
        {
            try
            {
                if (_lastContentSelection != null)
                {
                    Editor.Erase(_lastContentSelection);
                    ResetSelection();
                }
            }
            catch (Exception ex)
            {
                var msgDialog = new MessageDialog(ex.ToString());
                await msgDialog.ShowAsync();
            }
        }

        private async void Popup_CommandHandler_Copy(FlyoutCommand command)
        {
            try
            {
                if (_lastContentSelection != null)
                    Editor.Copy(_lastContentSelection);
            }
            catch (Exception ex)
            {
                var msgDialog = new MessageDialog(ex.ToString());
                await msgDialog.ShowAsync();
            }
        }

        private async void Popup_CommandHandler_Paste(FlyoutCommand command)
        {
            try
            {
                Editor.Paste(_lastPointerPosition.X, _lastPointerPosition.Y);
            }
            catch (Exception ex)
            {
                var msgDialog = new MessageDialog(ex.ToString());
                await msgDialog.ShowAsync();
            }
        }

        private async void Popup_CommandHandler_Import(FlyoutCommand command)
        {
            var part = Editor.Part;
            if (part == null)
                return;

            if (_lastContentSelection == null)
                return;

            var mimeTypes = Editor.GetSupportedImportMimeTypes(_lastContentSelection);

            if (mimeTypes == null)
                return;

            if (mimeTypes.Count() == 0)
                return;

            var result = await EnterImportData("Import", mimeTypes);

            if (result != null)
            {
                var idx = result.Item1;
                var data = result.Item2;

                if ( (idx >= 0) && (idx < mimeTypes.Count()) && (String.IsNullOrWhiteSpace(data) == false) )
                {
                    try
                    {
                        Editor.Import_(mimeTypes[idx], data, _lastContentSelection);
                    }
                    catch (Exception ex)
                    {
                        var msgDialog = new MessageDialog(ex.ToString());
                        await msgDialog.ShowAsync();
                    }
                }

            }
        }

        private async void Popup_CommandHandler_Export(FlyoutCommand command)
        {
            var part = Editor.Part;
            if (part == null)
                return;

            using (var rootBlock = Editor.GetRootBlock())
            {
                var onRawContent = part.Type == "Raw Content";
                var contentBlock = _lastContentSelection as ContentBlock;
                IContentSelection contentSelection = (contentBlock != null) ? (onRawContent ? rootBlock : contentBlock)
                    : _lastContentSelection;

                if (contentSelection == null)
                    return;

                var mimeTypes = Editor.GetSupportedExportMimeTypes(contentSelection);

                if (mimeTypes == null)
                    return;

                if (mimeTypes.Count() == 0)
                    return;

                // Show export dialog
                var fileName = await ChooseExportFilename(mimeTypes);

                if (!string.IsNullOrEmpty(fileName))
                {
                    var localFolder = Windows.Storage.ApplicationData.Current.LocalFolder;
                    var item = await localFolder.TryGetItemAsync(fileName);
                    string filePath = null;

                    if (item != null)
                    {
                        ContentDialog overwriteDialog = new ContentDialog
                        {
                            Title = "File Already Exists",
                            Content = "A file with that name already exists, overwrite it?",
                            PrimaryButtonText = "Cancel",
                            SecondaryButtonText = "Overwrite"
                        };

                        ContentDialogResult result = await overwriteDialog.ShowAsync();
                        if (result == ContentDialogResult.Primary)
                            return;

                        filePath = item.Path.ToString();
                    }
                    else
                    {
                        filePath = System.IO.Path.Combine(localFolder.Path.ToString(), fileName);
                    }

                    try
                    {
                        var imagePainter = new ImagePainter();

                        imagePainter.ImageLoader = UcEditor.ImageLoader;

                        Editor.WaitForIdle();
                        Editor.Export_(contentSelection, filePath, imagePainter);

                        var file = await StorageFile.GetFileFromPathAsync(filePath);
                        await Windows.System.Launcher.LaunchFileAsync(file);
                    }
                    catch (Exception ex)
                    {
                        var msgDialog = new MessageDialog(ex.ToString());
                        await msgDialog.ShowAsync();
                    }
                }
            }
        }

        private async void Popup_CommandHandler_OfficeClipboard(FlyoutCommand command)
        {
            try
            {
                MimeType[] mimeTypes = null;

                if (_lastContentSelection != null)
                    mimeTypes = Editor.GetSupportedExportMimeTypes(_lastContentSelection);

                if (mimeTypes != null && mimeTypes.Contains(MimeType.OFFICE_CLIPBOARD))
                {
                    // export block to a file
                    var localFolder = ApplicationData.Current.LocalFolder.Path;
                    var clipboardPath = System.IO.Path.Combine(localFolder.ToString(), "tmp/clipboard.gvml");
                    var imagePainter = new ImagePainter();

                    imagePainter.ImageLoader = UcEditor.ImageLoader;

                    Editor.Export_(_lastContentSelection, clipboardPath.ToString(), MimeType.OFFICE_CLIPBOARD, imagePainter);

                    // read back exported data
                    var clipboardData = File.ReadAllBytes(clipboardPath);
                    var clipboardStream = new MemoryStream(clipboardData);

                    // store the data into clipboard
                    Windows.ApplicationModel.DataTransfer.Clipboard.Clear();
                    var clipboardContent = new Windows.ApplicationModel.DataTransfer.DataPackage();
                    clipboardContent.SetData(MimeTypeF.GetTypeName(MimeType.OFFICE_CLIPBOARD), clipboardStream.AsRandomAccessStream());
                    Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(clipboardContent);
                }
            }
            catch (Exception ex)
            {
                MessageDialog msgDialog = new MessageDialog(ex.ToString());
                await msgDialog.ShowAsync();
            }
        }

        private async void Popup_CommandHandler_FormatH1(FlyoutCommand command)
        {
            try
            {
                if (_lastContentSelection != null)
                    Editor.SetTextFormat(_lastContentSelection, TextFormat.H1);
            }
            catch (Exception ex)
            {
                var msgDialog = new MessageDialog(ex.ToString());
                await msgDialog.ShowAsync();
            }
        }

        private async void Popup_CommandHandler_FormatH2(FlyoutCommand command)
        {
            try
            {
                if (_lastContentSelection != null)
                    Editor.SetTextFormat(_lastContentSelection, TextFormat.H2);
            }
            catch (Exception ex)
            {
                var msgDialog = new MessageDialog(ex.ToString());
                await msgDialog.ShowAsync();
            }
        }

        private async void Popup_CommandHandler_FormatP(FlyoutCommand command)
        {
            try
            {
                if (_lastContentSelection != null)
                    Editor.SetTextFormat(_lastContentSelection, TextFormat.PARAGRAPH);
            }
            catch (Exception ex)
            {
                var msgDialog = new MessageDialog(ex.ToString());
                await msgDialog.ShowAsync();
            }
        }

        private async void Popup_CommandHandler_FormatBulletList(FlyoutCommand command)
        {
            try
            {
                if (_lastContentSelection != null)
                    Editor.SetTextFormat(_lastContentSelection, TextFormat.LIST_BULLET);
            }
            catch (Exception ex)
            {
                var msgDialog = new MessageDialog(ex.ToString());
                await msgDialog.ShowAsync();
            }
        }

        private async void Popup_CommandHandler_FormatCheckboxList(FlyoutCommand command)
        {
            try
            {
                if (_lastContentSelection != null)
                    Editor.SetTextFormat(_lastContentSelection, TextFormat.LIST_CHECKBOX);
            }
            catch (Exception ex)
            {
                var msgDialog = new MessageDialog(ex.ToString());
                await msgDialog.ShowAsync();
            }
        }

        private async void Popup_CommandHandler_FormatNumberedList(FlyoutCommand command)
        {
            try
            {
                if (_lastContentSelection != null)
                    Editor.SetTextFormat(_lastContentSelection, TextFormat.LIST_NUMBERED);
            }
            catch (Exception ex)
            {
                var msgDialog = new MessageDialog(ex.ToString());
                await msgDialog.ShowAsync();
            }
        }

        private async void Popup_CommandHandler_IncreaseIndentation(FlyoutCommand command)
        {
            try
            {
                if (_lastContentSelection != null)
                    Editor.Indent(_lastContentSelection, 1);
            }
            catch (Exception ex)
            {
                var msgDialog = new MessageDialog(ex.ToString());
                await msgDialog.ShowAsync();
            }
        }

        private async void Popup_CommandHandler_DecreaseIndentation(FlyoutCommand command)
        {
            try
            {
                if (_lastContentSelection != null)
                    Editor.Indent(_lastContentSelection, -1);
            }
            catch (Exception ex)
            {
                var msgDialog = new MessageDialog(ex.ToString());
                await msgDialog.ShowAsync();
            }
        }

        private void SavePackage()
        {
            var part = Editor.Part;
            var package = part?.Package;
            package?.Save();
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
            var cancelable = Editor.Part != null;
            var partType = await ChoosePartType(cancelable);
            if (string.IsNullOrEmpty(partType))
                return;

            ResetSelection();

            try
            {
                // Save and close current package
                SavePackage();
                ClosePackage();

                // Create package and part
                var packageName = MakeUntitledFilename();
                var package = Editor.Engine.CreatePackage(packageName);
                var part = package.CreatePart(partType);
                Editor.Part = part;
                _packageName = System.IO.Path.GetFileName(packageName);
                Title.Text = _packageName + " - " + part.Type;
            }
            catch (Exception ex)
            {
                ClosePackage();

                var msgDialog = new MessageDialog(ex.ToString());
                await msgDialog.ShowAsync();
            }
        }

        private string MakeUntitledFilename()
        {
            var localFolder = Windows.Storage.ApplicationData.Current.LocalFolder.Path;
            var tempFolder = Editor.Engine.Configuration.GetString("content-package.temp-folder");
            string fileName;
            string folderName;

            do
            {
                var baseName = "File" + (++_filenameIndex) + ".iink";
                fileName = System.IO.Path.Combine(localFolder, baseName);
                var tempName = baseName + "-file";
                folderName = System.IO.Path.Combine(tempFolder, tempName);
            }
            while (System.IO.File.Exists(fileName) || System.IO.File.Exists(folderName));

            return fileName;
        }


        private async System.Threading.Tasks.Task<string> ChoosePartType(bool cancelable)
        {
            var types = Editor.Engine.SupportedPartTypes.ToList();

            if (types.Count == 0)
                return null;

            var view = new ListView
            {
                ItemsSource = types,
                IsItemClickEnabled = true,
                SelectionMode = ListViewSelectionMode.Single,
                SelectedIndex = -1
            };

            var grid = new Grid();
            grid.Children.Add(view);

            var dialog = new ContentDialog
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

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
                return types[view.SelectedIndex];
            else
                return null;
        }

        private async System.Threading.Tasks.Task<Tuple<int, string>> EnterImportData(string title, MimeType[] mimeTypes)
        {
            const bool defaultWrapping = false;
            const double defaultWidth = 400;

            var mimeTypeTextBlock = new TextBlock
            {
                Text = "Choose a mime type",
                MaxLines = 1,
                TextWrapping = TextWrapping.NoWrap,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 0),
                Width = defaultWidth,
            };

            var mimeTypeComboBox = new ComboBox
            {
                IsTextSearchEnabled = true,
                SelectedIndex = -1,
                Margin = new Thickness(0, 5, 0, 5),
                Width = defaultWidth
            };

            foreach (var mimeType in mimeTypes)
                mimeTypeComboBox.Items.Add(MimeTypeF.GetTypeName(mimeType));

            mimeTypeComboBox.SelectedIndex = 0;

            var dataTextBlock = new TextBlock
            {
                Text = "Enter some text",
                MaxLines = 1,
                TextWrapping = TextWrapping.NoWrap,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 5, 0, 0),
                Width = defaultWidth
            };

            var dataTextBox = new TextBox
            {
                Text = "",
                AcceptsReturn = true,
                TextWrapping = (defaultWrapping ? TextWrapping.Wrap : TextWrapping.NoWrap),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 12,
                Margin = new Thickness(0),
                Width = defaultWidth,
                Height = 200,
            };

            ScrollViewer.SetVerticalScrollBarVisibility(dataTextBox, ScrollBarVisibility.Auto);
            ScrollViewer.SetHorizontalScrollBarVisibility(dataTextBox, ScrollBarVisibility.Auto);

            var dataWrappingCheckBox = new CheckBox
            {
                Content = "Wrapping",
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 5),
                Width = defaultWidth,
                IsChecked = defaultWrapping,
            };

            dataWrappingCheckBox.Checked    += new RoutedEventHandler( (sender, e) =>  { dataTextBox.TextWrapping = TextWrapping.Wrap; } );
            dataWrappingCheckBox.Unchecked  += new RoutedEventHandler( (sender, e) =>  { dataTextBox.TextWrapping = TextWrapping.NoWrap; } );

            var panel = new StackPanel
            {
                Margin = new Thickness(10),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };

            panel.Children.Add(mimeTypeTextBlock);
            panel.Children.Add(mimeTypeComboBox);
            panel.Children.Add(dataTextBlock);
            panel.Children.Add(dataTextBox);
            panel.Children.Add(dataWrappingCheckBox);


            var dialog = new ContentDialog
            {
                Title = title,
                Content = panel,
                PrimaryButtonText = "OK",
                SecondaryButtonText = "Cancel",
                IsPrimaryButtonEnabled = true,
                IsSecondaryButtonEnabled = true
            };

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                // Convert '\r' to '\n'
                // https://stackoverflow.com/questions/42867242/uwp-textbox-puts-r-only-how-to-set-linebreak
                var text = dataTextBox.Text.Replace('\r', '\n');
                return new Tuple<int, string>(mimeTypeComboBox.SelectedIndex, text);
            }

            return null;
        }

        private async System.Threading.Tasks.Task<string> ChooseExportFilename(MimeType[] mimeTypes)
        {
            var mimeTypeTextBlock = new TextBlock
            {
                Text = "Choose a mime type",
                MaxLines = 1,
                TextWrapping = TextWrapping.NoWrap,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 0),
                Width = 300,
            };

            var mimeTypeComboBox = new ComboBox
            {
                IsTextSearchEnabled = true,
                SelectedIndex = -1,
                Margin = new Thickness(0, 5, 0, 0),
                Width = 300
            };

            foreach (var mimeType in mimeTypes)
                mimeTypeComboBox.Items.Add(MimeTypeF.GetTypeName(mimeType));

            mimeTypeComboBox.SelectedIndex = 0;

            var nameTextBlock = new TextBlock
            {
                Text = "Enter Export File Name",
                MaxLines = 1,
                TextWrapping = TextWrapping.NoWrap,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 10, 0, 0),
                Width = 300
            };

            var nameTextBox = new TextBox
            {
                Text = "",
                AcceptsReturn = false,
                MaxLength = 1024 * 1024,
                TextWrapping = TextWrapping.NoWrap,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 5, 0, 10),
                Width = 300
            };

            var panel = new StackPanel
            {
                Margin = new Thickness(10),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };

            panel.Children.Add(mimeTypeTextBlock);
            panel.Children.Add(mimeTypeComboBox);
            panel.Children.Add(nameTextBlock);
            panel.Children.Add(nameTextBox);


            var dialog = new ContentDialog
            {
                Title = "Export",
                Content = panel,
                PrimaryButtonText = "OK",
                SecondaryButtonText = "Cancel",
                IsPrimaryButtonEnabled = true,
                IsSecondaryButtonEnabled = true
            };

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                var fileName = nameTextBox.Text;
                var extIndex = mimeTypeComboBox.SelectedIndex;
                var extensions = MimeTypeF.GetFileExtensions(mimeTypes[extIndex]).Split(',');

                int ext;
                for (ext = 0; ext < extensions.Count(); ++ext)
                {
                    if (fileName.EndsWith(extensions[ext], StringComparison.OrdinalIgnoreCase))
                        break;
                }

                if (ext >= extensions.Count())
                    fileName += extensions[0];

                return fileName;
            }

            return null;
        }

        private void AppBar_EnableSmartGuide_Click(object sender, RoutedEventArgs e)
        {
            AppBarToggleButton checkBox = sender as AppBarToggleButton;
            UcEditor.SmartGuideEnabled = (bool)checkBox.IsChecked;
        }
    }
}
