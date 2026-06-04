using Amazon.SecretsManager.Model;
using Amazon.SecretsManager;
using Amazon;
using System.Text.Json;

namespace PersonalFinanceBudgetTrackerAPI.Extensions
{
     /// <summary>
    /// Adds AWS Secrets Manager as an <see cref="IConfiguration"/> source
    /// at application startup.
    ///
    /// Configuration source — ECS Task Environment Variables
    /// ───────────────────────────────────────────────────────
    /// The secret name and AWS region are read exclusively from OS / ECS Task
    /// environment variables.  They must NEVER come from appsettings.json so
    /// that different ECS task definitions (Production, Staging, Dev) can each
    /// point to a different secret without any code or file change.
    ///
    ///   FINANCEAPP_SECRET_NAME   e.g.  financeapp/Production/config
    ///   FINANCEAPP_AWS_REGION    e.g.  ap-south-1
    ///
    /// Both variables are REQUIRED in Production and Staging.
    /// In Development, if either is absent, a warning is printed and the
    /// extension falls back gracefully to appsettings values — letting
    /// developers run the API locally without AWS credentials.
    ///
    /// Secret value expected in Secrets Manager
    /// ──────────────────────────────────────────
    /// A single JSON object whose keys use double-underscore (__) as the
    /// hierarchy separator (AWS does not allow colons in JSON keys).
    /// The extension converts __ → : when injecting into IConfiguration.
    ///
    ///   {
    ///     "ConnectionStrings__DefaultConnection": "Host=rds.example.com;...",
    ///     "JwtSettings__SecretKey":               "min-32-char-secret",
    ///     "JwtSettings__Issuer":                  "FinanceApp",
    ///     "JwtSettings__Audience":                "FinanceAppUsers",
    ///     "JwtSettings__ExpiryMinutes":            "60",
    ///     "ImportSettings__ApiKey":               "lambda-api-key"
    ///   }
    ///
    /// IAM permissions required by the ECS Task Role
    ///   secretsmanager:GetSecretValue  on the secret ARN
    ///   kms:Decrypt                    if a customer-managed KMS key is used
    /// </summary>
    public static class AwsSecretsManagerExtensions
    {
        // ── ECS Task environment variable names ──────────────────────────────
        // These are set in the ECS Task Definition → Container → Environment.
        // They are NOT read from appsettings.json.

        /// <summary>ECS Task env-var: full Secrets Manager secret name.</summary>
        public const string EnvSecretName = "SECRET_NAME";

        /// <summary>ECS Task env-var: AWS region where the secret lives.</summary>
        public const string EnvAwsRegion  = "AWS_REGION";

        // ── Hardcoded fallback region (last resort) ───────────────────────────
        // Used only when FINANCEAPP_AWS_REGION is not set AND the environment
        // is Development — keeps local dev smooth without any config required.
        private const string FallbackRegion = "ap-south-1";   // Mumbai

        /// <summary>
        /// Resolves the secret name and AWS region from ECS Task environment
        /// variables, fetches the secret from AWS Secrets Manager, and merges
        /// all key-value pairs into the .NET configuration pipeline.
        ///
        /// Call this before any code that reads secrets, e.g.:
        /// <code>
        ///   builder.Configuration.AddAwsSecretsManager(builder.Environment);
        /// </code>
        /// </summary>
        /// <param name="configBuilder">The configuration builder to add the source to.</param>
        /// <param name="environment">Used only to determine whether missing
        /// env-vars should be fatal (Production/Staging) or just a warning (Development).</param>
        public static IConfigurationBuilder AddAwsSecretsManager(
            this IConfigurationBuilder configBuilder,
            IHostEnvironment           environment)
        {
            // ── Read environment variables set by the ECS Task Definition ────
            string? secretName = Environment.GetEnvironmentVariable(EnvSecretName);
            string? region     = Environment.GetEnvironmentVariable(EnvAwsRegion);

            // ── Validate ──────────────────────────────────────────────────────
            bool isDevelopment = environment.IsDevelopment();

            if (string.IsNullOrWhiteSpace(secretName))
            {
                string message =
                    $"[AwsSecretsManager] Environment variable '{EnvSecretName}' is not set. " +
                    (isDevelopment
                        ? "Falling back to appsettings.json for local development."
                        : $"This variable is REQUIRED in {environment.EnvironmentName}. " +
                          "Set it in the ECS Task Definition under Container → Environment.");

                Console.Error.WriteLine(message);

                if (!isDevelopment)
                    throw new InvalidOperationException(message);

                // Development only: skip Secrets Manager entirely
                return configBuilder;
            }

            if (string.IsNullOrWhiteSpace(region))
            {
                string message =
                    $"[AwsSecretsManager] Environment variable '{EnvAwsRegion}' is not set. " +
                    (isDevelopment
                        ? $"Using fallback region '{FallbackRegion}' for local development."
                        : $"This variable is REQUIRED in {environment.EnvironmentName}. " +
                          "Set it in the ECS Task Definition under Container → Environment.");

                Console.Error.WriteLine(message);

                if (!isDevelopment)
                    throw new InvalidOperationException(message);

                region = FallbackRegion;
            }

            // ── Log what is being loaded (secret name only — never the value) ─
            Console.WriteLine(
                $"[AwsSecretsManager] Loading secret '{secretName}' " +
                $"from region '{region}' (env: {environment.EnvironmentName}).");

            // ── Fetch the secret ──────────────────────────────────────────────
            var secretJson = FetchSecretAsync(secretName, region, isDevelopment)
                .GetAwaiter()
                .GetResult();   // Sync-over-async is safe here: runs once at startup
                                // before the HTTP pipeline is built.

            if (string.IsNullOrWhiteSpace(secretJson))
                return configBuilder;   // FetchSecretAsync already logged the reason

            // ── Parse JSON → flat IConfiguration key-value pairs ─────────────
            var secretValues = FlattenJson(secretJson);

            Console.WriteLine(
                $"[AwsSecretsManager] Loaded {secretValues.Count} configuration " +
                $"key(s) from secret '{secretName}'.");

            // ── Inject into pipeline (highest precedence) ─────────────────────
            // AddInMemoryCollection added LAST has the HIGHEST precedence, so
            // Secrets Manager values override anything in appsettings.json.
            configBuilder.AddInMemoryCollection(secretValues!);

            return configBuilder;
        }

