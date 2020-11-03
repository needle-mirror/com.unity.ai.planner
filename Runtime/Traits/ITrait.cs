using System;

namespace Unity.AI.Planner.Traits
{
    /// <summary>
    /// The interface denoting that the container is a trait.
    /// </summary>
    public interface ITrait
    {
        /// <summary>
        /// Get the value of a field
        /// </summary>
        /// <param name="fieldName">Name of field</param>
        /// <returns>Field value if field exists</returns>
        [Obsolete("GetField is deprecated. Cast the trait to the appropriate type, then access the field. (RemovedAfter 2020-12-14)")]
        object GetField(string fieldName);

        /// <summary>
        /// Set a field on the trait (alternative to using reflection)
        /// </summary>
        /// <param name="fieldName">Name of the field</param>
        /// <param name="value">Value to set</param>
        [Obsolete("SetField is deprecated. Cast the trait to the appropriate type, then access the field. (RemovedAfter 2020-12-14)")]
        void SetField(string fieldName, object value);
    }
}
