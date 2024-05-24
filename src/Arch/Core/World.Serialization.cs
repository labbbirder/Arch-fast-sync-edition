using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Arch.LowLevel;
namespace Arch.Core;

public partial class World
{
    private World _syncingWorld = null;
    public void SetSyncingSourceWorld(World another)
    {
        _syncingWorld = another;
    }

    public void Sync()
    {
        Debug.Assert(_syncingWorld != null, "Syncing world is null, you must set one by SetSyncingSourceWorld first");

        // sync remove ids
        RecycledIds.Clear();
        foreach(var id in _syncingWorld.RecycledIds)
        {
            RecycledIds.Enqueue(id);
        }

        var dstArchetypes = Archetypes;
        var srcArchetypes = _syncingWorld.Archetypes;
        for(var i = srcArchetypes.Count - 1; i >= 0; i--)
        {
            var srcArche = srcArchetypes[i];
            var dstArche = GetOrCreate(srcArche.Types);
            dstArche.Clear();
            dstArche.EnsureEntityCapacity(srcArche.ChunkCapacity);
            var j = 0;
            for (; j < srcArche.ChunkCount; j++)
            {
                var srcChunk = srcArche.GetChunk(i);
                var dstChunk = dstArche.GetChunk(i);
                Array.ConstrainedCopy(srcChunk.Entities, 0, dstChunk.Entities, 0, srcChunk.Size);
                for (var k = 0; k < srcArche.Types.Length; k++)
                {
                    Array.ConstrainedCopy(srcChunk.Components[k], 0, dstChunk.Components[k], 0, srcChunk.Size);
                }
                dstChunk.Size = srcChunk.Size;
            }
            dstArche.ChunkCount = srcArche.ChunkCount;
            //for (; j < dstArche.ChunkCount; j++)
            //{
                // Do some clean work here...
            //}
        }


    }
}
