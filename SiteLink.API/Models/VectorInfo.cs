namespace SiteLink.API.Models
{
    public class VectorInfo
    {
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }

        public Vector3 ToVector() => new Vector3(X, Y, Z);
    }
}
