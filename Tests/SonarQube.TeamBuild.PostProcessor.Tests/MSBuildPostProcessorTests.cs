﻿//-----------------------------------------------------------------------
// <copyright file="MSBuildPostProcessorTests.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarQube.Common;
using SonarQube.TeamBuild.Integration;
using SonarScanner.Shim;
using TestUtilities;
using System.Linq;

namespace SonarQube.TeamBuild.PostProcessor.Tests
{
    [TestClass]
    public class MSBuildPostProcessorTests
    {
        public TestContext TestContext { get; set; }

        #region Tests

        [TestMethod]
        public void PostProc_ExecutionFailsIfCodeCoverageFails()
        {
            // Arrange
            PostProcTestContext context = new PostProcTestContext(this.TestContext);
            context.CodeCoverage.InitialiseValueToReturn = true;
            context.CodeCoverage.ProcessValueToReturn = false;

            // Act
            bool success = Execute(context);

            // Assert
            Assert.IsFalse(success, "Not expecting post-processor to have succeeded");

            context.CodeCoverage.AssertInitializedCalled();
            context.CodeCoverage.AssertExecuteCalled();
            context.Scanner.AssertNotExecuted();
            context.ReportBuilder.AssertNotExecuted();

            context.Logger.AssertErrorsLogged(0);
            context.Logger.AssertWarningsLogged(0);
        }

        [TestMethod]
        public void PostProc_ExecutionFailsIfSonarScannerFails()
        {
            // Arrange
            PostProcTestContext context = new PostProcTestContext(this.TestContext);
            context.Scanner.ValueToReturn = new ProjectInfoAnalysisResult();
            context.Scanner.ValueToReturn.RanToCompletion = false;

            // Act
            bool success = Execute(context);

            // Assert
            Assert.IsFalse(success, "Not expecting post-processor to have succeeded");

            context.CodeCoverage.AssertExecuteCalled();
            context.Scanner.AssertExecuted();
            context.ReportBuilder.AssertExecuted(); // should be called even if the sonar-scanner fails

            context.Logger.AssertErrorsLogged(0);
            context.Logger.AssertWarningsLogged(0);
        }

        [TestMethod]
        public void PostProc_ExecutionSucceeds()
        {
            // Arrange
            PostProcTestContext context = new PostProcTestContext(this.TestContext);
            context.Scanner.ValueToReturn = new ProjectInfoAnalysisResult();
            context.Scanner.ValueToReturn.RanToCompletion = true;

            // Act
            bool success = Execute(context);

            // Assert
            Assert.IsTrue(success, "Expecting post-processor to have succeeded");

            context.CodeCoverage.AssertInitializedCalled();
            context.CodeCoverage.AssertExecuteCalled();
            context.Scanner.AssertExecuted();

            context.ReportBuilder.AssertExecuted(); // should be called even if the sonar-scanner fails

            CollectionAssert.AreEqual(new string[] { "-Dsonar.scanAllFiles=true" }, context.Scanner.SuppliedCommandLineArgs.ToArray(), "Unexpected command line args passed to the sonar-scanner");

            context.Logger.AssertErrorsLogged(0);
            context.Logger.AssertWarningsLogged(0);
        }

        [TestMethod]
        public void PostProc_ExecutionSucceedsWithErrorLogs()
        {
            // Arrange
            PostProcTestContext context = new PostProcTestContext(this.TestContext);
            context.Scanner.ValueToReturn = new ProjectInfoAnalysisResult();
            context.Scanner.ValueToReturn.RanToCompletion = true;
            context.Scanner.ErrorToLog = "Errors";

            // Act
            bool success = Execute(context);

            // Assert
            Assert.IsTrue(success, "Expecting post-processor to have succeeded");

            context.CodeCoverage.AssertInitializedCalled();
            context.CodeCoverage.AssertExecuteCalled();
            context.Scanner.AssertExecuted();

            context.ReportBuilder.AssertExecuted(); // should be called even if the sonar-scanner fails

            CollectionAssert.AreEqual(new string[] { "-Dsonar.scanAllFiles=true" }, context.Scanner.SuppliedCommandLineArgs.ToArray(), "Unexpected command line args passed to the sonar-scanner");

            context.Logger.AssertErrorsLogged(1);
            context.Logger.AssertWarningsLogged(0);
        }

