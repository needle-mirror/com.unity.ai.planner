using System;
using System.Collections.Generic;
using System.Text;
using Unity.AI.Planner;
using Unity.AI.Planner.DomainLanguage.TraitBased;
using Unity.AI.Planner.Jobs;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using UnityEngine;

namespace KeyDomain
{
    struct StateEntityKey : IEquatable<StateEntityKey>, IStateKey
    {
        public Entity Entity;
        public int HashCode;

        public bool Equals(StateEntityKey other) => Entity == other.Entity;
        public bool Equals(IStateKey other) => (other is StateEntityKey otherKey) && Equals(otherKey);

        public override int GetHashCode() => HashCode;

        public override string ToString() => $"StateEntityKey({Entity} {HashCode})";
        public string Label => Entity.ToString();
    }

    struct TerminationEvaluator : ITerminationEvaluator<StateData>
    {
        public bool IsTerminal(StateData state, out float terminalReward)
        {
            terminalReward = 0f;
            var endObjects = new NativeList<int>(1, Allocator.Temp);
            state.GetTraitBasedObjectIndices(endObjects, ComponentType.ReadWrite<End>());

            return endObjects.Length > 0;
        }
    }

    struct Heuristic : IHeuristic<StateData>
    {
        public BoundedValue Evaluate(StateData state)
        {
            return new BoundedValue(-100, 0, 100);
        }
    }

    static class TraitArrayIndex<T>
        where T : struct, ITrait
    {
        public static readonly int Index = -1;

        static TraitArrayIndex()
        {
            var typeIndex = TypeManager.GetTypeIndex<T>();
            if (typeIndex == TypeManager.GetTypeIndex<Colored>())
                Index = 0;
            else if (typeIndex == TypeManager.GetTypeIndex<Carrier>())
                Index = 1;
            else if (typeIndex == TypeManager.GetTypeIndex<Carriable>())
                Index = 2;
            else if (typeIndex == TypeManager.GetTypeIndex<Localized>())
                Index = 3;
            else if (typeIndex == TypeManager.GetTypeIndex<Lockable>())
                Index = 4;
            else if (typeIndex == TypeManager.GetTypeIndex<End>())
                Index = 5;
        }
    }

    struct TraitBasedObject : ITraitBasedObject
    {
        public int Length => 6;

        public byte this[int i]
        {
            get
            {
                switch (i)
                {
                    case 0:
                        return ColoredIndex;
                    case 1:
                        return CarrierIndex;
                    case 2:
                        return CarriableIndex;
                    case 3:
                        return LocalizedIndex;
                    case 4:
                        return LockableIndex;
                    case 5:
                        return EndIndex;
                }

                return Unset;
            }
            set
            {
                switch (i)
                {
                    case 0:
                        ColoredIndex = value;
                        break;
                    case 1:
                        CarrierIndex = value;
                        break;
                    case 2:
                        CarriableIndex = value;
                        break;
                    case 3:
                        LocalizedIndex = value;
                        break;
                    case 4:
                        LockableIndex = value;
                        break;
                    case 5:
                        EndIndex = value;
                        break;
                }
            }
        }

        public static readonly byte Unset = Byte.MaxValue;

        public static TraitBasedObject Default => new TraitBasedObject()
        {
            ColoredIndex = Unset,
            CarrierIndex = Unset,
            CarriableIndex = Unset,
            LocalizedIndex = Unset,
            LockableIndex = Unset,
            EndIndex = Unset,
        };

        public byte ColoredIndex;
        public byte CarrierIndex;
        public byte CarriableIndex;
        public byte LocalizedIndex;
        public byte LockableIndex;
        public byte EndIndex;

        static readonly int s_ColoredType =   TypeManager.GetTypeIndex<Colored>();
        static readonly int s_CarrierType =   TypeManager.GetTypeIndex<Carrier>();
        static readonly int s_CarriableType = TypeManager.GetTypeIndex<Carriable>();
        static readonly int s_LocalizedType = TypeManager.GetTypeIndex<Localized>();
        static readonly int s_LockableType =  TypeManager.GetTypeIndex<Lockable>();
        static readonly int s_EndType =       TypeManager.GetTypeIndex<End>();

        public bool HasSameTraits(TraitBasedObject other)
        {
            for (var i = 0; i < Length; i++)
            {
                var traitIndex = this[i];
                var otherTraitIndex = other[i];
                if (traitIndex == Unset && otherTraitIndex != Unset || traitIndex != Unset && otherTraitIndex == Unset)
                    return false;
            }
            return true;
        }

        public bool HasTraitSubset(TraitBasedObject traitSubset)
        {
            for (var i = 0; i < Length; i++)
            {
                var requiredTrait = traitSubset[i];
                if (requiredTrait != Unset && this[i] == Unset)
                    return false;
            }
            return true;
        }

        public bool MatchesTraitFilter(NativeArray<ComponentType> componentTypes)
        {
            for (int i = 0; i < componentTypes.Length; i++)
            {
                var t = componentTypes[i];
                if (t.TypeIndex == s_ColoredType)
                {
                    if (t.AccessModeType == ComponentType.AccessMode.Exclude ^ ColoredIndex == Unset)
                        return false;
                }
                else if (t.TypeIndex == s_CarrierType)
                {
                    if (t.AccessModeType == ComponentType.AccessMode.Exclude ^ CarrierIndex == Unset)
                        return false;
                }
                else if (t.TypeIndex == s_CarriableType)
                {
                    if (t.AccessModeType == ComponentType.AccessMode.Exclude ^ CarriableIndex == Unset)
                        return false;
                }
                else if (t.TypeIndex == s_LocalizedType)
                {
                    if (t.AccessModeType == ComponentType.AccessMode.Exclude ^ LocalizedIndex == Unset)
                        return false;
                }
                else if (t.TypeIndex == s_LockableType)
                {
                    if (t.AccessModeType == ComponentType.AccessMode.Exclude ^ LockableIndex == Unset)
                        return false;
                }
                else if (t.TypeIndex == s_EndType)
                {
                    if (t.AccessModeType == ComponentType.AccessMode.Exclude ^ EndIndex == Unset)
                        return false;
                }
                else
                {
                    throw new ArgumentException($"Incorrect trait type used in domain object query: {t}");
                }
            }

            return true;
        }

