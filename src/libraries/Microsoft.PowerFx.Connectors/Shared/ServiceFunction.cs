﻿// <autogenerated>
// Use autogenerated to suppress styelcop warnings since this is shared from another repo.

// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PowerFx;
using Microsoft.PowerFx.Connectors;
using Microsoft.PowerFx.Core.App.ErrorContainers;
using Microsoft.PowerFx.Core.Functions;
using Microsoft.PowerFx.Core.Functions.Publish;
using Microsoft.PowerFx.Core.Localization;
using Microsoft.PowerFx.Core.Types;
using Microsoft.PowerFx.Core.Utils;
using Microsoft.PowerFx.Functions;
using Microsoft.PowerFx.Intellisense;
using Microsoft.PowerFx.Syntax;
using Microsoft.PowerFx.Types;
using Contracts = Microsoft.PowerFx.Core.Utils.Contracts;

namespace Microsoft.AppMagic.Authoring.Texl.Builtins
{
    [System.Diagnostics.DebuggerDisplay("ServiceFunction: {LocaleSpecificName}")]
    internal sealed class ServiceFunction : BuiltinFunction, IAsyncTexlFunction2, IHasUnsupportedFunctions
    {
        private readonly List<string[]> _signatures;
        private readonly string[] _orderedRequiredParams;
        private readonly Dictionary<string, TypedName> _optionalParamInfo;
        private readonly Dictionary<string, string> _parameterDescriptionMap;
        private readonly bool _isBehaviorOnly;
        private readonly bool _isAutoRefreshable;
        private readonly bool _isDynamic;
        private readonly bool _isCacheEnabled;
        private readonly int _cacheTimeoutMs;
        private readonly bool _isHidden;
        private readonly Dictionary<TypedName, List<string>> _parameterOptions;
        private readonly Dictionary<string, Tuple<string, DType>> _parameterDefaultValues;
        private readonly WeakReference<IService> _parentService;
        private readonly string _actionName;
        private readonly bool _numberIsFloat;
        private readonly string _pageLink;
        private readonly bool _isDeprecated;
        private readonly bool _isSupported;
        private readonly string _notSupportedReason;
        private readonly int _maxRows;
        internal readonly ServiceFunctionParameterTemplate[] _requiredParameters;

        public IEnumerable<TypedName> OptionalParams => _optionalParamInfo.Values;
        public Dictionary<string, TypedName> OptionalParamInfo => _optionalParamInfo;
        public override Capabilities Capabilities => Capabilities.OutboundInternetAccess | Capabilities.EnterpriseAuthentication | Capabilities.PrivateNetworkAccess;
        public override bool IsHidden => _isHidden;
        public override bool IsSelfContained => !_isBehaviorOnly;
        public bool IsPageable => !string.IsNullOrEmpty(_pageLink);
        public bool IsDeprecated => _isDeprecated;
        public bool IsNotSupported => !_isSupported;
        public string NotSupportedReason => _notSupportedReason;

        // Provide as hook for execution. 
        public ScopedHttpFunctionInvoker _invoker { get; init; }

