using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace BrowserContradictions.Web;

/// <summary>
/// The per window inputs of the parameterised browser tests, derived from a
/// server secret, and the tag the gatherer puts on a value it measured with
/// them. The client never sees the secret: the server sends it the derived
/// inputs and reads the tagged values back. A window is a 5 second slot.
/// The Python twin is gatherer/lab/textoracle/derive.py.
/// </summary>
public static class WindowParams
{
    public const int WindowSeconds = 5;

    private const string Alphabet =
        "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789 .,;:!?@&#%()-+=/";

    private static readonly string[] ShapePool =
        "g Q @ & R 3 B a e $ k m s w y z % ? # ß ą ж é ø ç ł".Split(' ');

    private const int GlyphsLength = 24;

    public static readonly Params Defaults = new(
        "The quick brown fox jumps over 1234567890 lazy dogs", 16,
        ["g", "@", "&", "ß", "ą", "ж", "R", "3"],
        [2654435769u, 2135587861u]);

    public sealed record Params(string Glyphs, int Size, string[] Shapes, uint[] Walk);

    public static long Window(DateTimeOffset at) => at.ToUnixTimeSeconds() / WindowSeconds;

    public static Params Derive(byte[] secret, long window)
    {
        var mac = HMACSHA256.HashData(secret, WindowBytes(window));
        var stream = new byte[4 * 32];
        for (var i = 0; i < 4; i++)
        {
            byte[] block = [.. mac, (byte)i];
            HMACSHA256.HashData(secret, block).CopyTo(stream, i * 32);
        }
        var glyphs = new StringBuilder(GlyphsLength);
        for (var i = 0; i < GlyphsLength; i++)
        {
            glyphs.Append(Alphabet[stream[i] % Alphabet.Length]);
        }
        var size = 12 + stream[GlyphsLength] % 12;
        var pool = ShapePool.ToList();
        var shapes = new string[8];
        for (var i = 0; i < 8; i++)
        {
            var at = stream[GlyphsLength + 1 + i] % pool.Count;
            shapes[i] = pool[at];
            pool.RemoveAt(at);
        }
        var w1 = (uint)(stream[40] << 24 | stream[41] << 16 | stream[42] << 8 | stream[43]);
        var w2 = (uint)(stream[44] << 24 | stream[45] << 16 | stream[46] << 8 | stream[47]);
        return new Params(glyphs.ToString(), size, shapes, [w1, w2]);
    }

    /// <summary>The tag each parameterised test puts on its value.</summary>
    public static Dictionary<string, string> Tags(Params p) => new()
    {
        ["f_metrics.1"] = Tag(Json([p.Glyphs, p.Size]), Json([Defaults.Glyphs, Defaults.Size])),
        ["f_segoeshape.2"] = Tag(Json([p.Shapes]), Json([Defaults.Shapes])),
        ["m_tanhwalk.1"] = Tag(Json([p.Walk]), Json([Defaults.Walk])),
    };

    /// <summary>
    /// The value without its tag when the tag is the one for these params,
    /// otherwise null (a value measured with other inputs, or a default
    /// value sent to a window that issued its own).
    /// </summary>
    public static string Untag(string value, string tag)
    {
        if (tag.Length == 0)
        {
            return value.StartsWith('p') && value.Length > 9 && value[9] == ':' ? null : value;
        }
        return value.StartsWith(tag, StringComparison.Ordinal) ? value[tag.Length..] : null;
    }

    private static string Tag(string used, string defaults) =>
        used == defaults ? "" : "p" + Fnv1a32(used) + ":";

    private static readonly JsonSerializerOptions JsOptions = new()
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    // JSON.stringify's output: no spaces, non ASCII characters as themselves.
    private static string Json(object[] inputs) => JsonSerializer.Serialize(inputs, JsOptions);

    private static byte[] WindowBytes(long w)
    {
        var b = new byte[8];
        for (var i = 7; i >= 0; i--) { b[i] = (byte)w; w >>= 8; }
        return b;
    }

    private static string Fnv1a32(string text)
    {
        var h = 0x811c9dc5u;
        foreach (var ch in text)
        {
            h ^= ch;
            h = unchecked(h * 0x01000193u);
        }
        return h.ToString("x8");
    }
}
