﻿using System;
using JetBrains.Application.Progress;
using JetBrains.ProjectModel;
using JetBrains.ReSharper.Feature.Services.Bulbs;
using JetBrains.ReSharper.Feature.Services.CSharp.Bulbs;
using JetBrains.ReSharper.Intentions.Extensibility;
using JetBrains.TextControl;
using JetBrains.Util;
using YC.ReSharper.AbstractAnalysis.LanguageApproximation;

namespace ApproximatorTester
{
    [ContextAction(Name = "RunCSharpApproximator", Description = "Run Approximator for C#", Group = "C#")]
    public class RunCSharpApproximator : ContextActionBase
    {
        private readonly ICSharpContextActionDataProvider _provider;

        public RunCSharpApproximator(ICSharpContextActionDataProvider provider)
        {
            _provider = provider;
        }

        public override bool IsAvailable(IUserDataHolder cache)
        {
            return true;
        }

        protected override Action<ITextControl> ExecutePsiTransaction(ISolution solution, IProgressIndicator progress)
        {
            var inputFile = _provider.PsiFile;
            var ddg = ApproximationBuilderCSharp.buildDdg(inputFile);
            Utils.OutputCSharpResult(Utils.GenericCfgStructureToDot(ddg), _provider);
            return null;
        }

        public override string Text
        {
            get { return "Run Approximator for CSharp"; }
        }
    }
}