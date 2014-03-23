﻿using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using NUnit.Framework;
using ReactiveTables.Framework.SimpleBinaryEncoding;
using ReactiveTables.Framework.Tests.Comms.Protobuf;
using ReactiveTables.Framework.Utils;

namespace ReactiveTables.Framework.Tests.Comms.SimpleBinaryEncoding
{
    [TestFixture]
    public class SbeTableEncoderTest
    {
        [Test]
        public void TestAdd()
        {
            var tableEncoderTester = new TableEncoderTester();
            // Setup encoder
            SbeTableEncoder encoder = new SbeTableEncoder();
            MemoryStream stream = new MemoryStream();
            var table = TestTableHelper.CreateReactiveTableFull();
            var columnsToFieldIds = new Dictionary<string, int>
                                        {
                                            {TestTableColumns.IdColumn, 101},
                                            {TestTableColumns.StringColumn, 102}, 
//                                            {TestTableColumns.DecimalColumn, 103},
                                            {TestTableColumns.BoolColumn, 104},
                                            {TestTableColumns.DoubleColumn, 105},
                                            {TestTableColumns.ShortColumn, 106},
                                            {TestTableColumns.LongColumn, 107},
//                                            {TestTableColumns.DateTimeColumn, 108},
//                                            {TestTableColumns.TimespanColumn, 109},
//                                            {TestTableColumns.GuidColumn, 110},
                                            {TestTableColumns.FloatColumn, 111},
                                            {TestTableColumns.ByteColumn, 112},
                                            {TestTableColumns.CharColumn, 113},
                                        };
            encoder.Setup(stream, table, new SbeTableEncoderState { ColumnsToFieldIds = columnsToFieldIds});

            // Add data
            var row1 = TableEncoderTester.AddTestRow(table, false);
            var row2 = TableEncoderTester.AddTestRow(table, false);

            tableEncoderTester.UpdateTestRow(table, row1, false);

            encoder.Dispose();
            //stream.Flush();
            stream.Seek(0, SeekOrigin.Begin);

            // Decode
            var destTable = TestTableHelper.CreateReactiveTableFull();
            SbeTableDecoder tableDecoder = new SbeTableDecoder();
            var t = Task.Run(() => 
            tableDecoder.Setup(stream, destTable, new SbeTableDecoderState {FieldIdsToColumns = columnsToFieldIds.InverseUniqueDictionary()})
                );

            t.Wait(100);
            tableDecoder.Stop();

            tableEncoderTester.CompareTables(table, destTable);
        }
    }
}