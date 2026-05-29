using System.IO.Hashing;
using System.Text;

namespace Lfe.HashLine;

public static class XxHash32Utility
{
    public static uint HashXxh32(string input, int seed)
    {
        return XxHash32.HashToUInt32(Encoding.UTF8.GetBytes(input), seed);
    }
}
