// This file is part of ReactiveTables.
// 
// ReactiveTables is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// ReactiveTables is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with ReactiveTables.  If not, see <http://www.gnu.org/licenses/>.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Subjects;
using System.Security.Policy;
using ReactiveTables.Framework.Aggregate.Operations;
using ReactiveTables.Framework.Collections;
using ReactiveTables.Framework.Columns;

namespace ReactiveTables.Framework.Aggregate
{
    /// <summary>
    /// A table which shows an aggregated view of another source table
    /// </summary>
    public class AggregatedTable : ReactiveTableBase, IDisposable
    {
        private readonly IReactiveTable _sourceTable;

        /// <summary>
        /// Columns the source table is grouped by
        /// </summary>
        private readonly Dictionary<string, IReactiveColumn> _groupColumns = new Dictionary<string, IReactiveColumn>();

        /// <summary>
        /// A way of getting the hashcode for each group column
        /// </summary>
        private readonly List<IHashcodeAccessor> _hashcodeAccessors = new List<IHashcodeAccessor>();

        /// <summary>
        /// The source rows grouped by <see cref="GroupByKey"/>
        /// </summary>
        private readonly IndexedDictionary<GroupByKey, List<int>> _groupedRows = new IndexedDictionary<GroupByKey, List<int>>();

        /// <summary>
        /// A map of source rows to the matching <see cref="GroupByKey"/>
        /// </summary>
        private readonly Dictionary<int, GroupByKey> _sourceRowsToKeys = new Dictionary<int, GroupByKey>();

        /// <summary>
        /// A map of each key to its external index
        /// </summary>
        private readonly Dictionary<GroupByKey, int> _keyPositions = new Dictionary<GroupByKey, int>();

        /// <summary>
        /// Columns added directly to this table
        /// </summary>
        private readonly Dictionary<string, IReactiveColumn> _localColumns = new Dictionary<string, IReactiveColumn>();

        private readonly List<IAggregateColumn> _aggregateColumns = new List<IAggregateColumn>();
        private readonly IDisposable _token;
        private readonly Subject<TableUpdate> _updates = new Subject<TableUpdate>();

        /// <summary>
        /// Aggregate the provided source table.
        /// </summary>
        /// <param name="sourceTable"></param>
        public AggregatedTable(IReactiveTable sourceTable)
        {
            _sourceTable = sourceTable;
            _token = sourceTable.Subscribe(OnSourceValue);
        }

        public override int RowCount
        {
            get { return _groupedRows.Count; }
        }

        public override IDictionary<string, IReactiveColumn> Columns
        {
            get { return _groupColumns; }
        }

        /// <summary>
        /// Group the source table by the given column
        /// </summary>
        /// <param name="columnId"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        public IReactiveColumn GroupBy<T>(string columnId)
        {
            var column = (IReactiveColumn<T>) _sourceTable.Columns[columnId];
            if (_groupColumns.ContainsKey(column.ColumnId))
            {
                throw new ArgumentException(string.Format("Column {0} is already in group by statement", column.ColumnId),
                                            "columnId");
            }

            var hashcodeAccessor = new HashcodeAccessor<T>(column);
            _hashcodeAccessors.Add(hashcodeAccessor);
            _groupColumns.Add(columnId, column);

            return column;
        }

