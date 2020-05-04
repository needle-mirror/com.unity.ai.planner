using System;
using Unity.Collections;
using Unity.Entities;

namespace Unity.AI.Planner.DomainLanguage.TraitBased
{
    struct ObjectIdPairs
    {
        public ObjectId Left;
        public ObjectId Right;
    }

    struct ObjectCorrespondence
    {
        NativeHashMap<ObjectId, byte> m_RightIds;
        NativeHashMap<ObjectId, ObjectId> m_Matches;
        NativeList<ObjectIdPairs> m_TempMatches;
        NativeList<ObjectIdPairs> m_ObjectPairsQueue;
        NativeHashMap<ObjectId, int> m_LhsIdToIndex;
        NativeHashMap<ObjectId, int> m_RhsIdToIndex;

        public ObjectCorrespondence(int numberOfObjects, Allocator allocator)
        {
            m_RightIds = new NativeHashMap<ObjectId, byte>(numberOfObjects, allocator);
            m_Matches = new NativeHashMap<ObjectId, ObjectId>(numberOfObjects + 1, allocator)
            {
                [ ObjectId.None ] = ObjectId.None // default null relation case
            };
            m_TempMatches = new NativeList<ObjectIdPairs>(numberOfObjects, allocator);
            m_ObjectPairsQueue = new NativeList<ObjectIdPairs>(numberOfObjects, allocator);

            m_LhsIdToIndex = new NativeHashMap<ObjectId, int>(numberOfObjects, allocator);
            m_RhsIdToIndex = new NativeHashMap<ObjectId, int>(numberOfObjects, allocator);
        }

        public ObjectCorrespondence(DynamicBuffer<TraitBasedObjectId> lhsObjects, DynamicBuffer<TraitBasedObjectId> rhsObjects, Allocator allocator)
            : this(lhsObjects.Length, allocator)
        {
            Initialize(lhsObjects, rhsObjects);
        }

        public void Initialize(DynamicBuffer<TraitBasedObjectId> lhsObjects, DynamicBuffer<TraitBasedObjectId> rhsObjects)
        {
            Clear();
            Resize(lhsObjects.Length);
            for (int i = 0; i < lhsObjects.Length; i++)
            {
                m_LhsIdToIndex[lhsObjects[i].Id] = i;
                m_RhsIdToIndex[rhsObjects[i].Id] = i;
            }
        }

        public void Add(ObjectId left, ObjectId right)
        {
            var startPair = new ObjectIdPairs { Left = left, Right = right };
            m_ObjectPairsQueue.Add(startPair);
            m_Matches.TryAdd(left, right);
            m_TempMatches.Add(startPair);
            m_RightIds.TryAdd(right, 0);
        }

        public bool TryGetValue(ObjectId lhs, out ObjectId rhs)
        {
            return m_Matches.TryGetValue(lhs, out rhs);
        }

        public bool ContainsRHS(ObjectId rhs)
        {
            return m_RightIds.ContainsKey(rhs);
        }

        public void BeginNewTraversal()
        {
            m_TempMatches.Clear();
        }

        public void RevertTraversalChanges()
        {
            for (int i = 0; i < m_TempMatches.Length; i++)
            {
                var tempPair = m_TempMatches[i];
                m_Matches.Remove(tempPair.Left);
                m_RightIds.Remove(tempPair.Right);
            }
            m_TempMatches.Clear();
        }

        public bool Next(out ObjectId left, out ObjectId right)
        {
            int length = m_ObjectPairsQueue.Length;
            if (length == 0)
            {
                left = default;
                right = default;
                return false;
            }

            length--;
            var pair = m_ObjectPairsQueue[length];
            m_ObjectPairsQueue.RemoveAtSwapBack(length);
            left = pair.Left;
            right = pair.Right;
            return true;
        }

        public int GetLHSIndex(ObjectId obj)
        {
            return m_LhsIdToIndex[obj];
        }

        public int GetRHSIndex(ObjectId obj)
        {
            return m_RhsIdToIndex[obj];
        }

        public NativeHashMap<ObjectId, ObjectId> GetCorrespondence(Allocator allocator)
        {
            var copy = new NativeHashMap<ObjectId, ObjectId>(m_Matches.Count(), allocator);

            using(var keys = m_Matches.GetKeyArray(Allocator.TempJob))
            using(var values = m_Matches.GetValueArray(Allocator.TempJob))
            {
                for (int i = 0; i < keys.Length; i++)
                    copy[keys[i]] = values[i];
            }

            return copy;
        }

        public void Clear()
        {
            m_Matches.Clear();
            m_Matches[ObjectId.None] = ObjectId.None; // add back default mapping for ObjectId.None
            m_TempMatches.Clear();
            m_RightIds.Clear();
            m_ObjectPairsQueue.Clear();
            m_LhsIdToIndex.Clear();
            m_RhsIdToIndex.Clear();
        }

        public void Resize(int size)
        {
            if (size < m_RightIds.Capacity)
                return;

            m_RightIds.Capacity = size;
            m_Matches.Capacity = size + 1;
            m_TempMatches.Capacity = size;
            m_ObjectPairsQueue.Capacity = size;
            m_LhsIdToIndex.Capacity = size;
            m_RhsIdToIndex.Capacity = size;
        }

        public void Dispose()
        {
            if (m_Matches.IsCreated)
                m_Matches.Dispose();
            if (m_TempMatches.IsCreated)
                m_TempMatches.Dispose();
            if (m_RightIds.IsCreated)
                m_RightIds.Dispose();
            if (m_ObjectPairsQueue.IsCreated)
                m_ObjectPairsQueue.Dispose();
            if (m_LhsIdToIndex.IsCreated)
                m_LhsIdToIndex.Dispose();
            if (m_RhsIdToIndex.IsCreated)
                m_RhsIdToIndex.Dispose();
        }
    }

}
