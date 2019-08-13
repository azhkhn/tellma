﻿using BSharp.Data.Queries;
using BSharp.EntityModel;
using BSharp.Services.ClientInfo;
using BSharp.Services.Identity;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Globalization;
using System.Threading.Tasks;
using System.Transactions;

namespace BSharp.Data
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Code Quality", "IDE0067:Dispose objects before losing scope", Justification = "To maintain the SESSION_CONTEXT we keep a hold of the SqlConnection object for the lifetime of the repository")]
    public class AdminRepository : IRepository, IDisposable
    {

        private readonly IExternalUserAccessor _externalUserAccessor;
        private readonly IClientInfoAccessor _clientInfoAccessor;
        private readonly IStringLocalizer<AdminRepository> _localizer;
        private readonly string _connectionString;

        private SqlConnection _conn;
        private GlobalUserInfo _userInfo;

        #region Lifecycle

        public AdminRepository(IOptions<AdminRepositoryOptions> config, IExternalUserAccessor externalUserAccessor, 
            IClientInfoAccessor clientInfoAccessor, IStringLocalizer<AdminRepository> localizer)
        {
            _connectionString = config?.Value?.ConnectionString ?? throw new ArgumentException("The admin connection string was not supplied", nameof(config));
            _externalUserAccessor = externalUserAccessor;
            _clientInfoAccessor = clientInfoAccessor;
            _localizer = localizer;
        }

        public void Dispose()
        {
            if (_conn != null)
            {
                _conn.Close();
                _conn.Dispose();
            }
        }

        #endregion

        #region Connection Management

        /// <summary>
        /// Initializes the connection if it is not already initialized
        /// </summary>
        /// <returns>The connection string that was initialized</returns>
        private async Task<SqlConnection> GetConnectionAsync()
        {
            if (_conn == null)
            {
                _conn = new SqlConnection(_connectionString);
                _conn.Open();

                // Always call OnConnect SP as soon as you create the connection
                var externalUserId = _externalUserAccessor.GetUserId();
                var externalEmail = _externalUserAccessor.GetUserEmail();
                var culture = CultureInfo.CurrentUICulture.Name;
                var neutralCulture = CultureInfo.CurrentUICulture.IsNeutralCulture ? CultureInfo.CurrentUICulture.Name : CultureInfo.CurrentUICulture.Parent.Name;

                _userInfo = await OnConnect(externalUserId, externalEmail, culture, neutralCulture);
            }

            // Since we opened the connection once, we need to explicitly enlist it in any ambient transaction
            // every time it is requested, otherwise commands will be executed outside the boundaries of the transaction
            _conn.EnlistTransaction(Transaction.Current);
            return _conn;
        }

        /// <summary>
        /// Loads a <see cref="GlobalUserInfo"/> object from the database, this occurs once per <see cref="ApplicationRepository"/> 
        /// instance, subsequent calls are satisfied from a scoped cache
        /// </summary>
        public async Task<GlobalUserInfo> GetGlobalUserInfoAsync()
        {
            await GetConnectionAsync(); // This automatically initializes the user info
            return _userInfo;
        }

        #endregion

        #region Queries

        public async Task<Query<T>> QueryAsync<T>() where T : Entity
        {
            var conn = await GetConnectionAsync();
            var sources = GetSources();
            var userInfo = await GetUserInfoAsync();
            var userId = userInfo.UserId ?? 0;
            var userTimeZone = _clientInfoAccessor.GetInfo().TimeZone;

            return new Query<T>(conn, sources, _localizer, userId, userTimeZone);
        }

        public async Task<AggregateQuery<T>> AggregateQueryAsync<T>() where T : Entity
        {
            var conn = await GetConnectionAsync();
            var sources = GetSources();
            var userInfo = await GetUserInfoAsync();
            var userId = userInfo.UserId ?? 0;
            var userTimeZone = _clientInfoAccessor.GetInfo().TimeZone;

            return new AggregateQuery<T>(conn, sources, _localizer, userId, userTimeZone);
        }

        private Func<Type, SqlSource> GetSources()
        {
            return (t) =>
            {
                switch (t.Name)
                {
                    case nameof(GlobalUser):
                        return new SqlSource("[dbo].[GlobalUsers]");

                    //case nameof(SqlDataba):
                    //    return new SqlSource("(SELECT * FROM [dbo].[MeasurementUnits] WHERE UnitType <> 'Money')");

                    default:
                        throw new InvalidOperationException($"The requested type {t.Name} is not supported in {nameof(ApplicationRepository)} queries");
                }
            };
        }


        #endregion

        #region Stored Procedures

        private async Task<GlobalUserInfo> OnConnect(string externalUserId, string userEmail, string culture, string neutralCulture)
        {
            GlobalUserInfo result = null;

            using (SqlCommand cmd = _conn.CreateCommand()) // Use the private field _conn to avoid infinite recursion
            {
                // Parameters
                cmd.Parameters.AddWithValue("@ExternalUserId", externalUserId);
                cmd.Parameters.AddWithValue("@UserEmail", userEmail);
                cmd.Parameters.AddWithValue("@Culture", culture);
                cmd.Parameters.AddWithValue("@NeutralCulture", neutralCulture);

                // Command
                cmd.CommandText = @"EXEC [dal].[OnConnect] 
@ExternalUserId = @ExternalUserId, 
@UserEmail      = @UserEmail, 
@Culture        = @Culture, 
@NeutralCulture = @NeutralCulture";

                // Execute and Load
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    if (await reader.ReadAsync())
                    {
                        int i = 0;

                        // The user Info
                        result = new GlobalUserInfo
                        {
                            UserId = reader.IsDBNull(i) ? (int?)null : reader.GetInt32(i++),
                            ExternalId = reader.IsDBNull(i) ? null : reader.GetString(i++),
                            Email = reader.IsDBNull(i) ? null : reader.GetString(i++),
                        };
                    }
                    else
                    {
                        throw new InvalidOperationException($"[dal].[OnConnect] did not return any data from Admin Database, ExternalUserId: {externalUserId}, UserEmail: {userEmail}");
                    }
                }
            }

            return result;
        }



        public Task<GlobalUserInfo> GetUserInfoAsync()
        {
            // TODO
            throw new NotImplementedException();
        }

        public Task SetUserExternalIdByUserIdAsync(int userId, string externalId)
        {
            throw new NotImplementedException();
        }

        public Task SetUserEmailByUserIdAsync(int userId, string externalEmail)
        {
            throw new NotImplementedException();
        }

        public Task SetUserExternalIdByEmailAsync(string email, string externalId)
        {
            // Finds the user with the given email and sets its externalId as specified
            throw new NotImplementedException();
        }

        public Task<IEnumerable<EmailExternalId>> GlobalUsers__AddAndMatch(IEnumerable<string> emails, int databaseId)
        {
            throw new NotImplementedException();
        }


        public async Task<DatabaseConnectionInfo> GetDatabaseConnectionInfo(int databaseId)
        {
            DatabaseConnectionInfo result = null;

            var conn = await GetConnectionAsync();
            using(var cmd = conn.CreateCommand())
            {
                // Parameters
                cmd.Parameters.AddWithValue("@DatabaseId", databaseId);

                // Command
                cmd.CommandText = $@"EXEC [dal].[{nameof(GetDatabaseConnectionInfo)}] @DatabaseId = @DatabaseId";

                // Execute and Load
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    if (await reader.ReadAsync())
                    {
                        int i = 0;

                        // The user Info
                        result = new DatabaseConnectionInfo
                        {
                            ServerName = reader.IsDBNull(i) ? null : reader.GetString(i++),
                            DatabaseName = reader.IsDBNull(i) ? null : reader.GetString(i++),
                            UserName = reader.IsDBNull(i) ? null : reader.GetString(i++),
                            PasswordKey = reader.IsDBNull(i) ? null : reader.GetString(i++),
                        };
                    }
                }
            }

            return result;
        }

        #endregion
    }
}
