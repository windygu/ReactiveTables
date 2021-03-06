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

namespace ReactiveTables.Framework.Columns
{
    public interface IColumnIndex<in T>
    {
        int GetRow(T value);
        void SetRowValue(int rowIndex, T value, T oldValue);
        void RemoveRowValue(T value);
    }

    public class ColumnIndex<T> : IColumnIndex<T>
    {
        private readonly Dictionary<T, int> _valueRows;

        public ColumnIndex(int? initialSize = null)
        {
            _valueRows = initialSize != null ? new Dictionary<T, int>(initialSize.Value) : new Dictionary<T, int>();
        }

        public int GetRow(T value)
        {
            int rowIndex;
            if (_valueRows.TryGetValue(value, out rowIndex))
            {
                return rowIndex;
            }

            return -1;
        }

        public void SetRowValue(int rowIndex, T value, T oldValue)
        {
            if (_valueRows.Count > 0)
            {
                if (_valueRows.ContainsKey(value))
                {
                    throw new InvalidOperationException("Cannot add duplicate item to indexed row");
                }

                if (!value.Equals(oldValue) && !Equals(default(T), oldValue))
                {
                    _valueRows.Remove(oldValue);
                }
            }

            _valueRows[value] = rowIndex;
        }

        public void RemoveRowValue(T value)
        {
            _valueRows.Remove(value);
        }
    }
}