        public bool MatchesTraitFilter(ComponentType[] componentTypes)
        {
            for (int i = 0; i < componentTypes.Length; i++)
            {
                var t = componentTypes[i];
                if (t.TypeIndex == s_ColoredType)
                {
                    if (t.AccessModeType == ComponentType.AccessMode.Exclude ^ ColoredIndex == Unset)
                        return false;
                }
                else if (t.TypeIndex == s_CarrierType)
                {
                    if (t.AccessModeType == ComponentType.AccessMode.Exclude ^ CarrierIndex == Unset)
                        return false;
                }
                else if (t.TypeIndex == s_CarriableType)
                {
                    if (t.AccessModeType == ComponentType.AccessMode.Exclude ^ CarriableIndex == Unset)
                        return false;
                }
                else if (t.TypeIndex == s_LocalizedType)
                {
                    if (t.AccessModeType == ComponentType.AccessMode.Exclude ^ LocalizedIndex == Unset)
                        return false;
                }
                else if (t.TypeIndex == s_LockableType)
                {
                    if (t.AccessModeType == ComponentType.AccessMode.Exclude ^ LockableIndex == Unset)
                        return false;
                }
                else if (t.TypeIndex == s_EndType)
                {
                    if (t.AccessModeType == ComponentType.AccessMode.Exclude ^ EndIndex == Unset)
                        return false;
                }
                else
                {
                    throw new ArgumentException($"Incorrect trait type used in domain object query: {t}");
                }
            }

            return true;
        }
    }

    struct StateData : ITraitBasedStateData<TraitBasedObject, StateData>
    {
        public Entity StateEntity;
        public DynamicBuffer<TraitBasedObject> TraitBasedObjects;
        public DynamicBuffer<TraitBasedObjectId> TraitBasedObjectIds;

        public DynamicBuffer<Colored> ColoredBuffer;
        public DynamicBuffer<Carrier> CarrierBuffer;
        public DynamicBuffer<Carriable> CarriableBuffer;
        public DynamicBuffer<Localized> LocalizedBuffer;
        public DynamicBuffer<Lockable> LockableBuffer;
        public DynamicBuffer<End> EndBuffer;

        static readonly int s_ColoredType =   TypeManager.GetTypeIndex<Colored>();
        static readonly int s_CarrierType =   TypeManager.GetTypeIndex<Carrier>();
        static readonly int s_CarriableType = TypeManager.GetTypeIndex<Carriable>();
        static readonly int s_LocalizedType = TypeManager.GetTypeIndex<Localized>();
        static readonly int s_LockableType =  TypeManager.GetTypeIndex<Lockable>();
        static readonly int s_EndType =       TypeManager.GetTypeIndex<End>();

        public StateData(JobComponentSystem system, Entity stateEntity, bool readWrite = false)
        {
            StateEntity = stateEntity;
            TraitBasedObjects = system.GetBufferFromEntity<TraitBasedObject>(!readWrite)[stateEntity];
            TraitBasedObjectIds = system.GetBufferFromEntity<TraitBasedObjectId>(!readWrite)[stateEntity];
            ColoredBuffer = system.GetBufferFromEntity<Colored>(!readWrite)[stateEntity];
            CarrierBuffer = system.GetBufferFromEntity<Carrier>(!readWrite)[stateEntity];
            CarriableBuffer = system.GetBufferFromEntity<Carriable>(!readWrite)[stateEntity];
            LocalizedBuffer = system.GetBufferFromEntity<Localized>(!readWrite)[stateEntity];
            LockableBuffer = system.GetBufferFromEntity<Lockable>(!readWrite)[stateEntity];
            EndBuffer = system.GetBufferFromEntity<End>(!readWrite)[stateEntity];
        }

        public StateData(int jobIndex, EntityCommandBuffer.Concurrent entityCommandBuffer, Entity stateEntity)
        {
            StateEntity = stateEntity;
            TraitBasedObjects = entityCommandBuffer.AddBuffer<TraitBasedObject>(jobIndex, stateEntity);
            TraitBasedObjectIds = entityCommandBuffer.AddBuffer<TraitBasedObjectId>(jobIndex, stateEntity);
            ColoredBuffer =  entityCommandBuffer.AddBuffer<Colored>(jobIndex, stateEntity);
            CarrierBuffer = entityCommandBuffer.AddBuffer<Carrier>(jobIndex, stateEntity);
            CarriableBuffer = entityCommandBuffer.AddBuffer<Carriable>(jobIndex, stateEntity);
            LocalizedBuffer = entityCommandBuffer.AddBuffer<Localized>(jobIndex, stateEntity);
            LockableBuffer = entityCommandBuffer.AddBuffer<Lockable>(jobIndex, stateEntity);
            EndBuffer = entityCommandBuffer.AddBuffer<End>(jobIndex, stateEntity);
        }

        public StateData Copy(int jobIndex, EntityCommandBuffer.Concurrent entityCommandBuffer)
        {
            var stateEntity = entityCommandBuffer.Instantiate(jobIndex, StateEntity);
            var traitBasedObjects = entityCommandBuffer.SetBuffer<TraitBasedObject>(jobIndex, stateEntity);
            traitBasedObjects.CopyFrom(TraitBasedObjects.AsNativeArray());
            var traitBasedObjectIds = entityCommandBuffer.SetBuffer<TraitBasedObjectId>(jobIndex, stateEntity);
            traitBasedObjectIds.CopyFrom(TraitBasedObjectIds.AsNativeArray());

            var Coloreds = entityCommandBuffer.SetBuffer<Colored>(jobIndex, stateEntity);
            Coloreds.CopyFrom(ColoredBuffer.AsNativeArray());
            var Carriers = entityCommandBuffer.SetBuffer<Carrier>(jobIndex, stateEntity);
            Carriers.CopyFrom(CarrierBuffer.AsNativeArray());
            var Carriables = entityCommandBuffer.SetBuffer<Carriable>(jobIndex, stateEntity);
            Carriables.CopyFrom(CarriableBuffer.AsNativeArray());
            var Localizeds = entityCommandBuffer.SetBuffer<Localized>(jobIndex, stateEntity);
            Localizeds.CopyFrom(LocalizedBuffer.AsNativeArray());
            var Lockables = entityCommandBuffer.SetBuffer<Lockable>(jobIndex, stateEntity);
            Lockables.CopyFrom(LockableBuffer.AsNativeArray());
            var Ends = entityCommandBuffer.SetBuffer<End>(jobIndex, stateEntity);
            Ends.CopyFrom(EndBuffer.AsNativeArray());

            return new StateData
            {
                StateEntity = stateEntity,
                TraitBasedObjects = traitBasedObjects,
                TraitBasedObjectIds = traitBasedObjectIds,
                ColoredBuffer = Coloreds,
                CarrierBuffer = Carriers,
                CarriableBuffer = Carriables,
                LocalizedBuffer = Localizeds,
                LockableBuffer = Lockables,
                EndBuffer = Ends,
            };
        }

