﻿using BSharp.Controllers.Dto;
using BSharp.Controllers.Utilities;
using BSharp.Data;
using BSharp.Data.Queries;
using BSharp.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace BSharp.Controllers
{
    // Specific API, works with a certain definitionId, and allows read-write
    [Route("api/" + BASE_ADDRESS)]
    [ApplicationApi]
    public class AccountsController : CrudControllerBase<AccountForSave, Account, int>
    {
        public const string BASE_ADDRESS = "accounts";

        private readonly ILogger _logger;
        private readonly IStringLocalizer _localizer;
        private readonly ApplicationRepository _repo;
        private readonly ISettingsCache _settingsCache;

        private string ViewId => $"{BASE_ADDRESS}";

        public AccountsController(
            ILogger<AccountsController> logger,
            IStringLocalizer<Strings> localizer,
            ApplicationRepository repo,
            ISettingsCache settingsCache) : base(logger, localizer)
        {
            _logger = logger;
            _localizer = localizer;
            _repo = repo;
            _settingsCache = settingsCache;
        }

        [HttpPut("activate")]
        public async Task<ActionResult<EntitiesResponse<Account>>> Activate([FromBody] List<int> ids, [FromQuery] ActivateArguments args)
        {
            bool returnEntities = args.ReturnEntities ?? false;

            return await ControllerUtilities.InvokeActionImpl(() =>
                Activate(ids: ids,
                    returnEntities: returnEntities,
                    expand: args.Expand,
                    isDeprecated: false)
            , _logger);
        }

        [HttpPut("deactivate")]
        public async Task<ActionResult<EntitiesResponse<Account>>> Deprecate([FromBody] List<int> ids, [FromQuery] DeactivateArguments args)
        {
            bool returnEntities = args.ReturnEntities ?? false;

            return await ControllerUtilities.InvokeActionImpl(() =>
                Activate(ids: ids,
                    returnEntities: returnEntities,
                    expand: args.Expand,
                    isDeprecated: true)
            , _logger);
        }

        private async Task<ActionResult<EntitiesResponse<Account>>> Activate([FromBody] List<int> ids, bool returnEntities, string expand, bool isDeprecated)
        {
            // Parse parameters
            var expandExp = ExpandExpression.Parse(expand);
            var idsArray = ids.ToArray();

            // Check user permissions
            await CheckActionPermissions("IsDeprecated", idsArray);

            // Execute and return
            using (var trx = ControllerUtilities.CreateTransaction())
            {
                await _repo.Accounts__Deprecate(ids, isDeprecated);

                if (returnEntities)
                {
                    var response = await GetByIdListAsync(idsArray, expandExp);

                    trx.Complete();
                    return Ok(response);
                }
                else
                {
                    trx.Complete();
                    return Ok();
                }
            }
        }

        protected override async Task<IEnumerable<AbstractPermission>> UserPermissions(string action)
        {
            return await _repo.UserPermissions(action, ViewId);
        }

        protected override IRepository GetRepository()
        {
            return _repo;
        }

        protected override Query<Account> Search(Query<Account> query, GetArguments args, IEnumerable<AbstractPermission> filteredPermissions)
        {
            string search = args.Search;
            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.Replace("'", "''"); // escape quotes by repeating them

                var name = nameof(Account.Name);
                var name2 = nameof(Account.Name2);
                var name3 = nameof(Account.Name3);
                var code = nameof(Account.Code);

                query = query.Filter($"{name} {Ops.contains} '{search}' or {name2} {Ops.contains} '{search}' or {name3} {Ops.contains} '{search}' or {code} {Ops.contains} '{search}'");
            }

            return query;
        }

        protected override Task<List<AccountForSave>> SavePreprocessAsync(List<AccountForSave> entities)
        {
            // Defaults
            var settings = _settingsCache.GetCurrentSettingsIfCached().Data;
            entities.ForEach(entity =>
            {
                entity.IsSmart = entity.IsSmart ?? false;

                if (!entity.IsSmart.Value)
                {
                    // Only for dumb accounts
                    entity.CurrencyId = entity.CurrencyId ?? settings.FunctionalCurrencyId;
                }
            });

            return Task.FromResult(entities);
        }

        protected override async Task SaveValidateAsync(List<AccountForSave> entities)
        {
            // SQL validation
            int remainingErrorCount = ModelState.MaxAllowedErrors - ModelState.ErrorCount;
            var sqlErrors = await _repo.Accounts_Validate__Save(entities, top: remainingErrorCount);

            // Add errors to model state
            ModelState.AddLocalizedErrors(sqlErrors, _localizer);
        }

        protected override async Task<List<int>> SaveExecuteAsync(List<AccountForSave> entities, ExpandExpression expand, bool returnIds)
        {
            return await _repo.Accounts__Save(entities, returnIds: returnIds);
        }

        protected override async Task DeleteValidateAsync(List<int> ids)
        {
            // SQL validation
            int remainingErrorCount = ModelState.MaxAllowedErrors - ModelState.ErrorCount;
            var sqlErrors = await _repo.Accounts_Validate__Delete(ids, top: remainingErrorCount);

            // Add errors to model state
            ModelState.AddLocalizedErrors(sqlErrors, _localizer);
        }

        protected override async Task DeleteExecuteAsync(List<int> ids)
        {
            try
            {
                await _repo.Accounts__Delete(ids);
            }
            catch (ForeignKeyViolationException)
            {
                throw new BadRequestException(_localizer["Error_CannotDelete0AlreadyInUse", _localizer["Account"]]);
            }
        }

        protected override Query<Account> GetAsQuery(List<AccountForSave> entities)
        {
            return _repo.Accounts__AsQuery(entities);
        }
    }
}
