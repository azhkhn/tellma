﻿using Tellma.Controllers.Dto;
using Tellma.Data;
using Tellma.Services.ApiAuthentication;
using Tellma.Services.ClientInfo;
using Tellma.Services.Identity;
using Tellma.Services.Sharding;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Tellma.Controllers
{
    [Route("api/companies")]
    [ApiController]
    [AuthorizeAccess]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public class CompaniesController : ControllerBase
    {
        // Private fields

        private readonly AdminRepository _repo;
        private readonly ILogger _logger;
        private readonly IShardResolver _shardResolver;
        private readonly IExternalUserAccessor _externalUserAccessor;
        private readonly IClientInfoAccessor _clientInfoAccessor;


        // Constructor

        public CompaniesController(AdminRepository db, ILogger<CompaniesController> logger,
            IShardResolver shardResolver, IExternalUserAccessor externalUserAccessor, IClientInfoAccessor clientInfoAccessor)
        {
            _repo = db;
            _logger = logger;
            _shardResolver = shardResolver;
            _externalUserAccessor = externalUserAccessor;
            _clientInfoAccessor = clientInfoAccessor;
        }

        [HttpGet("client")]
        public async Task<ActionResult<IEnumerable<UserCompany>>> CompaniesForClient()
        {
            try
            {
                var result = await GetForClientImpl();
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

        private async Task<IEnumerable<UserCompany>> GetForClientImpl()
        {
            var result = new List<UserCompany>();

            var databaseIds = await _repo.GetAccessibleDatabaseIds();
            var globalUserInfo = await _repo.GetAdminUserInfoAsync();
            foreach (var databaseId in databaseIds)
            {
                try
                {
                    var connString = _shardResolver.GetConnectionString(databaseId);
                    using var appRepo = new ApplicationRepository(null, _externalUserAccessor, _clientInfoAccessor, null);

                    await appRepo.InitConnectionAsync(connString, setLastActive: false);
                    var userInfo = await appRepo.GetUserInfoAsync();
                    if (userInfo.UserId != null)
                    {
                        var tenantInfo = await appRepo.GetTenantInfoAsync();
                        result.Add(new UserCompany
                        {
                            Id = databaseId,
                            Name = tenantInfo.ShortCompanyName,
                            Name2 = tenantInfo.ShortCompanyName2,
                            Name3 = tenantInfo.ShortCompanyName3
                        });
                    }
                }
                catch(Exception ex)
                {
                    _logger.LogError($"Exception while loading user companies: DatabaseId: {databaseId}, UserId: {globalUserInfo?.UserId}, {ex.GetType().Name}: {ex.Message}");
                }
            }

            return result;
        }
    }
}
