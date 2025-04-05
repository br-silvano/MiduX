using System.Globalization;
using System.Resources;

namespace MiduX.Localization
{
    public static class Messages
    {
        private static readonly ResourceManager ResourceManager =
            new("MiduX.Resources.Messages", typeof(Messages).Assembly);

        public static string Get(string key, params object[] args)
        {
            var culture = CultureInfo.CurrentUICulture;
            var message = ResourceManager.GetString(key, culture) ?? $"[{key}]";
            return string.Format(message, args);
        }
    }
}