        public void AddObject(NativeArray<ComponentType> types, out TraitBasedObject traitBasedObject, TraitBasedObjectId traitBasedObjectId, NativeString64 name = default)
        {
            traitBasedObject = TraitBasedObject.Default;
#if DEBUG
            traitBasedObjectId.Name.CopyFrom(name);
#endif

            foreach (var t in types)
            {
                if (t.TypeIndex == s_ColoredType)
                {
                    ColoredBuffer.Add(default);
                    traitBasedObject.ColoredIndex = (byte) (ColoredBuffer.Length - 1);
                }
                else if (t.TypeIndex == s_CarrierType)
                {
                    CarrierBuffer.Add(default);
                    traitBasedObject.CarrierIndex = (byte) (CarrierBuffer.Length - 1);
                }
                else if (t.TypeIndex == s_CarriableType)
                {
                    CarriableBuffer.Add(default);
                    traitBasedObject.CarriableIndex = (byte) (CarriableBuffer.Length - 1);
                }
                else if (t.TypeIndex == s_LocalizedType)
                {
                    LocalizedBuffer.Add(default);
                    traitBasedObject.LocalizedIndex = (byte) (LocalizedBuffer.Length - 1);
                }
                else if (t.TypeIndex == s_LockableType)
                {
                    LockableBuffer.Add(default);
                    traitBasedObject.LockableIndex = (byte) (LockableBuffer.Length - 1);
                }
                else if (t.TypeIndex == s_EndType)
                {
                    EndBuffer.Add(default);
                    traitBasedObject.EndIndex = (byte) (EndBuffer.Length - 1);
                }
            }

            TraitBasedObjectIds.Add(traitBasedObjectId);
            TraitBasedObjects.Add(traitBasedObject);
        }

        public void AddObject(NativeArray<ComponentType> types, out TraitBasedObject traitBasedObject, out TraitBasedObjectId traitBasedObjectId, NativeString64 name = default)
        {
            traitBasedObjectId = new TraitBasedObjectId() { Id = ObjectId.GetNext() };
            AddObject(types, out traitBasedObject, traitBasedObjectId, name);
        }

        public void AddTraitBasedObject(ComponentType[] types, out TraitBasedObject traitBasedObject, out TraitBasedObjectId traitBasedObjectId, NativeString64 name = default)
        {
            traitBasedObject = TraitBasedObject.Default;
            traitBasedObjectId = new TraitBasedObjectId() { Id = ObjectId.GetNext() };
#if DEBUG
            traitBasedObjectId.Name.CopyFrom(name);
#endif

            foreach (var t in types)
            {
                if (t.TypeIndex == s_ColoredType)
                {
                    ColoredBuffer.Add(default);
                    traitBasedObject.ColoredIndex = (byte) (ColoredBuffer.Length - 1);
                }
                else if (t.TypeIndex == s_CarrierType)
                {
                    CarrierBuffer.Add(default);
                    traitBasedObject.CarrierIndex = (byte) (CarrierBuffer.Length - 1);
                }
                else if (t.TypeIndex == s_CarriableType)
                {
                    CarriableBuffer.Add(default);
                    traitBasedObject.CarriableIndex = (byte) (CarriableBuffer.Length - 1);
                }
                else if (t.TypeIndex == s_LocalizedType)
                {
                    LocalizedBuffer.Add(default);
                    traitBasedObject.LocalizedIndex = (byte) (LocalizedBuffer.Length - 1);
                }
                else if (t.TypeIndex == s_LockableType)
                {
                    LockableBuffer.Add(default);
                    traitBasedObject.LockableIndex = (byte) (LockableBuffer.Length - 1);
                }
                else if (t.TypeIndex == s_EndType)
                {
                    EndBuffer.Add(default);
                    traitBasedObject.EndIndex = (byte) (EndBuffer.Length - 1);
                }
            }

            TraitBasedObjectIds.Add(traitBasedObjectId);
            TraitBasedObjects.Add(traitBasedObject);
        }

        public void SetTraitOnObject(ITrait trait, ref TraitBasedObject traitBasedObject)
        {
            if (trait is Colored ColoredTrait)
                SetTraitOnObject(ColoredTrait, ref traitBasedObject);
            else if (trait is Carrier CarrierTrait)
                SetTraitOnObject(CarrierTrait, ref traitBasedObject);
            else if (trait is Carriable CarriableTrait)
                SetTraitOnObject(CarriableTrait, ref traitBasedObject);
            else if (trait is Localized LocalizedTrait)
                SetTraitOnObject(LocalizedTrait, ref traitBasedObject);
            else if (trait is Lockable LockableTrait)
                SetTraitOnObject(LockableTrait, ref traitBasedObject);
            else if (trait is End EndTrait)
                SetTraitOnObject(EndTrait, ref traitBasedObject);

            throw new ArgumentException($"Trait {trait} of type {trait.GetType()} is not supported in this domain.");
        }

        public TTrait GetTraitOnObject<TTrait>(TraitBasedObject traitBasedObject) where TTrait : struct, ITrait
        {
            var traitBasedObjectTraitIndex = TraitArrayIndex<TTrait>.Index;
            if (traitBasedObjectTraitIndex == -1)
                throw new ArgumentException($"Trait {typeof(TTrait)} not supported in this domain");

            var traitBufferIndex = traitBasedObject[traitBasedObjectTraitIndex];
            if (traitBufferIndex == TraitBasedObject.Unset)
                throw new ArgumentException($"Trait of type {typeof(TTrait)} does not exist on object {traitBasedObject}.");

            return GetBuffer<TTrait>()[traitBufferIndex];
        }

