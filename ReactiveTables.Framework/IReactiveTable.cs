using System;
using System.Collections.Generic;
using ReactiveTables.Framework.Columns;
using ReactiveTables.Framework.Filters;

namespace ReactiveTables.Framework
{
    /// <summary>
    /// The main interface for a read-only table.  See <see cref="IWritableReactiveTable"/> for the writable version.
    /// </summary>
    public interface IReactiveTable : IObservable<TableUpdate>
    {
        /// <summary>
        /// Typed version
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="columnId"></param>
        /// <param name="rowIndex"></param>
        /// <returns></returns>
        T GetValue<T>(string columnId, int rowIndex);

        /// <summary>
        /// Untyped version
        /// </summary>
        /// <param name="columnId"></param>
        /// <param name="rowIndex"></param>
        /// <returns></returns>
        object GetValue(string columnId, int rowIndex);

        /// <summary>
        /// How many rows there are
        /// </summary>
        int RowCount { get; }

        /// <summary>
        /// All the columns - addressable by index
        /// </summary>
        IReactiveColumn GetColumnByIndex(int index);

        /// <summary>
        /// Facility class for propagating PropertyChanged events
        /// </summary>
        PropertyChangedNotifier ChangeNotifier { get; }

        /// <summary>
        /// Join this table to another table
        /// </summary>
        /// <param name="otherTable"></param>
        /// <param name="joiner"></param>
        /// <returns>The joined table</returns>
        IReactiveTable Join(IReactiveTable otherTable, IReactiveTableJoiner joiner);

        /// <summary>
        /// Filter this table using a given predicate
        /// </summary>
        /// <param name="predicate"></param>
        /// <returns>The filtered table</returns>
        IReactiveTable Filter(IReactivePredicate predicate);

        /// <summary>
        /// Replay all the rows that are present in the table
        /// </summary>
        /// <param name="observer"></param>
        void ReplayRows(IObserver<TableUpdate> observer);

        /// <summary>
        /// Get the row at the given position
        /// </summary>
        /// <param name="position"></param>
        /// <returns>The row index</returns>
        int GetRowAt(int position);

        /// <summary>
        /// Get the position of a given row
        /// </summary>
        /// <param name="rowIndex"></param>
        /// <returns>The position</returns>
        int GetPositionOfRow(int rowIndex);


        /// <summary>
        /// All the columns
        /// </summary>
        IReadOnlyList<IReactiveColumn> Columns { get; }

        /// <summary>
        /// Schedule tasks to be run after any current observable notifications.
        /// Use this when subscribing to a tbale as the result of an observable notification.
        /// </summary>
        /// <param name="action"></param>
//        void ScheduleTask(Action action);

        /// <summary>
        /// Get the column by the column name
        /// </summary>
        /// <param name="columnId"></param>
        /// <returns></returns>
        IReactiveColumn GetColumnByName(string columnId);

        /// <summary>
        /// Returns whether the column is defined in the table or not.
        /// </summary>
        /// <param name="columnId"></param>
        /// <param name="column"></param>
        /// <returns></returns>
        bool GetColumnByName(string columnId, out IReactiveColumn column);

        /// <summary>
        /// Add a column to the table
        /// </summary>
        /// <param name="column"></param>
        /// <param name="subscribe">Should the tbale subscribe to the column</param>
        IReactiveColumn AddColumn(IReactiveColumn column, bool subscribe = true);
    }
}