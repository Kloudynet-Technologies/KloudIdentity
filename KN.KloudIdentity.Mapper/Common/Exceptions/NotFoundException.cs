//------------------------------------------------------------
// Copyright (c) Kloudynet Technologies Sdn Bhd.  All rights reserved.
//------------------------------------------------------------

namespace KN.KloudIdentity.Mapper.Common.Exceptions
{
    /// <summary>
    /// Represents an exception that is thrown when an entity is not found.
    /// </summary>
    public class NotFoundException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the NotFoundException class with a specific entity name and key.
        /// </summary>
        /// <param name="name">The name of the entity.</param>
        /// <param name="key">The key associated with the entity.</param>
        public NotFoundException(string name, object key)
            : base($"Entity \"{name}\" ({key}) was not found.") { }

        /// <summary>
        /// Initializes a new instance of the NotFoundException class with a specific message.
        /// </summary>
        /// <param name="message">The error message that explains the reason for the exception.</param>
        public NotFoundException(string message)
            : base(message) { }
    }
}
