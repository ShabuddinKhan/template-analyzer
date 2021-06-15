﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Azure.Templates.Analyzer.RuleEngines.JsonEngine.Expressions;
using Microsoft.Azure.Templates.Analyzer.RuleEngines.JsonEngine.Operators;
using Microsoft.Azure.Templates.Analyzer.Types;
using Microsoft.Azure.Templates.Analyzer.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace Microsoft.Azure.Templates.Analyzer.RuleEngines.JsonEngine.UnitTests
{
    [TestClass]
    public class AllOfExpressionTests
    {
        /// <summary>
        /// A mock implementation of an <see cref="Expression"/> for testing internal methods.
        /// </summary>
        private class MockExpression : Expression
        {
            public Func<IJsonPathResolver, JsonRuleEvaluation> EvaluationCallback { get; set; }

            public MockExpression(ExpressionCommonProperties commonProperties)
                : base(commonProperties)
            { }

            public override JsonRuleEvaluation Evaluate(IJsonPathResolver jsonScope)
            {
                return base.EvaluateInternal(jsonScope, EvaluationCallback);
            }
        }

        [DataTestMethod]
        [DataRow(true, true, DisplayName = "AllOf evaluates to true (true && true)")]
        [DataRow(true, false, DisplayName = "AllOf evaluates to false (true && false)")]
        [DataRow(false, true, DisplayName = "AllOf evaluates to false (false && true)")]
        [DataRow(false, false, DisplayName = "AllOf evaluates to false (false && false)")]
        [DataRow(false, false, "ResourceProvider/resource", DisplayName = "AllOf - scoped to resourceType - evaluates to false (false && false)")]
        [DataRow(false, false, "ResourceProvider/resource", "some.path", DisplayName = "AllOf - scoped to resourceType and path - evaluates to false (false && false)")]
        public void Evaluate_TwoLeafExpressions_ExpectedResultIsReturned(bool evaluation1, bool evaluation2, string resourceType = null, string path = null)
        {
            // Arrange
            var mockJsonPathResolver = new Mock<IJsonPathResolver>();
            var mockLineResolver = new Mock<ILineNumberResolver>().Object;

            // This AllOf will have 2 expressions
            var mockOperator1 = new Mock<LeafExpressionOperator>().Object;
            var mockOperator2 = new Mock<LeafExpressionOperator>().Object;

            var mockLeafExpression1 = new Mock<LeafExpression>(mockLineResolver, mockOperator1, new ExpressionCommonProperties { ResourceType = "ResourceProvider/resource", Path = "some.path" });
            var mockLeafExpression2 = new Mock<LeafExpression>(mockLineResolver, mockOperator2, new ExpressionCommonProperties { ResourceType = "ResourceProvider/resource", Path = "some.path" });

            var jsonRuleResult1 = new JsonRuleResult
            {
                Passed = evaluation1
            };

            var jsonRuleResult2 = new JsonRuleResult
            {
                Passed = evaluation2
            };

            mockJsonPathResolver
                .Setup(s => s.Resolve(It.Is<string>(path => path == "some.path")))
                .Returns(new List<IJsonPathResolver> { mockJsonPathResolver.Object });

            if (!string.IsNullOrEmpty(resourceType))
            {
                mockJsonPathResolver
                    .Setup(s => s.ResolveResourceType(It.Is<string>(type => type == resourceType)))
                    .Returns(new List<IJsonPathResolver> { mockJsonPathResolver.Object });
            }

            var results1 = new JsonRuleResult[] { jsonRuleResult1 };
            var results2 = new JsonRuleResult[] { jsonRuleResult2 };

            mockLeafExpression1
                .Setup(s => s.Evaluate(mockJsonPathResolver.Object))
                .Returns(new JsonRuleEvaluation(mockLeafExpression1.Object, evaluation1, results1));

            mockLeafExpression2
                .Setup(s => s.Evaluate(mockJsonPathResolver.Object))
                .Returns(new JsonRuleEvaluation(mockLeafExpression2.Object, evaluation2, results2));

            var expressionArray = new Expression[] { mockLeafExpression1.Object, mockLeafExpression2.Object };

            var allOfExpression = new AllOfExpression(expressionArray, new ExpressionCommonProperties { ResourceType = resourceType, Path = path });

            // Act
            var allOfEvaluation = allOfExpression.Evaluate(mockJsonPathResolver.Object);

            // Assert
            bool expectedAllOfEvaluation = evaluation1 && evaluation2;
            Assert.AreEqual(expectedAllOfEvaluation, allOfEvaluation.Passed);
            Assert.AreEqual(2, allOfEvaluation.Evaluations.Count());
            Assert.IsTrue(allOfEvaluation.HasResults);

            int expectedTrue = new[] { evaluation1, evaluation2 }.Count(e => e);
            int expectedFalse = 2 - expectedTrue;

            Assert.AreEqual(expectedTrue, allOfEvaluation.EvaluationsEvaluatedTrue.Count());
            Assert.AreEqual(expectedFalse, allOfEvaluation.EvaluationsEvaluatedFalse.Count());

            foreach (var evaluation in allOfEvaluation.Evaluations)
            {
                // Assert all leaf expressions have results and no evaluations
                Assert.IsTrue(evaluation.HasResults);
                Assert.AreEqual(0, evaluation.Evaluations.Count());
                Assert.AreEqual(1, evaluation.Results.Count());
            }
        }

        [TestMethod]
        public void Evaluate_SubResourceScopeNotFound_ExpectedResultIsReturned()
        {
            // Arrange
            var mockJsonPathResolver = new Mock<IJsonPathResolver>();
            mockJsonPathResolver
                .Setup(r => r.Resolve(It.IsAny<string>()))
                .Returns(() => new[] { mockJsonPathResolver.Object });
            mockJsonPathResolver
                .Setup(r => r.ResolveResourceType(It.IsAny<string>()))
                .Returns(() => new[] { mockJsonPathResolver.Object });

            // Create a mock expression for the Where condition.
            // It will return an Evaluation that has no results, but Passed is true.
            var whereExpression = new MockExpression(new ExpressionCommonProperties())
            {
                // This will only be executed if this where condition is evaluated.
                EvaluationCallback = pathResolver =>
                {
                    return new JsonRuleEvaluation(null, passed: true, results: Array.Empty<JsonRuleResult>());
                }
            };

            var mockLineResolver = new Mock<ILineNumberResolver>().Object;

            // This AnyOf will have 2 expressions
            // A top level mocked expression that contains a Where condition.
            var mockLeafExpression1 = new MockExpression(new ExpressionCommonProperties { ResourceType = "ResourceProvider/resource", Path = "some.path", Where = whereExpression })
            {
                // This will only be executed if the expression is evaluated.
                EvaluationCallback = pathResolver =>
                {
                    return null;
                }
            };
            var mockOperator2 = new Mock<LeafExpressionOperator>().Object;

            var mockLeafExpression2 = new Mock<LeafExpression>(mockLineResolver, mockOperator2, new ExpressionCommonProperties { ResourceType = "ResourceProvider/resource", Path = "some.path" });

            var jsonRuleResult2 = new JsonRuleResult
            {
                Passed = false
            };

            var results2 = new JsonRuleResult[] { jsonRuleResult2 };

            mockLeafExpression2
                .Setup(s => s.Evaluate(mockJsonPathResolver.Object))
                .Returns(new JsonRuleEvaluation(mockLeafExpression2.Object, false, results2));

            var expressionArray = new Expression[] { mockLeafExpression1, mockLeafExpression2.Object };

            var allOfExpression = new AllOfExpression(expressionArray, new ExpressionCommonProperties());

            // Act
            var allOfEvaluation = allOfExpression.Evaluate(mockJsonPathResolver.Object);

            // Assert
            Assert.AreEqual(false, allOfEvaluation.Passed);
            Assert.AreEqual(1, allOfEvaluation.Evaluations.Count());
            Assert.IsTrue(allOfEvaluation.HasResults);

            Assert.AreEqual(0, allOfEvaluation.EvaluationsEvaluatedTrue.Count());
            Assert.AreEqual(1, allOfEvaluation.EvaluationsEvaluatedFalse.Count());
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void Evaluate_NullScope_ThrowsException()
        {
            new AllOfExpression(new Expression[0], new ExpressionCommonProperties()).Evaluate(jsonScope: null);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void Constructor_NullExpressions_ThrowsException()
        {
            new AllOfExpression(null, new ExpressionCommonProperties());
        }
    }
}
