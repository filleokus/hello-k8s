using System.Collections;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration.Json;

var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

app.MapGet("/", async (context) =>
{
  var htmlContent = await File.ReadAllTextAsync("wwwroot/index.html");
  var rows = GetConfiguration(app.Configuration).Select(
    param => $"<tr><td>{param.Name}</td><td>{param.Value}</td><td>{param.Source}</td></tr>"
    ).Aggregate(new StringBuilder(), (acc, row) => acc.Append(row)).ToString();
  htmlContent = htmlContent.Replace("{{configurationRows}}", rows);
  var podname = Environment.GetEnvironmentVariable("HOSTNAME") ?? "Kubernetes";
  htmlContent = htmlContent.Replace("{{podName}}", podname);
  foreach (var (color, index) in RandomColors(podname).Select((color, index) => (color, index)))
  {
    htmlContent = htmlContent.Replace($"#fffff{index}", color);
  }

  context.Response.ContentType = "text/html";
  await context.Response.WriteAsync(htmlContent);
});

app.MapGet("/api/env", () =>
{
  var configuration = GetConfiguration(app.Configuration);
  return Results.Ok(configuration);
});

app.Run();

static List<Parameter> GetConfiguration(IConfiguration configuration)
{
  var configurationList = new List<Parameter>();
  foreach (DictionaryEntry envVar in Environment.GetEnvironmentVariables())
  {
    if (envVar.Key.ToString() is not null)
      configurationList.Add(new Parameter(envVar.Key.ToString()!, envVar.Value?.ToString() ?? string.Empty, "Environment variable"));
  }

  if (configuration is IConfigurationRoot configRoot)
  {
    var providers = configRoot.Providers
        .Where(provider => !provider.ToString()?.Contains("EnvironmentVariablesConfigurationProvider") ?? true);

    var parameters = providers.SelectMany(provider =>
    {
      // Determine the provider name
      string providerName = provider.ToString() ?? "Unknown provider";
      if (provider is JsonConfigurationProvider jsonProvider && jsonProvider.Source.Path != null)
      {
        providerName = jsonProvider.Source.Path;
      }

      // Use the recursive method to get all parameters
      return GetAllKeys(provider, null, providerName);
    });

    configurationList.AddRange(parameters);
  }

  return configurationList;
}

static IEnumerable<Parameter> GetAllKeys(
    IConfigurationProvider provider,
    string? parentPath,
    string providerName)
{
  // Get the distinct child keys at the current path
  var childKeys = provider.GetChildKeys(Enumerable.Empty<string>(), parentPath).Distinct();

  foreach (var key in childKeys)
  {
    // Construct the full key path
    var fullKey = string.IsNullOrEmpty(parentPath) ? key : $"{parentPath}:{key}";

    // Try to get the value for the full key path
    if (provider.TryGet(fullKey, out var value))
    {
      yield return new Parameter(fullKey, value ?? string.Empty, providerName);
    }

    // Recursively get keys for nested configurations
    foreach (var subParam in GetAllKeys(provider, fullKey, providerName))
    {
      yield return subParam;
    }
  }
}


static List<string> RandomColors(string input)
{
  const int numberOfColors = 5;
  var colors = new List<string>();

  int hash = 100;
  foreach (char c in input)
  {
    hash = hash * 31 + c;
  }
  int baseHue = Math.Abs(hash) % 360;
  int saturation = 60 + Math.Abs(hash) % 20; // 60-80%
  int lightness = 40 + Math.Abs(hash) % 20; // 40-60%

  int hueStep = 360 / numberOfColors;

  for (int i = 0; i < numberOfColors; i++)
  {
    int hue = (baseHue + hueStep * i + 180 * (i % 2)) % 360;

    string color = $"hsl({hue}, {saturation}%, {lightness}%)";
    colors.Add(color);
  }

  return colors;
}

record Parameter(string Name, string Value, string? Source);