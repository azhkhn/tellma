﻿namespace Tellma.Controllers.Dto
{
    /// <summary>
    /// DTO that carries the parameters for most Http GET all requests
    /// </summary>
    public class GetArguments
    {
        private const int DEFAULT_PAGE_SIZE = 50;

        /// <summary>
        /// Specifies the number of items the server should return
        /// </summary>
        public int Top { get; set; } = DEFAULT_PAGE_SIZE;

        /// <summary>
        /// Specifies how many items to skip before the returned collection
        /// </summary>
        public int Skip { get; set; } = 0;

        /// <summary>
        /// The name of the property to order the result by
        /// </summary>
        public string OrderBy { get; set; }

        /// <summary>
        /// A search string that is interpreted in a customized way by every controller
        /// </summary>
        public string Search { get; set; }

        /// <summary>
        /// An OData style filter string that is parsed into a Linq expression enabling a rich 
        /// query experience
        /// </summary>
        public string Filter { get; set; }

        /// <summary>
        /// Equivalent to linq's "Include", determines which related entities to include in 
        /// the result. If left empty then do not include any related entities
        /// </summary>
        public string Expand { get; set; }

        /// <summary>
        /// Equivalent to linq's "Select", determines which properties of the principal entities
        /// or of the included related entities to return the result. If left empty then all
        /// properties of the principalentity and included entities are returned
        /// </summary>
        public string Select { get; set; }
    }
}
