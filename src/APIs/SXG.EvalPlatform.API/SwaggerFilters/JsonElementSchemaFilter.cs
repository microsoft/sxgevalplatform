using Microsoft.OpenApi.Models;
using Microsoft.OpenApi.Any;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Text.Json;

namespace SxgEvalPlatformApi.SwaggerFilters;

/// <summary>
/// Schema filter to properly display JsonElement properties in Swagger UI
/// </summary>
public class JsonElementSchemaFilter : ISchemaFilter
{
    public void Apply(OpenApiSchema schema, SchemaFilterContext context)
    {
        if (context.Type == typeof(JsonElement) || context.Type == typeof(JsonElement?))
        {
            schema.Type = "object";
            schema.Description = "Flexible JSON structure - can be an object, array, or any valid JSON";
            schema.Example = new OpenApiObject
            {
                ["example"] = new OpenApiArray
                {
                    new OpenApiObject
                    {
                        ["id"] = new OpenApiInteger(1),
                        ["question"] = new OpenApiString("What is the release year of Inception?"),
                        ["actualAnswer"] = new OpenApiString("The release year of the movie Inception is 2010."),
                        ["expectedAnswer"] = new OpenApiString("The release year of the movie Inception is 2010."),
                        ["metrics"] = new OpenApiObject
                        {
                            ["similarity"] = new OpenApiObject
                            {
                                ["similarity"] = new OpenApiDouble(5.0),
                                ["similarity_result"] = new OpenApiString("pass"),
                                ["similarity_threshold"] = new OpenApiInteger(3)
                            }
                        }
                    }
                }
            };
        }
    }
}