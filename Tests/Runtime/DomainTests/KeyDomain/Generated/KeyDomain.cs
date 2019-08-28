using System;
using System.Text;
using Unity.AI.Planner;
using Unity.AI.Planner.DomainLanguage.TraitBased;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

namespace KeyDomain
{
    internal struct StateEntityKey : IEquatable<StateEntityKey>, IStateKey
    {
        public Entity Entity;
        public int HashCode;

        public bool Equals(StateEntityKey other) => Entity == other.Entity;
        public override bool Equals(object other) => (other is StateEntityKey otherKey) && Equals(otherKey);
        public static bool operator==(StateEntityKey x, StateEntityKey y) => x.Entity == y.Entity;
        public static bool operator !=(StateEntityKey x, StateEntityKey y) => x.Entity != y.Entity;

        public override int GetHashCode() => HashCode;

        public override string ToString() => $"StateEntityKey ({Entity} {HashCode})";
        public string Label => Entity.ToString();
    }

    internal struct TerminationEvaluator : ITerminationEvaluator<StateData>
    {
        public bool IsTerminal(StateData state)
        {
            var endObjects = new NativeList<(DomainObject, int)>(1, Allocator.Temp);
            state.GetDomainObjects(endObjects, typeof(End));

            return endObjects.Length > 0;
        }
    }

    internal struct Heuristic : IHeuristic<StateData>
    {
        public float Evaluate(StateData state)
        {
            return 0;
        }
    }

    internal static class TraitArrayIndex<T>
        where T : struct, ITrait
    {
        public static int Index = -1;
    }

    internal struct DomainObject : IDomainObject
    {
        static DomainObject()
        {
            TraitArrayIndex<Colored>.Index = 0;
            TraitArrayIndex<Carrier>.Index = 1;
            TraitArrayIndex<Carriable>.Index = 2;
            TraitArrayIndex<Localized>.Index = 3;
            TraitArrayIndex<Lockable>.Index = 4;
            TraitArrayIndex<End>.Index = 5;
        }

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

        public static byte Unset = Byte.MaxValue;

        public static DomainObject Default => new DomainObject()
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

        static ComponentType s_ColoredType = new ComponentType(typeof(Colored));
        static ComponentType s_CarrierType = new ComponentType(typeof(Carrier));
        static ComponentType s_CarriableType = new ComponentType(typeof(Carriable));
        static ComponentType s_LocalizedType = new ComponentType(typeof(Localized));
        static ComponentType s_LockableType = new ComponentType(typeof(Lockable));
        static ComponentType s_EndType = new ComponentType(typeof(End));

        public bool HasSameTraits(DomainObject other)
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

        public bool HasTraitSubset(DomainObject traitSubset)
        {
            for (var i = 0; i < Length; i++)
            {
                var requiredTrait = traitSubset[i];
                if (requiredTrait != Unset && this[i] == Unset)
                    return false;
            }
            return true;
        }

