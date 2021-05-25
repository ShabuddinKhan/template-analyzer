// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Azure.Templates.Analyzer.RuleEngines.PowerShellEngine.UnitTests
{
    [TestClass]
    public class PowerShellRuleEngineTests
    {
        private readonly string TemplatesFolder = @"..\..\..\templates\";

        [DataTestMethod]
        [DataRow(@"success.json", 0, new int[] { }, DisplayName = "Base template")]
        [DataRow(@"error_without_line_number.json", 1, new int[] { 0 }, DisplayName = "Template with an error reported without a line number")]
        [DataRow(@"error_with_line_number.json", 1, new int[] { 9 }, DisplayName = "Template with an error reported with a line number")]
        [DataRow(@"warning.json", 1, new int[] { 0 }, DisplayName = "Template with a warning")]
        public void EvaluateRules_ValidTemplate_ReturnsExpectedEvaluations(string templateFileName, int expectedErrorCount, dynamic lineNumbers)
        {
            var templateFilePath = TemplatesFolder + templateFileName;

            var evaluations = PowerShellRuleEngine.EvaluateRules(templateFilePath);

            var failedEvaluations = new List<PowerShellRuleEvaluation>();
            
            foreach (PowerShellRuleEvaluation evaluation in evaluations)
            {
                if (evaluation.Passed)
                {
                    Assert.IsFalse(evaluation.HasResults);
                }
                else
                {
                    Assert.IsTrue(evaluation.HasResults);
                    Assert.AreEqual(1, evaluation.Results.ToList().Count);

                    failedEvaluations.Add(evaluation);
                }
            }

            Assert.AreEqual(expectedErrorCount, failedEvaluations.Count);

            Assert.AreEqual(failedEvaluations.Count, lineNumbers.Length);
            for (int errorNumber = 0; errorNumber < lineNumbers.Length; errorNumber++)
            {
                Assert.AreEqual(lineNumbers[errorNumber], failedEvaluations[errorNumber].Results.First().LineNumber);
                Assert.IsFalse(failedEvaluations[errorNumber].RuleDescription.Contains(" on line: "));
            }
        }

        [TestMethod]
        public void EvaluateRules_RepeatedErrorSameMessage_ReturnsExpectedEvaluations()
        {
            var templateFilePath = TemplatesFolder + "repeated_error_same_message.json";

            var evaluations = PowerShellRuleEngine.EvaluateRules(templateFilePath);

            Assert.AreEqual(1, evaluations.ToList().Count);

            var resultsList = evaluations.First().Results.ToList();

            Assert.AreEqual(2, resultsList.Count);

            Assert.AreEqual(9, resultsList[0].LineNumber);
            Assert.AreEqual(13, resultsList[1].LineNumber);
        }

        [TestMethod]
        public void EvaluateRules_RepeatedErrorDifferentMessage_ReturnsExpectedEvaluations()
        {
            var templateFilePath = TemplatesFolder + "repeated_error_different_message.json";

            var evaluations = PowerShellRuleEngine.EvaluateRules(templateFilePath);

            var evaluationsList = evaluations.ToList();

            Assert.AreEqual(2, evaluationsList.Count);

            Assert.AreEqual(1, evaluationsList[0].Results.ToList().Count);
            Assert.AreEqual(1, evaluationsList[1].Results.ToList().Count);

            Assert.AreEqual(evaluationsList[0].RuleName, evaluationsList[1].RuleName);
            Assert.AreNotEqual(evaluationsList[0].RuleDescription, evaluationsList[1].RuleDescription);
        }
    }
}