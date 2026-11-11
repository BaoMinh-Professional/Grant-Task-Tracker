using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace GrantTracker.Core.Idempotency;

public static class RequestHasher
{
    private static readonly JsonSerializerOptions Options = new()
    {
        TypeInfoResolver = new DefaultJsonTypeInfoResolver
        {
            Modifiers = { typeInfo =>
            {
                if (typeInfo.Kind == System.Text.Json.Serialization.Metadata.JsonTypeInfoKind.Object)
                    for (var i = typeInfo.Properties.Count - 1; i >= 0; i--)
                        if (typeInfo.Properties[i].Name == nameof(IIdempotentCommand.IdempotencyKey))
                            typeInfo.Properties.RemoveAt(i);
            } }
        }
    };

    public static string Hash<T>(T request) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(request, Options))));
}
