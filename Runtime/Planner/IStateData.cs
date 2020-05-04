using System;

namespace Unity.AI.Planner
{
    /// <summary>
    /// An interface that marks an implementation of state data (custom per plan) to be used by the planner
    /// </summary>
    public interface IStateData : IEquatable<IStateData>
    {
    }
}
