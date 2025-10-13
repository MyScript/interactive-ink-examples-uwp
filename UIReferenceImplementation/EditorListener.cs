// Copyright @ MyScript. All rights reserved.

using Windows.UI.Core;
using Windows.UI.Popups;
using MyScript.IInk.UIReferenceImplementation.UserControls;

namespace MyScript.IInk.UIReferenceImplementation
{
    public class EditorListener : IEditorListener
    {
        private EditorUserControl _ucEditor;

        public EditorListener(EditorUserControl ucEditor)
        {
            _ucEditor = ucEditor;
        }

        public void PartChanged(Editor editor)
        {
            if (_ucEditor.SmartGuideEnabled && _ucEditor.SmartGuide != null)
            {
                var dispatcher = _ucEditor.Dispatcher;
                var task = dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => { _ucEditor.SmartGuide.OnPartChanged(); });
            }
        }

        public void ContentChanged(Editor editor, string[] blockIds)
        {
            if (_ucEditor.SmartGuideEnabled && _ucEditor.SmartGuide != null)
            {
                var dispatcher = _ucEditor.Dispatcher;
                var task = dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => { _ucEditor.SmartGuide.OnContentChanged(blockIds); });
            }
        }

        public void ContentChanged2(Editor editor, string[] addedBlockIds, string[] removedBlockIds, string[] updatedBlockIds)
        {
            // Optional (only supported for "Raw Content" parts)
        }

        public void SelectionChanged(Editor editor)
        {
            if (_ucEditor.SmartGuideEnabled && _ucEditor.SmartGuide != null)
            {
                using (var selection = editor.GetSelection())
                {
                    var mode = editor.GetSelectionMode();
                    var blockIds = editor.GetIntersectingBlocks(selection);
                    var dispatcher = _ucEditor.Dispatcher;
                    var task = dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => { _ucEditor.SmartGuide.OnSelectionChanged(blockIds, mode); });
                }
            }
        }

        public void ActiveBlockChanged(Editor editor, string blockId)
        {
            if (_ucEditor.SmartGuideEnabled && _ucEditor.SmartGuide != null)
            {
                var dispatcher = _ucEditor.Dispatcher;
                var task = dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => { _ucEditor.SmartGuide.OnActiveBlockChanged(blockId); });
            }
        }

        public void OnError(Editor editor, string blockId, EditorError error, string message)
        {
            var dispatcher = _ucEditor.Dispatcher;
            var task = dispatcher.RunAsync(CoreDispatcherPriority.Normal,
                () =>
                {
                    var dlg = new MessageDialog(message);
                    var dlgTask = dlg.ShowAsync();
                });
        }
    }
}