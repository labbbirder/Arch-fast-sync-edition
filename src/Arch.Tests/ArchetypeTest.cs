using Arch.Core;
using Arch.Core.Utils;
using static NUnit.Framework.Assert;

namespace Arch.Tests;

/// <summary>
///     Simulates a heavy/large component for testing purposes.
/// </summary>
internal unsafe struct HeavyComponent
{
    private fixed double _items[18];
}

[TestFixture]
public class ArchetypeTest
{
    private readonly ComponentType[] _group = { typeof(Transform), typeof(Rotation) };
    private readonly ComponentType[] _otherGroup = { typeof(Transform), typeof(Rotation), typeof(Ai) };
    private readonly ComponentType[] _heavyGroup = { typeof(Transform), typeof(Rotation), typeof(HeavyComponent) };

    /// <summary>
    ///     Tests if <see cref="Archetype"/>s and their <see cref="Chunk"/> are created correctly.
    /// </summary>
    [Test]
    public void CreateChunk()
    {
        // Create archetype
        var archetype = new Archetype(_group);
        var entities = archetype.CalculateEntitiesPerChunk(_group);

        // Fill archetype
        for (var index = 0; index < entities; index++)
        {
            var entity = new Entity(index, 0);
            archetype.Add(entity, out _);
        }

        That(archetype.Size, Is.EqualTo(1));  // Since we filled it with n entities, it must have one single chunk.
    }

    /// <summary>
    ///     Checks if the <see cref="Archetype.ChunkSize"/> increases when <see cref="Entity"/>s and their components become too large.
    /// </summary>
    [Test]
    public void ScaleChunkCapacity()
    {
        var archetype = new Archetype(_heavyGroup);
        That(archetype.ChunkSize, Is.EqualTo(Archetype.BaseSize * 2)); // heavyGroup should be large enough to force the chunk to pick a 32KB chunk instead of 16KB
    }

    /// <summary>
    ///     Checks if an <see cref="Archetype"/> successfully creates multiple <see cref="Chunk"/>.
    /// </summary>
    [Test]
    public void CreateMultipleChunk()
    {
        var archetype = new Archetype(_group);
        var entities = archetype.CalculateEntitiesPerChunk(_group) * 2;

        for (var index = 0; index < entities; index++)
        {
            var entity = new Entity(index, 0);
            archetype.Add(entity, out _);
        }

        That(archetype.Size, Is.EqualTo(2));
    }

    /// <summary>
    ///     Checks if an <see cref="Archetype"/> is able to reserve enough memory for a number of <see cref="Entity"/>s and their components.
    /// </summary>
    [Test]
    public void Reserve()
    {
        var archetype = new Archetype(_group);
        var entities = archetype.CalculateEntitiesPerChunk(_group) * 10;
        archetype.Reserve(entities);

        for (var index = 0; index < entities; index++)
        {
            var entity = new Entity(index, 0);
            archetype.Add(entity, out _);
        }

        That(archetype.Size, Is.EqualTo(10));
        That(archetype.Capacity, Is.EqualTo(10));
    }

    /// <summary>
    ///     Checks if removing an <see cref="Entity"/> from the <see cref="Archetype"/> causes another <see cref="Entity"/> to move to that position.
    /// </summary>
    [Test]
    public void RemoveFromChunkWithReplacement()
    {
        var archetype = new Archetype(_group);
        var entities = archetype.CalculateEntitiesPerChunk(_group) + 50;

        for (var index = 0; index < entities; index++)
        {
            var entity = new Entity(index, 0);
            archetype.Add(entity, out _);
        }

        var slot = new Slot(0, 0);
        archetype.Remove(ref slot, out _);

        That(archetype.Size, Is.EqualTo(2));
        That(archetype.Capacity, Is.EqualTo(2));
        That(archetype.Chunks[0].Size, Is.EqualTo(entities - 50));
        That(archetype.Chunks[1].Size, Is.EqualTo(49));
        That(archetype.Chunks[0].Entities[0].Id, Is.EqualTo(archetype.CalculateEntitiesPerChunk(_group) + 50 - 1)); // Last entity from second chunk now replaced the removed entity and is in the first chunk
    }

