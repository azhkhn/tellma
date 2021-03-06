﻿using Tellma.Controllers.Dto;
using Tellma.Data;
using Tellma.Data.Queries;
using Tellma.Entities;
using Tellma.Services.ApiAuthentication;
using Tellma.Services.ImportExport;
using Tellma.Services.MultiTenancy;
using Tellma.Services.Utilities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Tellma.Controllers
{
    [Route("api/settings")]
    [AuthorizeAccess]
    [ApplicationApi]
    [ApiController]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public class SettingsController : ControllerBase
    {
        // Private fields

        private readonly ApplicationRepository _repo;
        private readonly ILogger<SettingsController> _logger;
        private readonly IStringLocalizer _localizer;
        private readonly ISettingsCache _settingsCache;
        private readonly ITenantIdAccessor _tenantIdAccessor;

        public SettingsController(ApplicationRepository repo,
            ILogger<SettingsController> logger,
            IStringLocalizer<Strings> localizer,
            ISettingsCache settingsCache,
            ITenantIdAccessor tenantIdAccessor)
        {
            _repo = repo;
            _logger = logger;
            _localizer = localizer;
            _settingsCache = settingsCache;
            _tenantIdAccessor = tenantIdAccessor;
        }


        // API

        [HttpGet]
        public async Task<ActionResult<GetEntityResponse<Settings>>> Get([FromQuery] GetByIdArguments args)
        {
            // Authorized access (Criteria are not supported here)
            var readPermissions = await _repo.UserPermissions(Constants.Read, "settings");
            if (!readPermissions.Any())
            {
                return StatusCode(403);
            }

            try
            {
                return await GetImpl(args);
            }
            catch (BadRequestException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error: {ex.Message} {ex.StackTrace}");
                return BadRequest(ex.Message);
            }
        }

        [HttpPost]
        public async Task<ActionResult<SaveSettingsResponse>> Save([FromBody] SettingsForSave settingsForSave, [FromQuery] SaveArguments args)
        {
            // Authorized access (Criteria are not supported here)
            var updatePermissions = await _repo.UserPermissions(Constants.Update, "settings");
            if (!updatePermissions.Any())
            {
                return StatusCode(403);
            }

            try
            {
                // Trim all string fields just in case
                settingsForSave.TrimStringProperties();

                // Validate
                ValidateAndPreprocessSettings(settingsForSave);

                if (!ModelState.IsValid)
                {
                    return UnprocessableEntity(ModelState);
                }

                // Persist
                await _repo.Settings__Save(settingsForSave);

                // Update the settings cache
                var tenantId = _tenantIdAccessor.GetTenantId();
                var settingsForClient = await LoadSettingsForClient(_repo);
                _settingsCache.SetSettings(tenantId, settingsForClient);

                // If requested, return the updated entity
                if (args.ReturnEntities ?? false)
                {
                    // If requested, return the same response you would get from a GET
                    var res = await GetImpl(new GetByIdArguments { Expand = args.Expand });
                    var result = new SaveSettingsResponse
                    {
                        Entities = res.Entities,
                        Result = res.Result,
                        SettingsForClient = settingsForClient
                    };

                    return result;
                }
                else
                {
                    return Ok();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error: {ex.Message} {ex.StackTrace}");
                return BadRequest(ex.Message);
            }
        }

        [HttpGet("client")]
        public ActionResult<DataWithVersion<SettingsForClient>> SettingsForClient()
        {
            try
            {
                // Simply retrieves the cached settings, which were refreshed by ApiControllerAttribute
                var result = _settingsCache.GetCurrentSettingsIfCached();
                if (result == null)
                {
                    throw new InvalidOperationException("The definitions were missing from the cache");
                }

                return Ok(result);
            }
            catch (BadRequestException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error: {ex.Message} {ex.StackTrace}");
                return BadRequest(ex.Message);
            }
        }

        [HttpGet("ping")]
        public ActionResult Ping()
        {
            // If all you want is to check whether the cached versions of settings and permissions 
            // are fresh you can use this API that only does that through the [LoadTenantInfo] filter

            return Ok();
        }


        // Helper methods

        private async Task<GetEntityResponse<Settings>> GetImpl(GetByIdArguments args)
        {
            var settings = await _repo.Settings
                .Select(args.Select)
                .Expand(args.Expand)
                .OrderBy("PrimaryLanguageId")
                .FirstOrDefaultAsync();

            if (settings == null)
            {
                // Programmer mistake
                throw new BadRequestException("Settings have not been initialized");
            }

            var result = new GetEntityResponse<Settings>
            {
                Result = settings,
            };

            return result;
        }

        private void ValidateAndPreprocessSettings(SettingsForSave entity)
        {
            if (!string.IsNullOrWhiteSpace(entity.SecondaryLanguageId) || !string.IsNullOrWhiteSpace(entity.TernaryLanguageId))
            {
                if (string.IsNullOrWhiteSpace(entity.PrimaryLanguageSymbol))
                {
                    ModelState.AddModelError(nameof(entity.PrimaryLanguageSymbol),
                        _localizer[nameof(RequiredAttribute), _localizer["Settings_PrimaryLanguageSymbol"]]);
                }
            }

            if (string.IsNullOrWhiteSpace(entity.SecondaryLanguageId))
            {
                entity.SecondaryLanguageSymbol = null;
            }
            else
            {
                if (string.IsNullOrWhiteSpace(entity.SecondaryLanguageSymbol))
                {
                    ModelState.AddModelError(nameof(entity.SecondaryLanguageSymbol),
                        _localizer[nameof(RequiredAttribute), _localizer["Settings_SecondaryLanguageSymbol"]]);
                }

                if (entity.SecondaryLanguageId == entity.PrimaryLanguageId)
                {
                    ModelState.AddModelError(nameof(entity.SecondaryLanguageId),
                        _localizer["Error_SecondaryLanguageCannotBeTheSameAsPrimaryLanguage"]);
                }
            }

            if (string.IsNullOrWhiteSpace(entity.TernaryLanguageId))
            {
                entity.TernaryLanguageSymbol = null;
            }
            else
            {
                if (string.IsNullOrWhiteSpace(entity.TernaryLanguageSymbol))
                {
                    ModelState.AddModelError(nameof(entity.TernaryLanguageSymbol),
                        _localizer[nameof(RequiredAttribute), _localizer["Settings_TernaryLanguageSymbol"]]);
                }

                if (entity.TernaryLanguageId == entity.PrimaryLanguageId)
                {
                    ModelState.AddModelError(nameof(entity.TernaryLanguageId),
                        _localizer["Error_TernaryLanguageCannotBeTheSameAsPrimaryLanguage"]);
                }

                if (entity.TernaryLanguageId == entity.SecondaryLanguageId)
                {
                    ModelState.AddModelError(nameof(entity.TernaryLanguageId),
                        _localizer["Error_TernaryLanguageCannotBeTheSameAsSecondaryLanguage"]);
                }
            }

            // Make sure the color is a valid HTML color
            // Credit: https://bit.ly/2ToV6x4
            if (!string.IsNullOrWhiteSpace(entity.BrandColor) && !Regex.IsMatch(entity.BrandColor, "^#(?:[0-9a-fA-F]{3}){1,2}$"))
            {
                ModelState.AddModelError(nameof(entity.BrandColor),
                    _localizer["Error_TheField0MustBeAValidColorFormat", _localizer["Settings_BrandColor"]]);
            }
        }

        public static async Task<DataWithVersion<SettingsForClient>> LoadSettingsForClient(ApplicationRepository repo)
        {
            var (isMultiResponsibilityCenter, settings) = await repo.Settings__Load();
            if (settings == null)
            {
                // This should never happen
                throw new BadRequestException("Settings have not been initialized");
            }

            // Prepare the settings for client
            SettingsForClient settingsForClient = new SettingsForClient();
            foreach (var forClientProp in typeof(SettingsForClient).GetProperties())
            {
                var settingsProp = typeof(Settings).GetProperty(forClientProp.Name);
                if (settingsProp != null)
                {
                    var value = settingsProp.GetValue(settings);
                    forClientProp.SetValue(settingsForClient, value);
                }
            }

            // Is Multi Responsibility Center
            settingsForClient.IsMultiResponsibilityCenter = isMultiResponsibilityCenter;

            // Functional currency
            settingsForClient.FunctionalCurrencyDecimals = settings.FunctionalCurrency.E ?? 0;
            settingsForClient.FunctionalCurrencyName = settings.FunctionalCurrency.Name;
            settingsForClient.FunctionalCurrencyName2 = settings.FunctionalCurrency.Name2;
            settingsForClient.FunctionalCurrencyName3 = settings.FunctionalCurrency.Name3;
            settingsForClient.FunctionalCurrencyDescription = settings.FunctionalCurrency.Description;
            settingsForClient.FunctionalCurrencyDescription2 = settings.FunctionalCurrency.Description2;
            settingsForClient.FunctionalCurrencyDescription3 = settings.FunctionalCurrency.Description3;

            // Language
            settingsForClient.PrimaryLanguageName = GetCultureDisplayName(settingsForClient.PrimaryLanguageId);
            settingsForClient.SecondaryLanguageName = GetCultureDisplayName(settingsForClient.SecondaryLanguageId);
            settingsForClient.TernaryLanguageName = GetCultureDisplayName(settingsForClient.TernaryLanguageId);

            // Tag the settings for client with their current version
            var result = new DataWithVersion<SettingsForClient>
            {
                Version = settings.SettingsVersion.ToString(),
                Data = settingsForClient
            };

            return result;
        }

        private static string GetCultureDisplayName(string cultureName)
        {
            if (cultureName is null)
            {
                return null;
            }

            return System.Globalization.CultureInfo.GetCultureInfo(cultureName)?.NativeName;
        }

    }
}
