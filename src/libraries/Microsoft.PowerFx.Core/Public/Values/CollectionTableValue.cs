﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PowerFx.Core.IR;
using static Microsoft.PowerFx.Syntax.PrettyPrintVisitor;

namespace Microsoft.PowerFx.Types
{
    /// <summary>
    /// Create a TableValue over a dotnet collection class.
    /// Depending on what collection interfaces it exposes will determine the capabilities 
    /// (such as Enumeration, Count, Index).
    /// </summary>
    /// <typeparam name="T">The element type of the collection.</typeparam>
    public abstract class CollectionTableValue<T> : TableValue
    {
        // Required - basic enumeration. This is the source object. 
        private IEnumerable<T> _enumerator; // required. supports enumeration;

        // Async implementation
        private IAsyncEnumerable<T> _asyncEnumerable;        

        // Additional capabilities. 
        private IReadOnlyList<T> _sourceIndex; // maybe null. supports index. 
        private IReadOnlyCollection<T> _sourceCount; // maybe null. supports count;
        private ICollection<T> _sourceList; // maybe null. supports mutation;

        public CollectionTableValue(RecordType recordType, IEnumerable<T> source)
          : this(IRContext.NotInSource(recordType.ToTable()), source)
        {
            RecordType = recordType;
        }

        public CollectionTableValue(RecordType recordType, bool inMemory, IAsyncEnumerable<T> source)
            : base(IRContext.NotInSource(recordType.ToTable()))
        {
            RecordType = recordType;

            if (inMemory)
            {
                Init(source.ToListAsync().ConfigureAwait(false).GetAwaiter().GetResult());
            }
            else
            {
                _enumerator = null;
                _asyncEnumerable = source;
                _sourceIndex = null;
                _sourceCount = null;
                _sourceList = null;
            }
        }

        internal CollectionTableValue(IRContext irContext, IEnumerable<T> source)
            : base(irContext)
        {
            Init(source);
        }

        private void Init(IEnumerable<T> source)
        {
            _enumerator = source ?? throw new ArgumentNullException(nameof(source));
            _asyncEnumerable = null;

            _sourceIndex = source as IReadOnlyList<T>;
            _sourceCount = source as IReadOnlyCollection<T>;
            _sourceList = source as ICollection<T>;

            if (_sourceList != null && _sourceList.IsReadOnly)
            {
                _sourceList = null;
            }
        }

        public RecordType RecordType { get; }

        protected abstract DValue<RecordValue> Marshal(T item);

        protected virtual T MarshalInverse(RecordValue row)
        {
            throw new NotImplementedException();
        }

        public override IEnumerable<DValue<RecordValue>> Rows
        {
            get
            {
                if (_enumerator != null)
                {
                    foreach (var item in _enumerator)
                    {
                        var record = Marshal(item);
                        yield return record;
                    }
                }
                else
                {
                    IAsyncEnumerator<T> asyncEnumerator = _asyncEnumerable.GetAsyncEnumerator();

                    try
                    {                        
                        while (asyncEnumerator.MoveNextAsync().GetAwaiter().GetResult())
                        {
                            var record = Marshal(asyncEnumerator.Current);
                            yield return record;
                        }
                    }
                    finally
                    {
                        asyncEnumerator.DisposeAsync().GetAwaiter().GetResult();
                    }
                }
            }
        }

        public override int Count()
        {
            if (_sourceCount != null)
            {
                return _sourceCount.Count;
            }
            else
            {
                return base.Count();
            }
        }

        public override async Task<DValue<RecordValue>> AppendAsync(RecordValue record, CancellationToken cancellationToken)
        {
            if (_sourceList == null)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return await base.AppendAsync(record, cancellationToken).ConfigureAwait(false);
            }

            var item = MarshalInverse(record);

            _sourceList.Add(item);

            return DValue<RecordValue>.Of(record);
        }

        public override async Task<DValue<BooleanValue>> ClearAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (_sourceList == null)
            {
                return await base.ClearAsync(cancellationToken).ConfigureAwait(false);
            }

            _sourceList.Clear();

