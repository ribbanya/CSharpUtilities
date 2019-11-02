using JetBrains.Annotations;

namespace Ribbanya.Utilities {
  [PublicAPI]
  public static class UtilityHelper {
    public static void Swap<T>(ref T a, ref T b) {
      var _ = a;
      a = b;
      b = _;
    }
  }
}