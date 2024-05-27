//------------------------------------------------------------
// Copyright (c) Kloudynet Technologies Sdn Bhd.  All rights reserved.
//------------------------------------------------------------

namespace KN.KloudIdentity.Mapper.Common.Models
{
    /// <summary>
    /// Represents a model for an error response.
    /// </summary>
    public class ErrorResponseModel
    {
        /// <summary>
        /// The status code associated with the error.
        /// </summary>
        public int Status { get; set; }

        /// <summary>
        /// The title of the error.
        /// </summary>
        public string Title { get; set; }

        /// <summary>
        /// Message describing the error.
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// Additional details about the error (nullable).
        /// </summary>
        public string? Details { get; set; }
    }
}