        public bool HasTraitOnObject<TTrait>(TraitBasedObject traitBasedObject) where TTrait : struct, ITrait
        {
            var traitBasedObjectTraitIndex = TraitArrayIndex<TTrait>.Index;
            if (traitBasedObjectTraitIndex == -1)
                throw new ArgumentException($"Trait {typeof(TTrait)} not supported in this plan");

            var traitBufferIndex = traitBasedObject[traitBasedObjectTraitIndex];
            return traitBufferIndex != TraitBasedObject.Unset;
        }

        public void SetTraitOnObject<TTrait>(TTrait trait, ref TraitBasedObject traitBasedObject) where TTrait : struct, ITrait
        {
            var objectIndex = GetTraitBasedObjectIndex(traitBasedObject);
            if (objectIndex == -1)
                throw new ArgumentException($"Object {traitBasedObject} does not exist within the state data {this}.");

            var traitIndex = TraitArrayIndex<TTrait>.Index;
            var traitBuffer = GetBuffer<TTrait>();

            var bufferIndex = traitBasedObject[traitIndex];
            if (bufferIndex == TraitBasedObject.Unset)
            {
                traitBuffer.Add(trait);
                traitBasedObject[traitIndex] = (byte) (traitBuffer.Length - 1);

                TraitBasedObjects[objectIndex] = traitBasedObject;
            }
            else
            {
                traitBuffer[bufferIndex] = trait;
            }
        }

        public bool RemoveTraitOnObject<TTrait>(ref TraitBasedObject traitBasedObject) where TTrait : struct, ITrait
        {
            var objectTraitIndex = TraitArrayIndex<TTrait>.Index;
            var traitBuffer = GetBuffer<TTrait>();

            var traitBufferIndex = traitBasedObject[objectTraitIndex];
            if (traitBufferIndex == TraitBasedObject.Unset)
                return false;

            // last index
            var lastBufferIndex = traitBuffer.Length - 1;

            // Swap back and remove
            var lastTrait = traitBuffer[lastBufferIndex];
            traitBuffer[lastBufferIndex] = traitBuffer[traitBufferIndex];
            traitBuffer[traitBufferIndex] = lastTrait;
            traitBuffer.RemoveAt(lastBufferIndex);

            // Update index for object with last trait in buffer
            for (int i = 0; i < TraitBasedObjects.Length; i++)
            {
                var otherTraitBasedObject = TraitBasedObjects[i];
                if (otherTraitBasedObject[objectTraitIndex] == lastBufferIndex)
                {
                    otherTraitBasedObject[objectTraitIndex] = traitBufferIndex;
                    TraitBasedObjects[i] = otherTraitBasedObject;
                    break;
                }
            }

            // Update traitBasedObject in buffer (ref is to a copy)
            for (int i = 0; i < TraitBasedObjects.Length; i++)
            {
                if (traitBasedObject.Equals(TraitBasedObjects[i]))
                {
                    traitBasedObject[objectTraitIndex] = TraitBasedObject.Unset;
                    TraitBasedObjects[i] = traitBasedObject;
                    return true;
                }
            }

            throw new ArgumentException($"TraitBasedObject {traitBasedObject} does not exist in the state container {this}.");
        }

        public bool RemoveObject(TraitBasedObject traitBasedObject)
        {
            var objectIndex = GetTraitBasedObjectIndex(traitBasedObject);
            if (objectIndex == -1)
                return false;

            RemoveTraitOnObject<Colored>(ref traitBasedObject);
            RemoveTraitOnObject<Carrier>(ref traitBasedObject);
            RemoveTraitOnObject<Carriable>(ref traitBasedObject);
            RemoveTraitOnObject<Localized>(ref traitBasedObject);
            RemoveTraitOnObject<Lockable>(ref traitBasedObject);
            RemoveTraitOnObject<End>(ref traitBasedObject);

            TraitBasedObjects.RemoveAt(objectIndex);
            TraitBasedObjectIds.RemoveAt(objectIndex);

            return true;
        }

        public TTrait GetTraitOnObjectAtIndex<TTrait>(int traitBasedObjectIndex) where TTrait : struct, ITrait
        {
            var traitBasedObjectTraitIndex = TraitArrayIndex<TTrait>.Index;
            if (traitBasedObjectTraitIndex == -1)
                throw new ArgumentException($"Trait {typeof(TTrait)} not supported in this domain");

            var traitBasedObject = TraitBasedObjects[traitBasedObjectIndex];
            var traitBufferIndex = traitBasedObject[traitBasedObjectTraitIndex];
            if (traitBufferIndex == TraitBasedObject.Unset)
                throw new Exception($"Trait index for {typeof(TTrait)} is not set for object {traitBasedObject}");

            return GetBuffer<TTrait>()[traitBufferIndex];
        }

        public void SetTraitOnObjectAtIndex(ITrait trait, int traitBasedObjectIndex)
        {
            throw new NotImplementedException();
        }

        public void SetTraitOnObjectAtIndex<TTrait>(TTrait trait, int traitBasedObjectIndex) where TTrait : struct, ITrait
        {
            var traitBasedObjectTraitIndex = TraitArrayIndex<TTrait>.Index;
            if (traitBasedObjectTraitIndex == -1)
                throw new ArgumentException($"Trait {typeof(TTrait)} not supported in this domain");

            var traitBasedObject = TraitBasedObjects[traitBasedObjectIndex];
            var traitBufferIndex = traitBasedObject[traitBasedObjectTraitIndex];
            var traitBuffer = GetBuffer<TTrait>();
            if (traitBufferIndex == TraitBasedObject.Unset)
            {
                traitBuffer.Add(trait);
                traitBufferIndex = (byte)(traitBuffer.Length - 1);
                traitBasedObject[traitBasedObjectTraitIndex] = traitBufferIndex;
                TraitBasedObjects[traitBasedObjectIndex] = traitBasedObject;
            }
            else
            {
                traitBuffer[traitBufferIndex] = trait;
            }
        }

