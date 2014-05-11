﻿using System;
using System.Collections.ObjectModel;
using ReactiveTables.Demo.Utils;
using ReactiveTables.Framework;
using ReactiveTables.Framework.Aggregate;
using ReactiveTables.Framework.Aggregate.Operations;
using ReactiveTables.Framework.Columns;
using ReactiveTables.Framework.UI;
using ReactiveTables.Framework.Utils;

namespace ReactiveTables.Demo.GroupedData
{
    internal class SimpleGroupTestViewModel : ReactiveViewModelBase
    {
        private readonly ReactiveTable _table1;
        private readonly AggregatedTable _groupedTable;
        private const string SumColumnId = "Sum";
        private const string GroupColumnId = "GroupColumn1";
        private const string ValueColumnId = "ValueColumn1";
        private const string CountColumnId = "Count";
        private const string AverageColumnId = "Average";
        private const string MinColumnId = "Min";
        private const string MaxColumnId = "Max";

        public SimpleGroupTestViewModel()
        {
            _table1 = new ReactiveTable();
            var groupColumn = new ReactiveColumn<string>(GroupColumnId);
            _table1.AddColumn(groupColumn);
            var valueColumn = new ReactiveColumn<int>(ValueColumnId);
            _table1.AddColumn(valueColumn);
//            ReactiveTable table2 = new ReactiveTable();
//            table2.AddColumn(new ReactiveColumn<string>("GroupColumn2"));
//            table2.AddColumn(new ReactiveColumn<int>("ValueColumn2"));
            
            _groupedTable = new AggregatedTable(_table1);
            _groupedTable.GroupBy<string>(groupColumn.ColumnId);
            _groupedTable.AddAggregate(groupColumn, CountColumnId, () => new Count<string>());
            _groupedTable.AddAggregate(valueColumn, SumColumnId, () => new Sum<int>());
            _groupedTable.AddAggregate(valueColumn, AverageColumnId, () => new Average<int>());
            _groupedTable.AddAggregate(valueColumn, MinColumnId, () => new Min<int>());
            _groupedTable.AddAggregate(valueColumn, MaxColumnId, () => new Max<int>());

            LoadDataCommand = new DelegateCommand(LoadData);
            Items = new ObservableCollection<SimpleGroupItem>();

            _groupedTable.RowUpdates().Subscribe(OnRowUpdate);
        }

        private void OnRowUpdate(TableUpdate u)
        {
            if (u.Action == TableUpdateAction.Add)
            {
                Items.Add(new SimpleGroupItem(_groupedTable, u.RowIndex));
            }
            else if (u.Action == TableUpdateAction.Delete)
            {
                Items.RemoveAt(Items.IndexOf(item => item.RowIndex == u.RowIndex));
            }
        }

        private void LoadData()
        {
            AddRow("Mendel", 42);
            AddRow("Marie", 43);
            AddRow("Mendel", 44);
            AddRow("Marie", 45);
            AddRow("Mendel", 46);
            AddRow("Marie", 45);
        }

        private void AddRow(string groupVal, int value)
        {
            var row1 = _table1.AddRow();
            _table1.SetValue(GroupColumnId, row1, groupVal);
            _table1.SetValue(ValueColumnId, row1, value);
        }

        public DelegateCommand LoadDataCommand { get; private set; }
        public ObservableCollection<SimpleGroupItem> Items { get; private set; }

        public class SimpleGroupItem : ReactiveViewModelBase
        {
            private readonly IReactiveTable _grouped;
            private readonly int _rowIndex;

            public SimpleGroupItem(IReactiveTable grouped, int rowIndex)
            {
                _grouped = grouped;
                _rowIndex = rowIndex;
                grouped.ChangeNotifier.RegisterPropertyNotifiedConsumer(this, rowIndex);
            }

            public int RowIndex { get { return _rowIndex; } }
            public string Name { get { return _grouped.GetValue<string>(GroupColumnId, _rowIndex); } }
            public int Count { get { return _grouped.GetValue<int>(CountColumnId, _rowIndex); } }
            public int Sum { get { return _grouped.GetValue<int>(SumColumnId, _rowIndex); } }
            public double Average { get { return _grouped.GetValue<double>(AverageColumnId, _rowIndex); } }
            public double Min { get { return _grouped.GetValue<int>(MinColumnId, _rowIndex); } }
            public double Max { get { return _grouped.GetValue<int>(MaxColumnId, _rowIndex); } }
        }
    }
}
