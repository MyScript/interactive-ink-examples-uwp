using System;
using System.Collections.Generic;
using System.Json;
using System.Linq;
using Windows.Devices.Input;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Text;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;

namespace MyScript.IInk.UIReferenceImplementation.UserControls
{
    public class CandidateFlyoutCommand : System.Windows.Input.ICommand
    {
        public delegate void InvokedHandler(CandidateFlyoutCommand command);

        public int WordIndex { get; set; } = -1;
        public TextBlock WordView { get; set; } = null;
        public string Label { get; set; } = null;
        public InvokedHandler Handler = null;

        public bool CanExecute(object parameter)
        {
            return Handler != null;
        }

        public void Execute(object parameter)
        {
            Handler(this);
        }

        public event EventHandler CanExecuteChanged;
    }

    /// <summary>
    /// Interaction logic for SmartGuideUserControl.xaml
    /// </summary>
    public sealed partial class SmartGuideUserControl : UserControl
    {
        private class Word
        {
            public string Label;
            public bool Updated;
            public List<string> Candidates;
        };

        private const int SMART_GUIDE_SIZE = 32;

        private const int SMART_GUIDE_FADE_OUT_DELAY_WRITE_IN_DIAGRAM_DEFAULT   = 3000;
        private const int SMART_GUIDE_FADE_OUT_DELAY_WRITE_OTHER_DEFAULT        = 0;
        private const int SMART_GUIDE_FADE_OUT_DELAY_OTHER_DEFAULT              = 0;
        private const int SMART_GUIDE_HIGHLIGHT_REMOVAL_DELAY_DEFAULT           = 2000;

        private Color SMART_GUIDE_CONTROL_COLOR         = Color.FromArgb(0xFF, 0x95, 0x9D, 0xA6);
        private Color SMART_GUIDE_TEXT_DEFAULT_COLOR    = Color.FromArgb(0xFF, 0xBF, 0xBF, 0xBF);
        private Color SMART_GUIDE_TEXT_HIGHLIGHT_COLOR  = Colors.Black;

        private enum UpdateCause
        {
            Visual,     /**< A visual change occurred. */
            Edit,       /**< An edit occurred (writing or editing gesture). */
            Selection,  /**< The selection changed. */
            View        /**< View parameters changed (scroll or zoom). */
        };

        private enum TextBlockStyle
        {
            H1,
            H2,
            H3,
            NORMAL
        };

        public delegate void MoreClickedHandler(Point globalPos);

        private Editor _editor;

        private event MoreClickedHandler _moreClicked;

        private ContentBlock _activeBlock;
        private ContentBlock _selectedBlock;

        private ContentBlock _currentBlock;
        private List<Word> _currentWords;

        private ContentBlock _previousBlock;
        private List<Word> _previousWords;

        private bool _pointerDown;
        private Point _pointerPosition;

        private DispatcherTimer _timer1;
        private DispatcherTimer _timer2;
        private int fadeOutWriteInDiagramDelay;
        private int fadeOutWriteDelay;
        private int fadeOutOtherDelay;
        private int removeHighlightDelay;

        private ParameterSet _exportParams;

        public Editor Editor
        {
            get { return _editor; }
            set { SetEditor(value); }
        }

        public event MoreClickedHandler MoreClicked
        {
            add { _moreClicked += value; UpdateMoreItemVisibility(); }
            remove { _moreClicked -= value; UpdateMoreItemVisibility(); }
        }

        public ContentBlock ContentBlock
        {
            get { return _currentBlock; }
        }

        public SmartGuideUserControl()
        {
            InitializeComponent();
            Initialize();

            _currentWords = new List<Word>();
            _previousWords = new List<Word>();

            _pointerDown = false;
            _pointerPosition = new Point();

            _timer1 = new DispatcherTimer();
            _timer1.Tick += new EventHandler<object>(onTimeout1);
            _timer2 = new DispatcherTimer();
            _timer2.Tick += new EventHandler<object>(onTimeout2);
        }

