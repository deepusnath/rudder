﻿using Microsoft.VisualStudio.TestTools.UnitTesting;
using static ScopeProgramAnalysis.ScopeProgramAnalysis;
using CodeUnderTest;
using System.Collections.Generic;
using System.Linq;

namespace SimpleTests
{
    [TestClass]
    public class UnitTest1
    {
        [TestMethod]
        public void TestMethod1()
        {
            var t = typeof(CopyProcessor);
            var log = AnalyzeDll(t.Assembly.Location, ScopeMethodKind.All, true,  
                                    false, false, null);
        }
        [TestMethod]
        public void TestMethod2()
        {
            var t = typeof(CopyProcessor);
            var run = AnalyzeProcessor(t, "JobGUID: string, JobName: string", "JobGUID: string, JobName: string");

        }
        [TestMethod]
        public void TestMethod3()
        {
            var t = typeof(AddOneColumnProcessor);
            var run = AnalyzeProcessor(t, "JobGUID: string, JobName: string", "JobGUID: string, JobName: string, NewColumn: string");

        }

        [TestMethod]
        public void TestMethod4()
        {
            var t = typeof(SubtypeOfCopyProcessor);
            var run = AnalyzeProcessor(t, "JobGUID: string, JobName: string", "JobGUID: string, JobName: string");
        }

        [TestMethod]
        public void TestDictValues()
        {
            var t = typeof(TestDictProcessor);
            var run = AnalyzeProcessor(t, "JobGUID: string, JobName: string", "JobGUID: string, JobName: string");
        }

        [TestMethod]
        public void ReturnMethodCall()
        {
            var t = typeof(ProcessReturningMethodCall);
            var run = AnalyzeProcessor(t, "JobGUID: string, JobName: string", "JobGUID: string, JobName: string");
            Assert.IsNotNull(run);
            Assert.IsTrue(run.Id == "ProcessReturningMethodCall");
            Assert.IsTrue(run.ToolNotifications.Count == 1);
            Assert.IsTrue(run.ToolNotifications[0].Message == "Closure class not found");
        }
        [TestMethod]
        public void TopN()
        {
            var t = typeof(TopN);
            var run = AnalyzeProcessor(t, "JobGUID: string, JobName: string", "JobGUID: string, JobName: string");
        }
        [TestMethod]
        public void AccumulateList()
        {
            var t = typeof(AccumulateList);
            var run = AnalyzeProcessor(t, "X: ulong, Y: int", "X: ulong, Y: int");
        }
        [TestMethod]
        public void UseDictionary()
        {
            var t = typeof(UseDictionary);
            var run = AnalyzeProcessor(t, "X: long, Y: int", "X: long");
        }
        [TestMethod]
        public void UseLastX()
        {
            var t = typeof(LastX);
            var run = AnalyzeProcessor(t, "X: double", "X: double");
        }
        [TestMethod]
        public void UseConditionalSchemaWriteColumn()
        {
            var t = typeof(ConditionalSchemaWriteColumn);
            var run = AnalyzeProcessor(t, "X: int", "X: int");
        }

    }
}
