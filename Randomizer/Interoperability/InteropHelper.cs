using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Celeste.Mod.Randomizer.Interoperability
{
    internal static class InteropHelper
    {

        /// <summary>
        /// Throws an error describing a misuse of the ModInterop API
        /// </summary>
        /// <param name="arg">the object that was passed in place of the settings</param>
        /// <returns>never</returns>
        /// <exception cref="ArgumentException">Thrown always with a formatted error message</exception>
        internal static T TypeError<T>(object arg)
        {
            throw new ArgumentException($"Randomizer.Interop: received an object of type '{arg?.GetType()?.Name ?? "null"}', expected {typeof(T).Name}");
        }

    }
}