        public bool RemoveTraitOnObjectAtIndex<TTrait>(int traitBasedObjectIndex) where TTrait : struct, ITrait
        {
            var objectTraitIndex = TraitArrayIndex<TTrait>.Index;
            var traitBuffer = GetBuffer<TTrait>();

            var traitBasedObject = TraitBasedObjects[traitBasedObjectIndex];
            var traitBufferIndex = traitBasedObject[objectTraitIndex];
            if (traitBufferIndex == TraitBasedObject.Unset)
                return false;

            // last index
            var lastBufferIndex = traitBuffer.Length - 1;

            // Swap back and remove
            var lastTrait = traitBuffer[lastBufferIndex];
            traitBuffer[lastBufferIndex] = traitBuffer[traitBufferIndex];
            traitBuffer[traitBufferIndex] = lastTrait;
            traitBuffer.RemoveAt(lastBufferIndex);

            // Update index for object with last trait in buffer
            for (int i = 0; i < TraitBasedObjects.Length; i++)
            {
                var otherTraitBasedObject = TraitBasedObjects[i];
                if (otherTraitBasedObject[objectTraitIndex] == lastBufferIndex)
                {
                    otherTraitBasedObject[objectTraitIndex] = traitBufferIndex;
                    TraitBasedObjects[i] = otherTraitBasedObject;
                    break;
                }
            }

            traitBasedObject[objectTraitIndex] = TraitBasedObject.Unset;
            TraitBasedObjects[traitBasedObjectIndex] = traitBasedObject;

            throw new ArgumentException($"TraitBasedObject {traitBasedObject} does not exist in the state container {this}.");
        }

        public bool RemoveTraitBasedObjectAtIndex(int traitBasedObjectIndex)
        {
            RemoveTraitOnObjectAtIndex<Colored>(traitBasedObjectIndex);
            RemoveTraitOnObjectAtIndex<Carrier>(traitBasedObjectIndex);
            RemoveTraitOnObjectAtIndex<Carriable>(traitBasedObjectIndex);
            RemoveTraitOnObjectAtIndex<Localized>(traitBasedObjectIndex);
            RemoveTraitOnObjectAtIndex<Lockable>(traitBasedObjectIndex);
            RemoveTraitOnObjectAtIndex<End>(traitBasedObjectIndex);

            TraitBasedObjects.RemoveAt(traitBasedObjectIndex);
            TraitBasedObjectIds.RemoveAt(traitBasedObjectIndex);

            return true;
        }

        public NativeArray<int> GetTraitBasedObjectIndices(NativeList<int> traitBasedObjects, NativeArray<ComponentType> traitFilter)
        {
            for (var i = 0; i < TraitBasedObjects.Length; i++)
            {
                var traitBasedObject = TraitBasedObjects[i];
                if (traitBasedObject.MatchesTraitFilter(traitFilter))
                    traitBasedObjects.Add(i);
            }

            return traitBasedObjects.AsArray();
        }

        public NativeArray<int> GetTraitBasedObjectIndices(NativeList<int> traitBasedObjects, params ComponentType[] traitFilter)
        {
            for (var i = 0; i < TraitBasedObjects.Length; i++)
            {
                var traitBasedObject = TraitBasedObjects[i];
                if (traitBasedObject.MatchesTraitFilter(traitFilter))
                    traitBasedObjects.Add(i);
            }

            return traitBasedObjects.AsArray();
        }

        public void GetTraitBasedObjectIndices(TraitBasedObject traitSubset, NativeList<int> traitBasedObjects)
        {
            var traitBasedObjectArray = TraitBasedObjects.AsNativeArray();
            for (var i = 0; i < traitBasedObjectArray.Length; i++)
            {
                if (traitBasedObjectArray[i].HasTraitSubset(traitSubset))
                    traitBasedObjects.Add(i);
            }
        }

        public int GetTraitBasedObjectIndex(TraitBasedObject traitBasedObject)
        {
            var objectIndex = -1;
            for (int i = 0; i < TraitBasedObjects.Length; i++)
            {
                if (TraitBasedObjects[i].Equals(traitBasedObject))
                {
                    objectIndex = i;
                    break;
                }
            }

            return objectIndex;
        }

        public int GetTraitBasedObjectIndex(TraitBasedObjectId traitBasedObjectId)
        {
            throw new NotImplementedException();
        }

        public TraitBasedObjectId GetTraitBasedObjectId(TraitBasedObject traitBasedObject)
        {
            var index = GetTraitBasedObjectIndex(traitBasedObject);
            return TraitBasedObjectIds[index];
        }

        public TraitBasedObjectId GetTraitBasedObjectId(int traitBasedObjectIndex)
        {
            return TraitBasedObjectIds[traitBasedObjectIndex];
        }

        DynamicBuffer<T> GetBuffer<T>() where T : struct, ITrait
        {
            var index = TraitArrayIndex<T>.Index;
            switch (index)
            {
                case 0:
                    return ColoredBuffer.Reinterpret<T>();
                case 1:
                    return CarrierBuffer.Reinterpret<T>();
                case 2:
                    return CarriableBuffer.Reinterpret<T>();
                case 3:
                    return LocalizedBuffer.Reinterpret<T>();
                case 4:
                    return LockableBuffer.Reinterpret<T>();
                case 5:
                    return EndBuffer.Reinterpret<T>();
            }

            return default;
        }

        public bool Equals(IStateData other) => other is StateData otherData && Equals(otherData);

        public bool Equals(StateData rhsState)
        {
            if (StateEntity == rhsState.StateEntity)
                return true;

            // Easy check is to make sure each state has the same number of domain objects
            if (TraitBasedObjects.Length != rhsState.TraitBasedObjects.Length
                || ColoredBuffer.Length != rhsState.ColoredBuffer.Length
                || CarrierBuffer.Length != rhsState.CarrierBuffer.Length
                || CarriableBuffer.Length != rhsState.CarriableBuffer.Length
                || LocalizedBuffer.Length != rhsState.LocalizedBuffer.Length
                || LockableBuffer.Length != rhsState.LockableBuffer.Length
                || EndBuffer.Length != rhsState.EndBuffer.Length )
                return false;

            // New below
            var objectMap = new ObjectCorrespondence(TraitBasedObjectIds, rhsState.TraitBasedObjectIds, Allocator.Temp);
            var statesEqual = TryGetObjectMapping(rhsState, objectMap);
            objectMap.Dispose();

            return statesEqual;
        }

