using System.Collections;
using System.Collections.Generic;

namespace Ribbanya.Utilities.Tests {
  public sealed class OverloadCreationDataProvider : IEnumerable<object[]> {
    public IEnumerator<object[]> GetEnumerator() {
      yield return new object[] {
        nameof(OverloadCreationFeature.APlusBTimesCMinusD),
        "a,b,3,4",
        new object[] {3f, 4f},
        new object[] {1f, 2f}
      };
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
  }
}