            return DValue<BooleanValue>.Of(FormulaValue.New(true));
        }

        protected override bool TryGetIndex(int index1, out DValue<RecordValue> record)
        {
            var index0 = index1 - 1;
            if (_sourceIndex != null)
            {
                if (index0 < 0 || index0 >= _sourceCount.Count)
                {
                    record = null;
                    return false;
                }

                var item = _sourceIndex[index0];
                record = Marshal(item);
                return true;
            }
            else
            {
                return base.TryGetIndex(index1, out record);
            }
        }

        public override async Task<DValue<BooleanValue>> RemoveAsync(IEnumerable<FormulaValue> recordsToRemove, bool all, CancellationToken cancellationToken)
        {
            var ret = false;

            if (_sourceList == null)
            {
                return await base.RemoveAsync(recordsToRemove, all, cancellationToken).ConfigureAwait(false);
            }

            foreach (RecordValue recordToRemove in recordsToRemove)
            {
                var deleteList = new List<T>();

                foreach (var item in _enumerator)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var dRecord = Marshal(item);

                    if (await MatchesAsync(dRecord.Value, recordToRemove, cancellationToken).ConfigureAwait(false))
                    {
                        deleteList.Add(item);

                        if (!all)
                        {
                            break;
                        }
                    }
                }

                foreach (var delete in deleteList)
                {
                    _sourceList.Remove(delete);
                    ret = true;
                }
            }

            return DValue<BooleanValue>.Of(New(ret));
        }

        protected override async Task<DValue<RecordValue>> PatchCoreAsync(RecordValue baseRecord, RecordValue changeRecord, CancellationToken cancellationToken)
        {
            var actual = await FindAsync(baseRecord, cancellationToken).ConfigureAwait(false);

            if (actual != null)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return await actual.UpdateFieldsAsync(changeRecord, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                return DValue<RecordValue>.Of(FormulaValue.NewBlank(IRContext.ResultType));
            }
        }

        protected virtual RecordValue Find(RecordValue baseRecord)
        {
            return FindAsync(baseRecord, CancellationToken.None).Result;
        }

        /// <summary>
        /// Execute a linear search for the matching record.
        /// </summary>
        /// <param name="baseRecord">RecordValue argument.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A record instance within the current table. This record can then be updated.</returns>
        /// <remarks>A derived class may override if there's a more efficient way to find the match than by linear scan.</remarks>
        protected virtual async Task<RecordValue> FindAsync(RecordValue baseRecord, CancellationToken cancellationToken)
        {
            foreach (var current in Rows)
            {
                if (await MatchesAsync(current.Value, baseRecord, cancellationToken).ConfigureAwait(false))
                {
                    return current.Value;
                }
            }

            return null;
        }

        protected static async Task<bool> MatchesAsync(RecordValue currentRecord, RecordValue baseRecord, CancellationToken cancellationToken)
        {
            var ret = true;

            await foreach (var baseRecordField in baseRecord.GetFieldsAsync(cancellationToken).ConfigureAwait(false))
            {
                var currentFieldValue = await currentRecord.GetFieldAsync(baseRecordField.Value.Type, baseRecordField.Name, cancellationToken).ConfigureAwait(false);

                if (currentFieldValue is BlankValue && baseRecordField.Value is BlankValue)
                {
                    continue;
                }
                else if (currentFieldValue is BlankValue)
                {
                    ret = false;
                    break;
                }
                else if (currentFieldValue.Type._type.IsPrimitive && baseRecordField.Value.Type._type.IsPrimitive)
                {
                    var compare1 = currentFieldValue.ToObject();
                    var compare2 = baseRecordField.Value.ToObject();

                    if (!compare1.Equals(compare2))
                    {
                        ret = false;
                        break;
                    }
                }
                else if (baseRecordField.Value is RecordValue baseRecordValue && currentFieldValue is RecordValue currentRecordValue)
                {
                    ret = await MatchesAsync(currentRecordValue, baseRecordValue, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    throw new NotSupportedException("Field value not supported.");
                }
            }

            return ret;
        }
    }

    public static class AsyncEnumerableExtensions
    {
        public static async Task<List<T>> ToListAsync<T>(this IAsyncEnumerable<T> items, CancellationToken cancellationToken = default)
        {
            var results = new List<T>();

            await foreach (var item in items.WithCancellation(cancellationToken).ConfigureAwait(false))
            {
                results.Add(item);
            }

            return results;
        }
    }
}
