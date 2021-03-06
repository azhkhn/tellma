﻿using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Tellma.Entities
{
    [StrongEntity]
    public class ResourceForSave : EntityWithKey<int>
    {
        [Display(Name = "Resource_AccountType")]
        [Required(ErrorMessage = nameof(RequiredAttribute))]
        [AlwaysAccessible]
        public int? AccountTypeId { get; set; }

        [MultilingualDisplay(Name = "Name", Language = Language.Primary)]
        [Required(ErrorMessage = nameof(RequiredAttribute))]
        [StringLength(255, ErrorMessage = nameof(StringLengthAttribute))]
        [AlwaysAccessible]
        public string Name { get; set; }

        [MultilingualDisplay(Name = "Name", Language = Language.Secondary)]
        [StringLength(255, ErrorMessage = nameof(StringLengthAttribute))]
        [AlwaysAccessible]
        public string Name2 { get; set; }

        [MultilingualDisplay(Name = "Name", Language = Language.Ternary)]
        [StringLength(255, ErrorMessage = nameof(StringLengthAttribute))]
        [AlwaysAccessible]
        public string Name3 { get; set; }

        [Display(Name = "Resource_Identifier")]
        [StringLength(10, ErrorMessage = nameof(StringLengthAttribute))]
        [AlwaysAccessible]
        public string Identifier { get; set; }

        [Display(Name = "Code")]
        [StringLength(255, ErrorMessage = nameof(StringLengthAttribute))]
        [AlwaysAccessible]
        public string Code { get; set; }

        [Display(Name = "Resource_Currency")]
        public string CurrencyId { get; set; }

        [Display(Name = "Resource_MonetaryValue")]
        public decimal? MonetaryValue { get; set; }

        [Display(Name = "Resource_CountUnit")]
        public int? CountUnitId { get; set; }

        [Display(Name = "Resource_Count")]
        public decimal? Count { get; set; }

        [Display(Name = "Resource_MassUnit")]
        public int? MassUnitId { get; set; }

        [Display(Name = "Resource_Mass")]
        public decimal? Mass { get; set; }

        [Display(Name = "Resource_VolumeUnit")]
        public int? VolumeUnitId { get; set; }

        [Display(Name = "Resource_Volume")]
        public decimal? Volume { get; set; }

        [Display(Name = "Resource_TimeUnit")]
        public int? TimeUnitId { get; set; }

        [Display(Name = "Resource_Time")]
        public decimal? Time { get; set; }

        [MultilingualDisplay(Name = "Description", Language = Language.Primary)]
        [StringLength(2048, ErrorMessage = nameof(StringLengthAttribute))]
        [AlwaysAccessible]
        public string Description { get; set; }

        [MultilingualDisplay(Name = "Description", Language = Language.Secondary)]
        [StringLength(2048, ErrorMessage = nameof(StringLengthAttribute))]
        [AlwaysAccessible]
        public string Description2 { get; set; }

        [MultilingualDisplay(Name = "Description", Language = Language.Ternary)]
        [StringLength(2048, ErrorMessage = nameof(StringLengthAttribute))]
        [AlwaysAccessible]
        public string Description3 { get; set; }

        [Display(Name = "Resource_AvailableSince")]
        public DateTime? AvailableSince { get; set; }

        [Display(Name = "Resource_AvailableTill")]
        public DateTime? AvailableTill { get; set; }

        [Display(Name = "Resource_Lookup1")]
        public int? Lookup1Id { get; set; }

        [Display(Name = "Resource_Lookup2")]
        public int? Lookup2Id { get; set; }

        //[Display(Name = "Resource_Lookup3")]
        //public int? Lookup3Id { get; set; }

        //[Display(Name = "Resource_Lookup4")]
        //public int? Lookup4Id { get; set; }

        //[Display(Name = "Resource_Lookup5")]
        //public int? Lookup5Id { get; set; }
    }

    public class Resource : ResourceForSave
    {
        [Display(Name = "Definition")]
        public string DefinitionId { get; set; }

        [Display(Name = "IsActive")]
        [AlwaysAccessible]
        public bool? IsActive { get; set; }

        [Display(Name = "CreatedAt")]
        public DateTimeOffset? CreatedAt { get; set; }

        [Display(Name = "CreatedBy")]
        public int? CreatedById { get; set; }

        [Display(Name = "ModifiedAt")]
        public DateTimeOffset? ModifiedAt { get; set; }

        [Display(Name = "ModifiedBy")]
        public int? ModifiedById { get; set; }

        // For Query

        [Display(Name = "CreatedBy")]
        [ForeignKey(nameof(CreatedById))]
        public User CreatedBy { get; set; }

        [Display(Name = "ModifiedBy")]
        [ForeignKey(nameof(ModifiedById))]
        public User ModifiedBy { get; set; }

        [Display(Name = "Resource_AccountType")]
        [ForeignKey(nameof(AccountTypeId))]
        public AccountType AccountType { get; set; }

        [Display(Name = "Resource_Currency")]
        [ForeignKey(nameof(CurrencyId))]
        public Currency Currency { get; set; }

        [Display(Name = "Resource_MassUnit")]
        [ForeignKey(nameof(MassUnitId))]
        public MeasurementUnit MassUnit { get; set; }

        [Display(Name = "Resource_VolumeUnit")]
        [ForeignKey(nameof(VolumeUnitId))]
        public MeasurementUnit VolumeUnit { get; set; }

        [Display(Name = "Resource_TimeUnit")]
        [ForeignKey(nameof(TimeUnitId))]
        public MeasurementUnit TimeUnit { get; set; }

        [Display(Name = "Resource_CountUnit")]
        [ForeignKey(nameof(CountUnitId))]
        public MeasurementUnit CountUnit { get; set; }

        [Display(Name = "Resource_Lookup1")]
        [ForeignKey(nameof(Lookup1Id))]
        public Lookup Lookup1 { get; set; }

        [Display(Name = "Resource_Lookup2")]
        [ForeignKey(nameof(Lookup2Id))]
        public Lookup Lookup2 { get; set; }

        //[Display(Name = "Resource_Lookup3")]
        //[ForeignKey(nameof(Lookup3Id))]
        //public Lookup Lookup3 { get; set; }

        //[Display(Name = "Resource_Lookup4")]
        //[ForeignKey(nameof(Lookup4Id))]
        //public Lookup Lookup4 { get; set; }

        //[Display(Name = "Resource_Lookup5")]
        //[ForeignKey(nameof(Lookup5Id))]
        //public Lookup Lookup5 { get; set; }
    }
}