        public ServiceFunction(IService parentService, DPath theNamespace, string name, string localeSpecificName, string description, DType returnType, BigInteger maskLambdas, int arityMin, int arityMax, bool isBehaviorOnly,
            bool isAutoRefreshable, bool isDynamic, bool isCacheEnabled, int cacheTimeoutMs, bool isHidden, Dictionary<TypedName, List<string>> parameterOptions, ServiceFunctionParameterTemplate[] optionalParamInfo,
            ServiceFunctionParameterTemplate[] requiredParamInfo, Dictionary<string, Tuple<string, DType>> parameterDefaultValues, string pageLink, bool isSupported, string notSupportedReason, bool isDeprecated, int maxRows,
            string actionName = "", bool numberIsFloat = false, params DType[] paramTypes)
            : base(theNamespace, name, localeSpecificName, (l) => description, FunctionCategories.REST, returnType, maskLambdas, arityMin, arityMax, paramTypes)
        {
            Contracts.AssertValueOrNull(parentService);
            Contracts.AssertValueOrNull(localeSpecificName);
            Contracts.AssertValue(description);
            Contracts.AssertValue(parameterOptions);
            Contracts.AssertValue(optionalParamInfo);
            Contracts.AssertValue(requiredParamInfo);
            Contracts.AssertValue(parameterDefaultValues);
            Contracts.AssertValue(paramTypes);

            // These asserts verify that the parameter containers have the correct length.
            Contracts.Assert(paramTypes.Length == arityMax);
            Contracts.Assert(optionalParamInfo.Length != 0 || ((arityMin == arityMax) && (paramTypes.Length == requiredParamInfo.Length)));
            Contracts.Assert(optionalParamInfo.Length == 0 || ((arityMax == arityMin + 1) && (paramTypes.Length == requiredParamInfo.Length + 1)));
            Contracts.Assert(arityMin <= arityMax && arityMax <= arityMin + 1, "We only support up to one additional options argument");

            if (parentService != null)
                _parentService = new WeakReference<IService>(parentService, trackResurrection: false);

            _optionalParamInfo = new Dictionary<string, TypedName>(optionalParamInfo.Length);
            _parameterDescriptionMap = new Dictionary<string, string>(optionalParamInfo.Length + requiredParamInfo.Length);

            foreach (var optionalParam in optionalParamInfo)
            {
                if (_optionalParamInfo.ContainsKey(optionalParam.TypedName.Name))
                {
                    throw new PowerFxConnectorException($"Conflict between optional parameters: twice the same parameter at different locations: {optionalParam.TypedName.Name}");
                }

                _optionalParamInfo.Add(optionalParam.TypedName.Name, optionalParam.TypedName);
                _parameterDescriptionMap.Add(optionalParam.TypedName.Name.Value, optionalParam.Description);
            }

            foreach (var requiredParam in requiredParamInfo)
            {
                if (_parameterDescriptionMap.ContainsKey(requiredParam.TypedName.Name.Value))
                {
                    throw new PowerFxConnectorException($"Conflict between required parameters: twice the same parameter at different locations: {requiredParam.TypedName.Name.Value}");
                }

                _parameterDescriptionMap.Add(requiredParam.TypedName.Name.Value, requiredParam.Description);
            }

            _signatures = new List<string[]>();
            _parameterOptions = parameterOptions;
            _isBehaviorOnly = isBehaviorOnly;
            _isAutoRefreshable = isAutoRefreshable;
            _isDynamic = isDynamic;
            _isCacheEnabled = isCacheEnabled;
            _cacheTimeoutMs = cacheTimeoutMs;
            _isHidden = isHidden;
            _orderedRequiredParams = requiredParamInfo.Select(p => p.TypedName.Name.Value).ToArray();
            _signatures.Add(_orderedRequiredParams);
            _parameterDefaultValues = parameterDefaultValues;
            _actionName = actionName;
            _requiredParameters = requiredParamInfo;
            _numberIsFloat = numberIsFloat;
            _pageLink = pageLink;
            _isSupported = isSupported;
            _notSupportedReason = notSupportedReason;
            _isDeprecated = isDeprecated;
            _maxRows = maxRows;

            if (arityMax > arityMin)
            {
                Contracts.Assert(arityMax == arityMin + 1, "We currently only expect one extra param, holding the object with the optional arguments specified by name.");

                string[] optionalSignature = new string[arityMax];
                _orderedRequiredParams.CopyTo(optionalSignature, 0);

                var optionFormat = new StringBuilder(TexlLexer.PunctuatorCurlyOpen);
                string sep = "";

                // $$$ can't use current culture
                string listSep = TexlLexer.GetLocalizedInstance(CultureInfo.CurrentCulture).LocalizedPunctuatorListSeparator + " ";
                foreach (var option in optionalParamInfo)
                {
                    optionFormat.Append(sep);
                    optionFormat.Append(TexlLexer.EscapeName(option.TypedName.Name.ToString()));
                    optionFormat.Append(TexlLexer.PunctuatorColon);
                    optionFormat.Append(option.TypedName.Type.GetKindString());
                    sep = listSep;
                }
                optionFormat.Append(TexlLexer.PunctuatorCurlyClose);

                optionalSignature[arityMax - 1] = optionFormat.ToString();
                _signatures.Add(optionalSignature);
            }
        }

