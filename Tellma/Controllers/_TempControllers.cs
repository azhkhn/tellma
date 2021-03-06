﻿using Tellma.Controllers.Dto;
using Tellma.Data;
using Tellma.Data.Queries;
using Tellma.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Threading.Tasks;
namespace Tellma.Controllers
{
    // Here I add all the readonly controllers we need for the JV

    public static class TempUtil
    {
        public static IEnumerable<AbstractPermission> UserPermissions(string view)
        {
            yield return new AbstractPermission { Action = "All", View = view, };
        }
    }

    [Route("api/voucher-booklets")]
    [ApplicationApi]
    public class VoucherBookletsController : FactGetByIdControllerBase<VoucherBooklet, int>
    {
        private readonly ApplicationRepository _repo;

        private string VIEW => "voucher-booklets";

        public VoucherBookletsController(
            ILogger<VoucherBookletsController> logger,
            IStringLocalizer<Strings> localizer,
            ApplicationRepository repo) : base(logger, localizer)
        {
            _repo = repo;
        }

        protected override IRepository GetRepository()
        {
            return _repo;
        }

        protected override Task<IEnumerable<AbstractPermission>> UserPermissions(string action)
        {
            return Task.FromResult(TempUtil.UserPermissions(VIEW));
        }

        protected override Query<VoucherBooklet> Search(Query<VoucherBooklet> query, GetArguments args, IEnumerable<AbstractPermission> filteredPermissions)
        {
            string search = args.Search;
            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.Replace("'", "''"); // escape quotes by repeating them

                var stringPrefix = nameof(VoucherBooklet.StringPrefix); // TODO: Search the 

                query = query.Filter($"{stringPrefix} {Ops.contains} '{search}'");
            }

            return query;
        }
    }
}
