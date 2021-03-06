﻿using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace Tellma.Entities
{
    /// <summary>
    /// Rule 1: No Entity class can contain a property Id unless it inherits from <see cref="EntityWithKey"/>
    /// Rule 2: Every class that inherits from <see cref="EntityWithKey"/> must contain a property "Id"
    /// </summary>
    public abstract class EntityWithKey : Entity
    {
        /// <summary>
        /// Those strings cannot be used as Ids in Entities with string Ids because they will mess up the routing logic
        /// </summary>
        public static readonly string[] RESERVED_IDS = { "new", "import", "export", "aggregate", "children-of" };

        /// <summary>
        /// All inheriting classes will have a strongly typed Id property that is usually an int or a string,
        /// this method returns either one as an object, it is useful for logic that performs reflection
        /// </summary>
        public abstract object GetId();

        /// <summary>
        /// All inheriting classes will have a strongly typed Id property that is usually an int or a string,
        /// this method allows setting either one as an object, which is faster than doing it via reflection
        /// </summary>
        public abstract void SetId(object id);
    }

    /// <summary>
    /// Base class of all entities that have an Id property
    /// </summary>
    /// <typeparam name="TKey">The type of the Id property</typeparam>
    public abstract class EntityWithKey<TKey> : EntityWithKey, IValidatableObject
    {
        /// <summary>
        /// This is an integer for entities that have a simple integer Id in the SQL database,
        /// and a string for anything else (The string can encode composite keys for example) 
        /// it is important to have a single Id property for tracking HTTP resources as it simplifies
        /// so much shared logic for tracking resources and caching them
        /// </summary>
        public TKey Id { get; set; }

        // The below method is used by implementations that benefit from a generic object Id, such as Object Loader

        private object _id;
        public override object GetId()
        {
            if(_id == null) // Optimization: if statement is faster than boxing TKey Id into Object every single time
            {
                _id = Id;
            }

            return _id;
        }

        public override void SetId(object id)
        {
            Id = (TKey)id;
            _id = id;
        }

        public virtual IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            // This ensures that no Id is ever stored that is one of the reserved words
            if (typeof(TKey) == typeof(string))
            {
                string id = GetId()?.ToString();
                if (id != null && RESERVED_IDS.Any(ri => id.Equals(ri)))
                {
                    var localizer = validationContext.GetRequiredService<IStringLocalizer<Strings>>();
                    var errorMessage = localizer["Error_TheFollowingKeyWordsAreReserved0", string.Join(", ", RESERVED_IDS)];
                    var memberNames = new string[] { nameof(Id) };

                    yield return new ValidationResult(errorMessage, memberNames);
                }
            }
        }
    }
}