        public bool MatchesTraitFilter(ComponentType[] componentTypes)
        {
            foreach (var t in componentTypes)
            {
                if (t == s_ColoredType)
                {
                    if ((t.AccessModeType == ComponentType.AccessMode.Exclude && ColoredIndex != Unset) ||
                        (t.AccessModeType != ComponentType.AccessMode.Exclude && ColoredIndex == Unset))
                        return false;
                }
                else if (t == s_CarrierType)
                {
                    if ((t.AccessModeType == ComponentType.AccessMode.Exclude && CarrierIndex != Unset) ||
                        (t.AccessModeType != ComponentType.AccessMode.Exclude && CarrierIndex == Unset))
                        return false;
                }
                else if (t == s_CarriableType)
                {
                    if ((t.AccessModeType == ComponentType.AccessMode.Exclude && CarriableIndex != Unset) ||
                        (t.AccessModeType != ComponentType.AccessMode.Exclude && CarriableIndex == Unset))
                        return false;
                }
                else if (t == s_LocalizedType)
                {
                    if ((t.AccessModeType == ComponentType.AccessMode.Exclude && LocalizedIndex != Unset) ||
                        (t.AccessModeType != ComponentType.AccessMode.Exclude && LocalizedIndex == Unset))
                        return false;
                }
                else if (t == s_LockableType)
                {
                    if ((t.AccessModeType == ComponentType.AccessMode.Exclude && LockableIndex != Unset) ||
                        (t.AccessModeType != ComponentType.AccessMode.Exclude && LockableIndex == Unset))
                        return false;
                }
                else if (t == s_EndType)
                {
                    if ((t.AccessModeType == ComponentType.AccessMode.Exclude && EndIndex != Unset) ||
                        (t.AccessModeType != ComponentType.AccessMode.Exclude && EndIndex == Unset))
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

    internal struct StateData : ITraitBasedStateData<DomainObject>
    {
        public Entity StateEntity;
        public DynamicBuffer<DomainObject> DomainObjects;
        public DynamicBuffer<DomainObjectID> DomainObjectIDs;

        public DynamicBuffer<Colored> ColoredBuffer;
        public DynamicBuffer<Carrier> CarrierBuffer;
        public DynamicBuffer<Carriable> CarriableBuffer;
        public DynamicBuffer<Localized> LocalizedBuffer;
        public DynamicBuffer<Lockable> LockableBuffer;
        public DynamicBuffer<End> EndBuffer;

        static ComponentType s_ColoredType = new ComponentType(typeof(Colored));
        static ComponentType s_CarrierType = new ComponentType(typeof(Carrier));
        static ComponentType s_CarriableType = new ComponentType(typeof(Carriable));
        static ComponentType s_LocalizedType = new ComponentType(typeof(Localized));
        static ComponentType s_LockableType = new ComponentType(typeof(Lockable));
        static ComponentType s_EndType = new ComponentType(typeof(End));

        public StateData(JobComponentSystem system, Entity stateEntity, bool readWrite = false)
        {
            StateEntity = stateEntity;
            DomainObjects = system.GetBufferFromEntity<DomainObject>(!readWrite)[stateEntity];
            DomainObjectIDs = system.GetBufferFromEntity<DomainObjectID>(!readWrite)[stateEntity];
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
            DomainObjects = entityCommandBuffer.AddBuffer<DomainObject>(jobIndex, stateEntity);
            DomainObjectIDs = entityCommandBuffer.AddBuffer<DomainObjectID>(jobIndex, stateEntity);
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
            var domainObjects = entityCommandBuffer.SetBuffer<DomainObject>(jobIndex, stateEntity);
            domainObjects.CopyFrom(DomainObjects.AsNativeArray());
            var domainObjectIDs = entityCommandBuffer.SetBuffer<DomainObjectID>(jobIndex, stateEntity);
            domainObjectIDs.CopyFrom(DomainObjectIDs.AsNativeArray());

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
                DomainObjects = domainObjects,
                DomainObjectIDs = domainObjectIDs,
                ColoredBuffer = Coloreds,
                CarrierBuffer = Carriers,
                CarriableBuffer = Carriables,
                LocalizedBuffer = Localizeds,
                LockableBuffer = Lockables,
                EndBuffer = Ends,
            };
        }

        public (DomainObject, DomainObjectID) AddDomainObject(ComponentType[] types, string name = null)
        {
            var domainObject = DomainObject.Default;
            var domainObjectID = new DomainObjectID() { ID = ObjectID.GetNext() };
#if DEBUG
            if (!string.IsNullOrEmpty(name))
                domainObjectID.Name.CopyFrom(name);
#endif

            foreach (var t in types)
            {
                if (t == s_ColoredType)
                {
                    ColoredBuffer.Add(default);
                    domainObject.ColoredIndex = (byte) (ColoredBuffer.Length - 1);
                }
                else if (t == s_CarrierType)
                {
                    CarrierBuffer.Add(default);
                    domainObject.CarrierIndex = (byte) (CarrierBuffer.Length - 1);
                }
                else if (t == s_CarriableType)
                {
                    CarriableBuffer.Add(default);
                    domainObject.CarriableIndex = (byte) (CarriableBuffer.Length - 1);
                }
                else if (t == s_LocalizedType)
                {
                    LocalizedBuffer.Add(default);
                    domainObject.LocalizedIndex = (byte) (LocalizedBuffer.Length - 1);
                }
                else if (t == s_LockableType)
                {
                    LockableBuffer.Add(default);
                    domainObject.LockableIndex = (byte) (LockableBuffer.Length - 1);
                }
                else if (t == s_EndType)
                {
                    EndBuffer.Add(default);
                    domainObject.EndIndex = (byte) (EndBuffer.Length - 1);
                }
            }

            DomainObjectIDs.Add(domainObjectID);
            DomainObjects.Add(domainObject);

            return (domainObject, domainObjectID);
        }

        public void SetTraitOnObject(ITrait trait, ref DomainObject domainObject)
        {
            if (trait is Colored ColoredTrait)
                SetTraitOnObject(ColoredTrait, ref domainObject);
            else if (trait is Carrier CarrierTrait)
                SetTraitOnObject(CarrierTrait, ref domainObject);
            else if (trait is Carriable CarriableTrait)
                SetTraitOnObject(CarriableTrait, ref domainObject);
            else if (trait is Localized LocalizedTrait)
                SetTraitOnObject(LocalizedTrait, ref domainObject);
            else if (trait is Lockable LockableTrait)
                SetTraitOnObject(LockableTrait, ref domainObject);
            else if (trait is End EndTrait)
                SetTraitOnObject(EndTrait, ref domainObject);

            throw new ArgumentException($"Trait {trait} of type {trait.GetType()} is not supported in this domain.");
        }

        public TTrait GetTraitOnObject<TTrait>(DomainObject domainObject) where TTrait : struct, ITrait
        {
            var domainObjectTraitIndex = TraitArrayIndex<TTrait>.Index;
            if (domainObjectTraitIndex == -1)
                throw new ArgumentException($"Trait {typeof(TTrait)} not supported in this domain");

            var traitBufferIndex = domainObject[domainObjectTraitIndex];
            if (traitBufferIndex == DomainObject.Unset)
                throw new ArgumentException($"Trait of type {typeof(TTrait)} does not exist on object {domainObject}.");

            return GetBuffer<TTrait>()[traitBufferIndex];
        }

        public void SetTraitOnObject<TTrait>(TTrait trait, ref DomainObject domainObject) where TTrait : struct, ITrait
        {
            var objectIndex = GetDomainObjectIndex(domainObject);
            if (objectIndex == -1)
                throw new ArgumentException($"Object {domainObject} does not exist within the state data {this}.");

            var traitIndex = TraitArrayIndex<TTrait>.Index;
            var traitBuffer = GetBuffer<TTrait>();

            var bufferIndex = domainObject[traitIndex];
            if (bufferIndex == DomainObject.Unset)
            {
                traitBuffer.Add(trait);
                domainObject[traitIndex] = (byte) (traitBuffer.Length - 1);

                DomainObjects[objectIndex] = domainObject;
            }
            else
            {
                traitBuffer[bufferIndex] = trait;
            }
        }

        public bool RemoveTraitOnObject<TTrait>(ref DomainObject domainObject) where TTrait : struct, ITrait
        {
            var objectTraitIndex = TraitArrayIndex<TTrait>.Index;
            var traitBuffer = GetBuffer<TTrait>();

            var traitBufferIndex = domainObject[objectTraitIndex];
            if (traitBufferIndex == DomainObject.Unset)
                return false;

            // last index
            var lastBufferIndex = traitBuffer.Length - 1;

            // Swap back and remove
            var lastTrait = traitBuffer[lastBufferIndex];
            traitBuffer[lastBufferIndex] = traitBuffer[traitBufferIndex];
            traitBuffer[traitBufferIndex] = lastTrait;
            traitBuffer.RemoveAt(lastBufferIndex);

            // Update index for object with last trait in buffer
            for (int i = 0; i < DomainObjects.Length; i++)
            {
                var otherDomainObject = DomainObjects[i];
                if (otherDomainObject[objectTraitIndex] == lastBufferIndex)
                {
                    otherDomainObject[objectTraitIndex] = traitBufferIndex;
                    DomainObjects[i] = otherDomainObject;
                    break;
                }
            }

            // Update domainObject in buffer (ref is to a copy)
            for (int i = 0; i < DomainObjects.Length; i++)
            {
                if (domainObject.Equals(DomainObjects[i]))
                {
                    domainObject[objectTraitIndex] = DomainObject.Unset;
                    DomainObjects[i] = domainObject;
                    return true;
                }
            }

            throw new ArgumentException($"DomainObject {domainObject} does not exist in the state container {this}.");
        }

        public bool RemoveDomainObject(DomainObject domainObject)
        {
            var objectIndex = GetDomainObjectIndex(domainObject);
            if (objectIndex == -1)
                return false;

            RemoveTraitOnObject<Colored>(ref domainObject);
            RemoveTraitOnObject<Carrier>(ref domainObject);
            RemoveTraitOnObject<Carriable>(ref domainObject);
            RemoveTraitOnObject<Localized>(ref domainObject);
            RemoveTraitOnObject<Lockable>(ref domainObject);
            RemoveTraitOnObject<End>(ref domainObject);

            DomainObjects.RemoveAt(objectIndex);
            DomainObjectIDs.RemoveAt(objectIndex);

            return true;
        }


        public TTrait GetTraitOnObjectAtIndex<TTrait>(int domainObjectIndex) where TTrait : struct, ITrait
        {
            var domainObjectTraitIndex = TraitArrayIndex<TTrait>.Index;
            if (domainObjectTraitIndex == -1)
                throw new ArgumentException($"Trait {typeof(TTrait)} not supported in this domain");

            var domainObject = DomainObjects[domainObjectIndex];
            var traitBufferIndex = domainObject[domainObjectTraitIndex];
            if (traitBufferIndex == DomainObject.Unset)
                throw new Exception($"Trait index for {typeof(TTrait)} is not set for domain object {domainObject}");

            return GetBuffer<TTrait>()[traitBufferIndex];
        }

        public void SetTraitOnObjectAtIndex<TTrait>(TTrait trait, int domainObjectIndex) where TTrait : struct, ITrait
        {
            var domainObjectTraitIndex = TraitArrayIndex<TTrait>.Index;
            if (domainObjectTraitIndex == -1)
                throw new ArgumentException($"Trait {typeof(TTrait)} not supported in this domain");

            var domainObject = DomainObjects[domainObjectIndex];
            var traitBufferIndex = domainObject[domainObjectTraitIndex];
            var traitBuffer = GetBuffer<TTrait>();
            if (traitBufferIndex == DomainObject.Unset)
            {
                traitBuffer.Add(trait);
                traitBufferIndex = (byte)(traitBuffer.Length - 1);
                domainObject[domainObjectTraitIndex] = traitBufferIndex;
                DomainObjects[domainObjectIndex] = domainObject;
            }
            else
            {
                traitBuffer[traitBufferIndex] = trait;
            }
        }

        public bool RemoveTraitOnObjectAtIndex<TTrait>(int domainObjectIndex) where TTrait : struct, ITrait
        {
            var objectTraitIndex = TraitArrayIndex<TTrait>.Index;
            var traitBuffer = GetBuffer<TTrait>();

            var domainObject = DomainObjects[domainObjectIndex];
            var traitBufferIndex = domainObject[objectTraitIndex];
            if (traitBufferIndex == DomainObject.Unset)
                return false;

            // last index
            var lastBufferIndex = traitBuffer.Length - 1;

            // Swap back and remove
            var lastTrait = traitBuffer[lastBufferIndex];
            traitBuffer[lastBufferIndex] = traitBuffer[traitBufferIndex];
            traitBuffer[traitBufferIndex] = lastTrait;
            traitBuffer.RemoveAt(lastBufferIndex);

            // Update index for object with last trait in buffer
            for (int i = 0; i < DomainObjects.Length; i++)
            {
                var otherDomainObject = DomainObjects[i];
                if (otherDomainObject[objectTraitIndex] == lastBufferIndex)
                {
                    otherDomainObject[objectTraitIndex] = traitBufferIndex;
                    DomainObjects[i] = otherDomainObject;
                    break;
                }
            }

            domainObject[objectTraitIndex] = DomainObject.Unset;
            DomainObjects[domainObjectIndex] = domainObject;

            throw new ArgumentException($"DomainObject {domainObject} does not exist in the state container {this}.");
        }

        public bool RemoveDomainObjectAtIndex(int domainObjectIndex)
        {
            RemoveTraitOnObjectAtIndex<Colored>(domainObjectIndex);
            RemoveTraitOnObjectAtIndex<Carrier>(domainObjectIndex);
            RemoveTraitOnObjectAtIndex<Carriable>(domainObjectIndex);
            RemoveTraitOnObjectAtIndex<Localized>(domainObjectIndex);
            RemoveTraitOnObjectAtIndex<Lockable>(domainObjectIndex);
            RemoveTraitOnObjectAtIndex<End>(domainObjectIndex);

            DomainObjects.RemoveAt(domainObjectIndex);
            DomainObjectIDs.RemoveAt(domainObjectIndex);

            return true;
        }


        public NativeArray<(DomainObject, int)> GetDomainObjects(NativeList<(DomainObject, int)> domainObjects, params ComponentType[] traitFilter)
        {
            for (var i = 0; i < DomainObjects.Length; i++)
            {
                var domainObject = DomainObjects[i];
                if (domainObject.MatchesTraitFilter(traitFilter))
                    domainObjects.Add((domainObject, i));
            }

            return domainObjects.AsArray();
        }

        public void GetDomainObjectIndices(DomainObject traitSubset, NativeList<int> domainObjects)
        {
            var domainObjectArray = DomainObjects.AsNativeArray();
            for (var i = 0; i < domainObjectArray.Length; i++)
            {
                if (domainObjectArray[i].HasTraitSubset(traitSubset))
                    domainObjects.Add(i);
            }
        }

        public int GetDomainObjectIndex(DomainObject domainObject)
        {
            var objectIndex = -1;
            for (int i = 0; i < DomainObjects.Length; i++)
            {
                if (DomainObjects[i].Equals(domainObject))
                {
                    objectIndex = i;
                    break;
                }
            }

            return objectIndex;
        }

        public DomainObjectID GetDomainObjectID(DomainObject domainObject)
        {
            var index = GetDomainObjectIndex(domainObject); // TODO Should we have an easier way to retrieve a domain object ID ?
            return DomainObjectIDs[index];
        }

        public DomainObjectID GetDomainObjectID(int domainObjectIndex)
        {
            return DomainObjectIDs[domainObjectIndex];
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

        public bool Equals(StateData other)
        {
            if (StateEntity == other.StateEntity)
                return true;

            // Easy check is to make sure each state has the same number of domain objects
            if (DomainObjects.Length != other.DomainObjects.Length
                || ColoredBuffer.Length != other.ColoredBuffer.Length
                || CarrierBuffer.Length != other.CarrierBuffer.Length
                || CarriableBuffer.Length != other.CarriableBuffer.Length
                || LocalizedBuffer.Length != other.LocalizedBuffer.Length
                || LockableBuffer.Length != other.LockableBuffer.Length
                || EndBuffer.Length != other.EndBuffer.Length )
                return false;

            var lhsObjects = DomainObjects.AsNativeArray();
            var rhsObjects = new NativeArray<DomainObject>(other.DomainObjects.Length, Allocator.Temp);
            rhsObjects.CopyFrom(other.DomainObjects.AsNativeArray());

            for (int lhsIndex = 0; lhsIndex < lhsObjects.Length; lhsIndex++)
            {
                var domainObjectLHS = lhsObjects[lhsIndex];

                var hasColored = domainObjectLHS.ColoredIndex != DomainObject.Unset;
                var hasCarrier = domainObjectLHS.CarrierIndex != DomainObject.Unset;
                var hasCarriable = domainObjectLHS.CarriableIndex != DomainObject.Unset;
                var hasLocalized = domainObjectLHS.LocalizedIndex != DomainObject.Unset;
                var hasLockable = domainObjectLHS.LockableIndex != DomainObject.Unset;
                var hasEnd = domainObjectLHS.EndIndex != DomainObject.Unset;

                var foundMatch = false;
                for (var rhsIndex = lhsIndex; rhsIndex < rhsObjects.Length; rhsIndex++)
                {
                    var domainObjectRHS = rhsObjects[rhsIndex];

                    if (!domainObjectLHS.HasSameTraits(domainObjectRHS))
                        continue;

                    if (hasColored && !ColoredBuffer[domainObjectLHS.ColoredIndex].Equals(other.ColoredBuffer[domainObjectRHS.ColoredIndex]))
                        continue;

                    if (hasCarrier && !CarrierBuffer[domainObjectLHS.CarrierIndex].Equals(other.CarrierBuffer[domainObjectRHS.CarrierIndex]))
                        continue;

                    if (hasCarriable && !CarriableBuffer[domainObjectLHS.CarriableIndex].Equals(other.CarriableBuffer[domainObjectRHS.CarriableIndex]))
                        continue;

                    if (hasLocalized && !LocalizedBuffer[domainObjectLHS.LocalizedIndex].Equals(other.LocalizedBuffer[domainObjectRHS.LocalizedIndex]))
                        continue;

                    if (hasLockable && !LockableBuffer[domainObjectLHS.LockableIndex].Equals(other.LockableBuffer[domainObjectRHS.LockableIndex]))
                        continue;

                    if (hasEnd && !EndBuffer[domainObjectLHS.EndIndex].Equals(other.EndBuffer[domainObjectRHS.EndIndex]))
                        continue;

                    // Swap match to lhs index (to align). Keeps remaining objs in latter part of array without resize.
                    rhsObjects[rhsIndex] = rhsObjects[lhsIndex];  // technically only Lockable to preserve obj at lhsIndex

                    foundMatch = true;
                    break;
                }

                if (!foundMatch)
                {
                    rhsObjects.Dispose();
                    return false;
                }
            }

            rhsObjects.Dispose();
            return true;
        }

        public override int GetHashCode()
        {
            // h = 3860031 + (h+y)*2779 + (h*y*2)   // from How to Hash a Set by Richard O’Keefe
            var stateHashValue = 0;

            var objectIDs = DomainObjectIDs;
            foreach (var element in objectIDs.AsNativeArray())
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

            for (var domainObjectIndex = 0; domainObjectIndex < DomainObjects.Length; domainObjectIndex++)
            {
                var domainObject = DomainObjects[domainObjectIndex];
                sb.AppendLine(DomainObjectIDs[domainObjectIndex].ToString());

                var i = 0;

                var traitIndex = domainObject[i++];
                if (traitIndex != DomainObject.Unset)
                    sb.AppendLine(ColoredBuffer[traitIndex].ToString());

                traitIndex = domainObject[i++];
                if (traitIndex != DomainObject.Unset)
                    sb.AppendLine(CarrierBuffer[traitIndex].ToString());

                traitIndex = domainObject[i++];
                if (traitIndex != DomainObject.Unset)
                    sb.AppendLine(CarriableBuffer[traitIndex].ToString());

                traitIndex = domainObject[i++];
                if (traitIndex != DomainObject.Unset)
                    sb.AppendLine(LocalizedBuffer[traitIndex].ToString());

                traitIndex = domainObject[i++];
                if (traitIndex != DomainObject.Unset)
                    sb.AppendLine(LockableBuffer[traitIndex].ToString());

                traitIndex = domainObject[i++];
                if (traitIndex != DomainObject.Unset)
                    sb.AppendLine(EndBuffer[traitIndex].ToString());

                sb.AppendLine();
            }

            return sb.ToString();
        }
    }

    internal struct StateDataContext : ITraitBasedStateDataContext<DomainObject, StateEntityKey, StateData>
    {
        internal EntityCommandBuffer.Concurrent EntityCommandBuffer;
        internal EntityArchetype m_StateArchetype;
        internal int JobIndex;

        [ReadOnly] public BufferFromEntity<DomainObject> DomainObjects;
        [ReadOnly] public BufferFromEntity<DomainObjectID> DomainObjectIDs;
        [ReadOnly] public BufferFromEntity<Colored> ColoredData;
        [ReadOnly] public BufferFromEntity<Carrier> CarrierData;
        [ReadOnly] public BufferFromEntity<Carriable> CarriableData;
        [ReadOnly] public BufferFromEntity<Localized> LocalizedData;
        [ReadOnly] public BufferFromEntity<Lockable> LockableData;
        [ReadOnly] public BufferFromEntity<End> EndData;

        public StateDataContext(JobComponentSystem system, EntityArchetype stateArchetype)
        {
            EntityCommandBuffer = default;
            DomainObjects = system.GetBufferFromEntity<DomainObject>(true);
            DomainObjectIDs = system.GetBufferFromEntity<DomainObjectID>(true);
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
                DomainObjects = DomainObjects[stateEntity],
                DomainObjectIDs = DomainObjectIDs[stateEntity],

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

    internal class StateManager : JobComponentSystem, ITraitBasedStateManager<DomainObject, StateEntityKey, StateData, StateDataContext>, IStateManagerInternal
    {
        EntityArchetype m_StateArchetype;

        public StateManager()
        {
            m_StateArchetype = default;
        }

        protected override void OnCreate()
        {
            m_StateArchetype = EntityManager.CreateArchetype(typeof(State), typeof(DomainObject), typeof(DomainObjectID), typeof(HashCode),
                typeof(Colored),typeof(Carrier),typeof(Carriable),typeof(Localized),typeof(Lockable),typeof(End));
        }

        public StateData CreateStateData()
        {
            var stateEntity = EntityManager.CreateEntity(m_StateArchetype);
            return new StateData(this, stateEntity, true);
        }

        public StateData GetStateData(StateEntityKey stateKey, bool readWrite = false)
        {
            return !Enabled ? default : new StateData(this, stateKey.Entity, readWrite);
        }

        public void DestroyState(StateEntityKey stateKey)
        {
            EntityManager.DestroyEntity(stateKey.Entity);
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
            var copyStateEntity = EntityManager.Instantiate(stateData.StateEntity);
            return new StateData(this, copyStateEntity, true);
        }

        public StateEntityKey CopyState(StateEntityKey stateKey)
        {
            var copyStateEntity = EntityManager.Instantiate(stateKey.Entity);
            var stateData = GetStateData(stateKey);
            return new StateEntityKey { Entity = copyStateEntity, HashCode = stateData.GetHashCode()};
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps) => inputDeps;

        public bool Equals(StateData x, StateData y)
        {
            return x.Equals(y);
        }

        public int GetHashCode(StateData obj)
        {
            return obj.GetHashCode();
        }

        public IStateData GetStateData(IStateKey stateKey, bool readWrite)
        {
            return GetStateData((StateEntityKey)stateKey, readWrite);
        }
    }
}