        // ═════════════════════════════════════════════════════════════════════
        //  Private helpers
        // ═════════════════════════════════════════════════════════════════════

        private static async Task<string?> FetchSecretAsync(
            string secretName,
            string region,
            bool   isDevelopment)
        {
            try
            {
                var client   = new AmazonSecretsManagerClient(RegionEndpoint.GetBySystemName(region));
                var request  = new GetSecretValueRequest { SecretId = secretName };
                var response = await client.GetSecretValueAsync(request);

                // AWS returns either SecretString (JSON text) or SecretBinary
                return response.SecretString
                    ?? Convert.ToBase64String(response.SecretBinary.ToArray());
            }
            catch (ResourceNotFoundException ex)
            {
                string message =
                    $"[AwsSecretsManager] Secret '{secretName}' was not found " +
                    $"in region '{region}'. ({ex.Message})";

                if (isDevelopment)
                {
                    Console.Error.WriteLine(message + " Falling back to appsettings.json.");
                    return null;
                }

                throw new InvalidOperationException(message, ex);
            }
            catch (AmazonSecretsManagerException ex)
            {
                // Covers AccessDeniedException, InvalidRequestException, etc.
                throw new InvalidOperationException(
                    $"[AwsSecretsManager] Failed to retrieve secret '{secretName}' " +
                    $"from region '{region}'. Verify the ECS Task Role has " +
                    $"secretsmanager:GetSecretValue permission. ({ex.Message})", ex);
            }
        }

        /// <summary>
        /// Recursively flattens a JSON object into IConfiguration-compatible
        /// key-value pairs.  Double-underscore (__) in key names is converted to
        /// a colon (:) to match the .NET configuration hierarchy separator.
        ///
        /// Supported input styles:
        ///   Flat:   { "JwtSettings__SecretKey": "abc" }
        ///   Nested: { "JwtSettings": { "SecretKey": "abc" } }
        ///   Both produce the same output key: JwtSettings:SecretKey
        /// </summary>
        private static Dictionary<string, string?> FlattenJson(string json)
        {
            var result = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

            using var document = JsonDocument.Parse(json);
            FlattenElement(document.RootElement, prefix: string.Empty, result);

            return result;
        }

        private static void FlattenElement(
            JsonElement                element,
            string                     prefix,
            Dictionary<string, string?> result)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    foreach (var property in element.EnumerateObject())
                    {
                        string key = string.IsNullOrEmpty(prefix)
                            ? property.Name.Replace("__", ":")
                            : $"{prefix}:{property.Name.Replace("__", ":")}";

                        FlattenElement(property.Value, key, result);
                    }
                    break;

                case JsonValueKind.Array:
                    int index = 0;
                    foreach (var item in element.EnumerateArray())
                    {
                        FlattenElement(item, $"{prefix}:{index}", result);
                        index++;
                    }
                    break;

                default:
                    result[prefix] = element.ValueKind == JsonValueKind.Null
                        ? null
                        : element.ToString();
                    break;
            }
        }
    }

}