        bool ObjectsMatchAttributes(TraitBasedObject traitBasedObjectLHS, TraitBasedObject traitBasedObjectRHS, StateData rhsState)
        {
            // flat attribute comparison
            if (!traitBasedObjectLHS.HasSameTraits(traitBasedObjectRHS))
                return false;

            if (traitBasedObjectLHS.ColoredIndex != TraitBasedObject.Unset &&
                !ColoredBuffer[traitBasedObjectLHS.ColoredIndex].AttributesEqual(rhsState.ColoredBuffer[traitBasedObjectRHS.ColoredIndex]))
                return false;

            if (traitBasedObjectLHS.CarrierIndex != TraitBasedObject.Unset &&
                !CarrierBuffer[traitBasedObjectLHS.CarrierIndex].AttributesEqual(rhsState.CarrierBuffer[traitBasedObjectRHS.CarrierIndex]))
                return false;

            if (traitBasedObjectLHS.CarriableIndex != TraitBasedObject.Unset &&
                !CarriableBuffer[traitBasedObjectLHS.CarriableIndex].AttributesEqual(rhsState.CarriableBuffer[traitBasedObjectRHS.CarriableIndex]))
                return false;

            if (traitBasedObjectLHS.LocalizedIndex != TraitBasedObject.Unset &&
                !LocalizedBuffer[traitBasedObjectLHS.LocalizedIndex].AttributesEqual(rhsState.LocalizedBuffer[traitBasedObjectRHS.LocalizedIndex]))
                return false;

            if (traitBasedObjectLHS.LockableIndex != TraitBasedObject.Unset &&
                !LockableBuffer[traitBasedObjectLHS.LockableIndex].AttributesEqual(rhsState.LockableBuffer[traitBasedObjectRHS.LockableIndex]))
                return false;

            if (traitBasedObjectLHS.EndIndex != TraitBasedObject.Unset &&
                !EndBuffer[traitBasedObjectLHS.EndIndex].AttributesEqual(rhsState.EndBuffer[traitBasedObjectRHS.EndIndex]))
                return false;

            return true;
        }

        bool CheckRelationsAndQueueObjects(TraitBasedObject traitBasedObjectLHS, TraitBasedObject traitBasedObjectRHS, StateData rhsState, ObjectCorrespondence objectMap)
        {
            // edge walking - for relation properties
            // for key domain: carrier, carriable, localized
            if (traitBasedObjectLHS.CarrierIndex != TraitBasedObject.Unset)
            {
                // the unique relation Ids to match
                var lhsRelationId = CarrierBuffer[traitBasedObjectLHS.CarrierIndex].CarriedObject;
                var rhsRelationId = rhsState.CarrierBuffer[traitBasedObjectRHS.CarrierIndex].CarriedObject;

                if (lhsRelationId.Equals(ObjectId.None) ^ rhsRelationId.Equals(ObjectId.None))
                    return false;

                if (objectMap.TryGetValue(lhsRelationId, out var rhsAssignedId))
                {
                    if (!rhsRelationId.Equals(rhsAssignedId))
                        return false;
                }
                else
                {
                    objectMap.Add(lhsRelationId, rhsRelationId);
                }
            }

            if (traitBasedObjectLHS.CarriableIndex != TraitBasedObject.Unset)
            {
                // the unique relation Ids to match
                var lhsRelationId = CarriableBuffer[traitBasedObjectLHS.CarriableIndex].Carrier;
                var rhsRelationId = rhsState.CarriableBuffer[traitBasedObjectRHS.CarriableIndex].Carrier;

                if (lhsRelationId.Equals(ObjectId.None) ^ rhsRelationId.Equals(ObjectId.None))
                    return false;

                if (objectMap.TryGetValue(lhsRelationId, out var rhsAssignedId))
                {
                    if (!rhsRelationId.Equals(rhsAssignedId))
                        return false;
                }
                else
                {
                    objectMap.Add(lhsRelationId, rhsRelationId);
                }
            }

            if (traitBasedObjectLHS.LocalizedIndex != TraitBasedObject.Unset)
            {
                // the unique relation Ids to match
                var lhsRelationId = LocalizedBuffer[traitBasedObjectLHS.LocalizedIndex].Location;
                var rhsRelationId = rhsState.LocalizedBuffer[traitBasedObjectRHS.LocalizedIndex].Location;

                if (lhsRelationId.Equals(ObjectId.None) ^ rhsRelationId.Equals(ObjectId.None))
                    return false;

                if (objectMap.TryGetValue(lhsRelationId, out var rhsAssignedId))
                {
                    if (!rhsRelationId.Equals(rhsAssignedId))
                        return false;
                }
                else
                {
                    objectMap.Add(lhsRelationId, rhsRelationId);
                }
            }

            return true;
        }

        bool ITraitBasedStateData<TraitBasedObject, StateData>.TryGetObjectMapping(StateData rhsState, ObjectCorrespondence objectMap)
        {
            // Easy check is to make sure each state has the same number of domain objects
            if (TraitBasedObjects.Length != rhsState.TraitBasedObjects.Length
                || ColoredBuffer.Length != rhsState.ColoredBuffer.Length
                || CarrierBuffer.Length != rhsState.CarrierBuffer.Length
                || CarriableBuffer.Length != rhsState.CarriableBuffer.Length
                || LocalizedBuffer.Length != rhsState.LocalizedBuffer.Length
                || LockableBuffer.Length != rhsState.LockableBuffer.Length
                || EndBuffer.Length != rhsState.EndBuffer.Length )
                return false;

            return TryGetObjectMapping(rhsState, objectMap);
        }

