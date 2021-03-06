﻿namespace RealArtists.ShipHub.Legacy {
  using System;
  using System.Collections.Generic;
  using System.Data;
  using System.Data.SqlClient;
  using System.Diagnostics.CodeAnalysis;
  using System.Dynamic;
  using System.Threading;
  using System.Threading.Tasks;
  //using Microsoft.SqlServer.Types;

  public sealed class DynamicStoredProcedure : DynamicObject, IDisposable {
    private const string ReturnParameterName = "B0PMqldZfY";

    private Dictionary<string, object> _properties = new Dictionary<string, object>();
    private SqlCommand _command;
    private DynamicDataReader _reader;

    [SuppressMessage("Microsoft.Security", "CA2100:Review SQL queries for security vulnerabilities")]
    public DynamicStoredProcedure(string procedureName, SqlConnectionFactory provider) {
      var connection = provider.Get();
      _command = connection.CreateCommand();
      _command.CommandType = CommandType.StoredProcedure;
      _command.CommandText = procedureName;
    }

    public Task<int> ExecuteNonQueryAsync() {
      return ExecuteNonQueryAsync(CancellationToken.None);
    }

    public async Task<int> ExecuteNonQueryAsync(CancellationToken cancellationToken) {
      PrepareCommand();
      try {
        await OpenConnectionAsync(cancellationToken);
        return await _command.ExecuteNonQueryAsync(cancellationToken);
      } finally { CloseConnection(); }
    }

    public Task<T> ExecuteScalarAsync<T>() {
      return ExecuteScalarAsync<T>(CancellationToken.None);
    }

    public async Task<T> ExecuteScalarAsync<T>(CancellationToken cancellationToken) {
      PrepareCommand();
      try {
        await OpenConnectionAsync(cancellationToken);
        var temp = await _command.ExecuteScalarAsync(cancellationToken);
        if (temp == DBNull.Value) {
          temp = null;
        }

        return (T)temp;
      } finally { CloseConnection(); }
    }

    public Task<DynamicDataReader> ExecuteReaderAsync(CommandBehavior behavior = CommandBehavior.Default) {
      return ExecuteReaderAsync(CancellationToken.None, behavior);
    }

    public async Task<DynamicDataReader> ExecuteReaderAsync(CancellationToken cancellationToken, CommandBehavior behavior = CommandBehavior.Default) {
      PrepareCommand();
      await OpenConnectionAsync(cancellationToken);
      var reader = await _command.ExecuteReaderAsync(behavior, cancellationToken);
      return (_reader = new DynamicDataReader(reader));
    }

    public int? CloseAndReturn() {
      CloseConnection();
      return _command.Parameters[ReturnParameterName].Value as int?;
    }

    public override bool TryGetMember(GetMemberBinder binder, out object result) {
      var name = binder.Name.ToUpperInvariant();
      return _properties.TryGetValue(name, out result);
    }

    public override bool TrySetMember(SetMemberBinder binder, object value) {
      var name = binder.Name.ToUpperInvariant();
      _properties[name] = value;
      return true;
    }

    private Task OpenConnectionAsync(CancellationToken cancellationToken) {
      if (_command.Connection.State != ConnectionState.Open) {
        return _command.Connection.OpenAsync(cancellationToken);
      }
      return Task.CompletedTask;
    }

    private void CloseConnection() {
      if (_command.Connection.State != ConnectionState.Closed) {
        _command.Connection.Close();
      }
    }

    private void PrepareCommand() {
      _command.Parameters.Clear();

      // Add return parameter.
      _command.Parameters
          .Add(ReturnParameterName, SqlDbType.Int)
          .Direction = ParameterDirection.ReturnValue;

      // Add other parameters
      foreach (var prop in _properties) {
        AddParameter(prop);
      }
    }

    private void AddParameter(KeyValuePair<string, object> prop) {
      var key = prop.Key;
      var value = prop.Value;

      if (value == null) {
        _command.Parameters.AddWithValue(key, DBNull.Value);
        return;
      }

      switch (value) {
        case bool bit:
          _command.Parameters.Add(key, SqlDbType.Bit).Value = bit;
          break;
        case byte tinyInt:
          _command.Parameters.Add(key, SqlDbType.TinyInt).Value = tinyInt;
          break;
        case short smallInt:
          _command.Parameters.Add(key, SqlDbType.SmallInt).Value = smallInt;
          break;
        case int intValue:
          _command.Parameters.Add(key, SqlDbType.Int).Value = intValue;
          break;
        case long longValue:
          _command.Parameters.Add(key, SqlDbType.BigInt).Value = longValue;
          break;
        case double floatValue:
          _command.Parameters.Add(key, SqlDbType.Float).Value = floatValue;
          break;
        case string str:
          _command.Parameters.Add(key, SqlDbType.NVarChar).Value = str;
          break;
        case Guid guid:
          _command.Parameters.Add(key, SqlDbType.UniqueIdentifier).Value = guid;
          break;
        case DateTimeOffset dto:
          _command.Parameters.Add(key, SqlDbType.DateTimeOffset).Value = dto;
          break;
        case TimeSpan ts:
          _command.Parameters.Add(key, SqlDbType.Time).Value = ts;
          break;
        case byte[] bytes:
          _command.Parameters.Add(key, SqlDbType.VarBinary).Value = bytes;
          break;
        case SqlParameter param:
          if (!key.Equals(param.ParameterName, StringComparison.OrdinalIgnoreCase)) {
            throw new InvalidOperationException($"Property name ({key}) and parameter name ({param.ParameterName}) must match!");
          }
          _command.Parameters.Add(param);
          break;
        default:
          throw new InvalidOperationException($"Parameters of type: {value.GetType().FullName} are not supported.");
      }
    }

    private bool _disposed = false; // To detect redundant calls

    [SuppressMessage("Microsoft.Usage", "CA2202:Do not dispose objects multiple times")]
    void Dispose(bool disposing) {
      if (!_disposed && disposing) {
        using (var conn = _command.Connection)
        using (var command = _command)
        using (var reader = _reader) {
          if (_reader != null) {
            if (!_reader.IsClosed) {
              _reader.Close();
            }
            _reader = null;
          }

          if (conn.State != ConnectionState.Closed) {
            conn.Close();
          }
        }
      }
      _disposed = true;
    }

    public void Dispose() {
      Dispose(true);
      GC.SuppressFinalize(this);
    }
  }
}
