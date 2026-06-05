using System.Text;
using System.Text.Json;
using Xunit;

namespace DotnetOpenEhr.Serialization.Json.Flat.Tests;

/// <summary>
/// L6 (0604-04): the new
/// <see cref="FlatJsonReader.Read(ReadOnlyMemory{byte})"/> overload
/// must produce the same entry sequence as the
/// <see cref="FlatJsonReader.Read(ReadOnlySpan{byte})"/> overload, but
/// without the <c>ReadOnlySpan&lt;byte&gt;.ToArray()</c> copy.
/// </summary>
public sealed class FlatJsonReaderTests
{
    [Fact]
    public void ReadOnlyMemory_overload_returns_same_entries_as_span_overload()
    {
        string flat = """
            {
              "demo/context/start_time": "2024-05-27T10:25:03",
              "demo/category|code": "433",
              "demo/category|value": "event",
              "demo/category|terminology": "openehr"
            }
            """;
        byte[] bytes = Encoding.UTF8.GetBytes(flat);

        IReadOnlyList<KeyValuePair<FlatPath, JsonElement>> viaSpan = FlatJsonReader.Read(bytes.AsSpan());
        IReadOnlyList<KeyValuePair<FlatPath, JsonElement>> viaMemory = FlatJsonReader.Read(new ReadOnlyMemory<byte>(bytes));

        Assert.Equal(viaSpan.Count, viaMemory.Count);
        for (int i = 0; i < viaSpan.Count; i++)
        {
            Assert.Equal(viaSpan[i].Key.OriginalForm, viaMemory[i].Key.OriginalForm);
            Assert.Equal(viaSpan[i].Value.ToString(), viaMemory[i].Value.ToString());
            Assert.Equal(viaSpan[i].Value.ValueKind, viaMemory[i].Value.ValueKind);
        }
    }
}