        bool TryGetObjectMapping(StateData rhsState, ObjectCorrespondence objectMap)
        {
            objectMap.Initialize(TraitBasedObjectIds, rhsState.TraitBasedObjectIds);

            bool statesEqual = true;
            for (int lhsIndex = 0; lhsIndex < TraitBasedObjects.Length; lhsIndex++)
            {
                var lhsId = TraitBasedObjectIds[lhsIndex].Id;
                if (objectMap.TryGetValue(lhsId, out _)) // already matched
                    continue;

                // todo lhsIndex to start? would require swapping rhs on assignments, though
                bool matchFound = true;
                for (var rhsIndex = 0; rhsIndex < rhsState.TraitBasedObjects.Length; rhsIndex++)
                {
                    var rhsId = rhsState.TraitBasedObjectIds[rhsIndex].Id;
                    if (objectMap.ContainsRHS(rhsId)) // skip if already assigned todo optimize this
                        continue;

                    objectMap.BeginNewTraversal();
                    objectMap.Add(lhsId, rhsId);

                    // Traversal comparing all reachable objects
                    matchFound = true;
                    while (objectMap.Next(out var lhsIdToEvaluate, out var rhsIdToEvaluate))
                    {
                        // match objects, queueing as needed
                        var lhsTraitBasedObject = TraitBasedObjects[objectMap.GetLHSIndex(lhsIdToEvaluate)];
                        var rhsTraitBasedObject = rhsState.TraitBasedObjects[objectMap.GetRHSIndex(rhsIdToEvaluate)];

                        if (!ObjectsMatchAttributes(lhsTraitBasedObject, rhsTraitBasedObject, rhsState) ||
                            !CheckRelationsAndQueueObjects(lhsTraitBasedObject, rhsTraitBasedObject, rhsState, objectMap))
                        {
                            objectMap.RevertTraversalChanges();

                            matchFound = false;
                            break;
                        }
                    }

                    if (matchFound)
                        break;
                }

                if (!matchFound)
                {
                    statesEqual = false;
                    break;
                }
            }

            return statesEqual;
        }
        public override int GetHashCode()
        {
            // h = 3860031 + (h+y)*2779 + (h*y*2)   // from How to Hash a Set by Richard O’Keefe
            var stateHashValue = 0;

            var objectIds = TraitBasedObjectIds;
            foreach (var element in objectIds.AsNativeArray())
            {
                var value = element.GetHashCode();
                stateHashValue = 3860031 + (stateHashValue + value) * 2779 + (stateHashValue * value * 2);
            }

            foreach (var element in ColoredBuffer.AsNativeArray())
            {
                var value = element.GetHashCode();
                stateHashValue = 3860031 + (stateHashValue + value) * 2779 + (stateHashValue * value * 2);
            }

            foreach (var element in CarrierBuffer.AsNativeArray())
            {
                var value = element.GetHashCode();
                stateHashValue = 3860031 + (stateHashValue + value) * 2779 + (stateHashValue * value * 2);
            }

            foreach (var element in CarriableBuffer.AsNativeArray())
            {
                var value = element.GetHashCode();
                stateHashValue = 3860031 + (stateHashValue + value) * 2779 + (stateHashValue * value * 2);
            }

            foreach (var element in LocalizedBuffer.AsNativeArray())
            {
                var value = element.GetHashCode();
                stateHashValue = 3860031 + (stateHashValue + value) * 2779 + (stateHashValue * value * 2);
            }

            foreach (var element in LockableBuffer.AsNativeArray())
            {
                var value = element.GetHashCode();
                stateHashValue = 3860031 + (stateHashValue + value) * 2779 + (stateHashValue * value * 2);
            }

            foreach (var element in EndBuffer.AsNativeArray())
            {
                var value = element.GetHashCode();
                stateHashValue = 3860031 + (stateHashValue + value) * 2779 + (stateHashValue * value * 2);
            }

            return stateHashValue;
        }

        public override string ToString()
        {
            var sb = new StringBuilder();

            for (var traitBasedObjectIndex = 0; traitBasedObjectIndex < TraitBasedObjects.Length; traitBasedObjectIndex++)
            {
                var traitBasedObject = TraitBasedObjects[traitBasedObjectIndex];
                sb.AppendLine(TraitBasedObjectIds[traitBasedObjectIndex].ToString());

                var i = 0;

                var traitIndex = traitBasedObject[i++];
                if (traitIndex != TraitBasedObject.Unset)
                    sb.AppendLine(ColoredBuffer[traitIndex].ToString());

                traitIndex = traitBasedObject[i++];
                if (traitIndex != TraitBasedObject.Unset)
                    sb.AppendLine(CarrierBuffer[traitIndex].ToString());

                traitIndex = traitBasedObject[i++];
                if (traitIndex != TraitBasedObject.Unset)
                    sb.AppendLine(CarriableBuffer[traitIndex].ToString());

                traitIndex = traitBasedObject[i++];
                if (traitIndex != TraitBasedObject.Unset)
                    sb.AppendLine(LocalizedBuffer[traitIndex].ToString());

                traitIndex = traitBasedObject[i++];
                if (traitIndex != TraitBasedObject.Unset)
                    sb.AppendLine(LockableBuffer[traitIndex].ToString());

                traitIndex = traitBasedObject[i];
                if (traitIndex != TraitBasedObject.Unset)
                    sb.AppendLine(EndBuffer[traitIndex].ToString());

                sb.AppendLine();
            }

            return sb.ToString();
        }
    }

    struct StateDataContext : ITraitBasedStateDataContext<TraitBasedObject, StateEntityKey, StateData>
    {
        internal EntityCommandBuffer.Concurrent EntityCommandBuffer;
        internal EntityArchetype m_StateArchetype;
        internal int JobIndex;

        [ReadOnly] public BufferFromEntity<TraitBasedObject> TraitBasedObjects;
        [ReadOnly] public BufferFromEntity<TraitBasedObjectId> TraitBasedObjectIds;
        [ReadOnly] public BufferFromEntity<Colored> ColoredData;
        [ReadOnly] public BufferFromEntity<Carrier> CarrierData;
        [ReadOnly] public BufferFromEntity<Carriable> CarriableData;
        [ReadOnly] public BufferFromEntity<Localized> LocalizedData;
        [ReadOnly] public BufferFromEntity<Lockable> LockableData;
        [ReadOnly] public BufferFromEntity<End> EndData;

        public StateDataContext(JobComponentSystem system, EntityArchetype stateArchetype)
        {
            EntityCommandBuffer = default;
            TraitBasedObjects = system.GetBufferFromEntity<TraitBasedObject>(true);
            TraitBasedObjectIds = system.GetBufferFromEntity<TraitBasedObjectId>(true);
            ColoredData = system.GetBufferFromEntity<Colored>(true);
            CarrierData = system.GetBufferFromEntity<Carrier>(true);
            CarriableData = system.GetBufferFromEntity<Carriable>(true);
            LocalizedData = system.GetBufferFromEntity<Localized>(true);
            LockableData = system.GetBufferFromEntity<Lockable>(true);
            EndData = system.GetBufferFromEntity<End>(true);

            m_StateArchetype = stateArchetype;
            JobIndex = 0; // todo set on all actions
        }