        private void OnSourceValue(TableUpdate tableUpdate)
        {
            var columnUpdated = tableUpdate.Column;
            var sourceIndex = tableUpdate.RowIndex;
            int groupedIndex;
            bool groupChanged = false;
            if (tableUpdate.Action == TableUpdateAction.Add)
            {
                // New source row added
                var key = new GroupByKey(_hashcodeAccessors, sourceIndex);
                groupedIndex = AddItemToGroup(key, sourceIndex);
                _sourceRowsToKeys.Add(sourceIndex, key);
                groupChanged = true;
            }
            else if (tableUpdate.Action == TableUpdateAction.Delete)
            {
                // Source row deleted
                var key = _sourceRowsToKeys[sourceIndex];
                var group = _groupedRows[key];
                groupedIndex = _keyPositions[key];

                RemoveItemFromGroup(@group, sourceIndex, key, groupedIndex);

                _sourceRowsToKeys.Remove(sourceIndex);
                groupChanged = true;
            }
            else if (tableUpdate.Action == TableUpdateAction.Update &&
                     _groupColumns.ContainsKey(columnUpdated.ColumnId))
            {
                // Source row changing group
                var key = _sourceRowsToKeys[sourceIndex];
                // TODO: figure out how to do this without allocations
                var newKey = new GroupByKey(_hashcodeAccessors, sourceIndex);

                // Move the rowIndex from the old key to the new key
                var group = _groupedRows[key];
                var oldGroupIndex = _keyPositions[key];
                RemoveItemFromGroup(@group, sourceIndex, key, oldGroupIndex);
                groupedIndex = AddItemToGroup(newKey, sourceIndex);

                // Replace the rowIndex to key mapping
                _sourceRowsToKeys[sourceIndex] = newKey;

                var column = _hashcodeAccessors.Find(accessor => accessor.ColumnId == columnUpdated.ColumnId);
                column.NotifyObserversOnNext(groupedIndex);
                _updates.OnNext(TableUpdate.NewColumnUpdate(groupedIndex, (IReactiveColumn) column));
                Console.WriteLine("Grouped column updated");
                groupChanged = true;
            }
            else
            {
                var key = _sourceRowsToKeys[sourceIndex];
                groupedIndex = _keyPositions[key];
                Console.WriteLine("Non grouped column updated");
            }

            // Aggregated column has changed
            foreach (var aggregateColumn in _aggregateColumns)
            {
                if (groupChanged ||
                    tableUpdate.Column.ColumnId == aggregateColumn.SourceColumn.ColumnId)
                {
                    aggregateColumn.ProcessValue(sourceIndex, groupedIndex);
                    _updates.OnNext(TableUpdate.NewColumnUpdate(groupedIndex, aggregateColumn));
                }
            }
        }

        private int AddItemToGroup(GroupByKey key, int rowIndex)
        {
            List<int> rowsInGroup;
            int groupedIndex;
            if (!_groupedRows.TryGetValue(key, out rowsInGroup))
            {
                rowsInGroup = new List<int>();
                groupedIndex = _groupedRows.AddWithIndex(key, rowsInGroup);
                _keyPositions.Add(key, groupedIndex);
                rowsInGroup.Add(rowIndex);
                foreach (var aggregateColumn in _aggregateColumns)
                {
                    aggregateColumn.AddField(groupedIndex);
                }

                // Notify of new row appearing
                _updates.OnNext(TableUpdate.NewAddUpdate(groupedIndex));
                // Make sure all the column values are sent too
                foreach (var accessor in _hashcodeAccessors)
                {
                    _updates.OnNext(TableUpdate.NewColumnUpdate(groupedIndex, (IReactiveColumn) accessor));
                }
            }
            else
            {
                groupedIndex = _keyPositions[key];
                rowsInGroup.Add(rowIndex);
            }
            return groupedIndex;
        }

        private void RemoveItemFromGroup(List<int> @group, int rowIndex, GroupByKey key, int groupedIndex)
        {
            group.Remove(rowIndex);
            RemoveEmptyGroup(group, key, groupedIndex);
            foreach (var aggregateColumn in _aggregateColumns)
            {
                aggregateColumn.RemoveOldValue(rowIndex, groupedIndex);
            }
        }

        private void RemoveEmptyGroup(List<int> @group, GroupByKey key, int groupedIndex)
        {
            if (group.Count == 0)
            {
                _groupedRows.Remove(key);
                _keyPositions.Remove(key);
                foreach (var aggregateColumn in _aggregateColumns)
                {
                    aggregateColumn.AddField(groupedIndex);
                }

                // Notify of grouped row being removed
                _updates.OnNext(TableUpdate.NewDeleteUpdate(groupedIndex));
            }
        }