        public string ActionName { get { return _actionName; } }

        // Service functions are asyncronous
        public override bool IsAsync { get { return true; } }

        // Multiple invocations with the same args may result in different return values.
        public override bool IsStateless { get { return false; } }

        // This function may or may not be behavior-only.
        public override bool IsBehaviorOnly { get { return _isBehaviorOnly; } }

        public override bool IsAutoRefreshable { get { return _isAutoRefreshable; } }

        public bool IsCacheEnabled { get { return _isCacheEnabled; } }

        public int CacheTimeoutMs { get { return _cacheTimeoutMs; } }

        // Service functions currently require all columns to be filled.
        public override bool RequireAllParamColumns { get { return true; } }

        // Service functions do not have hardwired help links.
        // If some service function happens to have a help link (URL), it would have to be returned by this override.
        public override string HelpLink { get { return string.Empty; } }

        public IService ParentService
        {
            get
            {
                IService service;
                if (_parentService != null && _parentService.TryGetTarget(out service))
                    return service;

                return null;
            }
        }

        // Helper class so that we can return StringGetters from GetSignatures
        private class CaptureString
        {
            private string value;

            public CaptureString(string what)
            {
                value = what;
            }

            public string GetValue(string locale)
            {
                return value;
            }
        }

        public override IEnumerable<TexlStrings.StringGetter[]> GetSignatures()
        {
            foreach (string[] signature in _signatures)
            {
                var getters = new TexlStrings.StringGetter[signature.Length];
                for (int i = 0; i < signature.Length; i++)
                {
                    var captureValue = new CaptureString(signature[i]);
                    getters[i] = captureValue.GetValue;
                }

                yield return getters;
            }
        }


        public override bool CheckTypes(CheckTypesContext context, TexlNode[] args, DType[] argTypes, IErrorContainer errors, out DType returnType, out Dictionary<TexlNode, DType> nodeToCoercedTypeMap)
        {
            Contracts.AssertValue(args);
            Contracts.AssertValue(argTypes);
            Contracts.Assert(args.Length == argTypes.Length);
            Contracts.AssertValue(errors);
            Contracts.Assert(MinArity <= args.Length && args.Length <= MaxArity);

            bool fArgsValid = base.CheckTypes(context, args, argTypes, errors, out returnType, out nodeToCoercedTypeMap);

            return fArgsValid;
        }

        public override async Task<ConnectorSuggestions> GetConnectorSuggestionsAsync(FormulaValue[] knownParameters, int argPosition, CancellationToken cts)
        {
            if (argPosition >= 0 && MaxArity > 0 && _requiredParameters.Length > MaxArity - 1)
            {
                ConnectorDynamicValue cdv = _requiredParameters[Math.Min(argPosition, MaxArity - 1)].ConnectorDynamicValue;

                if (cdv != null && cdv.ServiceFunction != null)
                {
                    FormulaValue result = await ConnectorDynamicCallAsync(cdv, knownParameters, cts).ConfigureAwait(false);
                    List<ConnectorSuggestion> suggestions = new List<ConnectorSuggestion>();

                    if (result is ErrorValue ev)
                    {
                        return new ConnectorSuggestions(ev);
                    }

                    if (result is RecordValue rv)
                    {
                        if (!string.IsNullOrEmpty(cdv.ValuePath))
                        {
                            FormulaValue collection = rv.GetField(cdv.ValueCollection ?? "value");

                            if (collection is TableValue tv)
                            {
                                foreach (DValue<RecordValue> row in tv.Rows)
                                {
                                    FormulaValue suggestion = row.Value.GetField(cdv.ValuePath);
                                    string displayName = (row.Value.GetField(cdv.ValueTitle) as StringValue)?.Value;

                                    suggestions.Add(new ConnectorSuggestion(suggestion, displayName));
                                }
                            }
                            else
                            {
                                throw new NotImplementedException($"Expecting a TableValue and got {collection.GetType().FullName}");
                            }
                        }
                        else
                        {
                            throw new NotImplementedException($"ValuePath is null");
                        }
                    }

                    return new ConnectorSuggestions(suggestions);
                }

                ConnectorDynamicSchema cds = _requiredParameters[Math.Min(argPosition, MaxArity - 1)].ConnectorDynamicSchema;

                if (cds != null && cds.ServiceFunction != null && !string.IsNullOrEmpty(cds.ValuePath))
                {
                    FormulaValue result = await ConnectorDynamicCallAsync(cds, knownParameters.Take(Math.Min(argPosition, MaxArity - 1)).ToArray(), cts).ConfigureAwait(false);
                    List<ConnectorSuggestion> suggestions = new List<ConnectorSuggestion>();

                    if (result is ErrorValue ev)
                    {
                        return new ConnectorSuggestions(ev);
                    }

                    foreach (string vpPart in (cds.ValuePath + "/properties").Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        result = ((RecordValue)result).Fields.FirstOrDefault(f => f.Name.Equals(vpPart, StringComparison.OrdinalIgnoreCase)).Value;
                    }

                    foreach (NamedValue nv in ((RecordValue)result).Fields)
                    {
                        suggestions.Add(new ConnectorSuggestion(nv.Value, nv.Name));
                    }

                    return new ConnectorSuggestions(suggestions);
                }

                // Neither dynamic value nor dynamic schema
                return null;
            }

            return null;
        }

