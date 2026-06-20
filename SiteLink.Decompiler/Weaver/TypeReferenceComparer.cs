using Mono.CecilX;

namespace SiteLink.Decompiler.Weaver
{
    public class TypeReferenceComparer : IEqualityComparer<TypeReference>
    {
        public bool Equals(TypeReference x, TypeReference y) =>
            x.FullName == y.FullName;

        public int GetHashCode(TypeReference obj) =>
            obj.FullName.GetHashCode();
    }
}
