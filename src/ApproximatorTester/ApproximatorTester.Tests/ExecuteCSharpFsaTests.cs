﻿using JetBrains.ReSharper.Intentions.CSharp.Test;
using NUnit.Framework;

namespace ApproximatorTester.Tests
{
    [TestFixture]
    public class ExecuteCSharpFsaTests : CSharpContextActionExecuteTestBase<BuildFsaForCSharp>
    {
        protected override string ExtraPath
        {
            get { return "CSharpFsa"; }
        }

        protected override string RelativeTestDataPath
        {
            get { return "CSharpFsa"; }
        }

        [Test]
        public void TestSimpleQuery()
        {
            DoTestFiles("SimpleQuery.cs");
        }

        [Test]
        public void TestFourVars()
        {
            DoTestFiles("FourVars.cs");
        }

        [Test]
        public void TestSimpleMethodCall()
        {
            DoTestFiles("SimpleMethodCall.cs");
        }

        [Test]
        public void TestMethodWithRefArg()
        {
            DoTestFiles("MethodWithRefArg.cs");
        }

        [Test]
        public void TestMethodWithMethodCallArg()
        {
            DoTestFiles("MethodWithMethodCallArg.cs");
        }

        [Test]
        public void TestIfWithoutElse()
        {
            DoTestFiles("IfWithoutElse.cs");
        }

        [Test]
        public void TestSimpleConcat()
        {
            DoTestFiles("SimpleConcat.cs");
        }
    }
}