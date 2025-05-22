using System;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace CMSAgent.Service.Commands.Models
{
    /// <summary>
    /// Utility class for handling parameter value retrieval and type conversion from CommandRequest.Parameters
    /// </summary>
    public static class CommandParameterHelper
    {
        /// <summary>
        /// Retrieves a value from parameters and converts it to type T
        /// </summary>
        /// <typeparam name="T">The desired data type</typeparam>
        /// <param name="parameters">Dictionary containing parameters</param>
        /// <param name="key">Key to retrieve the value</param>
        /// <param name="defaultValue">Default value if not found or conversion fails</param>
        /// <param name="logger">Logger for error logging (optional)</param>
        /// <returns>Converted value or defaultValue if conversion fails</returns>
        public static T? GetValue<T>(Dictionary<string, object>? parameters, string key, T? defaultValue = default, ILogger? logger = null)
        {
            if (parameters == null || !parameters.TryGetValue(key, out var value))
            {
                return defaultValue;
            }

            try
            {
                if (value is JsonElement jsonElement)
                {
                    return jsonElement.ValueKind switch
                    {
                        JsonValueKind.True => (T)(object)true,
                        JsonValueKind.False => (T)(object)false,
                        JsonValueKind.Number => (T)Convert.ChangeType(jsonElement.GetDecimal(), typeof(T)),
                        JsonValueKind.String => (T)Convert.ChangeType(jsonElement.GetString(), typeof(T)),
                        _ => defaultValue
                    };
                }

                return (T)Convert.ChangeType(value, typeof(T));
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "Error converting value for key '{Key}' to type {Type}", key, typeof(T).Name);
                return defaultValue;
            }
        }

        /// <summary>
        /// Retrieves a boolean value from parameters
        /// </summary>
        /// <param name="parameters">Dictionary containing parameters</param>
        /// <param name="key">Key to retrieve the value</param>
        /// <param name="defaultValue">Default value if not found or conversion fails</param>
        /// <param name="logger">Logger for error logging (optional)</param>
        /// <returns>Boolean value or defaultValue if conversion fails</returns>
        public static bool GetBool(Dictionary<string, object>? parameters, string key, bool defaultValue = false, ILogger? logger = null)
        {
            return GetValue(parameters, key, defaultValue, logger);
        }

        /// <summary>
        /// Retrieves an integer value from parameters
        /// </summary>
        /// <param name="parameters">Dictionary containing parameters</param>
        /// <param name="key">Key to retrieve the value</param>
        /// <param name="defaultValue">Default value if not found or conversion fails</param>
        /// <param name="logger">Logger for error logging (optional)</param>
        /// <returns>Integer value or defaultValue if conversion fails</returns>
        public static int GetInt(Dictionary<string, object>? parameters, string key, int defaultValue = 0, ILogger? logger = null)
        {
            return GetValue(parameters, key, defaultValue, logger);
        }

        /// <summary>
        /// Retrieves a string value from parameters
        /// </summary>
        /// <param name="parameters">Dictionary containing parameters</param>
        /// <param name="key">Key to retrieve the value</param>
        /// <param name="defaultValue">Default value if not found or conversion fails</param>
        /// <param name="logger">Logger for error logging (optional)</param>
        /// <returns>String value or defaultValue if conversion fails</returns>
        public static string? GetString(Dictionary<string, object>? parameters, string key, string? defaultValue = null, ILogger? logger = null)
        {
            return GetValue(parameters, key, defaultValue, logger);
        }

        /// <summary>
        /// Retrieves a double value from parameters
        /// </summary>
        /// <param name="parameters">Dictionary containing parameters</param>
        /// <param name="key">Key to retrieve the value</param>
        /// <param name="defaultValue">Default value if not found or conversion fails</param>
        /// <param name="logger">Logger for error logging (optional)</param>
        /// <returns>Double value or defaultValue if conversion fails</returns>
        public static double GetDouble(Dictionary<string, object>? parameters, string key, double defaultValue = 0.0, ILogger? logger = null)
        {
            return GetValue(parameters, key, defaultValue, logger);
        }
    }
} 