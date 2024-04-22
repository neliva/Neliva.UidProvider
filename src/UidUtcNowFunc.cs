// This is free and unencumbered software released into the public domain.
// See the UNLICENSE file in the project root for more information.

using System;

namespace Neliva
{
    /// <summary>
    /// Encapsulates a method that returns a <see cref="DateTime"/> object
    /// that is set to the current UTC date and time on this computer.
    /// </summary>
    /// <returns>
    /// An object whose value is the current UTC local date and time.
    /// </returns>
    /// <seealso cref="UidProvider"/>
    public delegate DateTime UidUtcNowFunc();
}