        [TestMethod]
        [Description("The coverage processing has 2 paths for fail - initialisation failures which are non-critical and processing errors that stop the post-processor workflow")]
        public void PostProc_ExecutionSucceedsIfCoverageNotInitialised()
        {
            // Arrange
            PostProcTestContext context = new PostProcTestContext(this.TestContext);
            context.CodeCoverage.InitialiseValueToReturn = false;
            context.Scanner.ValueToReturn = new ProjectInfoAnalysisResult();
            context.Scanner.ValueToReturn.RanToCompletion = true;

            // Act
            bool success = Execute(context);

            // Assert
            Assert.IsTrue(success, "Expecting post-processor to have succeeded");

            context.CodeCoverage.AssertInitializedCalled();
            context.CodeCoverage.AssertExecuteNotCalled();
            context.Scanner.AssertExecuted();
            context.ReportBuilder.AssertExecuted(); // should be called even if the sonar-scanner fails

            CollectionAssert.AreEqual(new string[] {"-Dsonar.scanAllFiles=true" }, context.Scanner.SuppliedCommandLineArgs.ToArray(), "Unexpected command line args passed to the sonar-scanner");

            context.Logger.AssertErrorsLogged(0);
            context.Logger.AssertWarningsLogged(0);
        }

        [TestMethod]
        public void PostProc_FailsOnInvalidArgs()
        {
            // Arrange
            PostProcTestContext context = new PostProcTestContext(this.TestContext);

            // Act
            bool success = Execute(context, "/d:sonar.foo=bar");

            // Assert
            Assert.IsFalse(success, "Expecting post-processor to have failed");

            context.CodeCoverage.AssertInitialisedNotCalled();
            context.CodeCoverage.AssertExecuteNotCalled();
            context.Scanner.AssertNotExecuted();
            context.ReportBuilder.AssertNotExecuted();

            context.Logger.AssertErrorsLogged(1);
            context.Logger.AssertWarningsLogged(0);
        }

        [TestMethod]
        public void PostProc_ValidArgsPassedThrough()
        {
            // Arrange
            PostProcTestContext context = new PostProcTestContext(this.TestContext);
            context.Scanner.ValueToReturn = new ProjectInfoAnalysisResult();
            context.Scanner.ValueToReturn.RanToCompletion = true;

            string[] suppliedArgs = new string[]
            {
                "/d:sonar.jdbc.password=dbpwd",
                "/d:sonar.jdbc.username=dbuser",
                "/d:sonar.password=\"my pwd\"",
                "/d:sonar.login=login"
            };

            string[] expectedArgs = new string[]
            {
                "-Dsonar.jdbc.password=dbpwd",
                "-Dsonar.jdbc.username=dbuser",
                "-Dsonar.password=\"my pwd\"",
                "-Dsonar.login=login",
                "-Dsonar.scanAllFiles=true"
            };

            // Act
            bool success = Execute(context, suppliedArgs);

            // Assert
            Assert.IsTrue(success, "Expecting post-processor to have succeeded");

            context.CodeCoverage.AssertExecuteCalled();
            context.CodeCoverage.AssertInitializedCalled();
            context.Scanner.AssertExecuted();
            context.ReportBuilder.AssertExecuted();

            CollectionAssert.AreEqual(expectedArgs, context.Scanner.SuppliedCommandLineArgs.ToArray(), "Unexpected command line args passed to the sonar-runner");

            context.Logger.AssertErrorsLogged(0);
            context.Logger.AssertWarningsLogged(0);
        }

        #endregion

        /// <summary>
        /// Helper class that creates all of the necessary mocks
        /// </summary>
        private class PostProcTestContext
        {
            private readonly TestLogger logger;
            private readonly TeamBuildSettings settings;

            private readonly MockCodeCoverageProcessor codeCoverage;
            private readonly MockSonarScanner scanner;
            private readonly MockSummaryReportBuilder reportBuilder;

            public PostProcTestContext(TestContext testContext)
            {
                this.Config = new AnalysisConfig();
                this.settings = TeamBuildSettings.CreateNonTeamBuildSettingsForTesting(testContext.DeploymentDirectory);

                this.logger = new TestLogger();
                this.codeCoverage = new MockCodeCoverageProcessor();
                this.scanner = new MockSonarScanner();
                this.reportBuilder = new MockSummaryReportBuilder();

                this.codeCoverage.InitialiseValueToReturn = true;
                this.codeCoverage.ProcessValueToReturn = true;
            }

            public AnalysisConfig Config { get; set; }
            public TeamBuildSettings Settings { get { return this.settings; } }
            public MockCodeCoverageProcessor CodeCoverage {  get { return this.codeCoverage; } }
            public MockSonarScanner Scanner { get { return this.scanner; } }
            public MockSummaryReportBuilder ReportBuilder { get { return this.reportBuilder; } }
            public TestLogger Logger { get { return this.logger; } }
        }

        #region Private methods

        private static bool Execute(PostProcTestContext context, params string[] args)
        {
            MSBuildPostProcessor proc = new MSBuildPostProcessor(context.CodeCoverage, context.Scanner, context.ReportBuilder);
            bool success = proc.Execute(args, context.Config, context.Settings, context.Logger);
            return success;
        }

        #endregion
    }
}