        private void Initialize()
        {
            this.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
            UpdateMoreItemVisibility();

            // Required for positionning using margins
            this.HorizontalAlignment = HorizontalAlignment.Left;
            this.VerticalAlignment = VerticalAlignment.Top;

            // Iput events
            scrollItem.PointerEntered += OnPointerEnterEvent;
            scrollItem.PointerExited += OnPointerLeaveEvent;
            scrollItem.PointerPressed += OnPointerPressEvent;
            scrollItem.PointerReleased += OnPointerReleaseEvent;
            scrollItem.PointerMoved += OnPointerMoveEvent;
            scrollItem.PointerWheelChanged += OnPointerWheelEvent;
            scrollItem.Tapped += OnResultClicked;

            moreItem.Tapped += OnMoreClicked;

            // Sub-items
            textItem.Children.Clear();

            scrollItem.IsEnabled = true;
            scrollItem.VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;
            scrollItem.VerticalScrollMode = ScrollMode.Disabled;
            scrollItem.IsVerticalRailEnabled = false;
            scrollItem.HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden;
            scrollItem.HorizontalScrollMode = ScrollMode.Disabled;  // Disables default events
            scrollItem.IsHorizontalRailEnabled = true;
            scrollItem.IsScrollInertiaEnabled = false;
            scrollItem.IsDeferredScrollingEnabled = false;
            scrollItem.ZoomMode = ZoomMode.Disabled;
        }

        private void UpdateMoreItemVisibility()
        {
            moreItem.Visibility = (_moreClicked != null) ? Visibility.Visible : Visibility.Collapsed;
        }

        private void SetEditor(Editor editor)
        {
            _editor = editor;

            Configuration configuration = _editor.Engine.Configuration;
            fadeOutWriteInDiagramDelay = (int)configuration.GetNumber("smart-guide.fade-out-delay.write-in-diagram", SMART_GUIDE_FADE_OUT_DELAY_WRITE_IN_DIAGRAM_DEFAULT);
            fadeOutWriteDelay = (int)configuration.GetNumber("smart-guide.fade-out-delay.write", SMART_GUIDE_FADE_OUT_DELAY_WRITE_OTHER_DEFAULT);
            fadeOutOtherDelay = (int)configuration.GetNumber("smart-guide.fade-out-delay.other", SMART_GUIDE_FADE_OUT_DELAY_OTHER_DEFAULT);
            removeHighlightDelay = (int)configuration.GetNumber("smart-guide.highlight-removal-delay", SMART_GUIDE_HIGHLIGHT_REMOVAL_DELAY_DEFAULT);

            _exportParams = _editor.Engine.CreateParameterSet();
            _exportParams?.SetBoolean("export.jiix.strokes", false);
            _exportParams?.SetBoolean("export.jiix.bounding-box", false);
            _exportParams?.SetBoolean("export.jiix.glyphs", false);
            _exportParams?.SetBoolean("export.jiix.primitives", false);
            _exportParams?.SetBoolean("export.jiix.chars", false);
        }

        private static List<Word> CloneWords(List<Word> from)
        {
            if (from == null)
                return null;

            List<Word> to = new List<Word>();

            foreach (var word in from)
            {
                var word_ = new Word();

                word_.Label = word.Label;
                word_.Updated = word.Updated;
                word_.Candidates = null;

                if (word.Candidates != null)
                {
                    word_.Candidates = new List<string>();
                    foreach (var candidate in word_.Candidates)
                        word_.Candidates.Add(candidate);
                }

                to.Add(word_);
            }

            return to;
        }

        private void onTimeout1(object sender, object e)
        {
            this.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
            _timer1.Stop();
        }

        private void onTimeout2(object sender, object e)
        {
            foreach (var child in textItem.Children)
            {
                var label = child as TextBlock;
                label.Foreground = new SolidColorBrush(SMART_GUIDE_TEXT_DEFAULT_COLOR);
            }

            _timer2.Stop();
        }

        private void BackupData()
        {
            _previousBlock?.Dispose();
            _previousBlock = _currentBlock?.ShallowCopy();
            _previousWords = CloneWords(_currentWords);
        }

