﻿using Tellma.Data.Queries;
using Tellma.Entities;
using Tellma.Services.ClientInfo;
using Tellma.Services.Identity;
using Tellma.Services.Sharding;
using Tellma.Services.Utilities;
using Microsoft.Extensions.Localization;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Transactions;

namespace Tellma.Data
{
    /// <summary>
    /// A very thin and lightweight layer around the application database (every tenant
    /// has a dedicated application database). It's the entry point of all functionality that requires 
    /// SQL: Tables, Views, Stored Procedures etc.., it contains no logic of its own.
    /// By default it connects to the tenant Id supplied in the headers 
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Code Quality", "IDE0067:Dispose objects before losing scope", Justification = "To maintain the SESSION_CONTEXT we keep a hold of the SqlConnection object for the lifetime of the repository")]
    public class ApplicationRepository : IDisposable, IRepository
    {
        private readonly IShardResolver _shardResolver;
        private readonly IExternalUserAccessor _externalUserAccessor;
        private readonly IClientInfoAccessor _clientInfoAccessor;
        private readonly IStringLocalizer _localizer;

        private SqlConnection _conn;
        private UserInfo _userInfo;
        private TenantInfo _tenantInfo;
        private Transaction _transactionOverride;

        #region Lifecycle

        public ApplicationRepository(IShardResolver shardResolver, IExternalUserAccessor externalUserAccessor,
            IClientInfoAccessor clientInfoAccessor, IStringLocalizer<Strings> localizer)
        {
            _shardResolver = shardResolver;
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
        /// By default the <see cref="ApplicationRepository"/> connects to the database corresponding to 
        /// the current tenantId which is retrieved from an injected <see cref="IShardResolver"/>,
        /// this method makes it possible to conncet to a custom connection string instead, 
        /// this is useful when connecting to multiple tenants at the same time to do aggregate reporting for example
        /// </summary>
        public async Task InitConnectionAsync(string connectionString, bool setLastActive)
        {
            if (_conn != null)
            {
                throw new InvalidOperationException("The connection is already initialized");
            }

            _conn = new SqlConnection(connectionString);
            await _conn.OpenAsync();

            // Always call OnConnect SP as soon as you create the connection
            var externalUserId = _externalUserAccessor.GetUserId();
            var externalEmail = _externalUserAccessor.GetUserEmail();
            var culture = CultureInfo.CurrentUICulture.Name;
            var neutralCulture = CultureInfo.CurrentUICulture.IsNeutralCulture ? CultureInfo.CurrentUICulture.Name : CultureInfo.CurrentUICulture.Parent.Name;

            (_userInfo, _tenantInfo) = await OnConnect(externalUserId, externalEmail, culture, neutralCulture, setLastActive);
        }

        /// <summary>
        /// Initializes the connection if it is not already initialized
        /// </summary>
        /// <returns>The connection string that was initialized</returns>
        private async Task<SqlConnection> GetConnectionAsync()
        {
            if (_conn == null)
            {
                string connString = _shardResolver.GetConnectionString();
                await InitConnectionAsync(connString, setLastActive: true);
            }

            // Since we opened the connection once, we need to explicitly enlist it in any ambient transaction
            // every time it is requested, otherwise commands will be executed outside the boundaries of the transaction
            _conn.EnlistInTransaction(transactionOverride: _transactionOverride);
            return _conn;
        }

        /// <summary>
        /// Returns the name of the initial catalog from the active connection's connection string
        /// </summary>
        /// <returns></returns>
        private string InitialCatalog()
        {
            if (_conn == null || _conn.ConnectionString == null)
            {
                return null;
            }

            return new SqlConnectionStringBuilder(_conn.ConnectionString).InitialCatalog;
        }

        /// <summary>
        /// Loads a <see cref="UserInfo"/> object from the database, this occurs once per <see cref="ApplicationRepository"/> 
        /// instance, subsequent calls are satisfied from a scoped cache
        /// </summary>
        public async Task<UserInfo> GetUserInfoAsync()
        {
            await GetConnectionAsync(); // This automatically initializes the user info
            return _userInfo;
        }

        /// <summary>
        /// Loads a <see cref="UserInfo"/> object from the cache, or throws an exception if it's not available
        /// </summary>
        public UserInfo GetUserInfo()
        {
            return _userInfo ?? throw new InvalidOperationException("UserInfo are not initialized, call GetConnectionAsync() first or just use GetUserInfoAsync()");
        }

        /// <summary>
        /// Loads a <see cref="TenantInfo"/> object from the database, this occurs once per <see cref="ApplicationRepository"/> 
        /// instance, subsequent calls are satisfied from a scoped cache
        /// </summary>
        public async Task<TenantInfo> GetTenantInfoAsync()
        {
            await GetConnectionAsync(); // This automatically initializes the tenant info
            return _tenantInfo;
        }

        /// <summary>
        /// Loads a <see cref="TenantInfo"/> object from the cache, or throws an exception if it's not available
        /// </summary>
        public TenantInfo GetTenantInfo()
        {
            return _tenantInfo ?? throw new InvalidOperationException("TenantInfo are not initialized, call GetConnectionAsync() first or just use GetTenantInfoAsync()");
        }

        /// <summary>
        /// Enlists the repository's connection in the provided transaction such that all subsequent commands particupate in it, regardless of the ambient transaction
        /// </summary>
        /// <param name="transaction">The transaction to enlist the connection in</param>
        public void EnlistTransaction(Transaction transaction)
        {
            _transactionOverride = transaction;
        }

        #endregion

        #region Queries

        public Query<Settings> Settings => Query<Settings>();
        public Query<User> Users => Query<User>();
        public Query<Agent> Agents => Query<Agent>();

        /// <summary>
        /// Creates and returns a new <see cref="Queries.Query{T}"/>
        /// </summary>
        /// <typeparam name="T">The type of the <see cref="Queries.Query{T}"/></typeparam>
        public Query<T> Query<T>() where T : Entity
        {
            return new Query<T>(GetFactory());
        }

        /// <summary>
        /// Creates and returns a new <see cref="Queries.AggregateQuery{T}"/>
        /// </summary>
        /// <typeparam name="T">The root type of the <see cref="Queries.AggregateQuery{T}"/></typeparam>
        public AggregateQuery<T> AggregateQuery<T>() where T : Entity
        {
            return new AggregateQuery<T>(GetFactory());
        }

        private QueryArgumentsFactory GetFactory()
        {
            async Task<QueryArguments> Factory()
            {
                var conn = await GetConnectionAsync();
                var tenantInfo = await GetTenantInfoAsync();
                var userInfo = await GetUserInfoAsync();
                var userId = userInfo.UserId ?? 0;
                var userToday = _clientInfoAccessor.GetInfo().Today;

                return new QueryArguments(conn, Sources, userId, userToday, _localizer);
            }

            return Factory;
        }

        /// <summary>
        /// Returns a function that maps every <see cref="Entity"/> type in <see cref="ApplicationRepository"/> 
        /// to the default SQL query that retrieves it + some optional parameters
        /// </summary>
        private static string Sources(Type t)
        {
            switch (t.Name)
            {
                case nameof(Entities.Settings):
                    return "[dbo].[Settings]";

                case nameof(User):
                    return "[map].[Users]()";

                case nameof(Agent):
                    return "[map].[Agents]()";

                case nameof(MeasurementUnit):
                    return "[map].[MeasurementUnits]()";

                case nameof(Permission):
                    return "[dbo].[Permissions]";

                case nameof(RoleMembership):
                    return "[dbo].[RoleMemberships]";

                case nameof(Role):
                    return "[dbo].[Roles]";

                case nameof(LegacyType):
                    return "[dbo].[LegacyTypes]";

                case nameof(Lookup):
                    return "[map].[Lookups]()";

                case nameof(Currency):
                    return "[map].[Currencies]()";

                case nameof(Resource):
                    return "[map].[Resources]()";

                case nameof(LegacyClassification):
                    return "[map].[LegacyClassifications]()";

                case nameof(AccountType):
                    return "[map].[AccountTypes]()";

                case nameof(Account):
                    return "[map].[Accounts]()";

                case nameof(LookupDefinition):
                    return "[map].[LookupDefinitions]()";

                case nameof(ResponsibilityCenter):
                    return "[map].[ResponsibilityCenters]()";

                case nameof(EntryType):
                    return "[map].[EntryTypes]()";

                case nameof(Document):
                    return "[map].[Documents]()";

                case nameof(Line):
                    return "[map].[Lines]()";

                case nameof(Entry):
                    return "[map].[Entries]()";

                case nameof(Attachment):
                    return "[map].[Attachments]()";

                case nameof(DocumentSignature):
                    return "[map].[DocumentSignatures]()";

                case nameof(DocumentAssignment):
                    return "[map].[DocumentAssignmentsHistory]()";

                case nameof(ReportDefinition):
                    return "[map].[ReportDefinitions]()";

                case nameof(ReportParameterDefinition):
                    return "[map].[ReportParameterDefinitions]()";

                case nameof(ReportSelectDefinition):
                    return "[map].[ReportSelectDefinitions]()";

                case nameof(ReportRowDefinition):
                    return "[map].[ReportRowDefinitions]()";

                case nameof(ReportColumnDefinition):
                    return "[map].[ReportColumnDefinitions]()";

                case nameof(ReportMeasureDefinition):
                    return "[map].[ReportMeasureDefinitions]()";

                // Parametered fact tables
                case nameof(DetailsEntry):
                    return "[map].[DetailsEntries](@CountUnitId, @MassUnitId, @VolumeUnitId)";

                case nameof(SummaryEntry):
                    return "[map].[SummaryEntries](@FromDate, @ToDate, NULL, NULL, NULL, NULL, NULL, NULL)";

                #region _Temp

                case nameof(VoucherBooklet):
                    return "[dbo].[VoucherBooklets]";

                    #endregion
            }

            throw new InvalidOperationException($"The requested type '{t.Name}' is not supported in {nameof(ApplicationRepository)} queries");
        }

        #endregion

        #region Stored Procedures

        private async Task<(UserInfo, TenantInfo)> OnConnect(string externalUserId, string userEmail, string culture, string neutralCulture, bool setLastActive)
        {
            UserInfo userInfo = null;
            TenantInfo tenantInfo = null;

            using (SqlCommand cmd = _conn.CreateCommand()) // Use the private field _conn to avoid infinite recursion
            {
                // Parameters
                cmd.Parameters.Add("@ExternalUserId", externalUserId);
                cmd.Parameters.Add("@UserEmail", userEmail);
                cmd.Parameters.Add("@Culture", culture);
                cmd.Parameters.Add("@NeutralCulture", neutralCulture);
                cmd.Parameters.Add("@SetLastActive", setLastActive);

                // Command
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.CommandText = $"[dal].[{nameof(OnConnect)}]";

                // Execute and Load
                using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    int i = 0;

                    // The user Info
                    userInfo = new UserInfo
                    {
                        UserId = reader.Int32(i++),
                        Name = reader.String(i++),
                        Name2 = reader.String(i++),
                        Name3 = reader.String(i++),
                        ExternalId = reader.String(i++),
                        Email = reader.String(i++),
                        PermissionsVersion = reader.Guid(i++)?.ToString(),
                        UserSettingsVersion = reader.Guid(i++)?.ToString(),
                    };

                    // The tenant Info
                    tenantInfo = new TenantInfo
                    {
                        ShortCompanyName = reader.String(i++),
                        ShortCompanyName2 = reader.String(i++),
                        ShortCompanyName3 = reader.String(i++),
                        DefinitionsVersion = reader.Guid(i++)?.ToString(),
                        SettingsVersion = reader.Guid(i++)?.ToString(),
                        PrimaryLanguageId = reader.String(i++),
                        PrimaryLanguageSymbol = reader.String(i++),
                        SecondaryLanguageId = reader.String(i++),
                        SecondaryLanguageSymbol = reader.String(i++),
                        TernaryLanguageId = reader.String(i++),
                        TernaryLanguageSymbol = reader.String(i++)
                    };
                }
                else
                {
                    throw new InvalidOperationException($"[dal].[OnConnect] did not return any data, InitialCatalog: {InitialCatalog()}, ExternalUserId: {externalUserId}, UserEmail: {userEmail}");
                }
            }

            return (userInfo, tenantInfo);
        }

        public async Task Users__SetExternalIdByUserId(int userId, string externalId)
        {
            // Finds the user with the given id and sets its ExternalId to the one supplied only if it's null

            var conn = await GetConnectionAsync();
            using var cmd = conn.CreateCommand();
            // Parameters
            cmd.Parameters.Add("UserId", userId);
            cmd.Parameters.Add("ExternalId", externalId);

            // Command
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.CommandText = $"[dal].[{nameof(Users__SetExternalIdByUserId)}]";

            // Execute
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task Users__SetEmailByUserId(int userId, string externalEmail)
        {
            var conn = await GetConnectionAsync();
            using var cmd = conn.CreateCommand();

            // Parameters
            cmd.Parameters.Add("UserId", userId);
            cmd.Parameters.Add("ExternalEmail", externalEmail);

            // Command
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.CommandText = $"[dal].[{nameof(Users__SetEmailByUserId)}]";

            // Execute
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task<IEnumerable<AbstractPermission>> Action_View__Permissions(string action, string view)
        {
            var result = new List<AbstractPermission>();

            var conn = await GetConnectionAsync();
            using (SqlCommand cmd = conn.CreateCommand())
            {
                // Parameters
                cmd.Parameters.Add("@Action", action);
                cmd.Parameters.Add("@View", view);

                // Command
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.CommandText = $"[dal].[{nameof(Action_View__Permissions)}]";

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    int i = 0;
                    result.Add(new AbstractPermission
                    {
                        View = reader.GetString(i++),
                        Action = reader.GetString(i++),
                        Criteria = reader.String(i++),
                        Mask = reader.String(i++)
                    });
                }
            }

            return result;
        }

        public async Task<IEnumerable<AbstractPermission>> Action_ViewPrefix__Permissions(string action, string viewPrefix)
        {
            var result = new List<AbstractPermission>();

            var conn = await GetConnectionAsync();
            using (SqlCommand cmd = conn.CreateCommand())
            {
                // Parameters
                cmd.Parameters.Add("@Action", action);
                cmd.Parameters.Add("@ViewPrefix", viewPrefix);

                // Command
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.CommandText = $"[dal].[{nameof(Action_ViewPrefix__Permissions)}]";

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    int i = 0;
                    result.Add(new AbstractPermission
                    {
                        View = reader.GetString(i++),
                        Action = reader.GetString(i++),
                        Criteria = reader.String(i++),
                        Mask = reader.String(i++)
                    });
                }
            }

            return result;
        }

        public async Task<(Guid, User, IEnumerable<(string Key, string Value)>)> UserSettings__Load()
        {
            Guid version;
            var user = new User();
            var customSettings = new List<(string, string)>();

            var conn = await GetConnectionAsync();
            using (var cmd = conn.CreateCommand())
            {
                // Command
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.CommandText = $"[dal].[{nameof(UserSettings__Load)}]";

                // Execute
                using var reader = await cmd.ExecuteReaderAsync();
                // User Settings
                if (await reader.ReadAsync())
                {
                    int i = 0;

                    user.Id = reader.GetInt32(i++);
                    user.Name = reader.String(i++);
                    user.Name2 = reader.String(i++);
                    user.Name3 = reader.String(i++);
                    user.ImageId = reader.String(i++);
                    user.PreferredLanguage = reader.String(i++);

                    version = reader.GetGuid(i++);
                }
                else
                {
                    // Developer mistake
                    throw new InvalidOperationException("No settings for client were found");
                }

                // Custom settings
                await reader.NextResultAsync();
                while (await reader.ReadAsync())
                {
                    string key = reader.GetString(0);
                    string val = reader.GetString(1);

                    customSettings.Add((key, val));
                }
            }

            return (version, user, customSettings);
        }