        public override IReactiveColumn AddColumn(IReactiveColumn column)
        {
            // Handle calculated columns
            _localColumns.Add(column.ColumnId, column);
            return column;
        }

/*
        public void AddAggregate<TIn, TOut>(IReactiveColumn<TIn> sourceColumn, string columnId, Func<TIn, TOut, bool, TOut> accumulator)
        {
            _aggregateColumns.Add(new AggregateColumn<TIn, TOut>(sourceColumn, columnId, accumulator));
        }
*/

        public void AddAggregate<TIn, TOut>(IReactiveColumn<TIn> sourceColumn, string columnId, Func<IAccumulator<TIn, TOut>> accumulator)
        {
            _aggregateColumns.Add(new AggregateColumn<TIn, TOut>(sourceColumn, columnId, accumulator));
        }

        public override T GetValue<T>(string columnId, int rowIndex)
        {
            var sourceColumn = _hashcodeAccessors.Find(accessor => accessor.ColumnId == columnId);
            if (sourceColumn != null)
            {
                IReactiveColumn<T> column = (IReactiveColumn<T>) sourceColumn;
                var sourceRowIndex = GetSourceRowIndex(rowIndex);
                return column.GetValue(sourceRowIndex);
            }
            IReactiveColumn localColumn;
            if (_localColumns.TryGetValue(columnId, out localColumn))
            {
                IReactiveColumn<T> localTyped = (IReactiveColumn<T>) localColumn;
                var sourceRowIndex = GetSourceRowIndex(rowIndex);
                return localTyped.GetValue(sourceRowIndex);
            }
            var aggregateColumn = _aggregateColumns.Find(c => c.ColumnId == columnId);
            if (aggregateColumn != null)
            {
                var aggregateTyped = (IReactiveColumn<T>) aggregateColumn;
                return aggregateTyped.GetValue(rowIndex);
            }

            throw new ArgumentException(string.Format("Column {0} does not existing in the table", columnId), "columnId");
        }

        public override object GetValue(string columnId, int rowIndex)
        {
            var hashcodeAccessor = _hashcodeAccessors.Find(accessor => accessor.ColumnId == columnId);
            if (hashcodeAccessor != null)
            {
                var sourceRowIndex = GetSourceRowIndex(rowIndex);
                return hashcodeAccessor.GetValue(sourceRowIndex);
            }
            IReactiveColumn localColumn;
            if (_localColumns.TryGetValue(columnId, out localColumn))
            {
                var sourceRowIndex = GetSourceRowIndex(rowIndex);
                return localColumn.GetValue(sourceRowIndex);
            }
            var aggregateColumn = _aggregateColumns.Find(c => c.ColumnId == columnId);
            if (aggregateColumn != null)
            {
                return aggregateColumn.GetValue(rowIndex);
            }

            throw new ArgumentException(string.Format("Column {0} does not existing in the table", columnId), "columnId");
        }

        private int GetSourceRowIndex(int rowIndex)
        {
            // Doesn't matter which sub row we choose as we know they all have the same value
            var sourceRowIndex = _groupedRows[rowIndex][0];
            return sourceRowIndex;
        }

        public override IDisposable Subscribe(IObserver<TableUpdate> observer)
        {
            return _updates.Subscribe(observer);
        }

        public override IReactiveColumn GetColumnByIndex(int index)
        {
            return (IReactiveColumn) _hashcodeAccessors[index];
        }

        public override void ReplayRows(IObserver<TableUpdate> observer)
        {
            for (int i = 0; i < _groupedRows.Count; i++)
            {
                _updates.OnNext(TableUpdate.NewAddUpdate(i));
            }
        }

        public override int GetRowAt(int position)
        {
            // TODO: use row manager to avoid deletes in _groupedRows
            return position;
        }

        public override int GetPositionOfRow(int rowIndex)
        {
            // TODO: use row manager to avoid deletes in _groupedRows
            return rowIndex;
        }

        public void Dispose()
        {
            _token.Dispose();
        }
    }
}