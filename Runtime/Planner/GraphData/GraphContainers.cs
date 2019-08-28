using System;
using Unity.Collections;

namespace Unity.AI.Planner
{
    // NOTE: This may be useful for deterministic search domains
    struct OneToOneDirectedGraph<TNodeKey, TEdgeKey> : IDisposable
        where TNodeKey : struct, IEquatable<TNodeKey>
        where TEdgeKey : struct, IEquatable<TEdgeKey>
    {
        NativeMultiHashMap<TNodeKey, TEdgeKey> NodeToEdgeLookup;
        NativeHashMap<TEdgeKey, TNodeKey> EdgeToNodeLookup;

        public OneToOneDirectedGraph(int nodeCapacity = 1, int edgeCapacity = 1)
        {
            NodeToEdgeLookup = new NativeMultiHashMap<TNodeKey, TEdgeKey>(nodeCapacity, Allocator.Persistent);
            EdgeToNodeLookup = new NativeHashMap<TEdgeKey, TNodeKey>(edgeCapacity, Allocator.Persistent);
        }

        public void ExpandBy(int minimumFreeNodeCapacity, int minimumFreeEdgeCapacity)
        {
            NodeToEdgeLookup.Capacity = Math.Max(NodeToEdgeLookup.Capacity, NodeToEdgeLookup.Length + minimumFreeNodeCapacity);
            EdgeToNodeLookup.Capacity = Math.Max(EdgeToNodeLookup.Capacity, EdgeToNodeLookup.Length + minimumFreeEdgeCapacity);
        }

        public void Dispose()
        {
            if (NodeToEdgeLookup.IsCreated)
                NodeToEdgeLookup.Dispose();
            if (EdgeToNodeLookup.IsCreated)
                EdgeToNodeLookup.Dispose();
        }
    }

    struct OneToOneGraphInfo<TNodeKey, TNodeInfo, TEdgeKey, TEdgeInfo> : IDisposable
        where TNodeKey : struct, IEquatable<TNodeKey>
        where TNodeInfo : struct
        where TEdgeKey : struct, IEquatable<TEdgeKey>
        where TEdgeInfo : struct
    {
        NativeHashMap<TNodeKey, TNodeInfo> NodeInfoLookup;
        NativeHashMap<TEdgeKey, TEdgeInfo> EdgeInfoLookup;

        public OneToOneGraphInfo(int nodeCapacity, int edgeCapacity)
        {
            NodeInfoLookup = new NativeHashMap<TNodeKey, TNodeInfo>(nodeCapacity, Allocator.Persistent);
            EdgeInfoLookup = new NativeHashMap<TEdgeKey, TEdgeInfo>(edgeCapacity, Allocator.Persistent);
        }

        public void ExpandBy(int minimumFreeNodeCapacity, int minimumFreeEdgeCapacity)
        {
            NodeInfoLookup.Capacity = Math.Max(NodeInfoLookup.Capacity, NodeInfoLookup.Length + minimumFreeNodeCapacity);
            EdgeInfoLookup.Capacity = Math.Max(EdgeInfoLookup.Capacity, EdgeInfoLookup.Length + minimumFreeEdgeCapacity);
        }

        public void Dispose()
        {
            if (NodeInfoLookup.IsCreated)
                NodeInfoLookup.Dispose();
            if (EdgeInfoLookup.IsCreated)
                EdgeInfoLookup.Dispose();
        }
    }

    struct OneToManyDirectedGraph<TNodeKey, TEdgeKey> : IDisposable
        where TNodeKey : struct, IEquatable<TNodeKey>
        where TEdgeKey : struct, IEquatable<TEdgeKey>
    {
        internal NativeMultiHashMap<TNodeKey, TEdgeKey> NodeToEdgeLookup;
        internal NativeMultiHashMap<TEdgeKey, TNodeKey> EdgeToNodeLookup;

        public OneToManyDirectedGraph(int nodeCapacity = 1, int edgeCapacity = 1)
        {
            NodeToEdgeLookup = new NativeMultiHashMap<TNodeKey, TEdgeKey>(nodeCapacity, Allocator.Persistent);
            EdgeToNodeLookup = new NativeMultiHashMap<TEdgeKey, TNodeKey>(edgeCapacity, Allocator.Persistent);
        }

        public void ExpandBy(int minimumFreeNodeCapacity, int minimumFreeEdgeCapacity)
        {
            NodeToEdgeLookup.Capacity = Math.Max(NodeToEdgeLookup.Capacity, NodeToEdgeLookup.Length + minimumFreeNodeCapacity);
            EdgeToNodeLookup.Capacity = Math.Max(EdgeToNodeLookup.Capacity, EdgeToNodeLookup.Length + minimumFreeEdgeCapacity);
        }

        public void Dispose()
        {
            if (NodeToEdgeLookup.IsCreated)
                NodeToEdgeLookup.Dispose();
            if (EdgeToNodeLookup.IsCreated)
                EdgeToNodeLookup.Dispose();
        }
    }

    struct OneToManyGraphInfo<TNodeKey, TNodeInfo, TEdgeOriginKey, TEdgeOriginInfo, TEdgeDestinationKey, TEdgeDestinationInfo> : IDisposable
        where TNodeInfo : struct
        where TNodeKey : struct, IEquatable<TNodeKey>
        where TEdgeOriginInfo : struct
        where TEdgeDestinationInfo : struct
        where TEdgeOriginKey : struct, IEquatable<TEdgeOriginKey>
        where TEdgeDestinationKey : struct, IEquatable<TEdgeDestinationKey>
    {
        public NativeHashMap<TNodeKey, TNodeInfo> NodeInfoLookup;
        public NativeHashMap<TEdgeOriginKey, TEdgeOriginInfo> EdgeOriginInfoLookup;
        public NativeHashMap<TEdgeDestinationKey, TEdgeDestinationInfo> EdgeDestinationInfoLookup;

        public OneToManyGraphInfo(int nodeCapacity, int edgeCapacity)
        {
            NodeInfoLookup = new NativeHashMap<TNodeKey, TNodeInfo>(nodeCapacity, Allocator.Persistent);
            EdgeOriginInfoLookup = new NativeHashMap<TEdgeOriginKey, TEdgeOriginInfo>(edgeCapacity, Allocator.Persistent);
            EdgeDestinationInfoLookup = new NativeHashMap<TEdgeDestinationKey, TEdgeDestinationInfo>(edgeCapacity, Allocator.Persistent);
        }

        public void ExpandBy(int minimumFreeNodeCapacity, int minimumFreeEdgeCapacity)
        {
            NodeInfoLookup.Capacity = Math.Max(NodeInfoLookup.Capacity, NodeInfoLookup.Length + minimumFreeNodeCapacity);
            EdgeOriginInfoLookup.Capacity = Math.Max(EdgeOriginInfoLookup.Capacity, EdgeOriginInfoLookup.Length + minimumFreeEdgeCapacity);
            EdgeDestinationInfoLookup.Capacity = Math.Max(EdgeDestinationInfoLookup.Capacity, EdgeDestinationInfoLookup.Length + minimumFreeEdgeCapacity);
        }

        public void Dispose()
        {
            if (NodeInfoLookup.IsCreated)
                NodeInfoLookup.Dispose();
            if (EdgeOriginInfoLookup.IsCreated)
                EdgeOriginInfoLookup.Dispose();
            if (EdgeDestinationInfoLookup.IsCreated)
                EdgeDestinationInfoLookup.Dispose();
        }
    }
}