        public async Task<(bool, Settings)> Settings__Load()
        {
            // Returns 
            // (1) whether active leaf responsibility centers are multiple or single
            // (2) the settings with the functional currency expanded

            bool isMultiResonsibilityCenter = false;
            Settings settings = new Settings();

            var conn = await GetConnectionAsync();
            using (SqlCommand cmd = conn.CreateCommand())
            {
                // Command
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.CommandText = $"[dal].[{nameof(Settings__Load)}]";

                // Execute
                using var reader = await cmd.ExecuteReaderAsync();
                // Load the version
                if (await reader.ReadAsync())
                {
                    isMultiResonsibilityCenter = reader.GetBoolean(0);
                }
                else
                {
                    // Programmer mistake
                    throw new Exception($"IsMultiResonsibilityCenter was not returned from SP {nameof(Settings__Load)}");
                }

                // Next load settings
                await reader.NextResultAsync();

                if (await reader.ReadAsync())
                {
                    var props = typeof(Settings).GetMappedProperties();
                    foreach (var prop in props)
                    {
                        // get property value
                        var propValue = reader[prop.Name];
                        propValue = propValue == DBNull.Value ? null : propValue;

                        prop.SetValue(settings, propValue);
                    }
                }
                else
                {
                    // Programmer mistake
                    throw new Exception($"Settings was not returned from SP {nameof(Settings__Load)}");
                }

                // Next load functional currency
                await reader.NextResultAsync();

                if (await reader.ReadAsync())
                {
                    settings.FunctionalCurrency = new Currency();
                    var props = typeof(Currency).GetMappedProperties();
                    foreach (var prop in props)
                    {
                        // get property value
                        var propValue = reader[prop.Name];
                        propValue = propValue == DBNull.Value ? null : propValue;

                        prop.SetValue(settings.FunctionalCurrency, propValue);
                    }
                }
                else
                {
                    // Programmer mistake
                    throw new Exception($"The Functional Currency was not returned from SP {nameof(Settings__Load)}");
                }
            }

            return (isMultiResonsibilityCenter, settings);
        }

        public async Task<(Guid, IEnumerable<AbstractPermission>)> Permissions__Load()
        {
            Guid version;
            var permissions = new List<AbstractPermission>();

            var conn = await GetConnectionAsync();
            using (SqlCommand cmd = conn.CreateCommand())
            {
                // Command
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.CommandText = $"[dal].[{nameof(Permissions__Load)}]";

                // Execute
                using var reader = await cmd.ExecuteReaderAsync();
                // Load the version
                if (await reader.ReadAsync())
                {
                    version = reader.GetGuid(0);
                }
                else
                {
                    version = Guid.Empty;
                }

                // Load the permissions
                await reader.NextResultAsync();

                while (await reader.ReadAsync())
                {
                    int i = 0;
                    permissions.Add(new AbstractPermission
                    {
                        View = reader.String(i++),
                        Action = reader.String(i++),
                        Criteria = reader.String(i++),
                        Mask = reader.String(i++)
                    });
                }
            }

            return (version, permissions);
        }

        public async Task<(Guid, IEnumerable<LookupDefinition>, IEnumerable<AgentDefinition>, IEnumerable<ResourceDefinition>, IEnumerable<ReportDefinition>)> Definitions__Load()
        {
            Guid version;
            List<LookupDefinition> lookupDefinitions = new List<LookupDefinition>();
            List<AgentDefinition> agentDefinitions = new List<AgentDefinition>();
            List<ResourceDefinition> resourceDefinitions = new List<ResourceDefinition>();
            List<ReportDefinition> reportDefinitions = new List<ReportDefinition>();

            var conn = await GetConnectionAsync();
            using (SqlCommand cmd = conn.CreateCommand())
            {
                // Command
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.CommandText = $"[dal].[{nameof(Definitions__Load)}]";

                // Execute
                using var reader = await cmd.ExecuteReaderAsync();
                // Load the version
                if (await reader.ReadAsync())
                {
                    version = reader.GetGuid(0);
                }
                else
                {
                    version = Guid.Empty;
                }

                // Next load lookup definitions
                var lookupDefinitionProps = typeof(LookupDefinition).GetMappedProperties();

                await reader.NextResultAsync();
                while (await reader.ReadAsync())
                {
                    var entity = new LookupDefinition();
                    foreach (var prop in lookupDefinitionProps)
                    {
                        // get property value
                        var propValue = reader[prop.Name];
                        propValue = propValue == DBNull.Value ? null : propValue;

                        prop.SetValue(entity, propValue);
                    }

                    lookupDefinitions.Add(entity);
                }

                // Next load agent definitions
                var agentDefinitionProps = typeof(AgentDefinition).GetMappedProperties();
                
                await reader.NextResultAsync();
                while (await reader.ReadAsync())
                {
                    var entity = new AgentDefinition();
                    foreach (var prop in agentDefinitionProps)
                    {
                        // get property value
                        var propValue = reader[prop.Name];
                        propValue = propValue == DBNull.Value ? null : propValue;

                        prop.SetValue(entity, propValue);
                    }

                    agentDefinitions.Add(entity);
                }

                // Next load resource definitions
                var resourceDefinitionProps = typeof(ResourceDefinition).GetMappedProperties();

                await reader.NextResultAsync();
                while (await reader.ReadAsync())
                {
                    var entity = new ResourceDefinition();
                    foreach (var prop in resourceDefinitionProps)
                    {
                        // get property value
                        var propValue = reader[prop.Name];
                        propValue = propValue == DBNull.Value ? null : propValue;

                        prop.SetValue(entity, propValue);
                    }

                    resourceDefinitions.Add(entity);
                }


                // Next load report definitions
                await reader.NextResultAsync();

                var reportDefinitionsDic = new Dictionary<string, ReportDefinition>();
                var reportDefinitionProps = typeof(ReportDefinition).GetMappedProperties();
                while (await reader.ReadAsync())
                {
                    var entity = new ReportDefinition();
                    foreach (var prop in reportDefinitionProps)
                    {
                        // get property value
                        var propValue = reader[prop.Name];
                        propValue = propValue == DBNull.Value ? null : propValue;

                        prop.SetValue(entity, propValue);
                    }

                    reportDefinitionsDic[entity.Id] = entity;
                }

                // Parameters
                var reportParameterDefinitionProps = typeof(ReportParameterDefinition).GetMappedProperties();
                await reader.NextResultAsync();
                while (await reader.ReadAsync())
                {
                    var entity = new ReportParameterDefinition();
                    foreach (var prop in reportParameterDefinitionProps)
                    {
                        // get property value
                        var propValue = reader[prop.Name];
                        propValue = propValue == DBNull.Value ? null : propValue;

                        prop.SetValue(entity, propValue);
                    }

                    var reportDefinition = reportDefinitionsDic[entity.ReportDefinitionId];
                    reportDefinition.Parameters ??= new List<ReportParameterDefinition>();
                    reportDefinition.Parameters.Add(entity);
                }

                // Select
                var reportSelectDefinitionProps = typeof(ReportSelectDefinition).GetMappedProperties();
                await reader.NextResultAsync();
                while (await reader.ReadAsync())
                {
                    var entity = new ReportSelectDefinition();
                    foreach (var prop in reportSelectDefinitionProps)
                    {
                        // get property value
                        var propValue = reader[prop.Name];
                        propValue = propValue == DBNull.Value ? null : propValue;

                        prop.SetValue(entity, propValue);
                    }

                    var reportDefinition = reportDefinitionsDic[entity.ReportDefinitionId];
                    reportDefinition.Select ??= new List<ReportSelectDefinition>();
                    reportDefinition.Select.Add(entity);
                }

                // Rows
                var reportRowDefinitionProps = typeof(ReportRowDefinition).GetMappedProperties();
                await reader.NextResultAsync();
                while (await reader.ReadAsync())
                {
                    var entity = new ReportRowDefinition();
                    foreach (var prop in reportRowDefinitionProps)
                    {
                        // get property value
                        var propValue = reader[prop.Name];
                        propValue = propValue == DBNull.Value ? null : propValue;

                        prop.SetValue(entity, propValue);
                    }

                    var reportDefinition = reportDefinitionsDic[entity.ReportDefinitionId];
                    reportDefinition.Rows ??= new List<ReportRowDefinition>();
                    reportDefinition.Rows.Add(entity);
                }

                // Columns
                var reportColumnDefinitionProps = typeof(ReportColumnDefinition).GetMappedProperties();
                await reader.NextResultAsync();
                while (await reader.ReadAsync())
                {
                    var entity = new ReportColumnDefinition();
                    foreach (var prop in reportColumnDefinitionProps)
                    {
                        // get property value
                        var propValue = reader[prop.Name];
                        propValue = propValue == DBNull.Value ? null : propValue;

                        prop.SetValue(entity, propValue);
                    }

                    var reportDefinition = reportDefinitionsDic[entity.ReportDefinitionId];
                    reportDefinition.Columns ??= new List<ReportColumnDefinition>();
                    reportDefinition.Columns.Add(entity);
                }

                // Measures
                var reportMeasureDefinitionProps = typeof(ReportMeasureDefinition).GetMappedProperties();
                await reader.NextResultAsync();
                while (await reader.ReadAsync())
                {
                    var entity = new ReportMeasureDefinition();
                    foreach (var prop in reportMeasureDefinitionProps)
                    {
                        // get property value
                        var propValue = reader[prop.Name];
                        propValue = propValue == DBNull.Value ? null : propValue;

                        prop.SetValue(entity, propValue);
                    }

                    var reportDefinition = reportDefinitionsDic[entity.ReportDefinitionId];
                    reportDefinition.Measures ??= new List<ReportMeasureDefinition>();
                    reportDefinition.Measures.Add(entity);
                }

                reportDefinitions = reportDefinitionsDic.Values.ToList();
            }

            return (version, lookupDefinitions, agentDefinitions, resourceDefinitions, reportDefinitions);
        }

        #endregion

        #region MeasurementUnits

        public Query<MeasurementUnit> MeasurementUnits__AsQuery(List<MeasurementUnitForSave> entities)
        {
            // This method returns the provided entities as a Query that can be selected, filtered etc...
            // The Ids in the result are always the indices of the original collection, even when the entity has a string key

            // Parameters
            DataTable entitiesTable = RepositoryUtilities.DataTable(entities, addIndex: true);
            SqlParameter entitiesTvp = new SqlParameter("@Entities", entitiesTable)
            {
                TypeName = $"[dbo].[{nameof(MeasurementUnit)}List]",
                SqlDbType = SqlDbType.Structured
            };

            // Query
            var query = Query<MeasurementUnit>();
            return query.FromSql($"[map].[{nameof(MeasurementUnits__AsQuery)}] (@Entities)", null, entitiesTvp);
        }

        public async Task<IEnumerable<ValidationError>> MeasurementUnits_Validate__Save(List<MeasurementUnitForSave> entities, int top)
        {
            var conn = await GetConnectionAsync();
            using var cmd = conn.CreateCommand();
            // Parameters
            DataTable entitiesTable = RepositoryUtilities.DataTable(entities, addIndex: true);
            var entitiesTvp = new SqlParameter("@Entities", entitiesTable)
            {
                TypeName = $"[dbo].[{nameof(MeasurementUnit)}List]",
                SqlDbType = SqlDbType.Structured
            };

            cmd.Parameters.Add(entitiesTvp);
            cmd.Parameters.Add("@Top", top);

            // Command
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.CommandText = $"[bll].[{nameof(MeasurementUnits_Validate__Save)}]";

            // Execute
            return await RepositoryUtilities.LoadErrors(cmd);
        }

        public async Task<List<int>> MeasurementUnits__Save(List<MeasurementUnitForSave> entities, bool returnIds)
        {
            var result = new List<IndexedId>();

            var conn = await GetConnectionAsync();
            using (var cmd = conn.CreateCommand())
            {
                DataTable entitiesTable = RepositoryUtilities.DataTable(entities, addIndex: true);
                var entitiesTvp = new SqlParameter("@Entities", entitiesTable)
                {
                    TypeName = $"[dbo].[{nameof(MeasurementUnit)}List]",
                    SqlDbType = SqlDbType.Structured
                };

                cmd.Parameters.Add(entitiesTvp);
                cmd.Parameters.Add("@ReturnIds", returnIds);

                cmd.CommandType = CommandType.StoredProcedure;
                cmd.CommandText = $"[dal].[{nameof(MeasurementUnits__Save)}]";

                if (returnIds)
                {
                    using var reader = await cmd.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        int i = 0;
                        result.Add(new IndexedId
                        {
                            Index = reader.GetInt32(i++),
                            Id = reader.GetInt32(i++)
                        });
                    }
                }
                else
                {
                    await cmd.ExecuteNonQueryAsync();
                }
            }

            // Return ordered result
            var sortedResult = new int[entities.Count];
            result.ForEach(e =>
            {
                sortedResult[e.Index] = e.Id;
            });

            return sortedResult.ToList();
        }

        public async Task MeasurementUnits__Activate(List<int> ids, bool isActive)
        {
            var conn = await GetConnectionAsync();
            using var cmd = conn.CreateCommand();
            // Parameters
            DataTable idsTable = RepositoryUtilities.DataTable(ids.Select(id => new { Id = id }));
            var idsTvp = new SqlParameter("@Ids", idsTable)
            {
                TypeName = $"[dbo].[IdList]",
                SqlDbType = SqlDbType.Structured
            };

            cmd.Parameters.Add(idsTvp);
            cmd.Parameters.Add("@IsActive", isActive);

            // Command
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.CommandText = $"[dal].[{nameof(MeasurementUnits__Activate)}]";

            // Execute
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task<IEnumerable<ValidationError>> MeasurementUnits_Validate__Delete(List<int> ids, int top)
        {
            var conn = await GetConnectionAsync();
            using var cmd = conn.CreateCommand();
            // Parameters
            DataTable idsTable = RepositoryUtilities.DataTable(ids.Select(id => new { Id = id }), addIndex: true);
            var idsTvp = new SqlParameter("@Ids", idsTable)
            {
                TypeName = $"[dbo].[IndexedIdList]",
                SqlDbType = SqlDbType.Structured
            };

            cmd.Parameters.Add(idsTvp);
            cmd.Parameters.Add("@Top", top);

            // Command
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.CommandText = $"[bll].[{nameof(MeasurementUnits_Validate__Delete)}]";

            // Execute
            return await RepositoryUtilities.LoadErrors(cmd);
        }

        public async Task MeasurementUnits__Delete(IEnumerable<int> ids)
        {
            var conn = await GetConnectionAsync();
            using var cmd = conn.CreateCommand();
            // Parameters
            DataTable idsTable = RepositoryUtilities.DataTable(ids.Select(id => new { Id = id }));
            var idsTvp = new SqlParameter("@Ids", idsTable)
            {
                TypeName = $"[dbo].[IdList]",
                SqlDbType = SqlDbType.Structured
            };

            cmd.Parameters.Add(idsTvp);

            // Command
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.CommandText = $"[dal].[{nameof(MeasurementUnits__Delete)}]";

            // Execute
            try
            {
                await cmd.ExecuteNonQueryAsync();
            }
            catch (SqlException ex) when (RepositoryUtilities.IsForeignKeyViolation(ex))
            {
                throw new ForeignKeyViolationException();
            }
        }

        #endregion

        #region Agents

        public Query<Agent> Agents__AsQuery(string definitionId, List<AgentForSave> entities)
        {
            // Parameters
            SqlParameter definitionParameter = new SqlParameter("@DefinitionId", definitionId);

            DataTable entitiesTable = RepositoryUtilities.DataTable(entities, addIndex: true);
            SqlParameter entitiesTvp = new SqlParameter("@Entities", entitiesTable)
            {
                TypeName = $"[dbo].[AgentList]",
                SqlDbType = SqlDbType.Structured
            };

            // Query
            var query = Query<Agent>();
            return query.FromSql($"[map].[{nameof(Agents__AsQuery)}] (@Entities)", null, definitionParameter, entitiesTvp);
        }

        public async Task<IEnumerable<ValidationError>> Agents_Validate__Save(string definitionId, List<AgentForSave> entities, int top)
        {
            var conn = await GetConnectionAsync();
            using var cmd = conn.CreateCommand();
            // Parameters
            DataTable entitiesTable = RepositoryUtilities.DataTable(entities, addIndex: true);
            var entitiesTvp = new SqlParameter("@Entities", entitiesTable)
            {
                TypeName = $"[dbo].[AgentList]",
                SqlDbType = SqlDbType.Structured
            };

            cmd.Parameters.Add("@DefinitionId", definitionId);
            cmd.Parameters.Add(entitiesTvp);
            cmd.Parameters.Add("@Top", top);

            // Command
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.CommandText = $"[bll].[{nameof(Agents_Validate__Save)}]";

            // Execute
            return await RepositoryUtilities.LoadErrors(cmd);
        }

        public async Task<List<int>> Agents__Save(string definitionId, List<AgentForSave> entities, IEnumerable<IndexedImageId> imageIds, bool returnIds)
        {
            var result = new List<IndexedId>();

            var conn = await GetConnectionAsync();
            using (var cmd = conn.CreateCommand())
            {
                // Parameters
                DataTable entitiesTable = RepositoryUtilities.DataTable(entities, addIndex: true);
                var entitiesTvp = new SqlParameter("@Entities", entitiesTable)
                {
                    TypeName = $"[dbo].[{nameof(Agent)}List]",
                    SqlDbType = SqlDbType.Structured
                };

                DataTable imageIdsTable = RepositoryUtilities.DataTable(imageIds);
                var imageIdsTvp = new SqlParameter("@ImageIds", imageIdsTable)
                {
                    TypeName = $"[dbo].[IndexedImageIdList]",
                    SqlDbType = SqlDbType.Structured
                };

                cmd.Parameters.Add("@DefinitionId", definitionId);
                cmd.Parameters.Add(entitiesTvp);
                cmd.Parameters.Add(imageIdsTvp);
                cmd.Parameters.Add("@ReturnIds", returnIds);

                // Command
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.CommandText = $"[dal].[{nameof(Agents__Save)}]";

                // Execute
                if (returnIds)
                {
                    using var reader = await cmd.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        int i = 0;
                        result.Add(new IndexedId
                        {
                            Index = reader.GetInt32(i++),
                            Id = reader.GetInt32(i++)
                        });
                    }
                }
                else
                {
                    await cmd.ExecuteNonQueryAsync();
                }
            }

            // Return ordered result
            var sortedResult = new int[entities.Count];
            result.ForEach(e =>
            {
                sortedResult[e.Index] = e.Id;
            });

            return sortedResult.ToList();
        }

