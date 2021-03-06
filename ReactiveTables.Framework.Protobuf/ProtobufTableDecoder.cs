﻿// This file is part of ReactiveTables.
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
using System.IO;
using System.Threading;
using ProtoBuf;
using ReactiveTables.Framework.Comms;

namespace ReactiveTables.Framework.Protobuf
{
    /// <summary>
    /// Writes changes from the given protobuf stream to an <see cref="IWritableReactiveTable"/>.
    /// </summary>
    public class ProtobufTableDecoder : IReactiveTableProcessor<IWritableReactiveTable>
    {
        private IWritableReactiveTable _table;
        private Dictionary<int, string> _fieldIdsToColumns;
        private Stream _stream;
        private readonly ManualResetEventSlim _finished = new ManualResetEventSlim();
        private bool WithLengthPrefix = true;

        /// <summary>
        /// Setups and starts the decoder
        /// </summary>
        /// <param name="outputStream"></param>
        /// <param name="table"></param>
        /// <param name="state"></param>
        public void Setup(Stream outputStream, IWritableReactiveTable table, object state)
        {
            var config = (ProtobufDecoderState)state;
            _fieldIdsToColumns = config.FieldIdsToColumns;
            _table = table;
            _stream = outputStream;

            Start();
        }

        /// <summary>
        /// Start listening for changes on the stream and writing to the table
        /// </summary>
        private void Start()
        {
            var remoteToLocalRowIds = new Dictionary<int, int>();

            _finished.Reset();
            while (!_finished.Wait(0))
            {
                ReadStream(_stream, remoteToLocalRowIds);
            }
        }

        /// <summary>
        /// Stop listening to updates from the stream
        /// </summary>
        public void Stop()
        {
            _finished.Set();
        }

        private void ReadStream(Stream stream, Dictionary<int, int> remoteToLocalRowIds)
        {
            var len = 0;
            if (WithLengthPrefix)
            {
                // Read the length of the message from the stream.
                // DirectReadVarintInt32 calls Stream.ReadByte() which allocates a one byte array on every call - yuck!
                var header = ProtoReader.DirectReadVarintInt32(stream);
                len = ProtoReader.DirectReadVarintInt32(stream);
            }
            // Allocation of protoreader for each message - blurghh!
            using (var reader = new ProtoReader(stream, null, null, len))
            {
                int fieldId;
                while ((fieldId = reader.ReadFieldHeader()) != 0)
                {
                    if (fieldId == ProtobufOperationTypes.Add)
                    {
                        var rowId = ReadRowId(reader);
                        if (rowId >= 0)
                        {
                            remoteToLocalRowIds.Add(rowId, _table.AddRow());
                            var dummyFieldId = reader.ReadFieldHeader();
                            ReadUpdate(remoteToLocalRowIds, reader);
                        }
                    }
                    else if (fieldId == ProtobufOperationTypes.Update)
                    {
                        ReadUpdate(remoteToLocalRowIds, reader);
                    }
                    else if (fieldId == ProtobufOperationTypes.Delete)
                    {
                        var rowId = ReadRowId(reader);
                        if (rowId >= 0)
                        {
                            _table.DeleteRow(remoteToLocalRowIds[rowId]);
                            remoteToLocalRowIds.Remove(rowId);
                        }
                    }
                }
            }
        }

        private void ReadUpdate(Dictionary<int, int> remoteToLocalRowIds, ProtoReader reader)
        {
            var token = ProtoReader.StartSubItem(reader);

            var fieldId = reader.ReadFieldHeader();
            if (fieldId == ProtobufFieldIds.RowId) // Check for row id
            {
                var rowId = reader.ReadInt32();

                int localRowId;
                if (remoteToLocalRowIds.TryGetValue(rowId, out localRowId))
                {
                    WriteFieldsToTable(_table, _fieldIdsToColumns, reader, remoteToLocalRowIds[rowId]);
                }
            }

            ProtoReader.EndSubItem(token, reader);
        }

        private static int ReadRowId(ProtoReader reader)
        {
            var token = ProtoReader.StartSubItem(reader);
            var fieldId = reader.ReadFieldHeader();
            if (fieldId != ProtobufFieldIds.RowId) return -1;

            var rowId = reader.ReadInt32();
            var dummyFieldId = reader.ReadFieldHeader();
            ProtoReader.EndSubItem(token, reader);
            return rowId;
        }

        private void WriteFieldsToTable(IWritableReactiveTable table,
                                        Dictionary<int, string> fieldIdsToColumns,
                                        ProtoReader reader,
                                        int rowId)
        {
            int fieldId;
            while ((fieldId = reader.ReadFieldHeader()) != 0)
            {
                var columnId = fieldIdsToColumns[fieldId];
                var column = table.GetColumnByName(columnId);

                if (column.Type == typeof (int))
                {
                    table.SetValue(columnId, rowId, reader.ReadInt32());
                }
                else if (column.Type == typeof (short))
                {
                    table.SetValue(columnId, rowId, reader.ReadInt16());
                }
                else if (column.Type == typeof (string))
                {
                    var value = reader.ReadString();
                    table.SetValue(columnId, rowId, value);
                }
                else if (column.Type == typeof (bool))
                {
                    table.SetValue(columnId, rowId, reader.ReadBoolean());
                }
                else if (column.Type == typeof (double))
                {
                    table.SetValue(columnId, rowId, reader.ReadDouble());
                }
                else if (column.Type == typeof (long))
                {
                    table.SetValue(columnId, rowId, reader.ReadInt64());
                }
                else if (column.Type == typeof (decimal))
                {
                    table.SetValue(columnId, rowId, BclHelpers.ReadDecimal(reader));
                }
                else if (column.Type == typeof (DateTime))
                {
                    table.SetValue(columnId, rowId, BclHelpers.ReadDateTime(reader));
                }
                else if (column.Type == typeof (TimeSpan))
                {
                    table.SetValue(columnId, rowId, BclHelpers.ReadTimeSpan(reader));
                }
                else if (column.Type == typeof (Guid))
                {
                    table.SetValue(columnId, rowId, BclHelpers.ReadGuid(reader));
                }
                else if (column.Type == typeof(byte))
                {
                    table.SetValue(columnId, rowId, reader.ReadByte());
                }
                else if (column.Type == typeof(char))
                {
                    table.SetValue(columnId, rowId, (char)reader.ReadInt16());
                }
                else if (column.Type == typeof(float))
                {
                    table.SetValue(columnId, rowId, reader.ReadSingle());
                }
            }
        }

        public void Dispose()
        {
            if (_stream != null) _stream.Dispose();
            Stop();
        }
    }
}