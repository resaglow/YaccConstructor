﻿using System.Collections.Generic;
using System.Linq;
using Highlighting.Core;
using JetBrains.Application.Settings;
using JetBrains.DocumentModel;
using JetBrains.ReSharper.Daemon;
using JetBrains.ReSharper.Daemon.Stages;
using JetBrains.ReSharper.Psi.Tree;
using JetBrains.Util;

namespace YC.ReSharper.AbstractAnalysis.Plugin.Highlighting
{
    public class IdentifierHighlighterProcess : MyIncrementalDaemonStageProcessBase
    {
        private List<TextRange> addedRanges = new List<TextRange>();
     
        public IdentifierHighlighterProcess(IDaemonProcess daemonProcess, IContextBoundSettingsStore settingsStore, DaemonProcessKind processKind)
            : base(daemonProcess, settingsStore, processKind)
        {
        }

        public override void VisitSomething(ITreeNode treeNode, IHighlightingConsumer consumer)
        {
            var myTreeNode = treeNode as IAbstractTreeNode;
            if (myTreeNode == null) 
                return;

            //DocumentRange colorRange = myTreeNode.GetNavigationRange();

            //if (colorRange.Document == null || addedRanges.Contains(colorRange.TextRange))
            //    return;

            //AddHighLighting(colorRange, consumer, new MySomethingHighlighting(treeNode));

            //List<DocumentRange> colorConstantRange = myTreeNode.UserData.GetData(new Key<List<DocumentRange>>("ranges"));
            ICollection<DocumentRange> colorConstantRange;
            colorConstantRange = myTreeNode.GetAllPositions();

            foreach (DocumentRange range in colorConstantRange)
            {
                if (range.Document != null && !addedRanges.Contains(range.TextRange))
                {
                    AddHighLighting(range, consumer, new MySomethingHighlighting(treeNode));
                }
            }
        }

        private void AddHighLighting(DocumentRange range, IHighlightingConsumer consumer, IHighlighting highlighting)
        {
            var info = new HighlightingInfo(range, highlighting, new Severity?());
            IFile file = this.File;

            if (file != null)
            {
                addedRanges.Add(range.TextRange);
                //consumer.Highlightings.Add(info);
                consumer.AddHighlighting(info.Highlighting, file);
            }
        }
    }
}
