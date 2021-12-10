using Microsoft.Data.SqlClient;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using System;
using System.Data;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;

namespace DaJet.Data.Scripting
{
    public interface IScriptExecutor
    {
        string ConnectionString { get; }
        void UseConnectionString(string connectionString);
        ///<summary>Executes SQL script and returns result as JSON.</summary>
        string ExecuteJson(string sql);
        ///<summary>Executes SQL script non-query.</summary>
        void ExecuteScript(TSqlScript script);
        string ExecuteJsonString(string sql);
    }
    public sealed class ScriptExecutor : IScriptExecutor
    {
        public ScriptExecutor() { }
        public string ConnectionString { get; private set; }
        public void UseConnectionString(string connectionString)
        {
            ConnectionString = connectionString;
        }
        public string ExecuteJson(string sql)
        {
            string json;
            JsonWriterOptions options = new JsonWriterOptions { Indented = true };
            using (MemoryStream memory = new MemoryStream())
            {
                using (Utf8JsonWriter writer = new Utf8JsonWriter(memory, options))
                {
                    writer.WriteStartArray();
                    using (SqlConnection connection = new SqlConnection(ConnectionString))
                    using (SqlCommand command = new SqlCommand(sql, connection))
                    {
                        connection.Open();

                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            var schema = reader.GetColumnSchema();
                            while (reader.Read())
                            {
                                writer.WriteStartObject();
                                for (int c = 0; c < schema.Count; c++)
                                {
                                    object value = reader[c];
                                    string typeName = schema[c].DataTypeName;
                                    string columnName = schema[c].ColumnName;
                                    int valueSize = 0;
                                    if (schema[c].ColumnSize.HasValue)
                                    {
                                        valueSize = schema[c].ColumnSize.Value;
                                    }
                                    if (value == DBNull.Value)
                                    {
                                        writer.WriteNull(columnName);
                                    }
                                    else if (DbUtilities.IsString(typeName))
                                    {
                                        writer.WriteString(columnName, (string)value);
                                    }
                                    else if (DbUtilities.IsDateTime(typeName))
                                    {
                                        writer.WriteString(columnName, ((DateTime)value).ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture));
                                    }
                                    else if (DbUtilities.IsVersion(typeName))
                                    {
                                        writer.WriteString(columnName, $"0x{DbUtilities.ByteArrayToString((byte[])value)}");
                                    }
                                    else if (DbUtilities.IsBoolean(typeName, valueSize))
                                    {
                                        if (typeName == "bit")
                                        {
                                            writer.WriteBoolean(columnName, (bool)value);
                                        }
                                        else // binary(1)
                                        {
                                            writer.WriteBoolean(columnName, DbUtilities.GetInt32((byte[])value) == 0 ? false : true);
                                        }
                                    }
                                    else if (DbUtilities.IsNumber(typeName, valueSize))
                                    {
                                        if (typeName == "binary" || typeName == "varbinary") // binary(4) | varbinary(4)
                                        {
                                            writer.WriteNumber(columnName, DbUtilities.GetInt32((byte[])value));
                                        }
                                        else
                                        {
                                            writer.WriteNumber(columnName, (decimal)value);
                                        }
                                    }
                                    else if (DbUtilities.IsUUID(typeName, valueSize))
                                    {
                                        writer.WriteString(columnName, (new Guid((byte[])value)).ToString());
                                    }
                                    else if (DbUtilities.IsReference(typeName, valueSize))
                                    {
                                        byte[] reference = (byte[])value;
                                        int code = DbUtilities.GetInt32(reference[0..4]);
                                        Guid uuid = new Guid(reference[4..^0]);
                                        writer.WriteString(columnName, $"{{{code}:{uuid}}}");
                                    }
                                    else if (DbUtilities.IsBinary(typeName))
                                    {
                                        writer.WriteBase64String(columnName, (byte[])value);
                                    }
                                }
                                writer.WriteEndObject();
                            }
                        }
                    }
                    writer.WriteEndArray();
                }
                json = Encoding.UTF8.GetString(memory.ToArray());
            }
            return json;
        }
        public void ExecuteScript(TSqlScript script)
        {
            {
                SqlConnection connection = new SqlConnection(ConnectionString);
                SqlCommand command = connection.CreateCommand();
                command.CommandType = CommandType.Text;
                try
                {
                    connection.Open();

                    foreach (TSqlBatch batch in script.Batches)
                    {
                        command.CommandText = batch.ToSqlString();
                        _ = command.ExecuteNonQuery();
                    }
                }
                catch (Exception error)
                {
                    // TODO: log error
                    _ = error.Message;
                    throw;
                }
                finally
                {
                    if (command != null) command.Dispose();
                    if (connection != null) connection.Dispose();
                }
            }
        }
        public string ExecuteJsonString(string sql)
        {
            string json;
            using (StringWriter writer = new StringWriter())
            {
                writer.Write("[\n");
                using (SqlConnection connection = new SqlConnection(ConnectionString))
                using (SqlCommand command = new SqlCommand(sql, connection))
                {
                    connection.Open();
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        var schema = reader.GetColumnSchema();
                        while (reader.Read())
                        {
                            writer.Write("\t{\n");
                            for (int c = 0; c < schema.Count; c++)
                            {
                                writer.Write("\t\t");
                                object value = reader[c];
                                string typeName = schema[c].DataTypeName;
                                string columnName = schema[c].ColumnName;
                                int valueSize = 0;
                                if (schema[c].ColumnSize.HasValue)
                                {
                                    valueSize = schema[c].ColumnSize.Value;
                                }
                                if (value == DBNull.Value)
                                {
                                    writer.Write($"\"{columnName}\" : null");
                                }
                                else if (DbUtilities.IsString(typeName))
                                {
                                    writer.Write($"\"{columnName}\" : \"{(string)value}\"");
                                }
                                else if (DbUtilities.IsDateTime(typeName))
                                {
                                    writer.Write($"\"{columnName}\" : \"{((DateTime)value).ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture)}\"");
                                }
                                else if (DbUtilities.IsVersion(typeName))
                                {
                                    writer.Write($"\"{columnName}\" : \"0x{DbUtilities.ByteArrayToString((byte[])value)}\"");
                                }
                                else if (DbUtilities.IsBoolean(typeName, valueSize))
                                {
                                    if (typeName == "bit")
                                    {
                                        writer.Write($"\"{columnName}\" : {((bool)value ? "true" : "false")}");
                                    }
                                    else // binary(1)
                                    {
                                        writer.Write($"\"{columnName}\" : {(DbUtilities.GetInt32((byte[])value) == 0 ? "false" : "true")}");
                                    }
                                }
                                else if (DbUtilities.IsNumber(typeName, valueSize))
                                {
                                    if (typeName == "binary" || typeName == "varbinary") // binary(4) | varbinary(4)
                                    {
                                        writer.Write($"\"{columnName}\" : {DbUtilities.GetInt32((byte[])value)}");
                                    }
                                    else
                                    {
                                        writer.Write($"\"{columnName}\" : {((decimal)value).ToString().Replace(',', '.')}");
                                    }
                                }
                                else if (DbUtilities.IsUUID(typeName, valueSize))
                                {
                                    writer.Write($"\"{columnName}\" : \"{new Guid((byte[])value).ToString().ToLower()}\"");
                                }
                                else if (DbUtilities.IsReference(typeName, valueSize))
                                {
                                    byte[] reference = (byte[])value;
                                    int code = DbUtilities.GetInt32(reference[0..4]);
                                    Guid uuid = new Guid(reference[4..^0]);
                                    writer.Write($"\"{columnName}\" : \"{{{code}:{uuid}}}\"");
                                }
                                else if (DbUtilities.IsBinary(typeName))
                                {
                                    writer.Write($"\"{columnName}\" : \"{Convert.ToBase64String((byte[])value)}\"");
                                }
                                if (c < schema.Count - 1)
                                {
                                    writer.Write(",");
                                }
                                else
                                {
                                    writer.Write("");
                                }
                                writer.Write("\n");
                            }
                            writer.Write("\t}");
                            writer.Write(",");
                            writer.Write("\n");
                        }
                    }
                }
                json = writer.ToString().TrimEnd('\n').TrimEnd(',') + "\n]";
            }
            return json;
        }
    }
}