namespace SiteLink.API.Models
{
    public class BlockInfo
    {
        public string Name { get; set; }

        public int ObjectId { get; set; }
        public int ParentId { get; set; }

        public VectorInfo Position { get; set; }
        public VectorInfo Scale { get; set; }
        public VectorInfo Rotation { get; set; }

        public BlockType BlockType { get; set; }

        public Dictionary<string, object> Properties = new Dictionary<string, object>();
    }
}
