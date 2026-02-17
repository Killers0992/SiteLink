using AdminToys;
using Newtonsoft.Json;

namespace SiteLink.API.Misc
{
    public class Schematic
    {
        public int RootObjectId { get; set; }

        public List<BlockInfo> Blocks { get; set; } = new List<BlockInfo>();

        public static bool LoadFromFile(string path, out Schematic schematic)
        {
            schematic = null;

            if (!File.Exists(path))
                return false;

            string content = File.ReadAllText(path);
            schematic = JsonConvert.DeserializeObject<Schematic>(content);

            return schematic != null;
        }

        public bool Load(World world) => Load(world, null);

        public bool Load(World world, VectorInfo centerAt)
        {
            try
            {
                LoadInternal(world, centerAt);
                return true;
            }
            catch (Exception ex)
            {
                SiteLinkLogger.Error($"Failed to load schematic into world {world.Name}, error:\n{ex}", "Schematic");
                return false;
            }
        }

        private struct TRS
        {
            public UnityEngine.Vector3 Pos;
            public UnityEngine.Quaternion Rot;
            public UnityEngine.Vector3 Scale;
        }

        private void LoadInternal(World world, VectorInfo centerAt)
        {
            var byId = Blocks.ToDictionary(b => b.ObjectId);

            var worldTrs = new Dictionary<int, TRS>(Blocks.Count);

            TRS ResolveWorld(int objectId)
            {
                if (worldTrs.TryGetValue(objectId, out var cached))
                    return cached;

                if (!byId.TryGetValue(objectId, out var b))
                {
                    var identity = new TRS
                    {
                        Pos = UnityEngine.Vector3.zero,
                        Rot = UnityEngine.Quaternion.identity,
                        Scale = UnityEngine.Vector3.one
                    };
                    worldTrs[objectId] = identity;
                    return identity;
                }

                var localPos = b.Position?.ToVector() ?? UnityEngine.Vector3.zero;
                var localRot = b.Rotation?.EulerToQuaternion() ?? UnityEngine.Quaternion.identity;
                var localScale = b.Scale?.ToVector() ?? UnityEngine.Vector3.one;

                bool hasParent = b.ParentId != 0 && b.ParentId != b.ObjectId && byId.ContainsKey(b.ParentId);

                if (!hasParent)
                {
                    var rootTrs = new TRS { Pos = localPos, Rot = localRot, Scale = localScale };

                    worldTrs[objectId] = rootTrs;

                    return rootTrs;
                }

                var p = ResolveWorld(b.ParentId);

                var worldPos = p.Pos + (p.Rot * localPos);
                var worldRot = p.Rot * localRot;
                var worldScale = new UnityEngine.Vector3(
                    p.Scale.x * localScale.x,
                    p.Scale.y * localScale.y,
                    p.Scale.z * localScale.z
                );

                var trs = new TRS { Pos = worldPos, Rot = worldRot, Scale = worldScale };
                worldTrs[objectId] = trs;
                return trs;
            }

            foreach (var b in Blocks)
                _ = ResolveWorld(b.ObjectId);

            var min = new UnityEngine.Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
            var max = new UnityEngine.Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);

            foreach (var b in Blocks)
            {
                var p = worldTrs[b.ObjectId].Pos;
                min = UnityEngine.Vector3.Min(min, p);
                max = UnityEngine.Vector3.Max(max, p);
            }

            var schematicCenter = (min + max) * 0.5f;

            var offset = UnityEngine.Vector3.zero;
            if (centerAt != null)
                offset = centerAt.ToVector() - schematicCenter;

            foreach (var block in Blocks)
            {
                var trs = worldTrs[block.ObjectId];
                var spawnPos = trs.Pos + offset;

                switch (block.BlockType)
                {
                    case BlockType.Primitive:
                        {
                            var primitive = new PrimitiveObjectToyObject(world);

                            primitive.PrimitiveObjectToy.Position = spawnPos;
                            primitive.PrimitiveObjectToy.Scale = trs.Scale;
                            primitive.PrimitiveObjectToy.Rotation = trs.Rot;

                            primitive.PrimitiveObjectToy.PrimitiveFlags = PrimitiveFlags.Visible | PrimitiveFlags.Collidable;

                            if (block.Properties != null &&
                                block.Properties.TryGetValue("PrimitiveType", out object primType))
                            {
                                primitive.PrimitiveObjectToy.PrimitiveType =
                                    (PrimitiveType)Convert.ToInt32(primType);
                            }

                            if (block.Properties != null &&
                                block.Properties.TryGetValue("Color", out object primColor) &&
                                HtmlColorParser.TryParseHtmlString($"#{primColor}", out Color col))
                            {
                                primitive.PrimitiveObjectToy.MaterialColor = col;
                            }

                            break;
                        }

                    case BlockType.Light:
                        {
                            var light = new LightSourceToyObject(world);

                            light.LightSourceToy.Position = spawnPos;
                            light.LightSourceToy.Scale = trs.Scale;
                            light.LightSourceToy.Rotation = trs.Rot;

                            if (block.Properties != null &&
                                block.Properties.TryGetValue("Color", out object lightColor) &&
                                HtmlColorParser.TryParseHtmlString($"#{lightColor}", out Color lColor))
                            {
                                light.LightSourceToy.LightColor = lColor;
                            }

                            if (block.Properties != null &&
                                block.Properties.TryGetValue("Intensity", out object lightIntensity))
                                light.LightSourceToy.LightIntensity = Convert.ToSingle(lightIntensity);

                            if (block.Properties != null &&
                                block.Properties.TryGetValue("Range", out object lightRange))
                                light.LightSourceToy.LightRange = Convert.ToSingle(lightRange);

                            if (block.Properties != null &&
                                block.Properties.TryGetValue("Shadows", out object lightShadows))
                            {
                                bool shadows = (bool)lightShadows;
                                light.LightSourceToy.ShadowType = shadows ? LightShadows.Soft : LightShadows.None;
                            }

                            break;
                        }
                }
            }
        }
    }
}