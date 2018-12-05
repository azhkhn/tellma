﻿using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace BSharp.Controllers.Shared
{
    public class ActionArguments<TKey>
    {
        [Required]
        public string Action { get; set; }

        [Required]
        public List<TKey> Ids { get; set; }
    }
}