        public async Task Agents__Delete(IEnumerable<int> ids)
        {
            var conn = await GetConnectionAsync();
            using var cmd = conn.CreateCommand();
            // Parameters
            DataTable idsTable = RepositoryUtilities.DataTable(ids.Select(id => new { Id = id }));
            var idsTvp = new SqlParameter("@Ids", idsTable)
            {
                TypeName = $"[dbo].[IdList]",
                SqlDbType = SqlDbType.Structured
            };

            cmd.Parameters.Add(idsTvp);

            // Command
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.CommandText = $"[dal].[{nameof(Agents__Delete)}]";

            // Execute
            try
            {
                await cmd.ExecuteNonQueryAsync();
            }
            catch (SqlException ex) when (RepositoryUtilities.IsForeignKeyViolation(ex))
            {
                throw new ForeignKeyViolationException();
            }
        }

        public async Task<IEnumerable<ValidationError>> Agents_Validate__Delete(string definitionId, List<int> ids, int top)
        {
            var conn = await GetConnectionAsync();
            using var cmd = conn.CreateCommand();
            // Parameters
            DataTable idsTable = RepositoryUtilities.DataTable(ids.Select(id => new { Id = id }), addIndex: true);
            var idsTvp = new SqlParameter("@Ids", idsTable)
            {
                TypeName = $"[dbo].[IndexedIdList]",
                SqlDbType = SqlDbType.Structured
            };

            cmd.Parameters.Add("@DefinitionId", definitionId);
            cmd.Parameters.Add(idsTvp);
            cmd.Parameters.Add("@Top", top);

            // Command
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.CommandText = $"[bll].[{nameof(Agents_Validate__Delete)}]";

            // Execute
            return await RepositoryUtilities.LoadErrors(cmd);
        }

        public async Task Agents__Activate(List<int> ids, bool isActive)
        {
            var conn = await GetConnectionAsync();
            using var cmd = conn.CreateCommand();
            // Parameters
            DataTable idsTable = RepositoryUtilities.DataTable(ids.Select(id => new { Id = id }));
            var idsTvp = new SqlParameter("@Ids", idsTable)
            {
                TypeName = $"[dbo].[IdList]",
                SqlDbType = SqlDbType.Structured
            };

            cmd.Parameters.Add(idsTvp);
            cmd.Parameters.Add("@IsActive", isActive);

            // Command
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.CommandText = $"[dal].[{nameof(Agents__Activate)}]";

            // Execute
            await cmd.ExecuteNonQueryAsync();
        }

        #endregion

        #region Users

        public async Task Users__SaveSettings(string key, string value)
        {
            var conn = await GetConnectionAsync();
            using var cmd = conn.CreateCommand();
            // Parameters
            cmd.Parameters.Add("Key", key);
            cmd.Parameters.Add("Value", value);

            // Command
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.CommandText = $"[dal].[{nameof(Users__SaveSettings)}]";

            // Execute
            await cmd.ExecuteNonQueryAsync();
        }

        public Query<User> Users__AsQuery(List<UserForSave> entities)
        {
            // Parameters
            DataTable entitiesTable = RepositoryUtilities.DataTable(entities, addIndex: true);
            SqlParameter entitiesTvp = new SqlParameter("@Entities", entitiesTable)
            {
                TypeName = $"[dbo].[UserList]",
                SqlDbType = SqlDbType.Structured
            };

            // Query
            var query = Query<User>();
            return query.FromSql($"[map].[{nameof(Users__AsQuery)}] (@Entities)", null, entitiesTvp);
        }

        public async Task<IEnumerable<ValidationError>> Users_Validate__Save(List<UserForSave> entities, int top)
        {
            entities.ForEach(e =>
            {
                e.Roles?.ForEach(r =>
                {
                    r.UserId = e.Id;
                });
            });

            var conn = await GetConnectionAsync();
            using var cmd = conn.CreateCommand();
            // Parameters
            DataTable entitiesTable = RepositoryUtilities.DataTable(entities, addIndex: true);
            var entitiesTvp = new SqlParameter("@Entities", entitiesTable)
            {
                TypeName = $"[dbo].[UserList]",
                SqlDbType = SqlDbType.Structured
            };

            DataTable rolesTable = RepositoryUtilities.DataTableWithHeaderIndex(entities, e => e.Roles);
            var rolesTvp = new SqlParameter("@Roles", rolesTable)
            {
                TypeName = $"[dbo].[{nameof(RoleMembership)}List]",
                SqlDbType = SqlDbType.Structured
            };

            cmd.Parameters.Add(entitiesTvp);
            cmd.Parameters.Add(rolesTvp);
            cmd.Parameters.Add("@Top", top);

            // Command
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.CommandText = $"[bll].[{nameof(Users_Validate__Save)}]";

            // Execute
            return await RepositoryUtilities.LoadErrors(cmd);
        }

        public async Task<List<int>> Users__Save(List<UserForSave> entities, IEnumerable<IndexedImageId> imageIds, bool returnIds)
        {
            entities.ForEach(e =>
            {
                e.Roles?.ForEach(r =>
                {
                    r.UserId = e.Id;
                });
            });

            var result = new List<IndexedId>();

            var conn = await GetConnectionAsync();
            using (var cmd = conn.CreateCommand())
            {
                // Parameters
                DataTable entitiesTable = RepositoryUtilities.DataTable(entities, addIndex: true);
                var entitiesTvp = new SqlParameter("@Entities", entitiesTable)
                {
                    TypeName = $"[dbo].[{nameof(User)}List]",
                    SqlDbType = SqlDbType.Structured
                };

                DataTable imageIdsTable = RepositoryUtilities.DataTable(imageIds);
                var imageIdsTvp = new SqlParameter("@ImageIds", imageIdsTable)
                {
                    TypeName = $"[dbo].[IndexedImageIdList]",
                    SqlDbType = SqlDbType.Structured
                };

                DataTable rolesTable = RepositoryUtilities.DataTableWithHeaderIndex(entities, e => e.Roles);
                var rolesTvp = new SqlParameter("@Roles", rolesTable)
                {
                    TypeName = $"[dbo].[{nameof(RoleMembership)}List]",
                    SqlDbType = SqlDbType.Structured
                };

                cmd.Parameters.Add(entitiesTvp);
                cmd.Parameters.Add(imageIdsTvp);
                cmd.Parameters.Add(rolesTvp);
                cmd.Parameters.Add("@ReturnIds", returnIds);

                // Command
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.CommandText = $"[dal].[{nameof(Users__Save)}]";

                // Execute
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    int i = 0;
                    result.Add(new IndexedId
                    {
                        Index = reader.GetInt32(i++),
                        Id = reader.GetInt32(i++)
                    });
                }
            }

            // Return ordered result
            var sortedResult = new int[entities.Count];
            result.ForEach(e =>
            {
                sortedResult[e.Index] = e.Id;
            });

