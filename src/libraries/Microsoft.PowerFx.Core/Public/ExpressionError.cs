﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.PowerFx.Core.Errors;
using Microsoft.PowerFx.Core.Localization;
using Microsoft.PowerFx.Syntax;
using Microsoft.PowerFx.Types;

namespace Microsoft.PowerFx
{
    /// <summary>
    /// Error message. This could be a compile time error from parsing or binding, 
    /// or it could be a runtime error wrapped in a <see cref="ErrorValue"/>.
    /// </summary>
    public class ExpressionError
    {
        public ExpressionError()
        {
        }

        /// <summary>
        /// A description of the error message. 
        /// </summary>
        public string Message
        {
            get
            {
                if (_message == null && this.MessageKey != null)
                {
                    (var shortMessage, var _) = ErrorUtils.GetLocalizedErrorContent(new ErrorResourceKey(this.MessageKey, this.ResourceManager), _messageLocale, out _);

                    var msg = ErrorUtils.FormatMessage(shortMessage, _messageLocale, _messageArgs);

                    _message = msg;
                }

                return _message;
            }

            // If this is set directly, it will skip localization. 
            set => _message = value;
        }

        internal IExternalStringResources ResourceManager;

        /// <summary>
        /// Source location for this error.
        /// </summary>
        public Span Span { get; set; }

        /// <summary>
        /// Runtime error code.This may be empty for compile-time errors. 
        /// </summary>
        public ErrorKind Kind { get; set; }

        public ErrorSeverity Severity { get; set; } = ErrorSeverity.Severe;

        public ErrorResourceKey ResourceKey
        {
            get => new (MessageKey, ResourceManager);
            set
            {
                MessageKey = value.Key;
                ResourceManager = value.ResourceManager;
            }
        }

        public string MessageKey { get; set; }

        public object[] MessageArgs
        {
            get => _messageArgs;            
            set => _messageArgs = value;
        }

        /// <summary>
        /// A warning does not prevent executing the error. See <see cref="Severity"/> for more details.
        /// </summary>
        public bool IsWarning => Severity < ErrorSeverity.Severe;

        // localize message lazily 
        private string _message; 
        internal object[] _messageArgs;
        private CultureInfo _messageLocale;

        internal CultureInfo MessageLocale => _messageLocale;

        /// <summary>
        /// Get a copy of this error message for the given locale. 
        /// <see cref="Message"/> will get lazily localized using <see cref="MessageKey"/>.
        /// </summary>
        /// <param name="culture"></param>
        /// <returns></returns>
        public ExpressionError GetInLocale(CultureInfo culture)
        {
            // In order to localize, we need a message key
            if (this.MessageKey != null)
            {
                var error = new ExpressionError
                {
                    Span = this.Span,
                    Kind = this.Kind,
                    Severity = this.Severity,                    
                    ResourceKey = this.ResourceKey,

                    // New message can be localized
                    _message = null, // will be lazily computed in new locale 
                    _messageArgs = this._messageArgs,
                    _messageLocale = culture
                };

                return error;
            }
           
            return this;            
        }   

        public override string ToString()
        {
            var prefix = IsWarning ? "Warning" : "Error";
            if (Span != null)
            {
                return $"{prefix} {Span.Min}-{Span.Lim}: {Message}";
            }
            else
            {
                return $"{prefix}: {Message}";
            }    
        }

        // Build the public object from an internal error object. 
        internal static ExpressionError New(IDocumentError error)
        {
            return new ExpressionError
            {
                _message = error.ShortMessage,
                _messageArgs = error.MessageArgs,
                MessageKey = error.MessageKey,
                ResourceManager = error.ResourceManager,
                Span = error.TextSpan,
                Severity = (ErrorSeverity)error.Severity                
            };
        }    

        internal static ExpressionError New(IDocumentError error, CultureInfo locale)
        {
            return new ExpressionError
            {
                _messageLocale = locale,
                _messageArgs = error.MessageArgs,
                MessageKey = error.MessageKey,
                ResourceManager = error.ResourceManager,
                Span = error.TextSpan,
                Severity = (ErrorSeverity)error.Severity                       
            };
        }

        internal static IEnumerable<ExpressionError> New(IEnumerable<IDocumentError> errors)
        {
            if (errors == null)
            {
                return Array.Empty<ExpressionError>();
            }
            else
            {
                return errors.Select(x => ExpressionError.New(x, CultureInfo.InvariantCulture)).ToArray();
            }
        }

        internal static IEnumerable<ExpressionError> New(IEnumerable<IDocumentError> errors, CultureInfo locale)
        {
            if (errors == null)
            {
                return Array.Empty<ExpressionError>();
            }
            else
            {
                return errors.Select(x => ExpressionError.New(x, locale)).ToArray();
            }
        }
    }

    /// <summary>
    /// Used to compare CheckResult.Errors and avoid duplicates.
    /// </summary>
    internal class ExpressionErrorComparer : EqualityComparer<ExpressionError>
    {
        // We compare only Message
        public override bool Equals(ExpressionError error1, ExpressionError error2)
        {
            if (error1 == null && error2 == null)
            {
                return true;
            }

            if (error1 == null || error2 == null)
            {
                return false;
            }

            return error1.ToString() == error2.ToString();
        }

        public override int GetHashCode(ExpressionError error)
        {            
            return error.ToString().GetHashCode();
        }
    }
}
