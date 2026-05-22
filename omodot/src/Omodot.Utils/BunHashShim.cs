using System.IO.Hashing;
using System.Text;

namespace Omodot.Utils;

public static class BunHashShim
{
    public static uint BunHashXxh32(string input, int seed) => XxHash32.HashToUInt32(Encoding.UTF8.GetBytes(input), seed);
}
