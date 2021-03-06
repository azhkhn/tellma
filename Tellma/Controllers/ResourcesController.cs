﻿using Tellma.Controllers.Dto;
using Tellma.Controllers.Utilities;
using Tellma.Data;
using Tellma.Data.Queries;
using Tellma.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace Tellma.Controllers
{
    // Specific API, works with a certain definitionId, and allows read-write
    [Route("api/" + BASE_ADDRESS + "{definitionId}")]
    [ApplicationApi]
    public class ResourcesController : CrudControllerBase<ResourceForSave, Resource, int>
    {
        public const string BASE_ADDRESS = "resources/";

        private readonly ILogger _logger;
        private readonly IStringLocalizer _localizer;
        private readonly ApplicationRepository _repo;
        private readonly IDefinitionsCache _definitionsCache;
        private readonly IModelMetadataProvider _modelMetadataProvider;

        private string DefinitionId => RouteData.Values["definitionId"]?.ToString() ??
            throw new BadRequestException("URI must be of the form 'api/" + BASE_ADDRESS + "{definitionId}'");

        private ResourceDefinitionForClient Definition() => _definitionsCache.GetCurrentDefinitionsIfCached()?.Data?.Resources?
            .GetValueOrDefault(DefinitionId) ?? throw new InvalidOperationException($"Definition for '{DefinitionId}' was missing from the cache");

        private string View => $"{BASE_ADDRESS}{DefinitionId}";

        public ResourcesController(
            ILogger<ResourcesController> logger,
            IStringLocalizer<Strings> localizer,
            ApplicationRepository repo,
            IDefinitionsCache definitionsCache,
            IModelMetadataProvider modelMetadataProvider) : base(logger, localizer)
        {
            _logger = logger;
            _localizer = localizer;
            _repo = repo;
            _definitionsCache = definitionsCache;
            _modelMetadataProvider = modelMetadataProvider;
        }

        [HttpPut("activate")]
        public async Task<ActionResult<EntitiesResponse<Resource>>> Activate([FromBody] List<int> ids, [FromQuery] ActivateArguments args)
        {
            bool returnEntities = args.ReturnEntities ?? false;

            return await ControllerUtilities.InvokeActionImpl(() =>
                Activate(ids: ids,
                    returnEntities: returnEntities,
                    expand: args.Expand,
                    isActive: true)
            , _logger);
        }

        [HttpPut("deactivate")]
        public async Task<ActionResult<EntitiesResponse<Resource>>> Deactivate([FromBody] List<int> ids, [FromQuery] DeactivateArguments args)
        {
            bool returnEntities = args.ReturnEntities ?? false;

            return await ControllerUtilities.InvokeActionImpl(() =>
                Activate(ids: ids,
                    returnEntities: returnEntities,
                    expand: args.Expand,
                    isActive: false)
            , _logger);
        }

        private async Task<ActionResult<EntitiesResponse<Resource>>> Activate([FromBody] List<int> ids, bool returnEntities, string expand, bool isActive)
        {
            // Parse parameters
            var expandExp = ExpandExpression.Parse(expand);
            var idsArray = ids.ToArray();

            // Check user permissions
            await CheckActionPermissions("IsActive", idsArray);

            // Execute and return
            using var trx = ControllerUtilities.CreateTransaction();
            await _repo.Resources__Activate(ids, isActive);

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

        protected override async Task<IEnumerable<AbstractPermission>> UserPermissions(string action)
        {
            return await _repo.UserPermissions(action, View);
        }

        protected override IRepository GetRepository()
        {
            string filter = $"{nameof(Resource.DefinitionId)} {Ops.eq} '{DefinitionId}'";
            return new FilteredRepository<Resource>(_repo, filter);
        }

        protected override Query<Resource> Search(Query<Resource> query, GetArguments args, IEnumerable<AbstractPermission> filteredPermissions)
        {
            return ResourceControllerUtil.SearchImpl(query, args, filteredPermissions);
        }

        protected override async Task SaveValidateAsync(List<ResourceForSave> entities)
        {
            var definition = Definition();

            // Set default values
            SetDefaultValue(entities, e => e.Identifier, definition.IdentifierDefaultValue);
            SetDefaultValue(entities, e => e.CurrencyId, definition.CurrencyDefaultValue);
            SetDefaultValue(entities, e => e.MonetaryValue, definition.MonetaryValueDefaultValue);
            SetDefaultValue(entities, e => e.CountUnitId, definition.CountUnitDefaultValue);
            SetDefaultValue(entities, e => e.Count, definition.CountDefaultValue);
            SetDefaultValue(entities, e => e.MassUnitId, definition.MassUnitDefaultValue);
            SetDefaultValue(entities, e => e.Mass, definition.MassDefaultValue);
            SetDefaultValue(entities, e => e.VolumeUnitId, definition.VolumeUnitDefaultValue);
            SetDefaultValue(entities, e => e.Volume, definition.VolumeDefaultValue);
            SetDefaultValue(entities, e => e.TimeUnitId, definition.TimeUnitDefaultValue);
            SetDefaultValue(entities, e => e.Time, definition.TimeDefaultValue);
            //SetDefaultValue(entities, e => e.Description, definition.DescriptionDefaultValue);
            //SetDefaultValue(entities, e => e.Description2, definition.Description2DefaultValue);
            //SetDefaultValue(entities, e => e.Description3, definition.Description3DefaultValue);
            SetDefaultValue(entities, e => e.AvailableSince, definition.AvailableSinceDefaultValue);
            SetDefaultValue(entities, e => e.AvailableTill, definition.AvailableTillDefaultValue);
            SetDefaultValue(entities, e => e.Lookup1Id, definition.Lookup1DefaultValue);
            SetDefaultValue(entities, e => e.Lookup2Id, definition.Lookup2DefaultValue);
            //SetDefaultValue(entities, e => e.Lookup3Id, definition.Lookup3DefaultValue);
            //SetDefaultValue(entities, e => e.Lookup4Id, definition.Lookup4DefaultValue);
            //SetDefaultValue(entities, e => e.Lookup5Id, definition.Lookup5DefaultValue);

            // Validate required stuff
            ValidateIfRequired(entities, e => e.Identifier, definition.IdentifierVisibility);
            ValidateIfRequired(entities, e => e.CurrencyId, definition.CurrencyVisibility);
            ValidateIfRequired(entities, e => e.MonetaryValue, definition.MonetaryValueVisibility);
            ValidateIfRequired(entities, e => e.CountUnitId, definition.CountUnitVisibility);
            ValidateIfRequired(entities, e => e.Count, definition.CountVisibility);
            ValidateIfRequired(entities, e => e.MassUnitId, definition.MassUnitVisibility);
            ValidateIfRequired(entities, e => e.Mass, definition.MassVisibility);
            ValidateIfRequired(entities, e => e.VolumeUnitId, definition.VolumeUnitVisibility);
            ValidateIfRequired(entities, e => e.Volume, definition.VolumeVisibility);
            ValidateIfRequired(entities, e => e.TimeUnitId, definition.TimeUnitVisibility);
            ValidateIfRequired(entities, e => e.Time, definition.TimeVisibility);
            ValidateIfRequired(entities, e => e.Description, definition.DescriptionVisibility);
            ValidateIfRequired(entities, e => e.Description2, definition.DescriptionVisibility);
            ValidateIfRequired(entities, e => e.Description3, definition.DescriptionVisibility);
            ValidateIfRequired(entities, e => e.AvailableSince, definition.AvailableSinceVisibility);
            ValidateIfRequired(entities, e => e.AvailableTill, definition.AvailableTillVisibility);
            ValidateIfRequired(entities, e => e.Lookup1Id, definition.Lookup1Visibility);
            ValidateIfRequired(entities, e => e.Lookup2Id, definition.Lookup2Visibility);
            //ValidateIfRequired(entities, e => e.Lookup3Id, definition.Lookup3Visibility);
            //ValidateIfRequired(entities, e => e.Lookup4Id, definition.Lookup4Visibility);
            //ValidateIfRequired(entities, e => e.Lookup5Id, definition.Lookup5Visibility);

            // No need to invoke SQL if the model state is full of errors
            if (ModelState.HasReachedMaxErrors)
            {
                // null Ids will cause an error when calling the SQL validation
                return;
            }

            // SQL validation
            int remainingErrorCount = ModelState.MaxAllowedErrors - ModelState.ErrorCount;
            var sqlErrors = await _repo.Resources_Validate__Save(DefinitionId, entities, top: remainingErrorCount);

            // Add errors to model state
            ModelState.AddLocalizedErrors(sqlErrors, _localizer);
        }

        private void SetDefaultValue<TKey>(List<ResourceForSave> entities, Expression<Func<ResourceForSave, TKey>> selector, TKey defaultValue)
        {
            if (defaultValue != null)
            {
                Func<ResourceForSave, TKey> getPropValue = selector.Compile(); // The function to access the property value
                Action<ResourceForSave, TKey> setPropValue = ControllerUtilities.GetAssigner(selector).Compile();

                entities.ForEach(entity =>
                {
                    if (getPropValue(entity) == null)
                    {
                        setPropValue(entity, defaultValue);
                    }
                });
            }
        }

        private void ValidateIfRequired<TKey>(List<ResourceForSave> entities, Expression<Func<ResourceForSave, TKey>> selector, string visibility)
        {
            if (visibility == Visibility.Required && !ModelState.HasReachedMaxErrors)
            {
                Func<ResourceForSave, TKey> getPropValue = selector.Compile(); // The function to access the property value

                foreach (var (entity, index) in entities.Select((e, i) => (e, i)))
                {
                    if (getPropValue(entity) == null)
                    {
                        string propName = (selector.Body as MemberExpression).Member.Name; // The name of the property we're validating
                        string path = $"[{index}].{propName}";
                        string propDisplayName = _modelMetadataProvider.GetMetadataForProperty(typeof(ResourceForSave), propName)? .DisplayName;
                        string errorMsg = _localizer[nameof(RequiredAttribute), propDisplayName];

                        ModelState.AddModelError(path, errorMsg);

                        if (ModelState.HasReachedMaxErrors)
                        {
                            // No need to keep going forever
                            break;
                        }
                    }
                }
            }
        }

        protected override async Task<List<int>> SaveExecuteAsync(List<ResourceForSave> entities, ExpandExpression expand, bool returnIds)
        {
            return await _repo.Resources__Save(DefinitionId, entities, returnIds: returnIds);
        }

        protected override async Task DeleteValidateAsync(List<int> ids)
        {
            // SQL validation
            int remainingErrorCount = ModelState.MaxAllowedErrors - ModelState.ErrorCount;
            var sqlErrors = await _repo.Resources_Validate__Delete(DefinitionId, ids, top: remainingErrorCount);

            // Add errors to model state
            ModelState.AddLocalizedErrors(sqlErrors, _localizer);
        }

        protected override async Task DeleteExecuteAsync(List<int> ids)
        {
            try
            {
                await _repo.Resources__Delete(ids);
            }
            catch (ForeignKeyViolationException)
            {
                // TODO: test
                var definition = Definition();
                var tenantInfo = await _repo.GetTenantInfoAsync();
                var titleSingular = tenantInfo.Localize(definition.TitleSingular, definition.TitleSingular2, definition.TitleSingular3);

                throw new BadRequestException(_localizer["Error_CannotDelete0AlreadyInUse", titleSingular]);
            }
        }

        protected override Query<Resource> GetAsQuery(List<ResourceForSave> entities)
        {
            return _repo.Resources__AsQuery(DefinitionId, entities);
        }
    }

    // Generic API, allows reading all resources

    [Route("api/" + ResourcesController.BASE_ADDRESS)]
    [ApplicationApi]
    public class ResourcesGenericController : FactWithIdControllerBase<Resource, int>
    {
        private readonly ApplicationRepository _repo;

        public ResourcesGenericController(
            ILogger<ResourcesGenericController> logger,
            IStringLocalizer<Strings> localizer,
            ApplicationRepository repo) : base(logger, localizer)
        {
            _repo = repo;
        }

        protected override IRepository GetRepository()
        {
            return _repo;
        }

        protected override async Task<IEnumerable<AbstractPermission>> UserPermissions(string action)
        {
            // Get all permissions pertaining to resources
            string prefix = ResourcesController.BASE_ADDRESS;
            var permissions = await _repo.GenericUserPermissions(action, prefix);

            // Massage the permissions by adding definitionId = definitionId as an extra clause 
            // (since the controller will not filter the results per any specific definition Id)
            foreach (var permission in permissions.Where(e => e.View != "all"))
            {
                string definitionId = permission.View.Remove(0, prefix.Length).Replace("'", "''");
                string definitionPredicate = $"{nameof(Resource.DefinitionId)} {Ops.eq} '{definitionId}'";
                if (!string.IsNullOrWhiteSpace(permission.Criteria))
                {
                    permission.Criteria = $"{definitionPredicate} and ({permission.Criteria})";
                }
                else
                {
                    permission.Criteria = definitionPredicate;
                }
            }

            // Return the massaged permissions
            return permissions;
        }

        protected override Query<Resource> Search(Query<Resource> query, GetArguments args, IEnumerable<AbstractPermission> filteredPermissions)
        {
            return ResourceControllerUtil.SearchImpl(query, args, filteredPermissions);
        }
    }

    internal class ResourceControllerUtil
    {
        /// <summary>
        /// This is needed in both the generic and specific controllers, so we move it out here
        /// </summary>
        public static Query<Resource> SearchImpl(Query<Resource> query, GetArguments args, IEnumerable<AbstractPermission> _)
        {
            string search = args.Search;
            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.Replace("'", "''"); // escape quotes by repeating them

                var name = nameof(Resource.Name);
                var name2 = nameof(Resource.Name2);
                var name3 = nameof(Resource.Name3);
                var code = nameof(Resource.Code);

                query = query.Filter($"{name} {Ops.contains} '{search}' or {name2} {Ops.contains} '{search}' or {name3} {Ops.contains} '{search}' or {code} {Ops.contains} '{search}'");
            }

            return query;
        }
    }
}