        public StateData GetStateData(StateEntityKey stateKey)
        {
            var stateEntity = stateKey.Entity;

            return new StateData()
            {
                StateEntity = stateEntity,
                TraitBasedObjects = TraitBasedObjects[stateEntity],
                TraitBasedObjectIds = TraitBasedObjectIds[stateEntity],

                ColoredBuffer = ColoredData[stateEntity],
                CarrierBuffer = CarrierData[stateEntity],
                CarriableBuffer = CarriableData[stateEntity],
                LocalizedBuffer = LocalizedData[stateEntity],
                LockableBuffer = LockableData[stateEntity],
                EndBuffer = EndData[stateEntity],
            };
        }

        public StateData CopyStateData(StateData stateData)
        {
            return stateData.Copy(JobIndex, EntityCommandBuffer);
        }

        public StateEntityKey GetStateDataKey(StateData stateData)
        {
            return new StateEntityKey { Entity = stateData.StateEntity, HashCode = stateData.GetHashCode()};
        }

        public void DestroyState(StateEntityKey stateKey)
        {
            EntityCommandBuffer.DestroyEntity(JobIndex, stateKey.Entity);
        }

        public StateData CreateStateData()
        {
            return new StateData(JobIndex, EntityCommandBuffer, EntityCommandBuffer.CreateEntity(JobIndex, m_StateArchetype));
        }

        public bool Equals(StateData x, StateData y)
        {
            return x.Equals(y);
        }

        public int GetHashCode(StateData obj)
        {
            return obj.GetHashCode();
        }
    }

    [DisableAutoCreation]
    class StateManager : JobComponentSystem, ITraitBasedStateManager<TraitBasedObject, StateEntityKey, StateData, StateDataContext>
    {
        public ExclusiveEntityTransaction ExclusiveEntityTransaction;
        public event Action Destroying;

        List<EntityCommandBuffer> m_EntityCommandBuffers;
        EntityArchetype m_StateArchetype;

        protected override void OnCreate()
        {
            m_StateArchetype = EntityManager.CreateArchetype(typeof(State), typeof(TraitBasedObject), typeof(TraitBasedObjectId), typeof(HashCode),
                typeof(Colored),typeof(Carrier),typeof(Carriable),typeof(Localized),typeof(Lockable),typeof(End));

            BeginEntityExclusivity();
            m_EntityCommandBuffers = new List<EntityCommandBuffer>();
        }

        protected override void OnDestroy()
        {
            Destroying?.Invoke();
            EndEntityExclusivity();
            base.OnDestroy();
        }

        public EntityCommandBuffer GetEntityCommandBuffer()
        {
            var ecb = new EntityCommandBuffer(Allocator.Persistent);
            m_EntityCommandBuffers.Add(ecb);
            return ecb;
        }

       public StateData CreateStateData()
        {
            EndEntityExclusivity();
            var stateEntity = EntityManager.CreateEntity(m_StateArchetype);
            BeginEntityExclusivity();
            return new StateData(this, stateEntity, true);
        }

        public StateData GetStateData(StateEntityKey stateKey, bool readWrite = false)
        {
            return !Enabled ? default : new StateData(this, stateKey.Entity, readWrite);
        }

        public void DestroyState(StateEntityKey stateKey)
        {
            var stateEntity = stateKey.Entity;
            if (EntityManager != null && EntityManager.IsCreated && EntityManager.Exists(stateEntity))
            {
                EndEntityExclusivity();
                EntityManager.DestroyEntity(stateEntity);
                BeginEntityExclusivity();
            }
        }

        public StateDataContext GetStateDataContext()
        {
            return new StateDataContext(this, m_StateArchetype);
        }

        public StateEntityKey GetStateDataKey(StateData stateData)
        {
            return new StateEntityKey { Entity = stateData.StateEntity, HashCode = stateData.GetHashCode()};
        }

        public StateData CopyStateData(StateData stateData)
        {
            EndEntityExclusivity();
            var copyStateEntity = EntityManager.Instantiate(stateData.StateEntity);
            BeginEntityExclusivity();
            return new StateData(this, copyStateEntity, true);
        }

        public StateEntityKey CopyState(StateEntityKey stateKey)
        {
            EndEntityExclusivity();
            var copyStateEntity = EntityManager.Instantiate(stateKey.Entity);
            BeginEntityExclusivity();
            var stateData = GetStateData(stateKey);
            return new StateEntityKey { Entity = copyStateEntity, HashCode = stateData.GetHashCode()};
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps) => JobHandle.CombineDependencies(inputDeps, EntityManager.ExclusiveEntityTransactionDependency);

        public bool Equals(StateData x, StateData y)
        {
            return x.Equals(y);
        }

        public int GetHashCode(StateData obj)
        {
            return obj.GetHashCode();
        }

        void BeginEntityExclusivity()
        {
            ExclusiveEntityTransaction = EntityManager.BeginExclusiveEntityTransaction();
        }

        void EndEntityExclusivity()
        {
            EntityManager.EndExclusiveEntityTransaction();

            foreach (var ecb in m_EntityCommandBuffers)
            {
                if (ecb.IsCreated)
                    ecb.Dispose();
            }
            m_EntityCommandBuffers.Clear();
        }
    }

    struct DestroyStatesJobScheduler : IDestroyStatesScheduler<StateEntityKey, StateData, StateDataContext, StateManager>
    {
        public StateManager StateManager { private get; set; }
        public NativeQueue<StateEntityKey> StatesToDestroy { private get; set; }

        public JobHandle Schedule(JobHandle inputDeps)
        {
            var entityManager = StateManager.EntityManager;
            inputDeps = JobHandle.CombineDependencies(inputDeps, entityManager.ExclusiveEntityTransactionDependency);

            var stateDataContext = StateManager.GetStateDataContext();
            var ecb = StateManager.GetEntityCommandBuffer();
            stateDataContext.EntityCommandBuffer = ecb.ToConcurrent();
            var destroyStatesJobHandle = new DestroyStatesJob<StateEntityKey, StateData, StateDataContext>()
            {
                StateDataContext = stateDataContext,
                StatesToDestroy = StatesToDestroy
            }.Schedule(inputDeps);

            var playbackECBJobHandle = new PlaybackSingleECBJob()
            {
                ExclusiveEntityTransaction = StateManager.ExclusiveEntityTransaction,
                EntityCommandBuffer = ecb
            }.Schedule(destroyStatesJobHandle);

            entityManager.ExclusiveEntityTransactionDependency = playbackECBJobHandle;
            return playbackECBJobHandle;
        }
    }
}
