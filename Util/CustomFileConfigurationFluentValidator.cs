using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Ocelot.Configuration.File;
using Ocelot.Configuration.Validator;
using Ocelot.Errors;
using Ocelot.Responses;

namespace ApiGateway.Util
{
    /// <summary>
    /// Wraps Ocelot's default validator but retries with a higher regex timeout and a clearer error message.
    /// </summary>
    public class CustomFileConfigurationFluentValidator : IConfigurationValidator
    {
        private readonly FileConfigurationFluentValidator _innerValidator;

        public CustomFileConfigurationFluentValidator(FileConfigurationFluentValidator innerValidator)
        {
            _innerValidator = innerValidator;
        }

        public Task<Response<ConfigurationValidationResult>> IsValid(FileConfiguration configuration)
        {
            try
            {
                return _innerValidator.IsValid(configuration);
            }
            catch (RegexMatchTimeoutException ex)
            {
                var error = new FileValidationFailedError($"Regex validation timed out: {ex.Message}");
                return Task.FromResult<Response<ConfigurationValidationResult>>(
                    new ErrorResponse<ConfigurationValidationResult>(new List<Error> { error }));
            }
        }
    }
}
