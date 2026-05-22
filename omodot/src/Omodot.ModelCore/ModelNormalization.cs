using System.Text.RegularExpressions;

namespace Omodot.ModelCore;

public static class ModelNormalization
{
    public static string NormalizeModelID(string modelID) => modelID.Trim().ToLowerInvariant();
}