        private FormulaValue[] GetArguments(ConnectionDynamicApi dynamicApi, CallNode callNode)
        {
            List<FormulaValue> arguments = new List<FormulaValue>();

            foreach (ServiceFunctionParameterTemplate sfpt in dynamicApi.ServiceFunction._requiredParameters)
            {
                string paramName = sfpt.TypedName.Name;
                DType paramType = sfpt.TypedName.Type;

                string currentFunctionParamName = dynamicApi.ParameterMap.FirstOrDefault(kvp => kvp.Value == paramName).Value;
                int currentFunctionParamIndex = _requiredParameters.FindIndex(st => st.TypedName.Name == currentFunctionParamName);
                TexlNode texlNode = callNode.Args.ChildNodes[currentFunctionParamIndex];

                FormulaValue arg = texlNode switch
                {
                    StrLitNode str => FormulaValue.New(str.Value),
                    NumLitNode num => FormulaValue.New(num.ActualNumValue),
                    _ => null
                };

                if (arg == null)
                {
                    return null;
                }

                arguments.Add(arg);
            }

            return arguments.ToArray();
        }

        private async Task<FormulaValue> ConnectorDynamicCallAsync(ConnectionDynamicApi dynamicApi, FormulaValue[] arguments, CancellationToken cts)
        {
            cts.ThrowIfCancellationRequested();
            return await dynamicApi.ServiceFunction.InvokeAsync(FormattingInfoHelper.CreateFormattingInfo(), arguments, cts).ConfigureAwait(false);
        }

        // This method returns true if there are special suggestions for a particular parameter of the function.
        public override bool HasSuggestionsForParam(int argumentIndex)
        {
            Contracts.Assert(0 <= argumentIndex);

            return argumentIndex <= MaxArity;
        }

        // Given the input type and the index of the argument, this function returns the acceptable suggestion string and type.
        public IEnumerable<KeyValuePair<string, DType>> GetServiceFunctionArgumentSuggestions(DType scopeType, int argumentIndex, out bool requiresSuggestionEscaping)
        {
            Contracts.Assert(scopeType.IsValid);
            Contracts.Assert(0 <= argumentIndex);

            requiresSuggestionEscaping = false;
            if (argumentIndex < MinArity)
            {
                Contracts.Assert(_orderedRequiredParams.Length > argumentIndex);
                return GetOptionSuggestions(_orderedRequiredParams[argumentIndex]);
            }

            return _optionalParamInfo.Select(x =>
                new KeyValuePair<string, DType>(TexlLexer.PunctuatorCurlyOpen + TexlLexer.EscapeName(x.Key) + TexlLexer.PunctuatorColon, x.Value.Type));
        }

