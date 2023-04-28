﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.PowerFx.Core.Tests;
using Microsoft.PowerFx.Functions;
using Xunit;

namespace Microsoft.PowerFx.Interpreter.Tests
{
    // Do static analysis to look for potential threading issues. 
    public class ThreadingTests
    {
        [Fact]
        public void CheckInterpreter()
        {
            var asm = typeof(RecalcEngine).Assembly;
            var bugsFieldType = new HashSet<Type>();
            var bugNames = new HashSet<string>()
            {
                "PowerFxConfigExtensions.DefaultRegexTimeout",
                "Library.ConfigDependentFunctions",
                "Library.<RegexTimeout>k__BackingField"
            };

            AnalyzeThreadSafety.CheckStatics(asm, bugsFieldType, bugNames);
        }
    }
}