    /// <summary>
    ///     Checks whether empty <see cref="Chunk"/>s are deleted and their effect on <see cref="Archetype"/> capacity.
    /// </summary>
    [Test]
    public void RemoveChunk()
    {
        var archetype = new Archetype(_group);
        var entities = archetype.CalculateEntitiesPerChunk(_group) + 1;

        for (var index = 0; index < entities; index++)
        {
            var entity = new Entity(index, 0);
            archetype.Add(entity, out _);
        }

        var slot = new Slot(0, 0);
        archetype.Remove(ref slot, out _);

        That(archetype.Size, Is.EqualTo(1));
        That(archetype.Capacity, Is.EqualTo(2));
        That(archetype.Chunks[0].Size, Is.EqualTo(entities - 1));
        That(archetype.Chunks[0].Entities[0].Id, Is.EqualTo(archetype.CalculateEntitiesPerChunk(_group))); // Last entity from second chunk now replaced the removed entity and is in the first chunk
    }

    /// <summary>
    ///     Checks if moving an <see cref="Entity"/> between two <see cref="Archetype"/>s was successful.
    /// </summary>
    [Test]
    public void Move()
    {
        var archetype = new Archetype(_group);
        var otherArchetype = new Archetype(_otherGroup);

        var entity = new Entity(1, 0);
        var otherEntity = new Entity(2, 0);
        archetype.Add(entity, out var entityOneSlot);
        otherArchetype.Add(otherEntity, out var entityTwoSlot);

        archetype.Set(ref entityOneSlot, new Transform { X = 10, Y = 10 });
        archetype.Set(ref entityOneSlot, new Rotation { X = 10, Y = 10 });

        otherArchetype.Add(entity, out var newSlot);
        archetype.CopyRowTo(otherArchetype, ref entityOneSlot, ref newSlot);
        archetype.Remove(ref entityOneSlot, out var replacedEntityId);

        That(archetype.Chunks[0].Size, Is.EqualTo(0));
        That(otherArchetype.Chunks[0].Size, Is.EqualTo(2));
        That(otherArchetype.Get<Transform>(ref newSlot).X, Is.EqualTo(10));
        That(otherArchetype.Get<Transform>(ref newSlot).Y, Is.EqualTo(10));
        That(otherArchetype.Get<Rotation>(ref newSlot).X, Is.EqualTo(10));
        That(otherArchetype.Get<Rotation>(ref newSlot).Y, Is.EqualTo(10));
    }

    /// <summary>
    ///     Checks if a copy operation between <see cref="Archetype"/> was successful.
    ///     This is checked by the <see cref="Entity"/> shift through slots.
    /// </summary>
    /// <param name="sourceAmount">Different test entity amounts.</param>
    /// <param name="destinationAmount">Different test entity amounts.</param>
    [Test]
    public void CopyTo([Values(1111,2222,3333)] int sourceAmount, [Values(1111,2222,3333)] int destinationAmount)
    {
        var source = new Archetype(_group);
        var destination = new Archetype(_otherGroup);

        // Fill chunks with data to copy
        for (int index = 0; index < sourceAmount; index++)
        {
            var entity = new Entity(index, 0);
            source.Add(entity, out var entityOneSlot);
            source.Set(ref entityOneSlot, new Transform { X = 10, Y = 10 });
            source.Set(ref entityOneSlot, new Rotation { X = 10, Y = 10 });
        }

        // Fill chunks with data to copy
        for (int index = 0; index < destinationAmount; index++)
        {
            var entity = new Entity(index, 0);
            destination.Add(entity, out var entityOneSlot);
            destination.Set(ref entityOneSlot, new Transform { X = 10, Y = 10 });
            destination.Set(ref entityOneSlot, new Rotation { X = 10, Y = 10 });
        }

        // Calculate their slots and position of copied entity.
        var sourceSlot = source.LastSlot;
        var destinationSlot = destination.LastSlot;
        var resultSlot = sourceSlot + ++destinationSlot;
        resultSlot.Wrap(destination.EntitiesPerChunk);

        // Copy from one chunk into other.
        Archetype.CopyTo(source, destination);

        //That(source.Capacity, Is.EqualTo(destination.Capacity));
        That(source.Entity(ref sourceSlot), Is.EqualTo(destination.Entity(ref resultSlot)));  // Make sure entities were copied correctly.
    }
}