        private void UpdateData()
        {
            if (_currentBlock == null)
            {
                _currentWords.Clear();
            }
            else if (_currentBlock.IsValid())
            {
                string jiixStr;

                try
                {
                    jiixStr = _editor.Export_(_currentBlock, MimeType.JIIX, _exportParams);
                }
                catch
                {
                    // when processing is ongoing, export may fail : ignore
                    return;
                }

                var words = new List<Word>();
                var jiix = JsonValue.Parse(jiixStr) as JsonObject;
                var jiixWords = (JsonArray)jiix["words"];
                foreach (var jiixWord_ in jiixWords)
                {
                    var jiixWord = (JsonObject)jiixWord_;

                    var label = (string)jiixWord["label"];

                    var candidates = new List<string>();
                    JsonValue jiixCandidates_;
                    if (jiixWord.TryGetValue("candidates", out jiixCandidates_))
                    {
                        var jiixCandidates = (JsonArray)jiixCandidates_;
                        foreach (var jiixCandidate_ in jiixCandidates)
                            candidates.Add((string)jiixCandidate_);
                    }

                    words.Add(new Word() { Label = label, Candidates = candidates, Updated = false });
                }

                _currentWords = words;

                if ((_previousBlock != null) && (_currentBlock.Id == _previousBlock.Id))
                {
                    ComputeTextDifferences(_previousWords, _currentWords);
                }
                else
                {
                    var count = _currentWords.Count;
                    for (var c = 0; c < count; ++c)
                    {
                        var word = _currentWords[c];
                        word.Updated = false;
                        _currentWords[c] = word;
                    }
                }
            }
        }

        private void ResetWidgets()
        {
            Visibility = Windows.UI.Xaml.Visibility.Collapsed;
            Margin = new Thickness(0, 0, 0, 0);
            textItem.Children.Clear();
            SetTextBlockStyle(TextBlockStyle.NORMAL);
        }

        private static void GetBlockPadding(ContentBlock block, out float paddingLeft, out float paddingRight)
        {
            paddingLeft = 0.0f;
            paddingRight = 0.0f;

            if (!string.IsNullOrEmpty(block.Attributes))
            {
                var attributes = JsonValue.Parse(block.Attributes) as JsonObject;
                JsonValue padding_;

                if (attributes.TryGetValue("padding", out padding_))
                {
                    var padding = padding_ as JsonObject;
                    paddingLeft = (float)(double)padding["left"];
                    paddingRight = (float)(double)padding["right"];
                }
            }
        }

        void SetTextBlockStyle(TextBlockStyle textBlockStyle)
        {
            switch (textBlockStyle)
            {
                case TextBlockStyle.H1:
                    styleItem.Text = "H1";
                    styleBorder.BorderBrush = new SolidColorBrush(Colors.Black);
                    styleBorder.Background = new SolidColorBrush(Colors.Black);
                    styleItem.Foreground = new SolidColorBrush(Colors.White);
                    break;

                case TextBlockStyle.H2:
                    styleItem.Text = "H2";
                    styleBorder.BorderBrush = new SolidColorBrush(SMART_GUIDE_CONTROL_COLOR);
                    styleBorder.Background = new SolidColorBrush(SMART_GUIDE_CONTROL_COLOR);
                    styleItem.Foreground = new SolidColorBrush(Colors.White);
                    break;

                case TextBlockStyle.H3:
                    styleItem.Text = "H3";
                    styleBorder.BorderBrush = new SolidColorBrush(SMART_GUIDE_CONTROL_COLOR);
                    styleBorder.Background = new SolidColorBrush(SMART_GUIDE_CONTROL_COLOR);
                    styleItem.Foreground = new SolidColorBrush(Colors.White);
                    break;

                case TextBlockStyle.NORMAL:
                default:
                    styleItem.Text = "¶";
                    styleBorder.BorderBrush = new SolidColorBrush(SMART_GUIDE_CONTROL_COLOR);
                    styleBorder.Background = new SolidColorBrush(Colors.White);
                    styleItem.Foreground = new SolidColorBrush(SMART_GUIDE_CONTROL_COLOR);
                    break;
            }
        }