            return sortedResult.ToList();
        }

        public async Task<IEnumerable<ValidationError>> Users_Validate__Delete(List<int> ids, int top)
        {
            var conn = await GetConnectionAsync();
            using var cmd = conn.CreateCommand();
            // Parameters
            DataTable idsTable = RepositoryUtilities.DataTable(ids.Select(id => new { Id = id }), addIndex: true);
            var idsTvp = new SqlParameter("@Ids", idsTable)
            {
                TypeName = $"[dbo].[IndexedIdList]",
                SqlDbType = SqlDbType.Structured
            };

            cmd.Parameters.Add(idsTvp);
            cmd.Parameters.Add("@Top", top);

            // Command
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.CommandText = $"[bll].[{nameof(Users_Validate__Delete)}]";

            // Execute
            return await RepositoryUtilities.LoadErrors(cmd);
        }

        public async Task<IEnumerable<string>> Users__Delete(IEnumerable<int> ids)
        {
            var deletedEmails = new List<string>(); // the result

            var conn = await GetConnectionAsync();
            using (var cmd = conn.CreateCommand())
            {
                // Parameters
                DataTable idsTable = RepositoryUtilities.DataTable(ids.Select(id => new { Id = id }));
                var idsTvp = new SqlParameter("@Ids", idsTable)
                {
                    TypeName = $"[dbo].[IdList]",
                    SqlDbType = SqlDbType.Structured
                };

                cmd.Parameters.Add(idsTvp);

                // Command
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.CommandText = $"[dal].[{nameof(Users__Delete)}]";

                // Execute
                try
                {
                    // Execute
                    using var reader = await cmd.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        deletedEmails.Add(reader.GetString(0));
                    }
                }
                catch (SqlException ex) when (RepositoryUtilities.IsForeignKeyViolation(ex))
                {
                    throw new ForeignKeyViolationException();
                }
            }

            return deletedEmails;
        }

        public async Task Users__Activate(List<int> ids, bool isActive)
        {
            var conn = await GetConnectionAsync();
            using var cmd = conn.CreateCommand();
            // Parameters
            DataTable idsTable = RepositoryUtilities.DataTable(ids.Select(id => new { Id = id }));
            var idsTvp = new SqlParameter("@Ids", idsTable)
            {
                TypeName = $"[dbo].[IdList]",
                SqlDbType = SqlDbType.Structured
            };

            cmd.Parameters.Add(idsTvp);
            cmd.Parameters.Add("@IsActive", isActive);

            // Command
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.CommandText = $"[dal].[{nameof(Users__Activate)}]";

            // Execute
            await cmd.ExecuteNonQueryAsync();
        }


        #endregion

        #region Roles

        public Query<Role> Roles__AsQuery(List<RoleForSave> entities)
        {
            // Parameters
            DataTable entitiesTable = RepositoryUtilities.DataTable(entities, addIndex: true);
            SqlParameter entitiesTvp = new SqlParameter("@Entities", entitiesTable)
            {
                TypeName = $"[dbo].[RoleList]",
                SqlDbType = SqlDbType.Structured
            };

            // Query
            var query = Query<Role>();
            return query.FromSql($"[map].[{nameof(Roles__AsQuery)}] (@Entities)", null, entitiesTvp);
        }

        public async Task<List<int>> Roles__Save(List<RoleForSave> entities, bool returnIds)
        {
            entities.ForEach(e =>
            {
                e.Members?.ForEach(m =>
                {
                    m.RoleId = e.Id;
                });
            });

            var result = new List<IndexedId>();

            var conn = await GetConnectionAsync();
            using (var cmd = conn.CreateCommand())
            {
                // Parameters
                DataTable entitiesTable = RepositoryUtilities.DataTable(entities, addIndex: true);
                var entitiesTvp = new SqlParameter("@Entities", entitiesTable)
                {
                    TypeName = $"[dbo].[{nameof(Role)}List]",
                    SqlDbType = SqlDbType.Structured
                };

                DataTable membersTable = RepositoryUtilities.DataTableWithHeaderIndex(entities, e => e.Members);
                var membersTvp = new SqlParameter("@Members", membersTable)
                {
                    TypeName = $"[dbo].[{nameof(RoleMembership)}List]",
                    SqlDbType = SqlDbType.Structured
                };

                DataTable permissionsTable = RepositoryUtilities.DataTableWithHeaderIndex(entities, e => e.Permissions);
                var permissionsTvp = new SqlParameter("@Permissions", permissionsTable)
                {
                    TypeName = $"[dbo].[{nameof(Permission)}List]",
                    SqlDbType = SqlDbType.Structured
                };

                cmd.Parameters.Add(entitiesTvp);
                cmd.Parameters.Add(membersTvp);
                cmd.Parameters.Add(permissionsTvp);
                cmd.Parameters.Add("@ReturnIds", returnIds);

                // Command
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.CommandText = $"[dal].[{nameof(Roles__Save)}]";

                // Execute
                if (returnIds)
                {
                    using var reader = await cmd.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        int i = 0;
                        result.Add(new IndexedId
                        {
                            Index = reader.GetInt32(i++),
                            Id = reader.GetInt32(i++)
                        });
                    }
                }
                else
                {
                    await cmd.ExecuteNonQueryAsync();
                }
            }

            // Return ordered result
            var sortedResult = new int[entities.Count];
            result.ForEach(e =>
            {
                sortedResult[e.Index] = e.Id;
            });

            return sortedResult.ToList();
        }

        public async Task<IEnumerable<ValidationError>> Roles_Validate__Save(List<RoleForSave> entities, int top)
        {
            entities.ForEach(e =>
            {
                e.Members?.ForEach(m =>
                {
                    m.RoleId = e.Id;
                });
            });

            var conn = await GetConnectionAsync();
            using var cmd = conn.CreateCommand();
            // Parameters
            DataTable entitiesTable = RepositoryUtilities.DataTable(entities, addIndex: true);
            var entitiesTvp = new SqlParameter("@Entities", entitiesTable)
            {
                TypeName = $"[dbo].[{nameof(Role)}List]",
                SqlDbType = SqlDbType.Structured
            };

            DataTable membersTable = RepositoryUtilities.DataTableWithHeaderIndex(entities, e => e.Members);
            var membersTvp = new SqlParameter("@Members", membersTable)
            {
                TypeName = $"[dbo].[{nameof(RoleMembership)}List]",
                SqlDbType = SqlDbType.Structured
            };

            DataTable permissionsTable = RepositoryUtilities.DataTableWithHeaderIndex(entities, e => e.Permissions);
            var permissionsTvp = new SqlParameter("@Permissions", permissionsTable)
            {
                TypeName = $"[dbo].[{nameof(Permission)}List]",
                SqlDbType = SqlDbType.Structured
            };

            cmd.Parameters.Add(entitiesTvp);
            cmd.Parameters.Add(membersTvp);
            cmd.Parameters.Add(permissionsTvp);
            cmd.Parameters.Add("@Top", top);

            // Command
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.CommandText = $"[bll].[{nameof(Roles_Validate__Save)}]";

            // Execute
            return await RepositoryUtilities.LoadErrors(cmd);
        }

        public async Task Roles__Delete(IEnumerable<int> ids)
        {
            var conn = await GetConnectionAsync();
            using var cmd = conn.CreateCommand();
            // Parameters
            DataTable idsTable = RepositoryUtilities.DataTable(ids.Select(id => new { Id = id }));
            var idsTvp = new SqlParameter("@Ids", idsTable)
            {
                TypeName = $"[dbo].[IdList]",
                SqlDbType = SqlDbType.Structured
            };

            cmd.Parameters.Add(idsTvp);

            // Command
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.CommandText = $"[dal].[{nameof(Roles__Delete)}]";

            // Execute
            try
            {
                await cmd.ExecuteNonQueryAsync();
            }
            catch (SqlException ex) when (RepositoryUtilities.IsForeignKeyViolation(ex))
            {
                throw new ForeignKeyViolationException();
            }
        }

        public async Task<IEnumerable<ValidationError>> Roles_Validate__Delete(List<int> ids, int top)
        {
            var conn = await GetConnectionAsync();
            using var cmd = conn.CreateCommand();
            // Parameters
            DataTable idsTable = RepositoryUtilities.DataTable(ids.Select(id => new { Id = id }), addIndex: true);
            var idsTvp = new SqlParameter("@Ids", idsTable)
            {
                TypeName = $"[dbo].[IndexedIdList]",
                SqlDbType = SqlDbType.Structured
            };

            cmd.Parameters.Add(idsTvp);
            cmd.Parameters.Add("@Top", top);

            // Command
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.CommandText = $"[bll].[{nameof(Roles_Validate__Delete)}]";

            // Execute
            return await RepositoryUtilities.LoadErrors(cmd);
        }

        public async Task Roles__Activate(List<int> ids, bool isActive)
        {
            var conn = await GetConnectionAsync();
            using var cmd = conn.CreateCommand();
            // Parameters
            DataTable idsTable = RepositoryUtilities.DataTable(ids.Select(id => new { Id = id }));
            var idsTvp = new SqlParameter("@Ids", idsTable)
            {
                TypeName = $"[dbo].[IdList]",
                SqlDbType = SqlDbType.Structured
            };

            cmd.Parameters.Add(idsTvp);
            cmd.Parameters.Add("@IsActive", isActive);

            // Command
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.CommandText = $"[dal].[{nameof(Roles__Activate)}]";

            // Execute
            await cmd.ExecuteNonQueryAsync();
        }

        #endregion

        #region Blobs

        public async Task Blobs__Delete(IEnumerable<string> blobNames)
        {
            var conn = await GetConnectionAsync();
            using var cmd = conn.CreateCommand();
            // Parameters
            DataTable namesTable = RepositoryUtilities.DataTable(blobNames.Select(id => new { Id = id }));
            var namesTvp = new SqlParameter("@BlobNames", namesTable)
            {
                TypeName = $"[dbo].[StringList]",
                SqlDbType = SqlDbType.Structured
            };

            cmd.Parameters.Add(namesTvp);

            // Command
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.CommandText = $"[dal].[{nameof(Blobs__Delete)}]";

            // Execute
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task Blobs__Save(string name, byte[] blob)
        {
            var conn = await GetConnectionAsync();
            using var cmd = conn.CreateCommand();
            // Parameters
            cmd.Parameters.Add("@Name", name);
            cmd.Parameters.Add("@Blob", blob);

            // Command
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.CommandText = $"[dal].[{nameof(Blobs__Save)}]";

            // Execute
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task<byte[]> Blobs__Get(string name)
        {
            byte[] result = null;

            var conn = await GetConnectionAsync();
            using (var cmd = conn.CreateCommand())
            {
                // Parameters
                cmd.Parameters.Add("@Name", name);

                // Command
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.CommandText = $"[dal].[{nameof(Blobs__Get)}]";

                // Execute
                using var reader = await cmd.ExecuteReaderAsync(CommandBehavior.SequentialAccess);
                if (await reader.ReadAsync())
                {
                    result = (byte[])reader[0];
                }
            }

            return result;
        }

        #endregion

        #region Settings

        public async Task Settings__Save(SettingsForSave settingsForSave)
        {
            if (settingsForSave is null)
            {
                return;
            }

            var conn = await GetConnectionAsync();
            using var cmd = conn.CreateCommand();
            // Arguments
            var mappedProps = typeof(SettingsForSave).GetMappedProperties();

            var sqlBuilder = new System.Text.StringBuilder();
            sqlBuilder.AppendLine("UPDATE [dbo].[Settings] SET");

            foreach (var prop in mappedProps)
            {
                var propName = prop.Name;
                var key = $"@{propName}";
                var value = prop.GetValue(settingsForSave);

                cmd.Parameters.Add(key, value);
                sqlBuilder.AppendLine($"{propName} = {key},");
            }

            sqlBuilder.AppendLine($"ModifiedAt = SYSDATETIMEOFFSET(),");
            sqlBuilder.AppendLine($"ModifiedById = CONVERT(INT, SESSION_CONTEXT(N'UserId')),");
            sqlBuilder.AppendLine($"SettingsVersion = NEWID()");

            // Command
            cmd.CommandText = sqlBuilder.ToString();

            // Execute
            await cmd.ExecuteNonQueryAsync();
        }

        #endregion

        #region Lookups

        public Query<Lookup> Lookups__AsQuery(string definitionId, List<LookupForSave> entities)
        {
            // This method returns the provided entities as a Query that can be selected, filtered etc...
            // The Ids in the result are always the indices of the original collection, even when the entity has a string key

            // Parameters
            SqlParameter definitionParameter = new SqlParameter("@DefinitionId", definitionId);

            DataTable entitiesTable = RepositoryUtilities.DataTable(entities, addIndex: true);
            SqlParameter entitiesTvp = new SqlParameter("@Entities", entitiesTable)
            {
                TypeName = $"[dbo].[{nameof(Lookup)}List]",
                SqlDbType = SqlDbType.Structured
            };


            // Query
            var query = Query<Lookup>();
            return query.FromSql($"[map].[{nameof(Lookups__AsQuery)}] (@Entities)", null, definitionParameter, entitiesTvp);
        }

        public async Task<IEnumerable<ValidationError>> Lookups_Validate__Save(string definitionId, List<LookupForSave> entities, int top)
        {
            var conn = await GetConnectionAsync();
            using var cmd = conn.CreateCommand();
            // Parameters
            DataTable entitiesTable = RepositoryUtilities.DataTable(entities, addIndex: true);
            var entitiesTvp = new SqlParameter("@Entities", entitiesTable)
            {
                TypeName = $"[dbo].[{nameof(Lookup)}List]",
                SqlDbType = SqlDbType.Structured
            };

            cmd.Parameters.Add("@DefinitionId", definitionId);
            cmd.Parameters.Add(entitiesTvp);
            cmd.Parameters.Add("@Top", top);

            // Command
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.CommandText = $"[bll].[{nameof(Lookups_Validate__Save)}]";

            // Execute
            return await RepositoryUtilities.LoadErrors(cmd);
        }

        public async Task<List<int>> Lookups__Save(string definitionId, List<LookupForSave> entities, bool returnIds)
        {
            var result = new List<IndexedId>();

            var conn = await GetConnectionAsync();
            using (var cmd = conn.CreateCommand())
            {
                DataTable entitiesTable = RepositoryUtilities.DataTable(entities, addIndex: true);
                var entitiesTvp = new SqlParameter("@Entities", entitiesTable)
                {
                    TypeName = $"[dbo].[{nameof(Lookup)}List]",
                    SqlDbType = SqlDbType.Structured
                };

                cmd.Parameters.Add("@DefinitionId", definitionId);
                cmd.Parameters.Add(entitiesTvp);
                cmd.Parameters.Add("@ReturnIds", returnIds);

                cmd.CommandType = CommandType.StoredProcedure;
                cmd.CommandText = $"[dal].[{nameof(Lookups__Save)}]";

                if (returnIds)
                {
                    using var reader = await cmd.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        int i = 0;
                        result.Add(new IndexedId
                        {
                            Index = reader.GetInt32(i++),
                            Id = reader.GetInt32(i++)
                        });
                    }
                }
                else
                {
                    await cmd.ExecuteNonQueryAsync();
                }
            }

            // Return ordered result
            var sortedResult = new int[entities.Count];
            result.ForEach(e =>
            {
                sortedResult[e.Index] = e.Id;
            });

            return sortedResult.ToList();
        }

        public async Task Lookups__Activate(List<int> ids, bool isActive)
        {
            var conn = await GetConnectionAsync();
            using var cmd = conn.CreateCommand();
            // Parameters
            DataTable idsTable = RepositoryUtilities.DataTable(ids.Select(id => new { Id = id }));
            var idsTvp = new SqlParameter("@Ids", idsTable)
            {
                TypeName = $"[dbo].[IdList]",
                SqlDbType = SqlDbType.Structured
            };

            cmd.Parameters.Add(idsTvp);
            cmd.Parameters.Add("@IsActive", isActive);

            // Command
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.CommandText = $"[dal].[{nameof(Lookups__Activate)}]";

            // Execute
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task<IEnumerable<ValidationError>> Lookups_Validate__Delete(string definitionId, List<int> ids, int top)
        {
            var conn = await GetConnectionAsync();
            using var cmd = conn.CreateCommand();
            // Parameters
            DataTable idsTable = RepositoryUtilities.DataTable(ids.Select(id => new { Id = id }), addIndex: true);
            var idsTvp = new SqlParameter("@Ids", idsTable)
            {
                TypeName = $"[dbo].[IndexedIdList]",
                SqlDbType = SqlDbType.Structured
            };

            cmd.Parameters.Add("@DefinitionId", definitionId);
            cmd.Parameters.Add(idsTvp);
            cmd.Parameters.Add("@Top", top);

            // Command
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.CommandText = $"[bll].[{nameof(Lookups_Validate__Delete)}]";

            // Execute
            return await RepositoryUtilities.LoadErrors(cmd);
        }

        public async Task Lookups__Delete(IEnumerable<int> ids)
        {
            var conn = await GetConnectionAsync();
            using var cmd = conn.CreateCommand();
            // Parameters
            DataTable idsTable = RepositoryUtilities.DataTable(ids.Select(id => new { Id = id }));
            var idsTvp = new SqlParameter("@Ids", idsTable)
            {
                TypeName = $"[dbo].[IdList]",
                SqlDbType = SqlDbType.Structured
            };

            cmd.Parameters.Add(idsTvp);

            // Command
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.CommandText = $"[dal].[{nameof(Lookups__Delete)}]";

            // Execute
            try
            {
                await cmd.ExecuteNonQueryAsync();
            }
            catch (SqlException ex) when (RepositoryUtilities.IsForeignKeyViolation(ex))
            {
                throw new ForeignKeyViolationException();
            }
        }

        #endregion

        #region Currencies

        public Query<Currency> Currencies__AsQuery(List<CurrencyForSave> entities)
        {
            // This method returns the provided entities as a Query that can be selected, filtered etc...
            // The Ids in the result are always the indices of the original collection, even when the entity has a string key

            // Parameters
            DataTable entitiesTable = RepositoryUtilities.DataTable(entities, addIndex: true);
            SqlParameter entitiesTvp = new SqlParameter("@Entities", entitiesTable)
            {
                TypeName = $"[dbo].[{nameof(Currency)}List]",
                SqlDbType = SqlDbType.Structured
            };

            // Query
            var query = Query<Currency>();
            return query.FromSql($"[map].[{nameof(Currencies__AsQuery)}] (@Entities)", null, entitiesTvp);
        }

        public async Task<IEnumerable<ValidationError>> Currencies_Validate__Save(List<CurrencyForSave> entities, int top)
        {
            var conn = await GetConnectionAsync();
            using var cmd = conn.CreateCommand();
            // Parameters
            DataTable entitiesTable = RepositoryUtilities.DataTable(entities, addIndex: true);
            var entitiesTvp = new SqlParameter("@Entities", entitiesTable)
            {
                TypeName = $"[dbo].[{nameof(Currency)}List]",
                SqlDbType = SqlDbType.Structured
            };

            cmd.Parameters.Add(entitiesTvp);
            cmd.Parameters.Add("@Top", top);

            // Command
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.CommandText = $"[bll].[{nameof(Currencies_Validate__Save)}]";

            // Execute
            return await RepositoryUtilities.LoadErrors(cmd);
        }

        public async Task Currencies__Save(List<CurrencyForSave> entities)
        {
            var result = new List<IndexedId>();

            var conn = await GetConnectionAsync();
            using var cmd = conn.CreateCommand();
            DataTable entitiesTable = RepositoryUtilities.DataTable(entities, addIndex: true);
            var entitiesTvp = new SqlParameter("@Entities", entitiesTable)
            {
                TypeName = $"[dbo].[{nameof(Currency)}List]",
                SqlDbType = SqlDbType.Structured
            };

            cmd.Parameters.Add(entitiesTvp);

            cmd.CommandType = CommandType.StoredProcedure;
            cmd.CommandText = $"[dal].[{nameof(Currencies__Save)}]";

            await cmd.ExecuteNonQueryAsync();
        }

        public async Task Currencies__Activate(List<string> ids, bool isActive)
        {
            var conn = await GetConnectionAsync();
            using var cmd = conn.CreateCommand();
            // Parameters
            DataTable idsTable = RepositoryUtilities.DataTable(ids.Select(id => new { Id = id }));
            var idsTvp = new SqlParameter("@Ids", idsTable)
            {
                TypeName = $"[dbo].[StringList]",
                SqlDbType = SqlDbType.Structured
            };

            cmd.Parameters.Add(idsTvp);
            cmd.Parameters.Add("@IsActive", isActive);

            // Command
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.CommandText = $"[dal].[{nameof(Currencies__Activate)}]";

            // Execute
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task<IEnumerable<ValidationError>> Currencies_Validate__Delete(List<string> ids, int top)
        {
            var conn = await GetConnectionAsync();
            using var cmd = conn.CreateCommand();
            // Parameters
            DataTable idsTable = RepositoryUtilities.DataTable(ids.Select(id => new { Id = id }), addIndex: true);
            var idsTvp = new SqlParameter("@Ids", idsTable)
            {
                TypeName = $"[dbo].[IndexedStringList]",
                SqlDbType = SqlDbType.Structured
            };

            cmd.Parameters.Add(idsTvp);
            cmd.Parameters.Add("@Top", top);

            // Command
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.CommandText = $"[bll].[{nameof(Currencies_Validate__Delete)}]";

            // Execute
            return await RepositoryUtilities.LoadErrors(cmd);
        }

        public async Task Currencies__Delete(IEnumerable<string> ids)
        {
            var conn = await GetConnectionAsync();
            using var cmd = conn.CreateCommand();
            // Parameters
            DataTable idsTable = RepositoryUtilities.DataTable(ids.Select(id => new { Id = id }));
            var idsTvp = new SqlParameter("@Ids", idsTable)
            {
                TypeName = $"[dbo].[StringList]",
                SqlDbType = SqlDbType.Structured
            };

            cmd.Parameters.Add(idsTvp);

            // Command
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.CommandText = $"[dal].[{nameof(Currencies__Delete)}]";

            // Execute
            try
            {
                await cmd.ExecuteNonQueryAsync();
            }
            catch (SqlException ex) when (RepositoryUtilities.IsForeignKeyViolation(ex))
            {
                throw new ForeignKeyViolationException();
            }
        }

        #endregion

        #region Resources

        public Query<Resource> Resources__AsQuery(string definitionId, List<ResourceForSave> entities)
        {
            // This method returns the provided entities as a Query that can be selected, filtered etc...
            // The Ids in the result are always the indices of the original collection, even when the entity has a string key

            // Parameters
            SqlParameter definitionParameter = new SqlParameter("@DefinitionId", definitionId);

            DataTable entitiesTable = RepositoryUtilities.DataTable(entities, addIndex: true);
            SqlParameter entitiesTvp = new SqlParameter("@Entities", entitiesTable)
            {
                TypeName = $"[dbo].[{nameof(Resource)}List]",
                SqlDbType = SqlDbType.Structured
            };


            // Query
            var query = Query<Resource>();
            return query.FromSql($"[map].[{nameof(Resources__AsQuery)}] (@Entities)", null, definitionParameter, entitiesTvp);
        }

        public async Task<IEnumerable<ValidationError>> Resources_Validate__Save(string definitionId, List<ResourceForSave> entities, int top)
        {
            var conn = await GetConnectionAsync();
            using var cmd = conn.CreateCommand();
            // Parameters
            DataTable entitiesTable = RepositoryUtilities.DataTable(entities, addIndex: true);
            var entitiesTvp = new SqlParameter("@Entities", entitiesTable)
            {
                TypeName = $"[dbo].[{nameof(Resource)}List]",
                SqlDbType = SqlDbType.Structured
            };

            cmd.Parameters.Add("@DefinitionId", definitionId);
            cmd.Parameters.Add(entitiesTvp);
            cmd.Parameters.Add("@Top", top);

            // Command
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.CommandText = $"[bll].[{nameof(Resources_Validate__Save)}]";

            // Execute
            return await RepositoryUtilities.LoadErrors(cmd);
        }

        public async Task<List<int>> Resources__Save(string definitionId, List<ResourceForSave> entities, bool returnIds)
        {
            var result = new List<IndexedId>();

            var conn = await GetConnectionAsync();
            using (var cmd = conn.CreateCommand())
            {
                DataTable entitiesTable = RepositoryUtilities.DataTable(entities, addIndex: true);
                var entitiesTvp = new SqlParameter("@Entities", entitiesTable)
                {
                    TypeName = $"[dbo].[{nameof(Resource)}List]",
                    SqlDbType = SqlDbType.Structured
                };

                cmd.Parameters.Add("@DefinitionId", definitionId);
                cmd.Parameters.Add(entitiesTvp);
                cmd.Parameters.Add("@ReturnIds", returnIds);

                cmd.CommandType = CommandType.StoredProcedure;
                cmd.CommandText = $"[dal].[{nameof(Resources__Save)}]";

                if (returnIds)
                {
                    using var reader = await cmd.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        int i = 0;
                        result.Add(new IndexedId
                        {
                            Index = reader.GetInt32(i++),
                            Id = reader.GetInt32(i++)
                        });
                    }
                }
                else
                {
                    await cmd.ExecuteNonQueryAsync();
                }
            }

            // Return ordered result
            var sortedResult = new int[entities.Count];
            result.ForEach(e =>
            {
                sortedResult[e.Index] = e.Id;
            });

            return sortedResult.ToList();
        }

        public async Task Resources__Activate(List<int> ids, bool isActive)
        {
            var conn = await GetConnectionAsync();
            using var cmd = conn.CreateCommand();
            // Parameters
            DataTable idsTable = RepositoryUtilities.DataTable(ids.Select(id => new { Id = id }));
            var idsTvp = new SqlParameter("@Ids", idsTable)
            {
                TypeName = $"[dbo].[IdList]",
                SqlDbType = SqlDbType.Structured
            };

            cmd.Parameters.Add(idsTvp);
            cmd.Parameters.Add("@IsActive", isActive);

            // Command
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.CommandText = $"[dal].[{nameof(Resources__Activate)}]";

            // Execute
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task<IEnumerable<ValidationError>> Resources_Validate__Delete(string definitionId, List<int> ids, int top)
        {
            var conn = await GetConnectionAsync();
            using var cmd = conn.CreateCommand();
            // Parameters
            DataTable idsTable = RepositoryUtilities.DataTable(ids.Select(id => new { Id = id }), addIndex: true);
            var idsTvp = new SqlParameter("@Ids", idsTable)
            {
                TypeName = $"[dbo].[IndexedIdList]",
                SqlDbType = SqlDbType.Structured
            };

            cmd.Parameters.Add("@DefinitionId", definitionId);
            cmd.Parameters.Add(idsTvp);
            cmd.Parameters.Add("@Top", top);

            // Command
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.CommandText = $"[bll].[{nameof(Resources_Validate__Delete)}]";

            // Execute
            return await RepositoryUtilities.LoadErrors(cmd);
        }

        public async Task Resources__Delete(IEnumerable<int> ids)
        {
            var conn = await GetConnectionAsync();
            using var cmd = conn.CreateCommand();
            // Parameters
            DataTable idsTable = RepositoryUtilities.DataTable(ids.Select(id => new { Id = id }));
            var idsTvp = new SqlParameter("@Ids", idsTable)
            {
                TypeName = $"[dbo].[IdList]",
                SqlDbType = SqlDbType.Structured
            };

            cmd.Parameters.Add(idsTvp);

            // Command
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.CommandText = $"[dal].[{nameof(Resources__Delete)}]";

            // Execute
            try
            {
                await cmd.ExecuteNonQueryAsync();
            }
            catch (SqlException ex) when (RepositoryUtilities.IsForeignKeyViolation(ex))
            {
                throw new ForeignKeyViolationException();
            }
        }

        #endregion

        #region LegacyClassifications

        public Query<LegacyClassification> LegacyClassifications__AsQuery(List<LegacyClassificationForSave> entities)
        {
            // This method returns the provided entities as a Query that can be selected, filtered etc...
            // The Ids in the result are always the indices of the original collection, even when the entity has a string key

            // Parameters
            DataTable entitiesTable = RepositoryUtilities.DataTableWithParentIndex(entities, e => e.ParentIndex);
            SqlParameter entitiesTvp = new SqlParameter("@Entities", entitiesTable)
            {
                TypeName = $"[dbo].[{nameof(LegacyClassification)}List]",
                SqlDbType = SqlDbType.Structured
            };

            // Query
            var query = Query<LegacyClassification>();
            return query.FromSql($"[map].[{nameof(LegacyClassifications__AsQuery)}] (@Entities)", null, entitiesTvp);
        }

        public async Task<IEnumerable<ValidationError>> LegacyClassifications_Validate__Save(List<LegacyClassificationForSave> entities, int top)
        {
            var conn = await GetConnectionAsync();
            using var cmd = conn.CreateCommand();
            // Parameters
            DataTable entitiesTable = RepositoryUtilities.DataTableWithParentIndex(entities, e => e.ParentIndex);
            var entitiesTvp = new SqlParameter("@Entities", entitiesTable)
            {
                TypeName = $"[dbo].[{nameof(LegacyClassification)}List]",
                SqlDbType = SqlDbType.Structured
            };

            cmd.Parameters.Add(entitiesTvp);
            cmd.Parameters.Add("@Top", top);

            // Command
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.CommandText = $"[bll].[{nameof(LegacyClassifications_Validate__Save)}]";

            // Execute
            return await RepositoryUtilities.LoadErrors(cmd);
        }

        public async Task<List<int>> LegacyClassifications__Save(List<LegacyClassificationForSave> entities, bool returnIds)
        {
            var result = new List<IndexedId>();

            var conn = await GetConnectionAsync();
            using (var cmd = conn.CreateCommand())
            {
                DataTable entitiesTable = RepositoryUtilities.DataTableWithParentIndex(entities, e => e.ParentIndex);
                var entitiesTvp = new SqlParameter("@Entities", entitiesTable)
                {
                    TypeName = $"[dbo].[{nameof(LegacyClassification)}List]",
                    SqlDbType = SqlDbType.Structured
                };

                cmd.Parameters.Add(entitiesTvp);
                cmd.Parameters.Add("@ReturnIds", returnIds);

                cmd.CommandType = CommandType.StoredProcedure;
                cmd.CommandText = $"[dal].[{nameof(LegacyClassifications__Save)}]";

                if (returnIds)
                {
                    using var reader = await cmd.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        int i = 0;
                        result.Add(new IndexedId
                        {
                            Index = reader.GetInt32(i++),
                            Id = reader.GetInt32(i++)
                        });
                    }
                }
                else
                {
                    await cmd.ExecuteNonQueryAsync();
                }
            }

            // Return ordered result
            var sortedResult = new int[entities.Count];
            result.ForEach(e =>
            {
                sortedResult[e.Index] = e.Id;
            });

            return sortedResult.ToList();
        }

        public async Task LegacyClassifications__Deprecate(List<int> ids, bool isDeprecated)
        {
            var conn = await GetConnectionAsync();
            using var cmd = conn.CreateCommand();
            // Parameters
            DataTable idsTable = RepositoryUtilities.DataTable(ids.Select(id => new { Id = id }));
            var idsTvp = new SqlParameter("@Ids", idsTable)
            {
                TypeName = $"[dbo].[IdList]",
                SqlDbType = SqlDbType.Structured
            };

            cmd.Parameters.Add(idsTvp);
            cmd.Parameters.Add("@IsDeprecated", isDeprecated);

            // Command
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.CommandText = $"[dal].[{nameof(LegacyClassifications__Deprecate)}]";

            // Execute
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task<IEnumerable<ValidationError>> LegacyClassifications_Validate__Delete(List<int> ids, int top)
        {
            var conn = await GetConnectionAsync();
            using var cmd = conn.CreateCommand();
            // Parameters
            DataTable idsTable = RepositoryUtilities.DataTable(ids.Select(id => new { Id = id }), addIndex: true);
            var idsTvp = new SqlParameter("@Ids", idsTable)
            {
                TypeName = $"[dbo].[IndexedIdList]",
                SqlDbType = SqlDbType.Structured
            };

            cmd.Parameters.Add(idsTvp);
            cmd.Parameters.Add("@Top", top);

            // Command
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.CommandText = $"[bll].[{nameof(LegacyClassifications_Validate__Delete)}]";

            // Execute
            return await RepositoryUtilities.LoadErrors(cmd);
        }

        public async Task LegacyClassifications__Delete(IEnumerable<int> ids)
        {
            var conn = await GetConnectionAsync();
            using var cmd = conn.CreateCommand();
            // Parameters
            DataTable idsTable = RepositoryUtilities.DataTable(ids.Select(id => new { Id = id }));
            var idsTvp = new SqlParameter("@Ids", idsTable)
            {
                TypeName = $"[dbo].[IdList]",
                SqlDbType = SqlDbType.Structured
            };

            cmd.Parameters.Add(idsTvp);

            // Command
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.CommandText = $"[dal].[{nameof(LegacyClassifications__Delete)}]";

            // Execute
            try
            {
                await cmd.ExecuteNonQueryAsync();
            }
            catch (SqlException ex) when (RepositoryUtilities.IsForeignKeyViolation(ex))
            {
                throw new ForeignKeyViolationException();
            }
        }

        public async Task<IEnumerable<ValidationError>> LegacyClassifications_Validate__DeleteWithDescendants(List<int> ids, int top)
        {
            var conn = await GetConnectionAsync();
            using var cmd = conn.CreateCommand();
            // Parameters
            DataTable idsTable = RepositoryUtilities.DataTable(ids.Select(id => new { Id = id }), addIndex: true);
            var idsTvp = new SqlParameter("@Ids", idsTable)
            {
                TypeName = $"[dbo].[IndexedIdList]",
                SqlDbType = SqlDbType.Structured
            };

            cmd.Parameters.Add(idsTvp);
            cmd.Parameters.Add("@Top", top);

            // Command
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.CommandText = $"[bll].[{nameof(LegacyClassifications_Validate__DeleteWithDescendants)}]";

            // Execute
            return await RepositoryUtilities.LoadErrors(cmd);
        }

        public async Task LegacyClassifications__DeleteWithDescendants(IEnumerable<int> ids)
        {
            var conn = await GetConnectionAsync();
            using var cmd = conn.CreateCommand();
            // Parameters
            DataTable idsTable = RepositoryUtilities.DataTable(ids.Select(id => new { Id = id }));
            var idsTvp = new SqlParameter("@Ids", idsTable)
            {
                TypeName = $"[dbo].[IdList]",
                SqlDbType = SqlDbType.Structured
            };

            cmd.Parameters.Add(idsTvp);

            // Command
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.CommandText = $"[dal].[{nameof(LegacyClassifications__DeleteWithDescendants)}]";

            // Execute
            try
            {
                await cmd.ExecuteNonQueryAsync();
            }
            catch (SqlException ex) when (RepositoryUtilities.IsForeignKeyViolation(ex))
            {
                throw new ForeignKeyViolationException();
            }
        }

        #endregion

        #region AccountTypes

        public async Task<IEnumerable<ValidationError>> AccountTypes_Validate__Save(List<AccountTypeForSave> entities, int top)
        {
            var conn = await GetConnectionAsync();
            using var cmd = conn.CreateCommand();
            // Parameters
            DataTable entitiesTable = RepositoryUtilities.DataTableWithParentIndex(entities, e => e.ParentIndex);
            var entitiesTvp = new SqlParameter("@Entities", entitiesTable)
            {
                TypeName = $"[dbo].[{nameof(AccountType)}List]",
                SqlDbType = SqlDbType.Structured
            };

            cmd.Parameters.Add(entitiesTvp);
            cmd.Parameters.Add("@Top", top);

            // Command
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.CommandText = $"[bll].[{nameof(AccountTypes_Validate__Save)}]";

            // Execute
            return await RepositoryUtilities.LoadErrors(cmd);
        }

        public async Task<List<int>> AccountTypes__Save(List<AccountTypeForSave> entities, bool returnIds)
        {
            var result = new List<IndexedId>();

            var conn = await GetConnectionAsync();
            using (var cmd = conn.CreateCommand())
            {
                DataTable entitiesTable = RepositoryUtilities.DataTableWithParentIndex(entities, e => e.ParentIndex);
                var entitiesTvp = new SqlParameter("@Entities", entitiesTable)
                {
                    TypeName = $"[dbo].[{nameof(AccountType)}List]",
                    SqlDbType = SqlDbType.Structured
                };

                cmd.Parameters.Add(entitiesTvp);
                cmd.Parameters.Add("@ReturnIds", returnIds);

                cmd.CommandType = CommandType.StoredProcedure;
                cmd.CommandText = $"[dal].[{nameof(AccountTypes__Save)}]";

                if (returnIds)
                {
                    using var reader = await cmd.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        int i = 0;
                        result.Add(new IndexedId
                        {
                            Index = reader.GetInt32(i++),
                            Id = reader.GetInt32(i++)
                        });
                    }
                }
                else
                {
                    await cmd.ExecuteNonQueryAsync();
                }
            }

            // Return ordered result
            var sortedResult = new int[entities.Count];
            result.ForEach(e =>
            {
                sortedResult[e.Index] = e.Id;
            });

            return sortedResult.ToList();
        }

        public async Task AccountTypes__Activate(List<int> ids, bool isActive)
        {
            var conn = await GetConnectionAsync();
            using var cmd = conn.CreateCommand();
            // Parameters
            DataTable idsTable = RepositoryUtilities.DataTable(ids.Select(id => new { Id = id }));
            var idsTvp = new SqlParameter("@Ids", idsTable)
            {
                TypeName = $"[dbo].[IdList]",
                SqlDbType = SqlDbType.Structured
            };

            cmd.Parameters.Add(idsTvp);
            cmd.Parameters.Add("@IsActive", isActive);

            // Command
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.CommandText = $"[dal].[{nameof(AccountTypes__Activate)}]";

            // Execute
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task<IEnumerable<ValidationError>> AccountTypes_Validate__Delete(List<int> ids, int top)
        {
            var conn = await GetConnectionAsync();
            using var cmd = conn.CreateCommand();
            // Parameters
            DataTable idsTable = RepositoryUtilities.DataTable(ids.Select(id => new { Id = id }), addIndex: true);
            var idsTvp = new SqlParameter("@Ids", idsTable)
            {
                TypeName = $"[dbo].[IndexedIdList]",
                SqlDbType = SqlDbType.Structured
            };

            cmd.Parameters.Add(idsTvp);
            cmd.Parameters.Add("@Top", top);

            // Command
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.CommandText = $"[bll].[{nameof(AccountTypes_Validate__Delete)}]";

            // Execute
            return await RepositoryUtilities.LoadErrors(cmd);
        }

        public async Task AccountTypes__Delete(IEnumerable<int> ids)
        {
            var conn = await GetConnectionAsync();
            using var cmd = conn.CreateCommand();
            // Parameters
            DataTable idsTable = RepositoryUtilities.DataTable(ids.Select(id => new { Id = id }));
            var idsTvp = new SqlParameter("@Ids", idsTable)
            {
                TypeName = $"[dbo].[IdList]",
                SqlDbType = SqlDbType.Structured
            };

            cmd.Parameters.Add(idsTvp);

            // Command
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.CommandText = $"[dal].[{nameof(AccountTypes__Delete)}]";

            // Execute
            try
            {
                await cmd.ExecuteNonQueryAsync();
            }
            catch (SqlException ex) when (RepositoryUtilities.IsForeignKeyViolation(ex))
            {
                throw new ForeignKeyViolationException();
            }
        }

        public async Task<IEnumerable<ValidationError>> AccountTypes_Validate__DeleteWithDescendants(List<int> ids, int top)
        {
            var conn = await GetConnectionAsync();
            using var cmd = conn.CreateCommand();
            // Parameters
            DataTable idsTable = RepositoryUtilities.DataTable(ids.Select(id => new { Id = id }), addIndex: true);
            var idsTvp = new SqlParameter("@Ids", idsTable)
            {
                TypeName = $"[dbo].[IndexedIdList]",
                SqlDbType = SqlDbType.Structured
            };

            cmd.Parameters.Add(idsTvp);
            cmd.Parameters.Add("@Top", top);

            // Command
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.CommandText = $"[bll].[{nameof(AccountTypes_Validate__DeleteWithDescendants)}]";

            // Execute
            return await RepositoryUtilities.LoadErrors(cmd);
        }

        public async Task AccountTypes__DeleteWithDescendants(IEnumerable<int> ids)
        {
            var conn = await GetConnectionAsync();
            using var cmd = conn.CreateCommand();
            // Parameters
            DataTable idsTable = RepositoryUtilities.DataTable(ids.Select(id => new { Id = id }));
            var idsTvp = new SqlParameter("@Ids", idsTable)
            {
                TypeName = $"[dbo].[IdList]",
                SqlDbType = SqlDbType.Structured
            };

            cmd.Parameters.Add(idsTvp);

            // Command
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.CommandText = $"[dal].[{nameof(AccountTypes__DeleteWithDescendants)}]";

            // Execute
            try
            {
                await cmd.ExecuteNonQueryAsync();
            }
            catch (SqlException ex) when (RepositoryUtilities.IsForeignKeyViolation(ex))
            {
                throw new ForeignKeyViolationException();
            }
        }

        #endregion

        #region Accounts

        public Query<Account> Accounts__AsQuery(List<AccountForSave> entities)
        {
            // This method returns the provided entities as a Query that can be selected, filtered etc...
            // The Ids in the result are always the indices of the original collection, even when the entity has a string key

            // Parameters
            DataTable entitiesTable = RepositoryUtilities.DataTable(entities, addIndex: true);
            SqlParameter entitiesTvp = new SqlParameter("@Entities", entitiesTable)
            {
                TypeName = $"[dbo].[{nameof(Account)}List]",
                SqlDbType = SqlDbType.Structured
            };


            // Query
            var query = Query<Account>();
            return query.FromSql($"[map].[{nameof(Accounts__AsQuery)}] (@Entities)", null, entitiesTvp);
        }

        public async Task Accounts__Preprocess(List<AccountForSave> entities)
        {
            var conn = await GetConnectionAsync();
            using var cmd = conn.CreateCommand();
            // Parameters
            DataTable entitiesTable = RepositoryUtilities.DataTable(entities, addIndex: true);
            var entitiesTvp = new SqlParameter("@Entities", entitiesTable)
            {
                TypeName = $"[dbo].[{nameof(Account)}List]",
                SqlDbType = SqlDbType.Structured
            };

            cmd.Parameters.Add(entitiesTvp);

            // Command
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.CommandText = $"[bll].[{nameof(Accounts__Preprocess)}]";

            // Execute
            using var reader = await cmd.ExecuteReaderAsync();
            var props = typeof(AccountForSave).GetMappedProperties();
            while (await reader.ReadAsync())
            {
                var index = reader.GetInt32(0);
                var entity = entities[index];
                foreach (var prop in props)
                {
                    // get property value
                    var propValue = reader[prop.Name];
                    propValue = propValue == DBNull.Value ? null : propValue;

                    prop.SetValue(entity, propValue);
                }
            }
        }

        public async Task<IEnumerable<ValidationError>> Accounts_Validate__Save(List<AccountForSave> entities, int top)
        {
            var conn = await GetConnectionAsync();
            using var cmd = conn.CreateCommand();
            // Parameters
            DataTable entitiesTable = RepositoryUtilities.DataTable(entities, addIndex: true);
            var entitiesTvp = new SqlParameter("@Entities", entitiesTable)
            {
                TypeName = $"[dbo].[{nameof(Account)}List]",
                SqlDbType = SqlDbType.Structured
            };

            cmd.Parameters.Add(entitiesTvp);
            cmd.Parameters.Add("@Top", top);

            // Command
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.CommandText = $"[bll].[{nameof(Accounts_Validate__Save)}]";

            // Execute
            return await RepositoryUtilities.LoadErrors(cmd);
        }

        public async Task<List<int>> Accounts__Save(List<AccountForSave> entities, bool returnIds)
        {
            var result = new List<IndexedId>();

            var conn = await GetConnectionAsync();
            using (var cmd = conn.CreateCommand())
            {
                DataTable entitiesTable = RepositoryUtilities.DataTable(entities, addIndex: true);
                var entitiesTvp = new SqlParameter("@Entities", entitiesTable)
                {
                    TypeName = $"[dbo].[{nameof(Account)}List]",
                    SqlDbType = SqlDbType.Structured
                };

                cmd.Parameters.Add(entitiesTvp);
                cmd.Parameters.Add("@ReturnIds", returnIds);

                cmd.CommandType = CommandType.StoredProcedure;
                cmd.CommandText = $"[dal].[{nameof(Accounts__Save)}]";

                if (returnIds)
                {
                    using var reader = await cmd.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        int i = 0;
                        result.Add(new IndexedId
                        {
                            Index = reader.GetInt32(i++),
                            Id = reader.GetInt32(i++)
                        });
                    }
                }
                else
                {
                    await cmd.ExecuteNonQueryAsync();
                }
            }

            // Return ordered result
            var sortedResult = new int[entities.Count];
            result.ForEach(e =>
            {
                sortedResult[e.Index] = e.Id;
            });

            return sortedResult.ToList();
        }

        public async Task Accounts__Deprecate(List<int> ids, bool isDeprecated)
        {
            var conn = await GetConnectionAsync();
            using var cmd = conn.CreateCommand();
            // Parameters
            var isDeprecatedParam = new SqlParameter("@IsDeprecated", isDeprecated);

            DataTable idsTable = RepositoryUtilities.DataTable(ids.Select(id => new { Id = id }));
            var idsTvp = new SqlParameter("@Ids", idsTable)
            {
                TypeName = $"[dbo].[IdList]",
                SqlDbType = SqlDbType.Structured
            };

            cmd.Parameters.Add(idsTvp);
            cmd.Parameters.Add("@IsDeprecated", isDeprecated);

            // Command
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.CommandText = $"[dal].[{nameof(Accounts__Deprecate)}]";

            // Execute
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task<IEnumerable<ValidationError>> Accounts_Validate__Delete(List<int> ids, int top)
        {
            var conn = await GetConnectionAsync();
            using var cmd = conn.CreateCommand();
            // Parameters
            DataTable idsTable = RepositoryUtilities.DataTable(ids.Select(id => new { Id = id }), addIndex: true);
            var idsTvp = new SqlParameter("@Ids", idsTable)
            {
                TypeName = $"[dbo].[IndexedIdList]",
                SqlDbType = SqlDbType.Structured
            };

            cmd.Parameters.Add(idsTvp);
            cmd.Parameters.Add("@Top", top);

            // Command
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.CommandText = $"[bll].[{nameof(Accounts_Validate__Delete)}]";

            // Execute
            return await RepositoryUtilities.LoadErrors(cmd);
        }

        public async Task Accounts__Delete(IEnumerable<int> ids)
        {
            var conn = await GetConnectionAsync();
            using var cmd = conn.CreateCommand();
            // Parameters
            DataTable idsTable = RepositoryUtilities.DataTable(ids.Select(id => new { Id = id }));
            var idsTvp = new SqlParameter("@Ids", idsTable)
            {
                TypeName = $"[dbo].[IdList]",
                SqlDbType = SqlDbType.Structured
            };

            cmd.Parameters.Add(idsTvp);

            // Command
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.CommandText = $"[dal].[{nameof(Accounts__Delete)}]";

            // Execute
            try
            {
                await cmd.ExecuteNonQueryAsync();
            }
            catch (SqlException ex) when (RepositoryUtilities.IsForeignKeyViolation(ex))
            {
                throw new ForeignKeyViolationException();
            }
        }

        #endregion

        #region LookupDefinitions

        public Query<Currency> LookupDefinitions__AsQuery(List<CurrencyForSave> entities)
        {
            // This method returns the provided entities as a Query that can be selected, filtered etc...
            // The Ids in the result are always the indices of the original collection, even when the entity has a string key

            // Parameters
            DataTable entitiesTable = RepositoryUtilities.DataTable(entities, addIndex: true);
            SqlParameter entitiesTvp = new SqlParameter("@Entities", entitiesTable)
            {
                TypeName = $"[dbo].[{nameof(Currency)}List]",
                SqlDbType = SqlDbType.Structured
            };

            // Query
            var query = Query<Currency>();
            return query.FromSql($"[map].[{nameof(LookupDefinitions__AsQuery)}] (@Entities)", null, entitiesTvp);
        }

        public async Task<IEnumerable<ValidationError>> LookupDefinitions_Validate__Save(List<CurrencyForSave> entities, int top)
        {
            var conn = await GetConnectionAsync();
            using var cmd = conn.CreateCommand();
            // Parameters
            DataTable entitiesTable = RepositoryUtilities.DataTable(entities, addIndex: true);
            var entitiesTvp = new SqlParameter("@Entities", entitiesTable)
            {
                TypeName = $"[dbo].[{nameof(Currency)}List]",
                SqlDbType = SqlDbType.Structured
            };

            cmd.Parameters.Add(entitiesTvp);
            cmd.Parameters.Add("@Top", top);

            // Command
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.CommandText = $"[bll].[{nameof(LookupDefinitions_Validate__Save)}]";

            // Execute
            return await RepositoryUtilities.LoadErrors(cmd);
        }

        public async Task LookupDefinitions__Save(List<CurrencyForSave> entities)
        {
            var result = new List<IndexedId>();

            var conn = await GetConnectionAsync();
            using var cmd = conn.CreateCommand();
            DataTable entitiesTable = RepositoryUtilities.DataTable(entities, addIndex: true);
            var entitiesTvp = new SqlParameter("@Entities", entitiesTable)
            {
                TypeName = $"[dbo].[{nameof(Currency)}List]",
                SqlDbType = SqlDbType.Structured
            };

            cmd.Parameters.Add(entitiesTvp);

            cmd.CommandType = CommandType.StoredProcedure;
            cmd.CommandText = $"[dal].[{nameof(LookupDefinitions__Save)}]";

            await cmd.ExecuteNonQueryAsync();
        }

        public async Task LookupDefinitions__UpdateState(List<string> ids, string state)
        {
            var conn = await GetConnectionAsync();
            using var cmd = conn.CreateCommand();
            // Parameters
            DataTable idsTable = RepositoryUtilities.DataTable(ids.Select(id => new { Id = id }));
            var idsTvp = new SqlParameter("@Ids", idsTable)
            {
                TypeName = $"[dbo].[StringList]",
                SqlDbType = SqlDbType.Structured
            };

            cmd.Parameters.Add(idsTvp);
            cmd.Parameters.Add("@State", state);

            // Command
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.CommandText = $"[dal].[{nameof(LookupDefinitions__UpdateState)}]";

            // Execute
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task<IEnumerable<ValidationError>> LookupDefinitions_Validate__Delete(List<string> ids, int top)
        {
            var conn = await GetConnectionAsync();
            using var cmd = conn.CreateCommand();
            // Parameters
            DataTable idsTable = RepositoryUtilities.DataTable(ids.Select(id => new { Id = id }), addIndex: true);
            var idsTvp = new SqlParameter("@Ids", idsTable)
            {
                TypeName = $"[dbo].[IndexedStringList]",
                SqlDbType = SqlDbType.Structured
            };

            cmd.Parameters.Add(idsTvp);
            cmd.Parameters.Add("@Top", top);

            // Command
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.CommandText = $"[bll].[{nameof(LookupDefinitions_Validate__Delete)}]";

            // Execute
            return await RepositoryUtilities.LoadErrors(cmd);
        }

        public async Task LookupDefinitions__Delete(IEnumerable<string> ids)
        {
            var conn = await GetConnectionAsync();
            using var cmd = conn.CreateCommand();
            // Parameters
            DataTable idsTable = RepositoryUtilities.DataTable(ids.Select(id => new { Id = id }));
            var idsTvp = new SqlParameter("@Ids", idsTable)
            {
                TypeName = $"[dbo].[StringList]",
                SqlDbType = SqlDbType.Structured
            };

            cmd.Parameters.Add(idsTvp);

            // Command
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.CommandText = $"[dal].[{nameof(LookupDefinitions__Delete)}]";

            // Execute
            try
            {
                await cmd.ExecuteNonQueryAsync();
            }
            catch (SqlException ex) when (RepositoryUtilities.IsForeignKeyViolation(ex))
            {
                throw new ForeignKeyViolationException();
            }
        }

        #endregion

        #region ResponsibilityCenters

        public Query<ResponsibilityCenter> ResponsibilityCenters__AsQuery(List<ResponsibilityCenterForSave> entities)
        {
            // This method returns the provided entities as a Query that can be selected, filtered etc...
            // The Ids in the result are always the indices of the original collection, even when the entity has a string key

            // Parameters
            DataTable entitiesTable = RepositoryUtilities.DataTable(entities, addIndex: true);
            SqlParameter entitiesTvp = new SqlParameter("@Entities", entitiesTable)
            {
                TypeName = $"[dbo].[{nameof(ResponsibilityCenter)}List]",
                SqlDbType = SqlDbType.Structured
            };

            // Query
            var query = Query<ResponsibilityCenter>();
            return query.FromSql($"[map].[{nameof(ResponsibilityCenters__AsQuery)}] (@Entities)", null, entitiesTvp);
        }

        public async Task<IEnumerable<ValidationError>> ResponsibilityCenters_Validate__Save(List<ResponsibilityCenterForSave> entities, int top)
        {
            var conn = await GetConnectionAsync();
            using var cmd = conn.CreateCommand();
            // Parameters
            DataTable entitiesTable = RepositoryUtilities.DataTableWithParentIndex(entities, e => e.ParentIndex);
            var entitiesTvp = new SqlParameter("@Entities", entitiesTable)
            {
                TypeName = $"[dbo].[{nameof(ResponsibilityCenter)}List]",
                SqlDbType = SqlDbType.Structured
            };

            cmd.Parameters.Add(entitiesTvp);
            cmd.Parameters.Add("@Top", top);

            // Command
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.CommandText = $"[bll].[{nameof(ResponsibilityCenters_Validate__Save)}]";

            // Execute
            return await RepositoryUtilities.LoadErrors(cmd);
        }

        public async Task<List<int>> ResponsibilityCenters__Save(List<ResponsibilityCenterForSave> entities, bool returnIds)
        {
            var result = new List<IndexedId>();

            var conn = await GetConnectionAsync();
            using (var cmd = conn.CreateCommand())
            {
                DataTable entitiesTable = RepositoryUtilities.DataTableWithParentIndex(entities, e => e.ParentIndex);
                var entitiesTvp = new SqlParameter("@Entities", entitiesTable)
                {
                    TypeName = $"[dbo].[{nameof(ResponsibilityCenter)}List]",
                    SqlDbType = SqlDbType.Structured
                };

                cmd.Parameters.Add(entitiesTvp);
                cmd.Parameters.Add("@ReturnIds", returnIds);

                cmd.CommandType = CommandType.StoredProcedure;
                cmd.CommandText = $"[dal].[{nameof(ResponsibilityCenters__Save)}]";

                if (returnIds)
                {
                    using var reader = await cmd.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        int i = 0;
                        result.Add(new IndexedId
                        {
                            Index = reader.GetInt32(i++),
                            Id = reader.GetInt32(i++)
                        });
                    }
                }
                else
                {
                    await cmd.ExecuteNonQueryAsync();
                }
            }

            // Return ordered result
            var sortedResult = new int[entities.Count];
            result.ForEach(e =>
            {
                sortedResult[e.Index] = e.Id;
            });

            return sortedResult.ToList();
        }

        public async Task ResponsibilityCenters__Activate(List<int> ids, bool isActive)
        {
            var conn = await GetConnectionAsync();
            using var cmd = conn.CreateCommand();
            // Parameters
            DataTable idsTable = RepositoryUtilities.DataTable(ids.Select(id => new { Id = id }));
            var idsTvp = new SqlParameter("@Ids", idsTable)
            {
                TypeName = $"[dbo].[IdList]",
                SqlDbType = SqlDbType.Structured
            };

            cmd.Parameters.Add(idsTvp);
            cmd.Parameters.Add("@IsActive", isActive);

            // Command
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.CommandText = $"[dal].[{nameof(ResponsibilityCenters__Activate)}]";

            // Execute
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task<IEnumerable<ValidationError>> ResponsibilityCenters_Validate__Delete(List<int> ids, int top)
        {
            var conn = await GetConnectionAsync();
            using var cmd = conn.CreateCommand();
            // Parameters
            DataTable idsTable = RepositoryUtilities.DataTable(ids.Select(id => new { Id = id }), addIndex: true);
            var idsTvp = new SqlParameter("@Ids", idsTable)
            {
                TypeName = $"[dbo].[IndexedIdList]",
                SqlDbType = SqlDbType.Structured
            };

            cmd.Parameters.Add(idsTvp);
            cmd.Parameters.Add("@Top", top);

            // Command
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.CommandText = $"[bll].[{nameof(ResponsibilityCenters_Validate__Delete)}]";

            // Execute
            return await RepositoryUtilities.LoadErrors(cmd);
        }

        public async Task ResponsibilityCenters__Delete(IEnumerable<int> ids)
        {
            var conn = await GetConnectionAsync();
            using var cmd = conn.CreateCommand();
            // Parameters
            DataTable idsTable = RepositoryUtilities.DataTable(ids.Select(id => new { Id = id }));
            var idsTvp = new SqlParameter("@Ids", idsTable)
            {
                TypeName = $"[dbo].[IdList]",
                SqlDbType = SqlDbType.Structured
            };

            cmd.Parameters.Add(idsTvp);

            // Command
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.CommandText = $"[dal].[{nameof(ResponsibilityCenters__Delete)}]";

            // Execute
            try
            {
                await cmd.ExecuteNonQueryAsync();
            }
            catch (SqlException ex) when (RepositoryUtilities.IsForeignKeyViolation(ex))
            {
                throw new ForeignKeyViolationException();
            }
        }

        public async Task<IEnumerable<ValidationError>> ResponsibilityCenters_Validate__DeleteWithDescendants(List<int> ids, int top)
        {
            var conn = await GetConnectionAsync();
            using var cmd = conn.CreateCommand();
            // Parameters
            DataTable idsTable = RepositoryUtilities.DataTable(ids.Select(id => new { Id = id }), addIndex: true);
            var idsTvp = new SqlParameter("@Ids", idsTable)
            {
                TypeName = $"[dbo].[IndexedIdList]",
                SqlDbType = SqlDbType.Structured
            };

            cmd.Parameters.Add(idsTvp);
            cmd.Parameters.Add("@Top", top);

            // Command
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.CommandText = $"[bll].[{nameof(ResponsibilityCenters_Validate__DeleteWithDescendants)}]";

            // Execute
            return await RepositoryUtilities.LoadErrors(cmd);
        }

        public async Task ResponsibilityCenters__DeleteWithDescendants(IEnumerable<int> ids)
        {
            var conn = await GetConnectionAsync();
            using var cmd = conn.CreateCommand();
            // Parameters
            DataTable idsTable = RepositoryUtilities.DataTable(ids.Select(id => new { Id = id }));
            var idsTvp = new SqlParameter("@Ids", idsTable)
            {
                TypeName = $"[dbo].[IdList]",
                SqlDbType = SqlDbType.Structured
            };

            cmd.Parameters.Add(idsTvp);

            // Command
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.CommandText = $"[dal].[{nameof(ResponsibilityCenters__DeleteWithDescendants)}]";

            // Execute
            try
            {
                await cmd.ExecuteNonQueryAsync();
            }
            catch (SqlException ex) when (RepositoryUtilities.IsForeignKeyViolation(ex))
            {
                throw new ForeignKeyViolationException();
            }
        }

        #endregion

        #region EntryTypes

        public Query<EntryType> EntryTypes__AsQuery(List<EntryTypeForSave> entities)
        {
            // This method returns the provided entities as a Query that can be selected, filtered etc...
            // The Ids in the result are always the indices of the original collection, even when the entity has a string key

            // Parameters
            DataTable entitiesTable = RepositoryUtilities.DataTable(entities, addIndex: true);
            SqlParameter entitiesTvp = new SqlParameter("@Entities", entitiesTable)
            {
                TypeName = $"[dbo].[{nameof(EntryType)}List]",
                SqlDbType = SqlDbType.Structured
            };

            // Query
            var query = Query<EntryType>();
            return query.FromSql($"[map].[{nameof(EntryTypes__AsQuery)}] (@Entities)", null, entitiesTvp);
        }

        public async Task<IEnumerable<ValidationError>> EntryTypes_Validate__Save(List<EntryTypeForSave> entities, int top)
        {
            var conn = await GetConnectionAsync();
            using var cmd = conn.CreateCommand();
            // Parameters
            DataTable entitiesTable = RepositoryUtilities.DataTableWithParentIndex(entities, e => e.ParentIndex);
            var entitiesTvp = new SqlParameter("@Entities", entitiesTable)
            {
                TypeName = $"[dbo].[{nameof(EntryType)}List]",
                SqlDbType = SqlDbType.Structured
            };

            cmd.Parameters.Add(entitiesTvp);
            cmd.Parameters.Add("@Top", top);

            // Command
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.CommandText = $"[bll].[{nameof(EntryTypes_Validate__Save)}]";

            // Execute
            return await RepositoryUtilities.LoadErrors(cmd);
        }

        public async Task<List<int>> EntryTypes__Save(List<EntryTypeForSave> entities, bool returnIds)
        {
            var result = new List<IndexedId>();

            var conn = await GetConnectionAsync();
            using (var cmd = conn.CreateCommand())
            {
                DataTable entitiesTable = RepositoryUtilities.DataTableWithParentIndex(entities, e => e.ParentIndex);
                var entitiesTvp = new SqlParameter("@Entities", entitiesTable)
                {
                    TypeName = $"[dbo].[{nameof(EntryType)}List]",
                    SqlDbType = SqlDbType.Structured
                };

                cmd.Parameters.Add(entitiesTvp);
                cmd.Parameters.Add("@ReturnIds", returnIds);

                cmd.CommandType = CommandType.StoredProcedure;
                cmd.CommandText = $"[dal].[{nameof(EntryTypes__Save)}]";

                if (returnIds)
                {
                    using var reader = await cmd.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        int i = 0;
                        result.Add(new IndexedId
                        {
                            Index = reader.GetInt32(i++),
                            Id = reader.GetInt32(i++)
                        });
                    }
                }
                else
                {
                    await cmd.ExecuteNonQueryAsync();
                }
            }

            // Return ordered result
            var sortedResult = new int[entities.Count];
            result.ForEach(e =>
            {
                sortedResult[e.Index] = e.Id;
            });

            return sortedResult.ToList();
        }

        public async Task EntryTypes__Activate(List<int> ids, bool isActive)
        {
            var conn = await GetConnectionAsync();
            using var cmd = conn.CreateCommand();
            // Parameters
            DataTable idsTable = RepositoryUtilities.DataTable(ids.Select(id => new { Id = id }));
            var idsTvp = new SqlParameter("@Ids", idsTable)
            {
                TypeName = $"[dbo].[IdList]",
                SqlDbType = SqlDbType.Structured
            };

            cmd.Parameters.Add(idsTvp);
            cmd.Parameters.Add("@IsActive", isActive);

            // Command
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.CommandText = $"[dal].[{nameof(EntryTypes__Activate)}]";

            // Execute
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task<IEnumerable<ValidationError>> EntryTypes_Validate__Delete(List<int> ids, int top)
        {
            var conn = await GetConnectionAsync();
            using var cmd = conn.CreateCommand();
            // Parameters
            DataTable idsTable = RepositoryUtilities.DataTable(ids.Select(id => new { Id = id }), addIndex: true);
            var idsTvp = new SqlParameter("@Ids", idsTable)
            {
                TypeName = $"[dbo].[IndexedIdList]",
                SqlDbType = SqlDbType.Structured
            };

            cmd.Parameters.Add(idsTvp);
            cmd.Parameters.Add("@Top", top);

            // Command
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.CommandText = $"[bll].[{nameof(EntryTypes_Validate__Delete)}]";

            // Execute
            return await RepositoryUtilities.LoadErrors(cmd);
        }

        public async Task EntryTypes__Delete(IEnumerable<int> ids)
        {
            var conn = await GetConnectionAsync();
            using var cmd = conn.CreateCommand();
            // Parameters
            DataTable idsTable = RepositoryUtilities.DataTable(ids.Select(id => new { Id = id }));
            var idsTvp = new SqlParameter("@Ids", idsTable)
            {
                TypeName = $"[dbo].[IdList]",
                SqlDbType = SqlDbType.Structured
            };

            cmd.Parameters.Add(idsTvp);

            // Command
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.CommandText = $"[dal].[{nameof(EntryTypes__Delete)}]";

            // Execute
            try
            {
                await cmd.ExecuteNonQueryAsync();
            }
            catch (SqlException ex) when (RepositoryUtilities.IsForeignKeyViolation(ex))
            {
                throw new ForeignKeyViolationException();
            }
        }

        public async Task<IEnumerable<ValidationError>> EntryTypes_Validate__DeleteWithDescendants(List<int> ids, int top)
        {
            var conn = await GetConnectionAsync();
            using var cmd = conn.CreateCommand();
            // Parameters
            DataTable idsTable = RepositoryUtilities.DataTable(ids.Select(id => new { Id = id }), addIndex: true);
            var idsTvp = new SqlParameter("@Ids", idsTable)
            {
                TypeName = $"[dbo].[IndexedIdList]",
                SqlDbType = SqlDbType.Structured
            };

            cmd.Parameters.Add(idsTvp);
            cmd.Parameters.Add("@Top", top);

            // Command
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.CommandText = $"[bll].[{nameof(EntryTypes_Validate__DeleteWithDescendants)}]";

            // Execute
            return await RepositoryUtilities.LoadErrors(cmd);
        }

        public async Task EntryTypes__DeleteWithDescendants(IEnumerable<int> ids)
        {
            var conn = await GetConnectionAsync();
            using var cmd = conn.CreateCommand();
            // Parameters
            DataTable idsTable = RepositoryUtilities.DataTable(ids.Select(id => new { Id = id }));
            var idsTvp = new SqlParameter("@Ids", idsTable)
            {
                TypeName = $"[dbo].[IdList]",
                SqlDbType = SqlDbType.Structured
            };

            cmd.Parameters.Add(idsTvp);

            // Command
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.CommandText = $"[dal].[{nameof(EntryTypes__DeleteWithDescendants)}]";

            // Execute
            try
            {
                await cmd.ExecuteNonQueryAsync();
            }
            catch (SqlException ex) when (RepositoryUtilities.IsForeignKeyViolation(ex))
            {
                throw new ForeignKeyViolationException();
            }
        }

        #endregion

        #region Documents

        public Query<Document> Documents__AsQuery(string definitionId, List<DocumentForSave> entities)
        {
            // Parameters
            SqlParameter definitionParameter = new SqlParameter("@DefinitionId", definitionId);

            DataTable entitiesTable = RepositoryUtilities.DataTable(entities, addIndex: true);
            SqlParameter entitiesTvp = new SqlParameter("@Entities", entitiesTable)
            {
                TypeName = $"[dbo].[DocumentList]",
                SqlDbType = SqlDbType.Structured
            };

            // Query
            var query = Query<Document>();
            return query.FromSql($"[map].[{nameof(Documents__AsQuery)}] (@Entities)", null, definitionParameter, entitiesTvp);
        }

        public async Task Documents__Preprocess(string definitionId, List<DocumentForSave> documents)
        {
            var conn = await GetConnectionAsync();
            using var cmd = conn.CreateCommand();
            // Parameters
            var (docsTable, linesTable, entriesTable) = RepositoryUtilities.DataTableFromDocuments(documents);

            var docsTvp = new SqlParameter("@Documents", docsTable)
            {
                TypeName = $"[dbo].[{nameof(Document)}List]",
                SqlDbType = SqlDbType.Structured
            };

            var linesTvp = new SqlParameter("@Lines", linesTable)
            {
                TypeName = $"[dbo].[{nameof(Line)}List]",
                SqlDbType = SqlDbType.Structured
            };

            var entriesTvp = new SqlParameter("@Entries", entriesTable)
            {
                TypeName = $"[dbo].[{nameof(Entry)}List]",
                SqlDbType = SqlDbType.Structured
            };

            cmd.Parameters.Add("@DefinitionId", definitionId);
            cmd.Parameters.Add(docsTvp);
            cmd.Parameters.Add(linesTvp);
            cmd.Parameters.Add(entriesTvp);

            // Command
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.CommandText = $"[bll].[{nameof(Documents__Preprocess)}]";

            // Execute
            using var reader = await cmd.ExecuteReaderAsync();
            var props = typeof(EntryForSave).GetMappedProperties();
            while (await reader.ReadAsync())
            {
                var index = reader.GetInt32(2);
                var lineIndex = reader.GetInt32(1);
                var documentIndex = reader.GetInt32(0);

                var entry = documents[documentIndex].Lines[lineIndex].Entries[index];

                foreach (var prop in props)
                {
                    // get property value
                    var propValue = reader[prop.Name];
                    propValue = propValue == DBNull.Value ? null : propValue;

                    prop.SetValue(entry, propValue);
                }
            }
        }

        public async Task<IEnumerable<ValidationError>> Documents_Validate__Save(string definitionId, List<DocumentForSave> documents, int top)
        {
            var conn = await GetConnectionAsync();
            using var cmd = conn.CreateCommand();
            // Parameters
            var (docsTable, linesTable, entriesTable) = RepositoryUtilities.DataTableFromDocuments(documents);

            var docsTvp = new SqlParameter("@Documents", docsTable)
            {
                TypeName = $"[dbo].[{nameof(Document)}List]",
                SqlDbType = SqlDbType.Structured
            };

            var linesTvp = new SqlParameter("@Lines", linesTable)
            {
                TypeName = $"[dbo].[{nameof(Line)}List]",
                SqlDbType = SqlDbType.Structured
            };

            var entriesTvp = new SqlParameter("@Entries", entriesTable)
            {
                TypeName = $"[dbo].[{nameof(Entry)}List]",
                SqlDbType = SqlDbType.Structured
            };

            cmd.Parameters.Add("@DefinitionId", definitionId);
            cmd.Parameters.Add(docsTvp);
            cmd.Parameters.Add(linesTvp);
            cmd.Parameters.Add(entriesTvp);
            cmd.Parameters.Add("@Top", top);

            // Command
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.CommandText = $"[bll].[{nameof(Documents_Validate__Save)}]";

            // Execute
            return await RepositoryUtilities.LoadErrors(cmd);
        }

        public async Task<(List<int> Ids, List<string> DeletedFileIds)> Documents__Save(string definitionId, List<DocumentForSave> documents, List<AttachmentWithExtras> attachments, bool returnIds)
        {
            var deletedFileIds = new List<string>();
            var result = new List<IndexedId>();

            var conn = await GetConnectionAsync();
            using (var cmd = conn.CreateCommand())
            {
                // Parameters
                var (docsTable, linesTable, entriesTable) = RepositoryUtilities.DataTableFromDocuments(documents);

                var docsTvp = new SqlParameter("@Documents", docsTable)
                {
                    TypeName = $"[dbo].[{nameof(Document)}List]",
                    SqlDbType = SqlDbType.Structured
                };

                var linesTvp = new SqlParameter("@Lines", linesTable)
                {
                    TypeName = $"[dbo].[{nameof(Line)}List]",
                    SqlDbType = SqlDbType.Structured
                };

                var entriesTvp = new SqlParameter("@Entries", entriesTable)
                {
                    TypeName = $"[dbo].[{nameof(Entry)}List]",
                    SqlDbType = SqlDbType.Structured
                };

                var attachmentsTable = RepositoryUtilities.DataTable(attachments);
                var attachmentsTvp = new SqlParameter("@Attachments", attachmentsTable)
                {
                    TypeName = $"[dbo].[{nameof(Attachment)}List]",
                    SqlDbType = SqlDbType.Structured
                };

                cmd.Parameters.Add("@DefinitionId", definitionId);
                cmd.Parameters.Add(docsTvp);
                cmd.Parameters.Add(linesTvp);
                cmd.Parameters.Add(entriesTvp);
                cmd.Parameters.Add(attachmentsTvp);
                cmd.Parameters.Add("@ReturnIds", returnIds);

                // Command
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.CommandText = $"[dal].[{nameof(Documents__Save)}]";

                // Execute
                using var reader = await cmd.ExecuteReaderAsync();

                // Get the deleted file IDs
                while (await reader.ReadAsync())
                {
                    deletedFileIds.Add(reader.GetString(0));
                }

                // If requested, get the document Ids too
                if (returnIds)
                {
                    await reader.NextResultAsync();
                    while (await reader.ReadAsync())
                    {
                        int i = 0;
                        result.Add(new IndexedId
                        {
                            Index = reader.GetInt32(i++),
                            Id = reader.GetInt32(i++)
                        });
                    }
                }
            }

            // Return ordered result
            var sortedResult = new int[documents.Count];
            result.ForEach(e =>
            {
                sortedResult[e.Index] = e.Id;
            });

            return (sortedResult.ToList(), deletedFileIds);
        }

        public async Task<IEnumerable<ValidationError>> Lines_Validate__Sign(List<int> ids, int? agentId, int? roleId, short toState, int top)
        {
            var conn = await GetConnectionAsync();
            using var cmd = conn.CreateCommand();
            // Parameters
            DataTable idsTable = RepositoryUtilities.DataTable(ids.Select(id => new { Id = id }), addIndex: true);
            var idsTvp = new SqlParameter("@Ids", idsTable)
            {
                TypeName = $"[dbo].[IndexedIdList]",
                SqlDbType = SqlDbType.Structured
            };

            cmd.Parameters.Add(idsTvp);
            cmd.Parameters.Add("@AgentId", agentId);
            cmd.Parameters.Add("@RoleId", roleId);
            cmd.Parameters.Add("@ToState", toState);
            cmd.Parameters.Add("@Top", top);

            // Command
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.CommandText = $"[bll].[{nameof(Lines_Validate__Sign)}]";

            // Execute
            return await RepositoryUtilities.LoadErrors(cmd);
        }

        public async Task<IEnumerable<int>> Lines__Sign(IEnumerable<int> ids, short toState, int? reasonId, string reasonDetails, int? onBehalfOfUserId, int? roleId, DateTimeOffset? signedAt)
        {
            var result = new List<int>();

            var conn = await GetConnectionAsync();
            using (var cmd = conn.CreateCommand())
            {
                // Parameters
                DataTable idsTable = RepositoryUtilities.DataTable(ids.Select(id => new { Id = id }));
                var idsTvp = new SqlParameter("@Ids", idsTable)
                {
                    TypeName = $"[dbo].[IdList]",
                    SqlDbType = SqlDbType.Structured
                };

                cmd.Parameters.Add(idsTvp);
                cmd.Parameters.Add("@ToState", toState);
                cmd.Parameters.Add("@ReasonId", reasonId);
                cmd.Parameters.Add("@ReasonDetails", reasonDetails);
                cmd.Parameters.Add("@OnBehalfOfUserId", onBehalfOfUserId);
                cmd.Parameters.Add("@RoleId", roleId);
                cmd.Parameters.Add("@SignedAt", signedAt);

                // Command
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.CommandText = $"[dal].[{nameof(Lines__Sign)}]";

                // Execute                    
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    result.Add(reader.GetInt32(0));
                }
            }

            return result;
        }

        public async Task<IEnumerable<ValidationError>> Documents_Validate__Assign(IEnumerable<int> ids, int assigneeId, string comment)
        {
            var conn = await GetConnectionAsync();
            using var cmd = conn.CreateCommand();
            // Parameters
            DataTable idsTable = RepositoryUtilities.DataTable(ids.Select(id => new { Id = id }));
            var idsTvp = new SqlParameter("@Ids", idsTable)
            {
                TypeName = $"[dbo].[IdList]",
                SqlDbType = SqlDbType.Structured
            };

            cmd.Parameters.Add(idsTvp);

            // Command
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.CommandText = $"[dal].[{nameof(Documents_Validate__Assign)}]";

            // Execute
            return await RepositoryUtilities.LoadErrors(cmd);
        }

        public async Task Documents__Assign(IEnumerable<int> ids, int assigneeId, string comment)
        {
            var conn = await GetConnectionAsync();
            using var cmd = conn.CreateCommand();
            // Parameters
            DataTable idsTable = RepositoryUtilities.DataTable(ids.Select(id => new { Id = id }));
            var idsTvp = new SqlParameter("@Ids", idsTable)
            {
                TypeName = $"[dbo].[IdList]",
                SqlDbType = SqlDbType.Structured
            };

            cmd.Parameters.Add(idsTvp);
            cmd.Parameters.Add("@AssigneeId", assigneeId);
            cmd.Parameters.Add("@Comment", comment);

            // Command
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.CommandText = $"[dal].[{nameof(Documents__Assign)}]";

            // Execute                    
            await cmd.ExecuteNonQueryAsync();
        }

        // TODO: deal with the SPs below

        public async Task<List<string>> Documents__Delete(IEnumerable<int> ids)
        {
            // TODO
            var conn = await GetConnectionAsync();
            using var cmd = conn.CreateCommand();
            // Parameters
            DataTable idsTable = RepositoryUtilities.DataTable(ids.Select(id => new { Id = id }));
            var idsTvp = new SqlParameter("@Ids", idsTable)
            {
                TypeName = $"[dbo].[IdList]",
                SqlDbType = SqlDbType.Structured
            };

            cmd.Parameters.Add(idsTvp);

            // Command
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.CommandText = $"[dal].[{nameof(Documents__Delete)}]";

            // Execute
            try
            {
                await cmd.ExecuteNonQueryAsync();
            }
            catch (SqlException ex) when (RepositoryUtilities.IsForeignKeyViolation(ex))
            {
                throw new ForeignKeyViolationException();
            }

            return null; // TODO
        }

        public async Task<IEnumerable<ValidationError>> Documents_Validate__Delete(string definitionId, List<int> ids, int top)
        {
            // TODO
            var conn = await GetConnectionAsync();
            using var cmd = conn.CreateCommand();
            // Parameters
            DataTable idsTable = RepositoryUtilities.DataTable(ids.Select(id => new { Id = id }), addIndex: true);
            var idsTvp = new SqlParameter("@Ids", idsTable)
            {
                TypeName = $"[dbo].[IndexedIdList]",
                SqlDbType = SqlDbType.Structured
            };

            cmd.Parameters.Add("@DefinitionId", definitionId);
            cmd.Parameters.Add(idsTvp);
            cmd.Parameters.Add("@Top", top);

            // Command
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.CommandText = $"[bll].[{nameof(Documents_Validate__Delete)}]";

            // Execute
            return await RepositoryUtilities.LoadErrors(cmd);
        }

        public async Task Documents_Close(List<int> ids, bool isActive)
        {
            // TODO
            var conn = await GetConnectionAsync();
            using var cmd = conn.CreateCommand();
            // Parameters
            DataTable idsTable = RepositoryUtilities.DataTable(ids.Select(id => new { Id = id }));
            var idsTvp = new SqlParameter("@Ids", idsTable)
            {
                TypeName = $"[dbo].[IdList]",
                SqlDbType = SqlDbType.Structured
            };

            cmd.Parameters.Add(idsTvp);
            cmd.Parameters.Add("@IsActive", isActive);

            // Command
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.CommandText = $"[dal].[{nameof(Documents_Close)}]";

            // Execute
            await cmd.ExecuteNonQueryAsync();
        }


        public async Task Documents_Open(List<int> ids, bool isActive)
        {
            // TODO
            var conn = await GetConnectionAsync();
            using var cmd = conn.CreateCommand();
            // Parameters
            DataTable idsTable = RepositoryUtilities.DataTable(ids.Select(id => new { Id = id }));
            var idsTvp = new SqlParameter("@Ids", idsTable)
            {
                TypeName = $"[dbo].[IdList]",
                SqlDbType = SqlDbType.Structured
            };

            cmd.Parameters.Add(idsTvp);
            cmd.Parameters.Add("@IsActive", isActive);

            // Command
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.CommandText = $"[dal].[{nameof(Documents_Open)}]";

            // Execute
            await cmd.ExecuteNonQueryAsync();
        }

        #endregion

        #region ReportDefinitions

        public async Task<IEnumerable<ValidationError>> ReportDefinitions_Validate__Save(List<ReportDefinitionForSave> entities, int top)
        {
            var conn = await GetConnectionAsync();
            using var cmd = conn.CreateCommand();

            // Parameters
            DataTable entitiesTable = RepositoryUtilities.DataTable(entities, addIndex: true);
            var entitiesTvp = new SqlParameter("@Entities", entitiesTable)
            {
                TypeName = $"[dbo].[{nameof(ReportDefinition)}List]",
                SqlDbType = SqlDbType.Structured
            };

            DataTable parametersTable = RepositoryUtilities.DataTableWithHeaderIndex(entities, e => e.Parameters);
            var parametersTvp = new SqlParameter("@Parameters", parametersTable)
            {
                TypeName = $"[dbo].[{nameof(ReportParameterDefinition)}List]",
                SqlDbType = SqlDbType.Structured
            };

            DataTable selectTable = RepositoryUtilities.DataTableWithHeaderIndex(entities, e => e.Select);
            var selectTvp = new SqlParameter("@Select", selectTable)
            {
                TypeName = $"[dbo].[{nameof(ReportSelectDefinition)}List]",
                SqlDbType = SqlDbType.Structured
            };

            DataTable rowsTable = RepositoryUtilities.DataTableWithHeaderIndex(entities, e => e.Rows);
            var rowsTvp = new SqlParameter("@Rows", rowsTable)
            {
                TypeName = $"[dbo].[{nameof(ReportDimensionDefinition)}List]",
                SqlDbType = SqlDbType.Structured
            };

            DataTable columnsTable = RepositoryUtilities.DataTableWithHeaderIndex(entities, e => e.Columns);
            var columnsTvp = new SqlParameter("@Columns", columnsTable)
            {
                TypeName = $"[dbo].[{nameof(ReportDimensionDefinition)}List]",
                SqlDbType = SqlDbType.Structured
            };

            DataTable measuresTable = RepositoryUtilities.DataTableWithHeaderIndex(entities, e => e.Measures);
            var measuresTvp = new SqlParameter("@Measures", measuresTable)
            {
                TypeName = $"[dbo].[{nameof(ReportMeasureDefinition)}List]",
                SqlDbType = SqlDbType.Structured
            };

            cmd.Parameters.Add(entitiesTvp);
            cmd.Parameters.Add(parametersTvp);
            cmd.Parameters.Add(selectTvp);
            cmd.Parameters.Add(rowsTvp);
            cmd.Parameters.Add(columnsTvp);
            cmd.Parameters.Add(measuresTvp);
            cmd.Parameters.Add("@Top", top);

            // Command
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.CommandText = $"[bll].[{nameof(ReportDefinitions_Validate__Save)}]";

            // Execute
            return await RepositoryUtilities.LoadErrors(cmd);
        }

        public async Task ReportDefinitions__Save(List<ReportDefinitionForSave> entities)
        {
            var result = new List<IndexedId>();

            var conn = await GetConnectionAsync();
            using var cmd = conn.CreateCommand();

            // Parameters
            DataTable entitiesTable = RepositoryUtilities.DataTable(entities, addIndex: true);
            var entitiesTvp = new SqlParameter("@Entities", entitiesTable)
            {
                TypeName = $"[dbo].[{nameof(ReportDefinition)}List]",
                SqlDbType = SqlDbType.Structured
            };

            DataTable parametersTable = RepositoryUtilities.DataTableWithHeaderIndex(entities, e => e.Parameters);
            var parametersTvp = new SqlParameter("@Parameters", parametersTable)
            {
                TypeName = $"[dbo].[{nameof(ReportParameterDefinition)}List]",
                SqlDbType = SqlDbType.Structured
            };

            DataTable selectTable = RepositoryUtilities.DataTableWithHeaderIndex(entities, e => e.Select);
            var selectTvp = new SqlParameter("@Select", selectTable)
            {
                TypeName = $"[dbo].[{nameof(ReportSelectDefinition)}List]",
                SqlDbType = SqlDbType.Structured
            };

            DataTable rowsTable = RepositoryUtilities.DataTableWithHeaderIndex(entities, e => e.Rows);
            var rowsTvp = new SqlParameter("@Rows", rowsTable)
            {
                TypeName = $"[dbo].[{nameof(ReportDimensionDefinition)}List]",
                SqlDbType = SqlDbType.Structured
            };

            DataTable columnsTable = RepositoryUtilities.DataTableWithHeaderIndex(entities, e => e.Columns);
            var columnsTvp = new SqlParameter("@Columns", columnsTable)
            {
                TypeName = $"[dbo].[{nameof(ReportDimensionDefinition)}List]",
                SqlDbType = SqlDbType.Structured
            };

            DataTable measuresTable = RepositoryUtilities.DataTableWithHeaderIndex(entities, e => e.Measures);
            var measuresTvp = new SqlParameter("@Measures", measuresTable)
            {
                TypeName = $"[dbo].[{nameof(ReportMeasureDefinition)}List]",
                SqlDbType = SqlDbType.Structured
            };

            cmd.Parameters.Add(entitiesTvp);
            cmd.Parameters.Add(parametersTvp);
            cmd.Parameters.Add(selectTvp);
            cmd.Parameters.Add(rowsTvp);
            cmd.Parameters.Add(columnsTvp);
            cmd.Parameters.Add(measuresTvp);

            cmd.CommandType = CommandType.StoredProcedure;
            cmd.CommandText = $"[dal].[{nameof(ReportDefinitions__Save)}]";

            await cmd.ExecuteNonQueryAsync();
        }

        public async Task<IEnumerable<ValidationError>> ReportDefinitions_Validate__Delete(List<string> ids, int top)
        {
            var conn = await GetConnectionAsync();
            using var cmd = conn.CreateCommand();
            // Parameters
            DataTable idsTable = RepositoryUtilities.DataTable(ids.Select(id => new { Id = id }), addIndex: true);
            var idsTvp = new SqlParameter("@Ids", idsTable)
            {
                TypeName = $"[dbo].[IndexedStringList]",
                SqlDbType = SqlDbType.Structured
            };

            cmd.Parameters.Add(idsTvp);
            cmd.Parameters.Add("@Top", top);

            // Command
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.CommandText = $"[bll].[{nameof(ReportDefinitions_Validate__Delete)}]";

            // Execute
            return await RepositoryUtilities.LoadErrors(cmd);
        }

        public async Task ReportDefinitions__Delete(IEnumerable<string> ids)
        {
            var conn = await GetConnectionAsync();
            using var cmd = conn.CreateCommand();
            // Parameters
            DataTable idsTable = RepositoryUtilities.DataTable(ids.Select(id => new { Id = id }));
            var idsTvp = new SqlParameter("@Ids", idsTable)
            {
                TypeName = $"[dbo].[StringList]",
                SqlDbType = SqlDbType.Structured
            };

            cmd.Parameters.Add(idsTvp);

            // Command
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.CommandText = $"[dal].[{nameof(ReportDefinitions__Delete)}]";

            // Execute
            try
            {
                await cmd.ExecuteNonQueryAsync();
            }
            catch (SqlException ex) when (RepositoryUtilities.IsForeignKeyViolation(ex))
            {
                throw new ForeignKeyViolationException();
            }
        }

        #endregion
    }
}
