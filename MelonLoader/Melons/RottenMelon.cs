using System;
using System.Reflection;

namespace MelonLoader
{
    /// <summary>
    /// An info class for broken Melons.
    /// </summary>
    public sealed class RottenMelon
    {
        public readonly Assembly assembly;
        public readonly Type type;
        public readonly string errorMessage;
        public readonly string exception;

        public RottenMelon(Assembly assembly, string errorMessage, Exception exception = null)
        {
            this.assembly = assembly;
            this.errorMessage = errorMessage;
            this.exception = exception.ToString();
        }

        public RottenMelon(Assembly assembly, string errorMessage, string exception = null)
        {
            this.assembly = assembly;
            this.errorMessage = errorMessage;
            this.exception = exception;
        }

        public RottenMelon(Type type, string errorMessage, Exception exception = null)
        {
            assembly = type.Assembly;
            this.type = type;
            this.errorMessage = errorMessage;
            this.exception = exception.ToString();
        }

        public RottenMelon(Type type, string errorMessage, string exception = null)
        {
            assembly = type.Assembly;
            this.type = type;
            this.errorMessage = errorMessage;
            this.exception = exception;
        }
    }
}