        private void UpdateWidgets(UpdateCause cause)
        {
            if (_currentBlock != null)
            {
                // Update size and position
                var rectangle = _currentBlock.Box;
                float paddingLeft, paddingRight;
                GetBlockPadding(_currentBlock, out paddingLeft, out paddingRight);
                var transform = _editor.Renderer.GetViewTransform();
                var topLeft = transform.Apply(rectangle.X + paddingLeft, rectangle.Y);
                var topRight = transform.Apply(rectangle.X + rectangle.Width - paddingRight, rectangle.Y);
                var x = topLeft.X;
                var y = topLeft.Y;
                var width = topRight.X - topLeft.X;
                var yOffset = (ActualHeight > 0) ? ActualHeight : SMART_GUIDE_SIZE; // ActualHeight may be zero if control was collapsed

                Width = width;
                Margin = new Thickness(x, y - yOffset, 0, 0);

                TextBlock lastUpdatedItem = null;
                if ( (cause == UpdateCause.Edit) || (cause == UpdateCause.Selection) )
                {
                    // Update text
                    textItem.Children.Clear();

                    foreach (var word in _currentWords)
                    {
                        var label = word.Label;
                        label = label.Replace('\n', ' ');

                        var item = new TextBlock{
                                                    Text = label,
                                                    HorizontalAlignment = HorizontalAlignment.Left,
                                                    VerticalAlignment = VerticalAlignment.Stretch,
                                                    TextAlignment = TextAlignment.Left,
                                                    TextWrapping = TextWrapping.NoWrap,
                                                    Padding = new Thickness(0),
                                                    Margin = new Thickness(0),
                                                    MinWidth = 3,
                                                };

                        if (word.Updated)
                            item.Foreground = new SolidColorBrush(SMART_GUIDE_TEXT_HIGHLIGHT_COLOR);
                        else
                            item.Foreground = new SolidColorBrush(SMART_GUIDE_TEXT_DEFAULT_COLOR);

                        textItem.Children.Add(item);

                        if (word.Updated)
                            lastUpdatedItem = item;
                    }
                }

                // Set cursor position
                if (lastUpdatedItem != null)
                {
                    // Delay the scroll because the item's position/size within the scrollViewer are not updated yet
                    Windows.System.Threading.ThreadPoolTimer.CreateTimer
                            (
                                async (source) =>
                                {
                                    await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                                    {
                                        var tr = lastUpdatedItem.TransformToVisual((UIElement)scrollItem.Content);
                                        var pos = tr.TransformPoint(new Point(0, 0));
                                        scrollItem.ChangeView(pos.X, 0.0, 1.0f, true);
                                    });
                                },
                                TimeSpan.FromMilliseconds(10)
                            );
                }

                // Update Style item
                SetTextBlockStyle(TextBlockStyle.NORMAL);

                // Visibility/Fading
                {
                    int delay = 0;

                    if (cause == UpdateCause.Edit)
                        delay = _currentBlock.Id.StartsWith("diagram/") ? fadeOutWriteInDiagramDelay : fadeOutWriteDelay;
                    else
                        delay = fadeOutOtherDelay;

                    if (cause != UpdateCause.View)
                    {
                        _timer1.Stop();

                        if (delay > 0)
                        {
                            _timer1.Interval = TimeSpan.FromMilliseconds(delay);
                            _timer1.Start();
                        }

                        this.Visibility = Windows.UI.Xaml.Visibility.Visible;
                    }

                    if (lastUpdatedItem != null)
                    {
                        _timer2.Interval = TimeSpan.FromMilliseconds(removeHighlightDelay);
                        _timer2.Start();
                    }
                }
            }
            else
            {
                _timer1.Stop();
                ResetWidgets();
            }
        }

        public void OnPartChanged()
        {
            _previousBlock?.Dispose();
            _previousBlock = null;

            _currentBlock?.Dispose();
            _currentBlock = null;

            _activeBlock?.Dispose();
            _activeBlock = null;

            _selectedBlock?.Dispose();
            _selectedBlock = null;

            UpdateData();
            ResetWidgets();
        }

        public void OnContentChanged(string[] blockIds)
        {
            if (_editor == null)
            {
                ResetWidgets();
                return;
            }

            // The active block may have been removed then added again in which case
            // the old instance is invalid but can be restored by remapping the identifier
            if ( (_activeBlock != null) && !_activeBlock.IsValid())
            {
                _activeBlock?.Dispose();
                _activeBlock = _editor.GetBlockById(_activeBlock.Id);

                if (_activeBlock == null)
                    ResetWidgets();
            }

            if (_activeBlock != null)
            {
                if ((blockIds != null) && blockIds.Contains(_activeBlock.Id))
                {
                    _currentBlock?.Dispose();
                    _currentBlock = _activeBlock?.ShallowCopy();

                    BackupData();
                    UpdateData();
                    UpdateWidgets(UpdateCause.Edit);
                }
            }
        }

