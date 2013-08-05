﻿using System;
using System.Collections.Generic;
using System.ComponentModel.Composition.Hosting;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Text;
using SassyStudio.Compiler;
using SassyStudio.Compiler.Parsing;

namespace SassyStudio.Editor
{
    class SassEditorDocument
    {
        readonly ITextBuffer Buffer;
        readonly IParser Parser;
        readonly FileInfo SourceFile;

        public SassEditorDocument(ITextBuffer buffer, IParserFactory parserFactory)
        {
            Parser = parserFactory.Create();

            ITextDocument document;
            if (buffer.Properties.TryGetProperty(typeof(ITextDocument), out document))
                SourceFile = new FileInfo(document.FilePath);

            Buffer = buffer;
            Buffer.ChangedLowPriority += OnBufferChanged;

            Task.Run(() => Initialize(Buffer.CurrentSnapshot));
        }

        public static SassEditorDocument CreateFrom(ITextBuffer buffer, IParserFactory parserFactory)
        {
            return buffer.Properties.GetOrCreateSingletonProperty(() => new SassEditorDocument(buffer, parserFactory));
        }

        private ISassDocumentTree Tree { get; set; }

        private async Task Initialize(ITextSnapshot snapshot)
        {
            try
            {
                var tree = await Parse(snapshot);

                ReplaceTree(tree);
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }
        }

        public event EventHandler<TreeChangedEventArgs> TreeChanged;

        private async void OnBufferChanged(object sender, TextContentChangedEventArgs e)
        {
            // ignore stale event
            if (e.After != Buffer.CurrentSnapshot) return;

            // parse the new tree
            var current = await Parse(e.After);
            if (e.After == Buffer.CurrentSnapshot)
                ReplaceTree(current);
        }

        private void ReplaceTree(ISassDocumentTree current)
        {
            var original = Tree;
            //if (current != null)
            //    DumpTree(current, current.Items);
            // TODO: check to see if anything has changed

            Tree = current;
            OnTreeChanged(original, current);
        }

        private void DumpTree(ISassDocumentTree tree, ParseItemList items)
        {
            var pending = new Stack<ParseItem>();
            foreach (var sourceItem in items)
            {
                Logger.Log(string.Format("[{0},{1}] - {2}", sourceItem.Start, sourceItem.End, sourceItem.GetType()));
                if (sourceItem is Comment)
                    Logger.Log(tree.SourceText.GetText(sourceItem.Start, sourceItem.Length));

                var complex = sourceItem as ComplexItem;
                if (complex != null)
                    DumpTree(tree, complex.Children);
            }
        }

        private async Task<ISassDocumentTree> Parse(ITextSnapshot snapshot)
        {
            try
            {
                var context = new ParsingExecutionContext(new BufferSnapshotChangedCancellationToken(Buffer, snapshot));
                var items = await Parser.ParseAsync(new SnapshotTextProvider(snapshot), context);
                if (!context.IsCancellationRequested)
                {
                    Logger.Log(string.Format("Last Token {0:#0.00}", Parser.LastTokenizationDuration.TotalMilliseconds));
                    Logger.Log(string.Format("Last Parse {0:#0.00}", Parser.LastParsingDuration.TotalMilliseconds));
                }

                var tree = new SassDocumentTree(snapshot, items);
                return tree;
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
                return null;
            }
        }

        private void OnTreeChanged(ISassDocumentTree original, ISassDocumentTree current)
        {
            var handler = TreeChanged;
            if (handler != null)
            {
                handler(this, new TreeChangedEventArgs(current));
            }
        }
    }
}
