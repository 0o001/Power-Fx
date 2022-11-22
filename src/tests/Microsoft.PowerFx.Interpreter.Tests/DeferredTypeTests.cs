﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.PowerFx.Core.Localization;
using Microsoft.PowerFx.Types;
using Xunit;

namespace Microsoft.PowerFx.Interpreter
{
    public class DeferredTypeTests
    {
        [Theory]
        [InlineData("X")]
        [InlineData("$\"Test {X}\"")]
        [InlineData("!X")]
        [InlineData("Table(X)")]
        [InlineData("X.Field1")]
        [InlineData("{test: X}")]
        [InlineData("RX.XF")]
        [InlineData("RX.XF + 1")]

        [InlineData("X + 1")]
        [InlineData("X + \"1\"")]
        [InlineData("X + DateTime(2022, 11, 10, 0, 0, 0)")]
        [InlineData("X + Date(2022, 11, 10)")]
        [InlineData("X + Time(0, 0, 0)")]
        [InlineData("1 + X")]
        [InlineData("DateTime(2022, 11, 10, 0, 0, 0) + X")]
        [InlineData("Date(2022, 11, 10) + X")]
        [InlineData("Time(0, 0, 0) + X")]

        [InlineData("X * 1")]
        [InlineData("And(X, 1=1)")]
        [InlineData("X&\"test\"")]
        [InlineData("X = 1")]
        [InlineData("X = GUID(\"5cc45615-f759-4a53-b225-d3a2497f60ad\")")]

        [InlineData("X < 2")]
        [InlineData("X < DateTime(2022,11,10,0,0,0)")]
        [InlineData("\"The\" in X")]
        [InlineData("123 in X")]
        [InlineData("X in [1, 2, 3]")]
        [InlineData("123 in Table(X)")]
        [InlineData("IsBlank(X)")]
        [InlineData("IsError(X)")]
        [InlineData("Text(X)")]
        [InlineData("Value(X)")]
        [InlineData("Boolean(X)")]
        [InlineData("Index([1,2,3].Value, X)")]
        [InlineData("Sum(X, 1) < 5")]
        [InlineData("Sum(X, 1, R) < 5")] // All error are discarded for function calls, hence we don't get error for RecordType here.
        [InlineData("Sum(X, T) < 5")] // Since we discard all errors for function calls, Function calls are biased to non tabular overload.

        // Ensures expression binds without any errors - but issues a warning for the deferred(unknown) type.
        public void DeferredTypeTest(string script)
        {
            var symbolTable = new SymbolTable();

            // Record with deferred field
            var rX = RecordType.Empty().Add("XF", FormulaType.Deferred);

            symbolTable.AddVariable("X", FormulaType.Deferred);
            symbolTable.AddVariable("RX", rX);
            symbolTable.AddVariable("R", RecordType.Empty());
            symbolTable.AddVariable("T", RecordType.Empty().ToTable());

            TestDeferredTypeBindingWarning(script, Features.None, symbolTable);
        }

        [Theory]
        [InlineData("$\"Test {X} {R}\"", "Invalid argument type (Record). Expecting a Text value instead.")]
        [InlineData("X + R", "Invalid argument type. Expecting one of the following: Number, Text, Boolean.")]
        [InlineData("X.field + N.field", "Invalid use of '.'")]
        [InlineData("Index([1,2,3], X).missing", "Name isn't valid. 'missing' isn't recognized")]
        [InlineData("X < \"2021-12-09T20:28:52Z\"", "Invalid argument type. Expecting one of the following: Number, Date, Time, DateTime.")]
        [InlineData("First(Sum(X, 1))", "Invalid argument type (Number). Expecting a Table value instead.")]

        // Ensures expression issues an error if it exists, despite the deferred type.
        // NOTE: All error are discarded for function calls e.g. You don't get any errors for Table(deferred, number).
        public void DeferredTypeTest_Negative(string script, string errorMessage)
        {
            var symbolTable = new SymbolTable();
            symbolTable.AddVariable("X", FormulaType.Deferred);
            symbolTable.AddVariable("N", FormulaType.Number);
            symbolTable.AddVariable("R", RecordType.Empty());
            TestBindingError(script, Features.None, errorMessage, symbolTable);
        }

        private void TestDeferredTypeBindingWarning(string script, Features features, SymbolTable symbolTable = null)
        {
            var config = new PowerFxConfig(features)
            {
                SymbolTable = symbolTable
            };

            var engine = new RecalcEngine(config);
            var result = engine.Check(script);

            Assert.True(result.IsSuccess);

            Assert.True(result.Errors.Count() > 0);

            Assert.True(result.Errors.All(error => error.MessageKey.Equals(TexlStrings.WarnDeferredType.Key)));

            Assert.Throws<NotSupportedException>(() => CheckResultExtensions.GetEvaluator(result));
            Assert.Throws<AggregateException>(() => engine.Eval(script));
        }

        private void TestBindingError(string script, Features features, string errorMessage, SymbolTable symbolTable = null)
        {
            var config = new PowerFxConfig(features)
            {
                SymbolTable = symbolTable
            };

            var engine = new Engine(config);
            var result = engine.Check(script);

            Assert.False(result.IsSuccess);

            Assert.Contains(result.Errors, error => error.Message.Contains(errorMessage));
        }
    }
}