        // Given the parameter name, this method returns the options available for the parameter, if any.
        public IEnumerable<KeyValuePair<string, DType>> GetOptionSuggestions(string paramName)
        {
            Contracts.AssertNonEmpty(paramName);

            if (!_parameterOptions.Any())
                return EnumerableUtils.Yield<KeyValuePair<string, DType>>();

            var option = _parameterOptions.Where(x => x.Key.Name.Value.Equals(paramName));
            Contracts.Assert(option.Count() <= 1);

            if (option.Count() == 0)
                return EnumerableUtils.Yield<KeyValuePair<string, DType>>();

            List<KeyValuePair<string, DType>> suggestions = new List<KeyValuePair<string, DType>>();
            var paramOptions = option.FirstOrDefault();
            DType paramType = paramOptions.Key.Type;
            string paramTypeString = paramType.ToString();

            foreach (var val in paramOptions.Value)
            {
                switch (paramTypeString)
                {
                    case "s":
                        suggestions.Add(new KeyValuePair<string, DType>("\"" + val + "\"", paramType));
                        break;
                    case "n":
                    case "b":
                        suggestions.Add(new KeyValuePair<string, DType>(val, paramType));
                        break;
                    default:
                        Contracts.Assert(false, "Parameter options should be of primitive type.");
                        break;
                }
            }

            return suggestions;
        }

        // Fetch the description associated with the specified parameter name.
        // If the param has no description, this will return false.
        public override bool TryGetParamDescription(string paramName, out string paramDescription)
        {
            Contracts.AssertNonEmpty(paramName);

            return _parameterDescriptionMap.TryGetValue(paramName, out paramDescription);
        }

        public bool TryGetParamDefaultValue(string paramName, out Tuple<string, DType> defaultValue)
        {
            Contracts.AssertValue(paramName);

            return _parameterDefaultValues.TryGetValue(paramName, out defaultValue);
        }


        public async Task<FormulaValue> InvokeAsync(FormattingInfo context, FormulaValue[] args, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            FormulaValue result = await (_invoker ?? throw new InvalidOperationException($"Function {Name} can't be invoked.")).InvokeAsync(context, args, cancellationToken).ConfigureAwait(false);
            result = await PostProcessResultAsync(result, cancellationToken).ConfigureAwait(false);

            return result;
        }

        // Can return 3 possible FormulaValues
        // - PagesRecordValue if the next page has a next link
        // - RecordValue if there is no next link
        // - ErrorValue
        private async Task<FormulaValue> GetNextPageAsync(string nextLink, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            FormulaValue result = await _invoker.InvokeAsync(nextLink, cancellationToken).ConfigureAwait(false);
            result = await PostProcessResultAsync(result, cancellationToken).ConfigureAwait(false);

            return result;
        }

        private async Task<FormulaValue> PostProcessResultAsync(FormulaValue result, CancellationToken cancellationToken)
        {
            ExpressionError er = null;

            if (result is ErrorValue ev && (er = ev.Errors.FirstOrDefault(e => e.Kind == ErrorKind.Network)) != null)
            {
                result = FormulaValue.NewError(new ExpressionError() { Kind = er.Kind, Severity = er.Severity, Message = $"{Namespace.ToDottedSyntax()}.{Name} failed: {er.Message}" }, ev.Type);
            }

            if (IsPageable && result is RecordValue rv)
            {
                (bool b, FormulaValue pageLink) = await rv.TryGetFieldAsync(FormulaType.String, _pageLink, cancellationToken).ConfigureAwait(false);
                if (true)
                {
                    string nextLink = (pageLink as StringValue)?.Value;

                    // If there is no next link, we'll return a "normal" RecordValue as no paging is needed
                    if (!string.IsNullOrEmpty(nextLink))
                    {
                        result = new PagedRecordValue(rv, async (cancellationToken) => await GetNextPageAsync(nextLink, cancellationToken).ConfigureAwait(false), _maxRows);
                    }
                }
            }

            return result;
        }

        // Swap for IService, to cut dependency on TransportType.
        public class IService
        {
        }
    }
}