        public void OnSelectionChanged(string[] blockIds)
        {
            _selectedBlock?.Dispose();
            _selectedBlock = null;

            if (blockIds != null)
            {
                foreach (var blockId in blockIds)
                {
                    using (var block_ = _editor.GetBlockById(blockId))
                    {
                        if ((block_ != null) && (block_.Type == "Text"))
                        {
                            _selectedBlock = block_?.ShallowCopy();
                            break;
                        }
                    }
                }
            }

            bool selectionChanged = false;

            if ((_selectedBlock != null) && (_currentBlock != null))
                selectionChanged = (_currentBlock.Id != _selectedBlock.Id);
            else
                selectionChanged = (_selectedBlock != _currentBlock);

            if (selectionChanged)
            {
                if (_selectedBlock != null)
                {
                    BackupData();

                    _currentBlock?.Dispose();
                    _currentBlock = _selectedBlock?.ShallowCopy();

                    UpdateData();
                    UpdateWidgets(UpdateCause.Selection);
                }
                else
                {
                    ResetWidgets();
                }
            }
        }

        public void OnActiveBlockChanged(string blockId)
        {
            _activeBlock?.Dispose();
            _activeBlock = _editor.GetBlockById(blockId);

            if ( (_currentBlock != null) && (_activeBlock != null) && (_currentBlock.Id == _activeBlock.Id) )
                return; // selectionChanged already changed the active block

            BackupData();
            _currentBlock?.Dispose();
            _currentBlock = _activeBlock?.ShallowCopy();
            UpdateData();

            if (_currentBlock != null)
                UpdateWidgets(UpdateCause.Edit);
            else
                ResetWidgets();
        }

        public void OnTransformChanged()
        {
            UpdateWidgets(UpdateCause.View);
        }

        static private void ComputeTextDifferences(List<Word> s1, List<Word> s2)
        {
            var len1 = s1.Count;
            var len2 = s2.Count;

            uint[,] d = new uint[len1 + 1, len2 + 1];
            int i;
            int j;

            // Levenshtein distance algorithm at word level
            d[0,0] = 0;
            for(i = 1; i <= len1; ++i)
                d[i,0] = (uint)i;
            for(i = 1; i <= len2; ++i)
                d[0,i] = (uint)i;

            for(i = 1; i <= len1; ++i)
            {
                for(j = 1; j <= len2; ++j)
                {
                    var d_ = Math.Min(d[i - 1,j] + 1, d[i,j - 1] + 1);
                    d[i,j] = (uint)(Math.Min(d_ , d[i - 1,j - 1] + (s1[i - 1].Label == s2[j - 1].Label ? 0 : 1) ));
                }
            }

            // Backward traversal
            for (j = 0; j < len2; ++j)
            {
                var word = s2[j];
                word.Updated = true;
                s2[j] = word;
            }

            if ( (len1 > 0) && (len2 > 0) )
            {
                i = len1;
                j = len2;

                while (j > 0)
                {
                    int d01 = (int)d[i,j-1];
                    int d11 = (i > 0) ? (int)d[i-1,j-1] : -1;
                    int d10 = (i > 0) ? (int)d[i-1,j] : -1;

                    if ( (d11 >= 0) && (d11 <= d10) && (d11 <= d01) )
                    {
                        --i;
                        --j;
                    }
                    else if ( (d10 >= 0) && (d10 <= d11) && (d10 <= d01) )
                    {
                        --i;
                    }
                    else //if ( (d01 <= d11) && (d01 <= d10) )
                    {
                        --j;
                    }

                    if ( (i < len1) && (j < len2) )
                    {
                        var word = s2[j];
                        word.Updated = s1[i].Label != s2[j].Label;
                        s2[j] = word;
                    }
                }
            }
        }

        private void OnPointerEnterEvent(Object sender, PointerRoutedEventArgs e)
        {
            _pointerDown = false;
        }

        private void OnPointerLeaveEvent(Object sender, PointerRoutedEventArgs e)
        {
            _pointerDown = false;
        }

        private void OnPointerPressEvent(Object sender, PointerRoutedEventArgs e)
        {
            var pointer = e.GetCurrentPoint(this);

            if ( ((e.Pointer.PointerDeviceType == PointerDeviceType.Mouse) && pointer.Properties.IsLeftButtonPressed)
                || (e.Pointer.PointerDeviceType == PointerDeviceType.Pen)
                || (e.Pointer.PointerDeviceType == PointerDeviceType.Touch) )
            {
                _pointerDown = true;
                _pointerPosition = pointer.Position;
            }

            e.Handled = true;
        }

