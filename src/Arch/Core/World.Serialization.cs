using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Arch.Core.Utils;
using Arch.LowLevel;
using Arch.LowLevel.Jagged;
namespace Arch.Core;

public interface IClonableComponent : ICloneable
{
    //public object Clone(Dictionary<object,object> managedObjectTable)
    //{
    //    ICloneable prop = new object() as ICloneable;
    //    ICloneable newProp = null;
    //    if (managedObjectTable.TryGetValue(prop,out var obj))
    //    {
    //        newProp = obj as ICloneable;
    //    }
    //    else
    //    {
    //        managedObjectTable[prop] = prop.Clone();
    //    }

    //}
}
public partial class World
{
    private World _syncingWorld = null;
    private Dictionary<Archetype, Archetype> _archetypeLut = new();
    public void SetSyncingSourceWorld(World another)
    {
        _syncingWorld = another;
        _archetypeLut.Clear();
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
        for (var i = 0; i < srcArchetypes.Count; i++)
        {
            var srcArche = srcArchetypes[i];
            var dstArche = GetOrCreate(srcArche.Types);
            _archetypeLut[srcArche] = dstArche;

            dstArche.Clear();
            dstArche.EnsureEntityCapacity(srcArche.EntityCapacity);

            dstArche.ChunkCount = srcArche.ChunkCount;
            dstArche.EntityCount = srcArche.EntityCount;

            // sync chunks in a archetype
            var j = 0;
            for (; j < srcArche.ChunkCount; j++)
            {
                ref var srcChunk = ref srcArche.GetChunk(j);
                ref var dstChunk = ref dstArche.GetChunk(j);
                SyncChunk(ref srcChunk, ref dstChunk, srcArche.Types);
            }
            //for (; j < dstArche.ChunkCount; j++)
            //{
            // Do some clean work here...
            //}
        }

        var dstEntityInfo = EntityInfo;
        var srcEntityInfo = _syncingWorld.EntityInfo;
        SyncJaggedArray(srcEntityInfo.Versions, dstEntityInfo.Versions);
        SyncEntitySlots(srcEntityInfo.EntitySlots, dstEntityInfo.EntitySlots, _archetypeLut);
        Size = _syncingWorld.Size;
        QueryCache.Clear();
    }


    private unsafe void SyncJaggedArray<T>(JaggedArray<T> srcArr, JaggedArray<T> dstArr) where T : unmanaged
    {
        dstArr.EnsureCapacity(srcArr.Capacity);
        int i = 0;
        for (; i < srcArr.Buckets; i++)
        {
            ref var srcBucket = ref srcArr.GetBucket(i);
            ref var dstBucket = ref dstArr.GetBucket(i);
            fixed (T* src = &srcBucket[0], dst = &dstBucket[0])
            {
                var bytesCount = srcBucket.Count * sizeof(T);
                System.Buffer.MemoryCopy(src, dst, bytesCount, bytesCount);
            }
            dstBucket.Count = srcBucket.Count;
        }
        for (; i < dstArr.Buckets; i++)
        {
            var dstBucket = dstArr.GetBucket(i);
            dstBucket.Count = 0;
        }
        //dstArr.TrimExcess(); // TODO: This may be harmful to performance
    }

    private void SyncEntitySlots(JaggedArray<EntitySlot> srcArr, JaggedArray<EntitySlot> dstArr, Dictionary<Archetype,Archetype> lut)
    {
        dstArr.EnsureCapacity(srcArr.Capacity);
        int i = 0;
        for (; i < srcArr.Buckets; i++)
        {
            ref var srcBucket = ref srcArr.GetBucket(i);
            ref var dstBucket = ref dstArr.GetBucket(i);
            for(int j=0; j< srcBucket.Count; j++)
            {
                dstBucket[j] = srcBucket[j];
                dstBucket[j].Archetype = lut[srcBucket[j].Archetype];
            }

            dstBucket.Count = srcBucket.Count;
        }

        for (; i < dstArr.Buckets; i++)
        {
            var dstBucket = dstArr.GetBucket(i);
            dstBucket.Count = 0;
        }

        //dstArr.TrimExcess(); // TODO: This may be harmful to performance
    }

    public void SyncChunk(ref Chunk srcChunk, ref Chunk dstChunk, ComponentType[] types)
    {
        Array.ConstrainedCopy(srcChunk.Entities, 0, dstChunk.Entities, 0, srcChunk.Size);
        for (var k = 0; k < types.Length; k++)
        {
            var srcType = types[k].Type;
            if (!typeof(ICloneable).IsAssignableFrom(srcType))
            {
                Array.ConstrainedCopy(srcChunk.Components[k], 0, dstChunk.Components[k], 0, srcChunk.Size);
            }
            else
            {
                CloneCloneableArray(srcChunk.Components[k], dstChunk.Components[k], srcChunk.Size);
            }
        }
        dstChunk.Size = srcChunk.Size;
    }

    private void CloneCloneableArray(Array src,Array dst,int length)
    {
        for (var i = 0;i < length; i++)
        {
            dst.SetValue((src.GetValue(i) as ICloneable)?.Clone(), i); 
        }
    }
}
