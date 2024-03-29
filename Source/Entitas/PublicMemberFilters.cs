using System;

namespace Ribbanya.Utilities.Entitas {
  [Flags]
  public enum PublicMemberFilters {
    Any = ~MustRead & ~MustWrite,
    MustRead = 0b01,
    MustWrite = 0b10,
    MustReadWrite = 0b11
  }
}