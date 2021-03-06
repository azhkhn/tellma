﻿using System;
using System.Collections.Generic;

namespace Tellma.Controllers.Dto
{
    public class SettingsForClient
    {
        public string ShortCompanyName { get; set; }

        public string ShortCompanyName2 { get; set; }

        public string ShortCompanyName3 { get; set; }

        public string FunctionalCurrencyId { get; set; }

        public string FunctionalCurrencyName { get; set; }

        public string FunctionalCurrencyName2 { get; set; }

        public string FunctionalCurrencyName3 { get; set; }

        public string FunctionalCurrencyDescription { get; set; }

        public string FunctionalCurrencyDescription2 { get; set; }

        public string FunctionalCurrencyDescription3 { get; set; }

        public byte FunctionalCurrencyDecimals { get; set; }

        public DateTime ArchiveDate { get; set; }

        public string PrimaryLanguageId { get; set; }

        public string PrimaryLanguageName { get; set; }

        public string PrimaryLanguageSymbol { get; set; }

        public string SecondaryLanguageId { get; set; }

        public string SecondaryLanguageName { get; set; }

        public string SecondaryLanguageSymbol { get; set; }

        public string TernaryLanguageId { get; set; }

        public string TernaryLanguageName { get; set; }

        public string TernaryLanguageSymbol { get; set; }

        public string BrandColor { get; set; }

        public DateTimeOffset CreatedAt { get; set; }

        public bool IsMultiResponsibilityCenter { get; set; }
    }
}