        private void OnPointerReleaseEvent(Object sender, PointerRoutedEventArgs e)
        {
            if (!_pointerDown)
                return;

            var pointer = e.GetCurrentPoint(this);

            if ( ((e.Pointer.PointerDeviceType == PointerDeviceType.Mouse) && !pointer.Properties.IsLeftButtonPressed)
                || (e.Pointer.PointerDeviceType == PointerDeviceType.Pen)
                || (e.Pointer.PointerDeviceType == PointerDeviceType.Touch) )
            {
                _pointerDown = false;
            }

            e.Handled = true;
        }

        private void OnPointerMoveEvent(Object sender, PointerRoutedEventArgs e)
        {
            var pointer = e.GetCurrentPoint(this);

            if (!_pointerDown)
                return;

            if (!pointer.IsInContact)
                return;

            e.Handled = true;

            if ( ((e.Pointer.PointerDeviceType == PointerDeviceType.Mouse) && pointer.Properties.IsLeftButtonPressed)
                || (e.Pointer.PointerDeviceType == PointerDeviceType.Pen)
                || (e.Pointer.PointerDeviceType == PointerDeviceType.Touch) )
            {
                var position = _pointerPosition;
                var offset = pointer.Position.X - position.X;

                _pointerPosition = pointer.Position;
                scrollItem.ChangeView(scrollItem.HorizontalOffset - offset, 0.0, 1.0f, true);
            }
        }

        private void OnPointerWheelEvent(Object sender, PointerRoutedEventArgs e)
        {
            e.Handled = true;
        }

        private void OnResultClicked(Object sender, TappedRoutedEventArgs e)
        {
            _timer1.Stop();

            int idx = 0;

            foreach (var item_ in textItem.Children)
            {
                var item = item_ as TextBlock;
                var xy = e.GetPosition(item);

                if ( (xy.X >= 0) && (xy.X < item.ActualWidth)
                    && (xy.Y >= 0) && (xy.Y < item.ActualHeight) )
                {
                    OnWordClicked(item, idx, e.GetPosition(null));
                    break;
                }

                ++idx;
            }

            e.Handled = true;
        }

        private void OnMoreClicked(Object sender, TappedRoutedEventArgs e)
        {
            if (_currentBlock != null)
            {
                var globalPos = e.GetPosition(null);
                _moreClicked?.Invoke(globalPos);
            }

            e.Handled = true;
        }

        private void OnWordClicked(TextBlock wordView, int wordIndex, Point globalPos)
        {
            if (wordView.Text == " ")
                return;

            try
            {
                var word = _currentWords[wordIndex];
                var flyoutMenu = new MenuFlyout();

                foreach (var candidate in word.Candidates)
                {
                    var item = new MenuFlyoutItem
                    {
                        Text = candidate,
                        FontWeight = (candidate == word.Label) ? FontWeights.Bold : FontWeights.Normal
                    };
                    if (candidate != word.Label)
                    {
                        item.Command = new CandidateFlyoutCommand
                        {
                            WordIndex = wordIndex,
                            WordView = wordView,
                            Label = candidate,
                            Handler = (cmd) => { OnCandidateClicked(cmd); }
                        };
                    }

                    flyoutMenu.Items.Add(item);
                }

                if (flyoutMenu.Items.Count == 0)
                {
                    var item = new MenuFlyoutItem
                    {
                        Text = word.Label,
                        FontWeight = FontWeights.Bold
                    };

                    flyoutMenu.Items.Add(item);
                }

                flyoutMenu.ShowAt(null, globalPos);
            }
            catch
            {
            }
        }

        private void OnCandidateClicked(CandidateFlyoutCommand command)
        {
            try
            {
                var jiixStr = _editor.Export_(_currentBlock, MimeType.JIIX, _exportParams);
                var jiix = JsonValue.Parse(jiixStr) as JsonObject;
                var jiixWords = (JsonArray)jiix["words"];
                var jiixWord = (JsonObject)jiixWords[command.WordIndex];
                jiixWord["label"] = command.Label;
                jiixStr = jiix.ToString();
                _editor.Import_(MimeType.JIIX, jiixStr, _currentBlock);
                _currentWords[command.WordIndex].Label = command.Label;
                command.WordView.Text = command.Label;
            }
            catch
            {
            }
        }
    }
}
