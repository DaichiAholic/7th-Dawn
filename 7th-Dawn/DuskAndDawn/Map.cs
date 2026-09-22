using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DuskAndDawn
{
    public class NightMap
    {
        public List<List<MapNode>> Layers { get; } = new List<List<MapNode>>();

        public NightMap(RoomGenerator roomGenerator, int layerCount, int nodesPerLayer)
        {
            for (int depth = 0; depth < layerCount; depth++)
            {
                var layer = new List<MapNode>();
                for (int i = 0; i < nodesPerLayer; i++)
                {
                    layer.Add(new MapNode(roomGenerator.PickNext(depth + 1), depth));
                }
                Layers.Add(layer);
            }
        }
    }
}
